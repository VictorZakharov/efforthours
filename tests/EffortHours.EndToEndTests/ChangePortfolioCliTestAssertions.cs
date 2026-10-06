namespace EffortHours.EndToEndTests;

public abstract partial class ChangeCliTestSupport
{
    protected static void AssertPortfolioTelemetry(
        string standardError,
        bool includesManifestPhases)
    {
        string[] lines = standardError.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string[] progressLines = [.. lines.Where(line => line.Contains(" progress ", StringComparison.Ordinal))];
        string[] phaseLines = [.. lines.Except(progressLines, StringComparer.Ordinal)];
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
        Assert.NotEmpty(progressLines);
        Assert.Contains(progressLines, line => line.Contains("analysis-cache=", StringComparison.Ordinal));
        Assert.Contains(
            progressLines,
            line => line.Contains("observed-peak-working-set=", StringComparison.Ordinal));
        Assert.Equal(phases.Length * 2, phaseLines.Length);
        Assert.All(lines, line => Assert.StartsWith("eh: portfolio phase ", line));
        foreach (string phase in phases)
        {
            string started = Assert.Single(phaseLines, line => string.Equals(
                line,
                $"eh: portfolio phase {phase} started",
                StringComparison.Ordinal));
            string completed = Assert.Single(phaseLines, line =>
                line.StartsWith($"eh: portfolio phase {phase} ", StringComparison.Ordinal) &&
                line.EndsWith(" ms (cumulative work)", StringComparison.Ordinal));
            string elapsed = completed[$"eh: portfolio phase {phase} ".Length..^" ms (cumulative work)".Length];
            Assert.True(decimal.TryParse(elapsed, System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture, out decimal milliseconds));
            Assert.True(milliseconds >= 0);
            Assert.True(Array.IndexOf(lines, started) < Array.IndexOf(lines, completed));
        }
    }
}
