using System.Globalization;
using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests
{
    [Fact]
    public async Task PrDiscoveryDeadlineRetainsRequestPlanAndProviderContextWithoutAggregates()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "efforthours-pr-deadline", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            using GitFixture repository = await GitFixture.CreateAsync(Path.Combine(workspace, "source"));
            repository.WriteText("Demo.csproj", ProjectFile);
            string baseline = await HistoricalCommitAsync(repository, "base", "2025-01-01T12:00:00Z", "2025-01-01T12:00:00Z");
            PrTimeoutRunner runner = new(new HistoricalProviderRunner(baseline, baseline, baseline));
            GitHubAuthorPeriodDiscovery discovery = new(runner,
                new GitHubRepositoryCache(new ExternalCommandRunner(), new GitClient(), Path.Combine(workspace, "cache"), _ => repository.RootPath),
                new GitHubProviderMetadataCache(Path.Combine(workspace, "metadata")));
            ChangePortfolioCommand command = new(new ChangeEstimator(), (_, _, _, _, _) => throw new NotSupportedException(),
                (_, _, _, _) => throw new NotSupportedException(), (_, _) => throw new NotSupportedException(),
                (_, _, _) => throw new NotSupportedException(), null, discovery.DiscoverTodayAsync);
            string output = Path.Combine(workspace, "incomplete.json");
            using StringWriter stdout = new(CultureInfo.InvariantCulture), stderr = new(CultureInfo.InvariantCulture);
            int result = await command.ExecuteAsync(["--native-period", "--owner", "example", "--repository", "example/repository", "--author", "selected",
                "--since", "2026-01-19", "--until", "2026-01-24", "--breakdown", "day", "--timezone", "UTC", "--scope", "engineering",
                "--capacity-hours-per-day", "8", "--generated-at", "2026-04-01T12:00:00Z", "--output", output, "--no-rate",
                "--discovery-timeout-seconds", "10"], stdout, stderr, CancellationToken.None);
            Assert.NotEqual(0, result);
            string json = await File.ReadAllTextAsync(output);
            SchemaValidationResult schema = ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioComparisonReport, json);
            Assert.True(schema.IsValid, string.Join(" ", schema.Errors));
            ChangePortfolioComparisonReport report = ContractJson.Deserialize<ChangePortfolioComparisonReport>(json);
            Assert.Empty(ContractValidation.Validate(report));
            Assert.Null(report.SourcePortfolio);
            Assert.Empty(report.Series);
            ChangePortfolioComparisonFailure failure = Assert.Single(report.Execution.Failures);
            Assert.Equal("open-pr-discovery", failure.Phase);
            Assert.Equal("inspect-pr-discovery-or-use-pinned-manifest", failure.AgentAction!.SuggestedAction);
            var diagnostics = report.Discovery!.ProviderDiagnostics!;
            Assert.Equal(1, diagnostics.HistoricalPullRequests!.PendingCount);
            Assert.Equal("pull-metadata-batch", diagnostics.LastRequest!.Operation);
            Assert.Equal("incomplete", diagnostics.LastRequest.State);
            Assert.True(runner.Drained);
            Assert.All(diagnostics.RepositoryObservations!, observation => Assert.True(observation.WallElapsedMilliseconds <= report.Discovery.ElapsedMilliseconds));
            Assert.DoesNotContain(workspace, json, StringComparison.OrdinalIgnoreCase);
        }
        finally { DeleteDirectory(workspace); }
    }

    private sealed class PrTimeoutRunner(IExternalCommandRunner inner) : IExternalCommandRunner
    {
        public bool Drained { get; private set; }
        public async Task<ExternalCommandResult> RunAsync(string executable, string directory, IReadOnlyList<string> arguments,
            CancellationToken token, bool requireSuccess = true)
        {
            if (!arguments.Any(value => value.Contains("pullRequest(number:", StringComparison.Ordinal)))
                return await inner.RunAsync(executable, directory, arguments, token, requireSuccess);
            try { await Task.Delay(Timeout.Infinite, token); throw new InvalidOperationException(); }
            finally { Drained = true; }
        }
    }
}
