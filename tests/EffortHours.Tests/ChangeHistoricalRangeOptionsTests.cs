using EffortHours.Cli;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ChangeHistoricalRangeOptionsTests
{
    private static readonly string[] Common =
    ["--native-period", "--owner", "example", "--author", "dev@example.invalid", "--timezone", "America/Toronto",
        "--scope", "engineering", "--capacity-hours-per-day", "8", "--generated-at", "2026-10-02T12:00:00Z"];

    [Fact]
    public void ExplicitRangeUsesJointHistoricalDiscoveryAndExactLocalBoundaries()
    {
        ChangePortfolioCommandParseResult result = ChangePortfolioCommandOptionsParser.Parse(
            [.. Common, "--since", "2026-01-01", "--until", "2026-02-01", "--breakdown", "day"]);

        Assert.Null(result.Error);
        ChangePortfolioCommandOptions options = Assert.IsType<ChangePortfolioCommandOptions>(result.Options);
        Assert.Equal(ChangePortfolioNativePeriodKind.CustomRange, options.Period);
        Assert.True(options.IncludeHistoricalPullRequests);
        Assert.Equal(ChangePortfolioContributorNormalization.Joint, options.ContributorNormalization);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 5, 0, 0, TimeSpan.Zero), options.SinceInclusive);
        Assert.Equal(new DateTimeOffset(2026, 2, 1, 5, 0, 0, TimeSpan.Zero), options.UntilExclusive);
    }

    [Theory]
    [InlineData("--until", "2026-11-01")]
    [InlineData("--period", "last-month")]
    [InlineData("--normalization", "isolated")]
    public void ConflictingOrFutureHistoricalInputsFail(string option, string value)
    {
        ChangePortfolioCommandParseResult result = ChangePortfolioCommandOptionsParser.Parse(
            [.. Common, "--since", "2026-01-01", "--until", "2026-02-01", option, value]);
        Assert.NotNull(result.Error);
    }
}
