namespace EffortHours.Analyzers.Docker;

internal static class ComposeYamlScanner
{
    private const int MaximumLines = 250_000;
    private const int MaximumDepth = 128;
    private const int MaximumLineLength = 256 * 1024;

    public static ComposeYamlScan Scan(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        string[] lines = NormalizeLines(source);
        List<ComposeYamlEntry> entries = [];
        List<YamlFrame> stack = [];
        MutableScan metrics = new();
        int? blockScalarIndent = null;
        bool documentHasEntries = false;

        for (int lineIndex = 0; lineIndex < lines.Length && lineIndex < MaximumLines; lineIndex++)
        {
            string raw = lines[lineIndex];
            if (raw.Length > MaximumLineLength)
            {
                metrics.Safeguards++;
                continue;
            }

            int indent = LeadingIndent(raw, out bool hasTab);
            if (hasTab) metrics.Safeguards++;
            if (blockScalarIndent is not null && raw.AsSpan().Trim().Length == 0) continue;
            if (blockScalarIndent is not null && indent > blockScalarIndent.Value) continue;
            blockScalarIndent = null;

            string content = StripComment(raw[indent..], metrics).TrimEnd();
            if (content.Length == 0) continue;
            if (content is "---" or "...")
            {
                documentHasEntries = false;
                stack.Clear();
                continue;
            }

            while (stack.Count > 0 && indent <= stack[^1].Indent) stack.RemoveAt(stack.Count - 1);
            if (stack.Count >= MaximumDepth)
            {
                metrics.Safeguards++;
                continue;
            }

            bool sequence = content.StartsWith("- ", StringComparison.Ordinal) || content == "-";
            string payload = sequence ? content[1..].TrimStart() : content.TrimStart();
            int colon = FindMappingColon(payload, metrics);
            string? key = null;
            string value = payload;
            if (colon >= 0)
            {
                key = NormalizeKey(payload[..colon]);
                value = payload[(colon + 1)..].Trim();
                if (key.Length == 0)
                {
                    key = null;
                    metrics.Safeguards++;
                }
            }

            string[] parents = [.. stack.Select(frame => frame.Key)];
            if (!documentHasEntries)
            {
                metrics.Documents++;
                documentHasEntries = true;
            }
            entries.Add(new ComposeYamlEntry(
                lineIndex + 1,
                indent,
                stack.Count,
                parents,
                key,
                value,
                sequence));

            CountSpecialSyntax(payload, metrics);
            if (value.StartsWith('|') || value.StartsWith('>'))
            {
                metrics.BlockScalars++;
                blockScalarIndent = indent;
            }

            if (key is not null && ShouldOpenMapping(value))
                stack.Add(new YamlFrame(indent, key));
        }

        if (lines.Length > MaximumLines) metrics.Safeguards++;
        return new ComposeYamlScan
        {
            Entries = entries,
            Documents = metrics.Documents,
            AnchorsAliasesAndMerges = metrics.AnchorsAliasesAndMerges,
            Interpolations = metrics.Interpolations,
            BlockScalars = metrics.BlockScalars,
            DynamicValues = metrics.DynamicValues,
            Safeguards = metrics.Safeguards,
        };
    }

    private static int LeadingIndent(string line, out bool hasTab)
    {
        int index = 0;
        hasTab = false;
        while (index < line.Length && line[index] is ' ' or '\t')
        {
            if (line[index] == '\t') hasTab = true;
            index++;
        }

        return index;
    }

    private static string StripComment(string value, MutableScan metrics)
    {
        char quote = '\0';
        bool escaped = false;
        int flowDepth = 0;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (quote == '"' && escaped)
            {
                escaped = false;
                continue;
            }

            if (quote == '"' && current == '\\')
            {
                escaped = true;
                continue;
            }

            if (quote != '\0')
            {
                if (current == quote)
                {
                    if (quote == '\'' && index + 1 < value.Length && value[index + 1] == '\'')
                    {
                        index++;
                        continue;
                    }

                    quote = '\0';
                }

                continue;
            }

            if (current is '\'' or '"') quote = current;
            else if (current is '[' or '{') flowDepth++;
            else if (current is ']' or '}') flowDepth--;
            else if (current == '#' && (index == 0 || char.IsWhiteSpace(value[index - 1])))
                return value[..index];
        }

        if (quote != '\0' || flowDepth != 0) metrics.Safeguards++;
        return value;
    }

    private static int FindMappingColon(string value, MutableScan metrics)
    {
        char quote = '\0';
        bool escaped = false;
        int flowDepth = 0;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (quote == '"' && escaped)
            {
                escaped = false;
                continue;
            }

            if (quote == '"' && current == '\\')
            {
                escaped = true;
                continue;
            }

            if (quote != '\0')
            {
                if (current == quote)
                {
                    if (quote == '\'' && index + 1 < value.Length && value[index + 1] == '\'') index++;
                    else quote = '\0';
                }

                continue;
            }

            if (current is '\'' or '"') quote = current;
            else if (current is '[' or '{') flowDepth++;
            else if (current is ']' or '}') flowDepth--;
            else if (current == ':' && flowDepth == 0 &&
                (index + 1 == value.Length || char.IsWhiteSpace(value[index + 1]))) return index;
        }

        if (quote != '\0' || flowDepth != 0) metrics.Safeguards++;
        return -1;
    }

    private static void CountSpecialSyntax(string value, MutableScan metrics)
    {
        metrics.Interpolations += Count(value, "${");
        metrics.AnchorsAliasesAndMerges += Count(value, "<<:");
        char quote = '\0';
        bool escaped = false;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (quote == '"' && escaped) { escaped = false; continue; }
            if (quote == '"' && current == '\\') { escaped = true; continue; }
            if (quote != '\0')
            {
                if (current == quote) quote = '\0';
                continue;
            }

            if (current is '\'' or '"') quote = current;
            else if ((current is '&' or '*') &&
                (index == 0 || char.IsWhiteSpace(value[index - 1]) || value[index - 1] is ':' or '[' or ','))
                metrics.AnchorsAliasesAndMerges++;
            else if (current == '!' && (index == 0 || char.IsWhiteSpace(value[index - 1])))
                metrics.DynamicValues++;
        }
    }

    private static bool ShouldOpenMapping(string value)
    {
        if (value.Length == 0) return true;
        if (!value.StartsWith('&')) return false;
        return !value.Any(char.IsWhiteSpace) || value.Split(
            [' ', '\t'], StringSplitOptions.RemoveEmptyEntries).Length == 1;
    }

    private static string NormalizeKey(string value)
    {
        string key = value.Trim();
        if (key.Length >= 2 && key[0] == key[^1] && key[0] is '\'' or '"')
            key = key[1..^1];
        return key;
    }

    private static int Count(string value, string candidate)
    {
        int count = 0;
        int offset = 0;
        while ((offset = value.IndexOf(candidate, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += candidate.Length;
        }

        return count;
    }

    private static string[] NormalizeLines(string source) => source
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n')
        .Split('\n');

    private sealed record YamlFrame(int Indent, string Key);

    private sealed class MutableScan
    {
        public int Documents;
        public int AnchorsAliasesAndMerges;
        public int Interpolations;
        public int BlockScalars;
        public int DynamicValues;
        public int Safeguards;
    }
}

internal sealed record ComposeYamlEntry(
    int Line,
    int Indent,
    int Depth,
    IReadOnlyList<string> Parents,
    string? Key,
    string Value,
    bool IsSequence);

internal sealed record ComposeYamlScan
{
    public IReadOnlyList<ComposeYamlEntry> Entries { get; init; } = [];

    public int Documents { get; init; }

    public int AnchorsAliasesAndMerges { get; init; }

    public int Interpolations { get; init; }

    public int BlockScalars { get; init; }

    public int DynamicValues { get; init; }

    public int Safeguards { get; init; }
}
