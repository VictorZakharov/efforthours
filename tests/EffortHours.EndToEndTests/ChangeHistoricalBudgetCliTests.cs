using System.Globalization;
using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests : ChangeCliTestSupport
{
    [Fact]
    public async Task HistoricalInventoryBudgetFailsBeforeAcquisitionWithObservedValidFailure()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "efforthours-historical-budget", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            HistoricalProviderRunner runner = new(new string('a', 40), new string('b', 40), new string('c', 40), repositoryCount: 257);
            string repositories = Path.Combine(workspace, "repositories");
            GitHubAuthorPeriodDiscovery discovery = new(runner,
                new GitHubRepositoryCache(new ExternalCommandRunner(), new GitClient(), repositories,
                    _ => throw new InvalidOperationException("Acquisition must not run after inventory exceeds the bound.")),
                new GitHubProviderMetadataCache(Path.Combine(workspace, "metadata")));
            ChangePortfolioCommand command = new(new ChangeEstimator(),
                (_, _, _, _, _) => throw new NotSupportedException(),
                (_, _, _, _) => throw new NotSupportedException(),
                (_, _) => throw new NotSupportedException(),
                (_, _, _) => throw new NotSupportedException(), null, discovery.DiscoverTodayAsync);
            string output = Path.Combine(workspace, "incomplete.json");
            using StringWriter stdout = new(CultureInfo.InvariantCulture);
            using StringWriter stderr = new(CultureInfo.InvariantCulture);
            int exitCode = await command.ExecuteAsync(["--native-period", "--owner", "example", "--author", "selected",
                "--since", "2026-01-19", "--until", "2026-01-24", "--breakdown", "day", "--timezone", "UTC",
                "--scope", "engineering", "--capacity-hours-per-day", "8", "--generated-at", "2026-04-01T12:00:00Z",
                "--output", output, "--no-rate"], stdout, stderr, CancellationToken.None);
            Assert.NotEqual(0, exitCode);
            string json = await File.ReadAllTextAsync(output);
            SchemaValidationResult schema = ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioComparisonReport, json);
            Assert.True(schema.IsValid, string.Join(" ", schema.Errors));
            ChangePortfolioComparisonReport report = ContractJson.Deserialize<ChangePortfolioComparisonReport>(json);
            Assert.Empty(ContractValidation.Validate(report));
            Assert.Equal(ChangePortfolioComparisonStatus.Incomplete, report.Status);
            Assert.Null(report.SourcePortfolio);
            Assert.Empty(report.Series);
            Assert.False(report.Discovery!.Complete);
            Assert.Equal(257, report.Discovery.ConsideredRepositoryCount);
            Assert.Equal(runner.Calls.Count, report.Discovery.ProviderQueryCount);
            Assert.Equal(runner.Calls.Count, report.Discovery.ProviderProcessCount);
            Assert.True(report.Discovery.ProviderPageCount > 0);
            ChangePortfolioComparisonFailure failure = Assert.Single(report.Execution.Failures);
            Assert.Equal("github-discovery-budget-exceeded", failure.Category);
            Assert.Equal("narrow-scope-or-use-pinned-manifest", failure.AgentAction!.SuggestedAction);
            Assert.Contains("256-repository", failure.Message, StringComparison.Ordinal);
            Assert.False(Directory.Exists(repositories));
            Assert.DoesNotContain(workspace, json, StringComparison.OrdinalIgnoreCase);
        }
        finally { DeleteDirectory(workspace); }
    }
}
