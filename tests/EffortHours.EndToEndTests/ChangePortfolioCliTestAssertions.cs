namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    private static void AssertManifestTimings(string standardError)
    {
        string[] timingLines = standardError.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(9, timingLines.Length);
        Assert.All(timingLines, line => Assert.StartsWith("eh: portfolio phase ", line));
        foreach (string phase in new[]
        {
            "manifest-validation",
            "head-validation",
            "history-union",
            "selection",
            "snapshot-diff-construction",
            "static-analysis",
            "reconciliation",
            "allocation",
            "rendering",
        })
        {
            Assert.Contains(timingLines, line => line.Contains(phase, StringComparison.Ordinal));
        }
    }
}
