namespace EffortHours.Contracts;

public static class ChangeHistoricalAnnotation
{
    private static readonly string[] ZeroVerdicts = ["No work occurred", "No work was done", "Zero work", "Zero actual labor", "Actual labor: 0"];
    public static string Classify(string description)
    {
        const string begin = "[EffortHours historical annotation]", end = "[/EffortHours historical annotation]";
        int first = description.IndexOf(begin, StringComparison.Ordinal), last = description.IndexOf(end, StringComparison.Ordinal);
        if (first < 0 && last < 0) return "none";
        if (first < 0 || last < first || description.IndexOf(begin, first + begin.Length, StringComparison.Ordinal) >= 0 ||
            description.IndexOf(end, last + end.Length, StringComparison.Ordinal) >= 0) return "ambiguous-managed-annotation";
        string block = description[(first + begin.Length)..last];
        foreach (string line in block.Split('\n').Select(value => value.Trim()))
        {
            // Recognize only explicit managed verdicts, never unrelated user prose.
            if (ZeroVerdicts
                .Any(prefix => line.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith(prefix + ";", StringComparison.OrdinalIgnoreCase))) return "zero-labor-claim";
        }
        return block.Contains("no-retained-change", StringComparison.OrdinalIgnoreCase) ||
            block.Contains("No retained change", StringComparison.OrdinalIgnoreCase)
            ? "retained-no-change-annotation" : "other-managed-annotation";
    }
}
