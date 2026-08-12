using System.Globalization;
using System.Text;

namespace EffortHours.Analyzers.Docker;

internal static class DockerfileAnalyzer
{
    private const int MaximumInstructions = 100_000;
    private const int MaximumTokensPerInstruction = 4_096;

    public static DockerfileAnalysis Analyze(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        string[] lines = NormalizeLines(source);
        DockerfileMetrics metrics = new();
        HashSet<string> stageAliases = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> externalImages = new(StringComparer.Ordinal);
        char escape = '\\';
        bool sawInstruction = false;

        for (int index = 0; index < lines.Length && metrics.Instructions < MaximumInstructions; index++)
        {
            string trimmed = lines[index].Trim();
            if (trimmed.Length == 0) continue;
            if (trimmed.StartsWith('#'))
            {
                if (!sawInstruction) ReadDirective(trimmed, ref escape, metrics);
                continue;
            }

            sawInstruction = true;
            StringBuilder logical = new(lines[index].TrimStart());
            while (HasContinuation(logical, escape))
            {
                RemoveContinuation(logical, escape);
                if (++index >= lines.Length)
                {
                    metrics.Safeguards++;
                    break;
                }

                string continuation = lines[index].TrimStart();
                if (continuation.StartsWith('#')) continue;
                if (logical.Length > 0) logical.Append(' ');
                logical.Append(continuation);
            }

            AnalyzeInstruction(logical.ToString(), stageAliases, externalImages, metrics);
            string[] heredocDelimiters = FindHeredocDelimiters(logical.ToString());
            if (heredocDelimiters.Length > 0)
            {
                metrics.Heredocs += heredocDelimiters.Length;
                foreach (string delimiter in heredocDelimiters)
                {
                    bool found = false;
                    while (++index < lines.Length)
                    {
                        if (lines[index].Trim().Equals(delimiter, StringComparison.Ordinal))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        metrics.Safeguards++;
                        break;
                    }
                }
            }
        }

        if (metrics.Instructions >= MaximumInstructions) metrics.Safeguards++;
        metrics.ExternalBaseImages = externalImages.Count;
        return metrics.ToAnalysis();
    }

    private static void AnalyzeInstruction(
        string logical,
        HashSet<string> stageAliases,
        HashSet<string> externalImages,
        DockerfileMetrics metrics)
    {
        int separator = logical.IndexOfAny([' ', '\t']);
        string instruction = (separator < 0 ? logical : logical[..separator]).Trim().ToUpperInvariant();
        string arguments = separator < 0 ? string.Empty : logical[(separator + 1)..].Trim();
        if (instruction.Length == 0) return;
        metrics.Instructions++;
        if (arguments.Contains('$'))
            metrics.UnresolvedValues += CountOccurrences(arguments, '$');

        string[] words = SplitWords(arguments, metrics);
        switch (instruction)
        {
            case "FROM":
                AnalyzeFrom(words, stageAliases, externalImages, metrics);
                break;
            case "RUN":
                metrics.RunInstructions++;
                metrics.BuildSteps++;
                AnalyzeMounts(arguments, metrics);
                break;
            case "COPY":
                metrics.CopyInstructions++;
                metrics.BuildSteps++;
                AnalyzeCopy(words, stageAliases, externalImages, metrics);
                break;
            case "ADD":
                metrics.AddInstructions++;
                metrics.BuildSteps++;
                AnalyzeCopy(words, stageAliases, externalImages, metrics);
                if (words.Any(IsRemoteReference)) metrics.RemoteAdds++;
                break;
            case "ARG":
                metrics.Arguments += Math.Max(1, words.Length);
                break;
            case "ENV":
                metrics.EnvironmentEntries += CountAssignments(words);
                break;
            case "WORKDIR":
                metrics.WorkDirectories++;
                break;
            case "USER":
                metrics.Users++;
                break;
            case "EXPOSE":
                metrics.ExposedPorts += Math.Max(1, words.Length);
                break;
            case "VOLUME":
                metrics.Volumes += Math.Max(1, CountListValues(arguments, words));
                break;
            case "HEALTHCHECK":
                metrics.HealthChecks++;
                break;
            case "CMD":
            case "ENTRYPOINT":
                metrics.RuntimeCommands++;
                break;
            case "LABEL":
            case "SHELL":
            case "STOPSIGNAL":
            case "ONBUILD":
            case "MAINTAINER":
                break;
            default:
                metrics.UnknownInstructions++;
                break;
        }
    }

    private static void AnalyzeFrom(
        string[] words,
        HashSet<string> stageAliases,
        HashSet<string> externalImages,
        DockerfileMetrics metrics)
    {
        string? image = words.FirstOrDefault(word => !word.StartsWith("--", StringComparison.Ordinal));
        if (image is null)
        {
            metrics.Safeguards++;
            return;
        }

        metrics.Stages++;
        if (image.Contains('$')) metrics.UnresolvedValues++;
        else if (stageAliases.Contains(image)) metrics.LocalStageReferences++;
        else externalImages.Add(image);

        int aliasIndex = words.FindIndex(word => word.Equals("AS", StringComparison.OrdinalIgnoreCase));
        if (aliasIndex >= 0 && aliasIndex + 1 < words.Length)
            stageAliases.Add(words[aliasIndex + 1]);
    }

    private static void AnalyzeCopy(
        string[] words,
        HashSet<string> stageAliases,
        HashSet<string> externalImages,
        DockerfileMetrics metrics)
    {
        string? source = words.FirstOrDefault(word =>
            word.StartsWith("--from=", StringComparison.OrdinalIgnoreCase));
        if (source is null) return;
        metrics.MultiStageCopies++;
        string value = source[7..];
        if (value.Contains('$')) metrics.UnresolvedValues++;
        else if (stageAliases.Contains(value) || int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            metrics.LocalStageReferences++;
        else externalImages.Add(value);
    }

    private static void AnalyzeMounts(string arguments, DockerfileMetrics metrics)
    {
        foreach (string segment in arguments.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!segment.StartsWith("--mount=", StringComparison.OrdinalIgnoreCase)) continue;
            if (segment.Contains("type=secret", StringComparison.OrdinalIgnoreCase) ||
                segment.Contains("type=ssh", StringComparison.OrdinalIgnoreCase))
                metrics.SecretOrSshMounts++;
            else if (segment.Contains("type=cache", StringComparison.OrdinalIgnoreCase) ||
                segment.Contains("type=bind", StringComparison.OrdinalIgnoreCase))
                metrics.CacheOrBindMounts++;
        }
    }

    private static void ReadDirective(string line, ref char escape, DockerfileMetrics metrics)
    {
        string directive = line[1..].Trim();
        int equals = directive.IndexOf('=');
        if (equals <= 0) return;
        string key = directive[..equals].Trim();
        string value = directive[(equals + 1)..].Trim();
        if (key.Equals("syntax", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("check", StringComparison.OrdinalIgnoreCase))
        {
            metrics.ParserDirectives++;
        }
        else if (key.Equals("escape", StringComparison.OrdinalIgnoreCase))
        {
            metrics.ParserDirectives++;
            if (value is "\\" or "`") escape = value[0];
            else metrics.Safeguards++;
        }
    }

    private static string[] SplitWords(string value, DockerfileMetrics metrics)
    {
        List<string> words = [];
        StringBuilder current = new();
        char quote = '\0';
        bool escaped = false;
        foreach (char character in value)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\' && quote != '\'')
            {
                current.Append(character);
                escaped = true;
                continue;
            }

            if (quote != '\0')
            {
                current.Append(character);
                if (character == quote) quote = '\0';
                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                current.Append(character);
            }
            else if (char.IsWhiteSpace(character))
            {
                AddWord(words, current);
            }
            else
            {
                current.Append(character);
            }

            if (words.Count >= MaximumTokensPerInstruction)
            {
                metrics.Safeguards++;
                break;
            }
        }

        AddWord(words, current);
        if (quote != '\0' || escaped) metrics.Safeguards++;
        return [.. words];
    }

    private static void AddWord(List<string> words, StringBuilder current)
    {
        if (current.Length == 0) return;
        words.Add(current.ToString());
        current.Clear();
    }

    private static string[] FindHeredocDelimiters(string logical)
    {
        List<string> delimiters = [];
        int offset = 0;
        while ((offset = logical.IndexOf("<<", offset, StringComparison.Ordinal)) >= 0)
        {
            int cursor = offset + 2;
            if (cursor < logical.Length && logical[cursor] == '-') cursor++;
            while (cursor < logical.Length && char.IsWhiteSpace(logical[cursor])) cursor++;
            char quote = cursor < logical.Length && logical[cursor] is '\'' or '"' ? logical[cursor++] : '\0';
            int start = cursor;
            while (cursor < logical.Length &&
                (char.IsAsciiLetterOrDigit(logical[cursor]) || logical[cursor] is '_' or '.' or '-')) cursor++;
            if (cursor > start && (quote == '\0' || cursor < logical.Length && logical[cursor] == quote))
                delimiters.Add(logical[start..cursor]);
            offset = Math.Max(cursor, offset + 2);
        }

        return [.. delimiters.Distinct(StringComparer.Ordinal)];
    }

    private static bool HasContinuation(StringBuilder value, char escape)
    {
        int index = value.Length - 1;
        while (index >= 0 && char.IsWhiteSpace(value[index])) index--;
        if (index < 0 || value[index] != escape) return false;
        int count = 0;
        while (index >= 0 && value[index--] == escape) count++;
        return count % 2 == 1;
    }

    private static void RemoveContinuation(StringBuilder value, char escape)
    {
        while (value.Length > 0 && char.IsWhiteSpace(value[^1])) value.Length--;
        if (value.Length > 0 && value[^1] == escape) value.Length--;
        while (value.Length > 0 && char.IsWhiteSpace(value[^1])) value.Length--;
    }

    private static int CountAssignments(string[] words)
    {
        int assignments = words.Count(word => word.Contains('='));
        return assignments > 0 ? assignments : words.Length > 0 ? 1 : 0;
    }

    private static int CountListValues(string arguments, string[] words) =>
        arguments.TrimStart().StartsWith('[')
            ? Math.Max(1, arguments.Count(character => character == ',') + 1)
            : words.Length;

    private static int CountOccurrences(string value, char candidate) =>
        value.Count(character => character == candidate);

    private static bool IsRemoteReference(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("git://", StringComparison.OrdinalIgnoreCase);

    private static string[] NormalizeLines(string source) => source
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n')
        .Split('\n');

    private static int FindIndex(this string[] values, Func<string, bool> predicate)
    {
        for (int index = 0; index < values.Length; index++)
            if (predicate(values[index])) return index;
        return -1;
    }
}
