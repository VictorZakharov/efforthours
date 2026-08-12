using System.Text;

namespace EffortHours.Change;

internal readonly record struct ContentChangeResult(bool FormattingOnly, int EditRegions);

internal static class ContentChangeAnalyzer
{
    internal const int MaximumTextBytes = 8 * 1024 * 1024;
    private const int MaximumReportedRegions = 1_000;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static ContentChangeResult Analyze(
        byte[] baseContent,
        byte[] headContent,
        string path)
    {
        ArgumentNullException.ThrowIfNull(baseContent);
        ArgumentNullException.ThrowIfNull(headContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (baseContent.Length > MaximumTextBytes || headContent.Length > MaximumTextBytes)
        {
            return new ContentChangeResult(false, 1);
        }

        string baseText;
        string headText;
        try
        {
            baseText = StrictUtf8.GetString(baseContent);
            headText = StrictUtf8.GetString(headContent);
        }
        catch (DecoderFallbackException)
        {
            return new ContentChangeResult(false, 1);
        }

        if (ContainsNull(baseText) || ContainsNull(headText))
        {
            return new ContentChangeResult(false, 1);
        }

        if ((SupportsFormattingComparison(path) ||
            SupportsShebangScriptFormatting(path, baseText, headText)) &&
            FormattingEquivalent(baseText, headText, path))
        {
            return new ContentChangeResult(true, 0);
        }

        string[] baseLines = NormalizeLines(baseText);
        string[] headLines = NormalizeLines(headText);
        int regions = CountPatienceRegions(baseLines, headLines);
        return new ContentChangeResult(false, Math.Clamp(regions, 1, MaximumReportedRegions));
    }

    private static bool ContainsNull(string value) => value.AsSpan().Contains('\0');

    private static bool FormattingEquivalent(string left, string right, string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (IsHcl(path))
        {
            return HclFormattingNormalizer.TryCreateSignature(left, out string leftSignature) &&
                HclFormattingNormalizer.TryCreateSignature(right, out string rightSignature) &&
                leftSignature == rightSignature;
        }

        if (extension is ".sh" or ".bash" or ".ksh" or ".bats" || IsShellProfile(path) ||
            (extension is "" or ".command") && BothUseShellShebang(left, right))
        {
            return ShellFormattingNormalizer.TryCreateSignature(left, out string leftSignature) &&
                ShellFormattingNormalizer.TryCreateSignature(right, out string rightSignature) &&
                leftSignature == rightSignature;
        }

        if (extension is ".ps1" or ".psm1" or ".psd1" ||
            (extension is "" or ".command") && BothUsePowerShellShebang(left, right))
        {
            return PowerShellFormattingNormalizer.TryCreateSignature(left, out string leftSignature) &&
                PowerShellFormattingNormalizer.TryCreateSignature(right, out string rightSignature) &&
                leftSignature == rightSignature;
        }

        if (Path.GetExtension(path).Equals(".sql", StringComparison.OrdinalIgnoreCase))
        {
            return SqlFormattingNormalizer.TryCreateSignature(left, out string leftSignature) &&
                SqlFormattingNormalizer.TryCreateSignature(right, out string rightSignature) &&
                leftSignature == rightSignature;
        }

        if (Path.GetExtension(path).ToLowerInvariant() is ".py" or ".pyi")
        {
            return PythonFormattingNormalizer.TryCreateSignature(left, out string leftSignature) &&
                PythonFormattingNormalizer.TryCreateSignature(right, out string rightSignature) &&
                leftSignature == rightSignature;
        }

        if (Path.GetExtension(path).Equals(".go", StringComparison.OrdinalIgnoreCase))
        {
            return GoFormattingNormalizer.TryCreateSignature(left, out string leftSignature) &&
                GoFormattingNormalizer.TryCreateSignature(right, out string rightSignature) &&
                leftSignature == rightSignature;
        }

        if (Path.GetExtension(path).Equals(".java", StringComparison.OrdinalIgnoreCase))
        {
            return JavaFormattingNormalizer.TryCreateSignature(left, out string leftSignature) &&
                JavaFormattingNormalizer.TryCreateSignature(right, out string rightSignature) &&
                leftSignature == rightSignature;
        }

        if (Path.GetExtension(path).ToLowerInvariant() is ".kt" or ".kts")
        {
            return KotlinFormattingNormalizer.TryCreateSignature(left, out string leftSignature) &&
                KotlinFormattingNormalizer.TryCreateSignature(right, out string rightSignature) &&
                leftSignature == rightSignature;
        }

        if (Path.GetExtension(path).Equals(".php", StringComparison.OrdinalIgnoreCase))
        {
            return PhpFormattingNormalizer.TryCreateSignature(left, out string leftSignature) &&
                PhpFormattingNormalizer.TryCreateSignature(right, out string rightSignature) &&
                leftSignature == rightSignature;
        }

        return FormattingSignature(left) == FormattingSignature(right);
    }

    private static string FormattingSignature(string value)
    {
        StringBuilder signature = new(value.Length);
        LexicalState state = LexicalState.Code;
        bool escaped = false;
        bool pendingWhitespace = false;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            char next = index + 1 < value.Length ? value[index + 1] : '\0';
            if (state == LexicalState.Code)
            {
                if (char.IsWhiteSpace(character))
                {
                    pendingWhitespace = signature.Length > 0;
                    continue;
                }

                if (pendingWhitespace)
                {
                    signature.Append(' ');
                    pendingWhitespace = false;
                }

                signature.Append(character);
                if (character == '/' && next == '/')
                {
                    signature.Append(next);
                    index++;
                    state = LexicalState.LineComment;
                }
                else if (character == '/' && next == '*')
                {
                    signature.Append(next);
                    index++;
                    state = LexicalState.BlockComment;
                }
                else if (character == '\'')
                {
                    state = LexicalState.SingleQuoted;
                    escaped = false;
                }
                else if (character == '"')
                {
                    state = LexicalState.DoubleQuoted;
                    escaped = false;
                }
                else if (character == '`')
                {
                    state = LexicalState.BacktickQuoted;
                    escaped = false;
                }

                continue;
            }

            signature.Append(character);
            if (state == LexicalState.LineComment)
            {
                if (character is '\r' or '\n')
                {
                    state = LexicalState.Code;
                }

                continue;
            }

            if (state == LexicalState.BlockComment)
            {
                if (character == '*' && next == '/')
                {
                    signature.Append(next);
                    index++;
                    state = LexicalState.Code;
                }

                continue;
            }

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                continue;
            }

            if ((state == LexicalState.SingleQuoted && character == '\'') ||
                (state == LexicalState.DoubleQuoted && character == '"') ||
                (state == LexicalState.BacktickQuoted && character == '`'))
            {
                state = LexicalState.Code;
            }
        }

        return signature.ToString();
    }

    private static bool SupportsFormattingComparison(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is
            ".cs" or ".js" or ".jsx" or ".mjs" or ".cjs" or
            ".ts" or ".tsx" or ".mts" or ".cts" or ".py" or ".pyi" or
            ".go" or ".java" or ".kt" or ".kts" or ".php" or ".sql" or
            ".tf" or ".tfvars" or ".tfbackend" or ".hcl" or
            ".sh" or ".bash" or ".ksh" or ".bats" or ".ps1" or ".psm1" or ".psd1" ||
        IsShellProfile(path) || IsHcl(path);

    private static bool IsHcl(string path)
    {
        string name = Path.GetFileName(path).ToLowerInvariant();
        return Path.GetExtension(path).ToLowerInvariant() is
            ".tf" or ".tfvars" or ".tfbackend" or ".hcl" ||
            name is ".terraformrc" or "terraform.rc";
    }

    private static bool IsShellProfile(string path) =>
        Path.GetFileName(path).ToLowerInvariant() is
            ".profile" or ".bash_profile" or ".bash_login" or ".bashrc";

    private static bool SupportsShebangScriptFormatting(
        string path,
        string left,
        string right)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return (extension is "" or ".command") &&
            (BothUseShellShebang(left, right) || BothUsePowerShellShebang(left, right));
    }

    private static bool BothUseShellShebang(string left, string right) =>
        UsesShebang(left, "sh", "ash", "bash", "dash", "ksh") &&
        UsesShebang(right, "sh", "ash", "bash", "dash", "ksh");

    private static bool BothUsePowerShellShebang(string left, string right) =>
        UsesShebang(left, "pwsh", "powershell") &&
        UsesShebang(right, "pwsh", "powershell");

    private static bool UsesShebang(string value, params string[] interpreters)
    {
        int newline = value.IndexOfAny(['\r', '\n']);
        string firstLine = (newline < 0 ? value : value[..newline]).TrimStart('\uFEFF').Trim();
        if (!firstLine.StartsWith("#!", StringComparison.Ordinal)) return false;
        string[] parts = firstLine[2..].Split(
            [' ', '\t'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Any(part => interpreters.Contains(
            Path.GetFileName(part),
            StringComparer.OrdinalIgnoreCase));
    }

    private static string[] NormalizeLines(string value) =>
    [
        .. value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0),
    ];

    private static int CountPatienceRegions(string[] baseLines, string[] headLines)
    {
        Dictionary<string, LineOccurrence> baseOccurrences = CountOccurrences(baseLines);
        Dictionary<string, LineOccurrence> headOccurrences = CountOccurrences(headLines);
        List<LinePair> candidates = [];
        foreach ((string line, LineOccurrence occurrence) in baseOccurrences)
        {
            if (occurrence.Count == 1 &&
                headOccurrences.TryGetValue(line, out LineOccurrence headOccurrence) &&
                headOccurrence.Count == 1)
            {
                candidates.Add(new LinePair(occurrence.Index, headOccurrence.Index));
            }
        }

        candidates.Sort(static (left, right) => left.BaseIndex.CompareTo(right.BaseIndex));
        LinePair[] anchors = LongestIncreasingSubsequence(candidates);
        int regions = 0;
        int previousBase = -1;
        int previousHead = -1;
        foreach (LinePair anchor in anchors.Append(new LinePair(baseLines.Length, headLines.Length)))
        {
            if (anchor.BaseIndex - previousBase > 1 || anchor.HeadIndex - previousHead > 1)
            {
                regions++;
            }

            previousBase = anchor.BaseIndex;
            previousHead = anchor.HeadIndex;
        }

        return regions;
    }

    private static Dictionary<string, LineOccurrence> CountOccurrences(string[] lines)
    {
        Dictionary<string, LineOccurrence> occurrences = new(StringComparer.Ordinal);
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            occurrences[line] = occurrences.TryGetValue(line, out LineOccurrence occurrence)
                ? occurrence with { Count = occurrence.Count + 1 }
                : new LineOccurrence(1, index);
        }

        return occurrences;
    }

    private static LinePair[] LongestIncreasingSubsequence(IReadOnlyList<LinePair> values)
    {
        if (values.Count == 0)
        {
            return [];
        }

        int[] tails = new int[values.Count];
        int[] previous = new int[values.Count];
        Array.Fill(previous, -1);
        int length = 0;
        for (int index = 0; index < values.Count; index++)
        {
            int low = 0;
            int high = length;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (values[tails[middle]].HeadIndex < values[index].HeadIndex)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            if (low > 0)
            {
                previous[index] = tails[low - 1];
            }

            tails[low] = index;
            if (low == length)
            {
                length++;
            }
        }

        LinePair[] result = new LinePair[length];
        int current = tails[length - 1];
        for (int index = length - 1; index >= 0; index--)
        {
            result[index] = values[current];
            current = previous[current];
        }

        return result;
    }

    private readonly record struct LineOccurrence(int Count, int Index);

    private readonly record struct LinePair(int BaseIndex, int HeadIndex);

    private enum LexicalState
    {
        Code,
        SingleQuoted,
        DoubleQuoted,
        BacktickQuoted,
        LineComment,
        BlockComment,
    }
}
