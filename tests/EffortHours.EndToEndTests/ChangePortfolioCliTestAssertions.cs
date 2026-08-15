namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    private static void AssertPortfolioTelemetry(
        string standardError,
        bool includesManifestPhases)
    {
        string[] lines = standardError.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string[] phases = includesManifestPhases
            ?
            [
                "manifest-validation",
                "head-validation",
                "history-union",
                "selection",
                "snapshot-diff-construction",
                "static-analysis",
                "reconciliation",
                "allocation",
                "rendering",
            ]
            :
            [
                "head-validation",
                "history-union",
                "selection",
                "snapshot-diff-construction",
                "static-analysis",
                "reconciliation",
                "rendering",
            ];
        Assert.Equal(phases.Length * 2, lines.Length);
        Assert.All(lines, line => Assert.StartsWith("eh: portfolio phase ", line));
        foreach (string phase in phases)
        {
            string started = Assert.Single(lines, line => string.Equals(
                line,
                $"eh: portfolio phase {phase} started",
                StringComparison.Ordinal));
            string completed = Assert.Single(lines, line =>
                line.StartsWith($"eh: portfolio phase {phase} ", StringComparison.Ordinal) &&
                line.EndsWith(" ms", StringComparison.Ordinal));
            Assert.True(Array.IndexOf(lines, started) < Array.IndexOf(lines, completed));
        }
    }
}
