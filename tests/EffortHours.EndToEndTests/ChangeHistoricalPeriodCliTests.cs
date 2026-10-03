using System.Globalization;
using System.Text.Json;
using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests : ChangeCliTestSupport
{
    [Fact]
    public async Task HistoricalPeriodRecoversJanuaryAuthorDateFromMergedPrAndReusesEvidence()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "efforthours-historical-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            using GitFixture repository = await GitFixture.CreateAsync(Path.Combine(workspace, "source"));
            repository.WriteText("Demo.csproj", ProjectFile);
            string baseline = await HistoricalCommitAsync(repository, "base", "2025-01-01T12:00:00Z", "2025-01-01T12:00:00Z");
            await repository.GitAsync("switch", "-c", "retained");
            repository.WriteText("Feature.cs", "public class Feature { public int Value => 1; }");
            string implementation = await HistoricalCommitAsync(repository, "implementation", "2026-01-19T17:35:17Z", "2026-03-13T11:47:19Z");
            repository.WriteText("Review.cs", "public class Review { public bool Ready => true; }");
            string retainedHead = await HistoricalCommitAsync(repository, "review", "2026-03-16T12:00:00Z", "2026-03-16T12:00:00Z");
            await repository.GitAsync("update-ref", "refs/pull/7/head", retainedHead);
            await repository.GitAsync("switch", "main");
            string statusBefore = await repository.GitAsync("status", "--porcelain=v1");
            HistoricalProviderRunner runner = new(baseline, implementation, retainedHead);
            GitHubAuthorPeriodDiscovery discovery = new(runner,
                new GitHubRepositoryCache(new ExternalCommandRunner(), new GitClient(), Path.Combine(workspace, "cache"),
                    _ => repository.RootPath), new GitHubProviderMetadataCache(Path.Combine(workspace, "metadata")));
            ChangePortfolioCommand command = new(new ChangeEstimator(),
                (_, _, _, _, _) => throw new NotSupportedException(),
                (_, _, _, _) => throw new NotSupportedException(),
                (_, _) => throw new NotSupportedException(),
                (_, _, _) => throw new NotSupportedException(), null, discovery.DiscoverTodayAsync);
            string reportPath = Path.Combine(workspace, "historical.json");
            string[] arguments = ["--native-period", "--owner", "example", "--author", "selected",
                "--since", "2026-01-19", "--until", "2026-01-24", "--breakdown", "day",
                "--timezone", "America/Toronto", "--scope", "engineering", "--capacity-hours-per-day", "8",
                "--generated-at", "2026-04-01T12:00:00Z", "--output", reportPath, "--no-rate"];
            using StringWriter stdout = new(CultureInfo.InvariantCulture);
            using StringWriter stderr = new(CultureInfo.InvariantCulture);
            int result = await command.ExecuteAsync(arguments, stdout, stderr, CancellationToken.None);
            Assert.True(result == 0, stderr.ToString());
            string json = await File.ReadAllTextAsync(reportPath);
            Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioComparisonReport, json).IsValid);
            ChangePortfolioComparisonReport report = ContractJson.Deserialize<ChangePortfolioComparisonReport>(json)!;
            Assert.Equal(implementation, Assert.Single(report.SourcePortfolio!.Items).Selection.Head.ObjectId);
            Assert.Equal(1, report.Discovery!.HistoricalPullRequestHeadCount);
            Assert.Equal(0, report.Discovery.OpenPullRequestHeadCount);
            Assert.Equal("measured-retained-change", report.NativePeriod!.DailyEvidence![0].State);
            Assert.All(report.NativePeriod.DailyEvidence.Skip(1), day => Assert.Equal("no-retained-change", day.State));
            Assert.Equal(40m, report.Series.Single(series => series.Kind == ChangePortfolioSeriesKind.Portfolio).TotalCapacityHours);
            Assert.DoesNotContain("selected@example.invalid", json, StringComparison.Ordinal);
            Assert.DoesNotContain(repository.RootPath, json, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(statusBefore, await repository.GitAsync("status", "--porcelain=v1"));
            Assert.Contains(runner.Calls, call => call.Contains("states:[OPEN,CLOSED,MERGED]", StringComparison.Ordinal));
            Assert.All(runner.Calls.Where(call => call.Contains("commits?sha=", StringComparison.Ordinal)),
                call => Assert.DoesNotContain("since=", call, StringComparison.Ordinal));

            Assert.Equal(0, await command.ExecuteAsync(arguments, stdout, stderr, CancellationToken.None));
            ChangePortfolioComparisonReport warm = ContractJson.Deserialize<ChangePortfolioComparisonReport>(
                await File.ReadAllTextAsync(reportPath))!;
            Assert.Equal(report.Verification.SemanticDigest, warm.Verification.SemanticDigest);
            Assert.Equal(1, warm.Execution.Checkpoint.HitCount);
        }
        finally
        {
            DeleteDirectory(workspace);
        }
    }

    private sealed class HistoricalProviderRunner(string baseline, string implementation, string head) : IExternalCommandRunner
    {
        public List<string> Calls { get; } = [];

        public Task<ExternalCommandResult> RunAsync(string executable, string workingDirectory,
            IReadOnlyList<string> arguments, CancellationToken cancellationToken, bool requireSuccess = true)
        {
            string call = string.Join(' ', arguments);
            Calls.Add(call);
            object response;
            if (call == "api user")
            {
                response = new { login = "reviewer" };
            }
            else if (call == "api users/example")
            {
                response = new { type = "Organization" };
            }
            else if (call.Contains("orgs/example/repos", StringComparison.Ordinal))
            {
                response = new[] { new[] { new { id = 42, full_name = "example/repository", default_branch = "main" } } };
            }
            else if (call.Contains("commits?sha=main", StringComparison.Ordinal))
            {
                response = new[] { new[] { Commit(baseline, [], "2025-01-01T12:00:00Z", "2025-01-01T12:00:00Z") } };
            }
            else if (call.Contains("graphql", StringComparison.Ordinal))
            {
                response = new[] { new { data = new { user = new { pullRequests = new { totalCount = 1,
                    nodes = new[] { new { number = 7, state = "MERGED", author = new { login = "selected" },
                        repository = new { nameWithOwner = "example/repository" } } },
                    pageInfo = new { hasNextPage = false, endCursor = (string?)null } } } } } };
            }
            else if (call.Contains("pulls/7/commits", StringComparison.Ordinal))
            {
                response = new[] { new[] { Commit(implementation, [baseline], "2026-01-19T17:35:17Z", "2026-03-13T11:47:19Z"),
                    Commit(head, [implementation], "2026-03-16T12:00:00Z", "2026-03-16T12:00:00Z") } };
            }
            else if (call == "api repos/example/repository/pulls/7")
            {
                response = new { commits = 2, head = new { sha = head } };
            }
            else
            {
                throw new InvalidOperationException("Unexpected synthetic provider query: " + call);
            }

            return Task.FromResult(new ExternalCommandResult(0, JsonSerializer.Serialize(response), string.Empty));
        }

        private static object Commit(string sha, string[] parents, string authorDate, string committerDate) => new
        {
            sha,
            parents = parents.Select(parent => new { sha = parent }),
            author = new { login = "selected" },
            commit = new
            {
                author = new { name = "Selected Contributor", email = "selected@example.invalid", date = authorDate },
                committer = new { name = "Integrator", email = "integrator@example.invalid", date = committerDate },
                message = "synthetic"
            },
        };
    }
}
