using System.Globalization;
using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests
{
    [Theory]
    [InlineData("timeout")]
    [InlineData("bytes")]
    [InlineData("cancel")]
    public async Task HistoricalAcquisitionStopsWithoutAggregatesAndRetainsReusableObjects(string failureMode)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "efforthours-acquisition-budget", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            using GitFixture repository = await GitFixture.CreateAsync(Path.Combine(workspace, "source"));
            repository.WriteText("Demo.csproj", ProjectFile);
            string baseline = await HistoricalCommitAsync(repository, "base", "2025-01-01T12:00:00Z", "2025-01-01T12:00:00Z");
            repository.WriteText("Feature.cs", "public class Feature { public int Value => 1; }");
            string retained = await HistoricalCommitAsync(repository, "feature", "2026-01-19T12:00:00Z", "2026-03-13T12:00:00Z");
            string implementation = retained;
            repository.WriteText("Review.cs", "public class Review { public bool Ready => true; }");
            retained = await HistoricalCommitAsync(repository, "review", "2026-03-16T12:00:00Z", "2026-03-16T12:00:00Z");
            await repository.GitAsync("update-ref", "refs/pull/7/head", retained);
            HistoricalProviderRunner provider = new(baseline, implementation, retained, retained);
            using CancellationTokenSource caller = new();
            AcquisitionRunner acquisition = new(failureMode, caller);
            GitHubAuthorPeriodDiscovery discovery = new(provider,
                new GitHubRepositoryCache(acquisition, new GitClient(acquisition, (_, _, _) => throw new NotSupportedException()), Path.Combine(workspace, "cache"), _ => repository.RootPath),
                new GitHubProviderMetadataCache(Path.Combine(workspace, "metadata")));
            ChangePortfolioCommand command = new(new ChangeEstimator(),
                (_, _, _, _, _) => throw new NotSupportedException(), (_, _, _, _) => throw new NotSupportedException(),
                (_, _) => throw new NotSupportedException(), (_, _, _) => throw new NotSupportedException(), null, discovery.DiscoverTodayAsync);
            string output = Path.Combine(workspace, "incomplete.json");
            string[] args = ["--native-period", "--owner", "example", "--repository", "example/repository", "--author", "selected",
                "--since", "2026-01-19", "--until", "2026-01-24", "--breakdown", "day", "--timezone", "UTC", "--scope", "engineering",
                "--capacity-hours-per-day", "8", "--generated-at", "2026-04-01T12:00:00Z", "--output", output, "--no-rate",
                "--discovery-timeout-seconds", failureMode == "timeout" ? "1" : "900", "--max-acquired-mib", "1"];
            using StringWriter stdout = new(CultureInfo.InvariantCulture);
            using StringWriter stderr = new(CultureInfo.InvariantCulture);
            if (failureMode == "cancel")
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command.ExecuteAsync(args, stdout, stderr, caller.Token));
                Assert.False(File.Exists(output));
            }
            else
            {
                int exit = await command.ExecuteAsync(args, stdout, stderr, caller.Token);
                Assert.NotEqual(0, exit);
                string json = await File.ReadAllTextAsync(output);
                SchemaValidationResult schema = ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioComparisonReport, json);
                Assert.True(schema.IsValid, string.Join(" ", schema.Errors));
                ChangePortfolioComparisonReport report = ContractJson.Deserialize<ChangePortfolioComparisonReport>(json);
                Assert.Empty(ContractValidation.Validate(report));
                Assert.Equal(ChangePortfolioComparisonStatus.Incomplete, report.Status);
                Assert.Null(report.SourcePortfolio);
                Assert.Empty(report.Series);
                Assert.Equal("github-discovery-budget-exceeded", Assert.Single(report.Execution.Failures).Category);
                Assert.Contains(failureMode == "timeout" ? "deadline" : "growth budget", report.Execution.Failures[0].Message, StringComparison.Ordinal);
                Assert.DoesNotContain(workspace, json, StringComparison.OrdinalIgnoreCase);
                if (failureMode == "bytes")
                {
                    Assert.True(report.Discovery!.Acquisition!.AcquiredBytes > 1048576);
                    // The completed immutable fetch survives the failed budget. Raising only
                    // the explicit operational bound reuses it without another Git fetch.
                    args[^1] = "4";
                    Assert.True(await command.ExecuteAsync(args, stdout, stderr, CancellationToken.None) == 0, stderr.ToString());
                    Assert.Equal(1, acquisition.FetchCount);
                    ChangePortfolioComparisonReport warm = ContractJson.Deserialize<ChangePortfolioComparisonReport>(await File.ReadAllTextAsync(output));
                    Assert.Equal(0, warm.Discovery!.Acquisition!.AcquiredBytes);
                    Assert.Single(Assert.Single(warm.Selection.AuthorPeriodManifest!.Repositories).Heads);
                    Assert.Equal(1, warm.Discovery.DefaultHeadCount);
                    Assert.Equal(0, warm.Discovery.HistoricalPullRequestHeadCount);
                }
            }
            Assert.Equal(1, acquisition.FetchCount);
            Assert.True(acquisition.FetchDrained);
        }
        finally { DeleteDirectory(workspace); }
    }

    private sealed class AcquisitionRunner(string mode, CancellationTokenSource caller) : IExternalCommandRunner
    {
        private readonly ExternalCommandRunner _real = new();
        public int FetchCount { get; private set; }
        public bool FetchDrained { get; private set; }
        public async Task<ExternalCommandResult> RunAsync(string executable, string workingDirectory,
            IReadOnlyList<string> arguments, CancellationToken cancellationToken, bool requireSuccess = true)
        {
            if (arguments.Contains("fetch", StringComparer.Ordinal))
            {
                FetchCount++;
                try
                {
                    if (mode == "cancel") caller.Cancel();
                    if (mode is "timeout" or "cancel") await Task.Delay(Timeout.Infinite, cancellationToken);
                    return await _real.RunAsync(executable, workingDirectory, arguments, cancellationToken, requireSuccess);
                }
                finally { FetchDrained = true; }
            }
            ExternalCommandResult result = await _real.RunAsync(executable, workingDirectory, arguments, cancellationToken, requireSuccess);
            if (mode == "bytes" && FetchDrained && arguments is ["count-objects", "-v"])
                return result with { StandardOutput = string.Join("\n", result.StandardOutput.ReplaceLineEndings("\n").Split('\n').Where(line => !line.StartsWith("size-pack:", StringComparison.Ordinal))) + "\nsize-pack: 2048\n" };
            return result;
        }
    }
}
