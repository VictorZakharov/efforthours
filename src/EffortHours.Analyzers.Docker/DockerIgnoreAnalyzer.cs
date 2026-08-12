namespace EffortHours.Analyzers.Docker;

internal static class DockerIgnoreAnalyzer
{
    public static DockerIgnoreAnalysis Analyze(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        int rules = 0;
        int negations = 0;
        int directoryRules = 0;
        int unresolved = 0;
        foreach (string rawLine in NormalizeLines(source))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            rules++;
            if (line.StartsWith('!')) negations++;
            if (line.EndsWith('/') || line.Contains("/**", StringComparison.Ordinal)) directoryRules++;
            if (line.Contains('[', StringComparison.Ordinal) &&
                !line.Contains(']', StringComparison.Ordinal)) unresolved++;
        }

        decimal units = rules == 0
            ? 0.25m
            : decimal.Min(2m, 0.25m + (0.25m * decimal.Ceiling(rules / 25m)));
        return new DockerIgnoreAnalysis
        {
            Rules = rules,
            Negations = negations,
            DirectoryRules = directoryRules,
            UnresolvedRules = unresolved,
            SemanticUnits = units,
        };
    }

    private static string[] NormalizeLines(string source) => source
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n')
        .Split('\n');
}
