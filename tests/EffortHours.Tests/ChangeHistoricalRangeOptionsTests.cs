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
    [Theory]
    [InlineData("--discovery-timeout-seconds", "0")]
    [InlineData("--discovery-timeout-seconds", "86401")]
    [InlineData("--max-acquired-mib", "0")]
    [InlineData("--max-acquired-mib", "16385")]
    [InlineData("--repository", "elsewhere/project")]
    [InlineData("--repository", "invalid")]
    public void InvalidAcquisitionConfigurationFailsBeforeProviderAccess(string option, string value)
    {
        Assert.NotNull(ChangePortfolioCommandOptionsParser.Parse(
            [.. Common, "--since", "2026-01-01", "--until", "2026-02-01", option, value]).Error);
    }

    [Fact]
    public void RepositoryRestrictionIsCanonicalAndOperationalBudgetsAreExplicit()
    {
        ChangePortfolioCommandParseResult result = ChangePortfolioCommandOptionsParser.Parse(
            [.. Common, "--since", "2026-01-01", "--until", "2026-02-01", "--repository", "EXAMPLE/Project",
                "--max-acquired-mib", "16", "--discovery-timeout-seconds", "30"]);
        Assert.Null(result.Error);
        Assert.Equal(["example/project"], result.Options!.Repositories);
        Assert.Equal(16, result.Options.MaximumAcquiredMebibytes);
        Assert.Equal(30, result.Options.DiscoveryTimeoutSeconds);
        Assert.NotNull(ChangePortfolioCommandOptionsParser.Parse(["--author-period-manifest", "frozen.json", "--repository", "example/project"]).Error);
        Assert.NotNull(ChangePortfolioCommandOptionsParser.Parse([.. Common, "--period", "last-month", "--repository", "example/project", "--repository", "EXAMPLE/PROJECT"]).Error);
        Assert.NotNull(ChangePortfolioCommandOptionsParser.Parse([.. Common, "--period", "last-month", "--max-acquired-mib", "16", "--max-acquired-mib", "16"]).Error);
    }

}
