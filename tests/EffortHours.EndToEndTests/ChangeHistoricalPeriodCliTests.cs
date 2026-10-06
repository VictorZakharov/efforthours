using System.Globalization;
using System.Text.Json;
using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests : ChangeCliTestSupport
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task HistoricalPeriodRecoversJanuaryAuthorDateFromMergedPrAndReusesEvidence(bool defaultHistory, bool restricted)
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
            if (defaultHistory) await repository.GitAsync("merge", "--ff-only", implementation.Trim());
            string statusBefore = await repository.GitAsync("status", "--porcelain=v1");
            HistoricalProviderRunner runner = new(baseline, implementation, retainedHead, defaultHistory ? implementation : null, repositoryCount: restricted ? 257 : 1);
            GitHubAuthorPeriodDiscovery discovery = new(runner,
                new GitHubRepositoryCache(new ExternalCommandRunner(), new GitClient(), Path.Combine(workspace, "cache"),
                    identity => { Assert.Equal("example/repository", identity); return repository.RootPath; }), new GitHubProviderMetadataCache(Path.Combine(workspace, "metadata")));
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
            if (restricted) arguments = [.. arguments, "--repository", "example/repository"];
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
            Assert.Equal(defaultHistory ? 1 : 0, report.Discovery.DefaultHeadCount);
            Assert.Equal("measured-retained-change", report.NativePeriod!.DailyEvidence![0].State);
            Assert.All(report.NativePeriod.DailyEvidence.Skip(1), day => Assert.Equal("no-retained-change", day.State));
            Assert.Equal(40m, report.Series.Single(series => series.Kind == ChangePortfolioSeriesKind.Portfolio).TotalCapacityHours);
            Assert.DoesNotContain("selected@example.invalid", json, StringComparison.Ordinal);
            Assert.DoesNotContain(repository.RootPath, json, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(statusBefore, await repository.GitAsync("status", "--porcelain=v1"));
            Assert.Contains(runner.Calls, call => call.Contains("states:[OPEN,CLOSED,MERGED]", StringComparison.Ordinal));
            Assert.All(runner.Calls.Where(call => call.Contains("commits?sha=main", StringComparison.Ordinal)),
                call =>
                {
                    Assert.DoesNotContain("since=", call, StringComparison.Ordinal);
                    Assert.DoesNotContain("until=", call, StringComparison.Ordinal);
                    Assert.DoesNotContain("--paginate", call, StringComparison.Ordinal);
                    Assert.Contains("per_page=1", call, StringComparison.Ordinal);
                });

            Assert.Equal(0, await command.ExecuteAsync(arguments, stdout, stderr, CancellationToken.None));
            ChangePortfolioComparisonReport warm = ContractJson.Deserialize<ChangePortfolioComparisonReport>(
                await File.ReadAllTextAsync(reportPath))!;
            Assert.Equal(report.Verification.SemanticDigest, warm.Verification.SemanticDigest);
            Assert.Equal(1, warm.Execution.Checkpoint.HitCount);
            Assert.Equal(0, warm.Discovery!.Acquisition!.AcquiredBytes);
            Assert.Equal(1, warm.Discovery.Acquisition.RepositoryCount);
            Assert.Equal(restricted ? 257 : 1, warm.Discovery.ProviderRepositoryCount);
            Assert.Equal(1, warm.Discovery.ConsideredRepositoryCount);
            if (restricted)
            {
                Assert.Equal(256, warm.Discovery.RepositoryRestriction!.ExcludedRepositoryCount);
                Assert.DoesNotContain(runner.Calls, call => call.Contains("repos/example/repository-", StringComparison.Ordinal));
                Assert.Contains("explicitly restricted", ChangePortfolioPeriodMarkdownRenderer.Render(warm), StringComparison.Ordinal);
            }
            Assert.Contains("reason=unpruned-default-author-date-evidence", stderr.ToString(), StringComparison.Ordinal);
            await AssertWorkdayAllocationAsync(workspace, report);
            Assert.Single(runner.Calls, call => call.Contains("&author=selected", StringComparison.Ordinal));
            repository.WriteText("Later.cs", "public class Later { public bool Added => true; }");
            string later = (await HistoricalCommitAsync(repository, "later", "2026-03-30T12:00:00Z", "2026-03-30T12:00:00Z")).Trim();
            runner.CurrentDefaultHead = later;
            Assert.Equal(0, await command.ExecuteAsync(arguments, stdout, stderr, CancellationToken.None));
            ChangePortfolioComparisonReport changedHead = ContractJson.Deserialize<ChangePortfolioComparisonReport>(
                await File.ReadAllTextAsync(reportPath))!;
            Assert.Equal(report.SourcePortfolio.TotalEffort, changedHead.SourcePortfolio!.TotalEffort);
            Assert.Equal(2, runner.Calls.Count(call => call.Contains("&author=selected", StringComparison.Ordinal)));
            Assert.DoesNotContain("selected@example.invalid", ContractJson.Serialize(changedHead), StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspace);
        }
    }

    private sealed class HistoricalProviderRunner(string baseline, string implementation, string head, string? defaultHead = null, int repositoryCount = 1, bool crossRepositoryAliases = false) : IExternalCommandRunner
    {
        public System.Collections.Concurrent.ConcurrentQueue<string> Calls { get; } = new();
        public string CurrentDefaultHead { get; set; } = defaultHead ?? baseline;

        public Task<ExternalCommandResult> RunAsync(string executable, string workingDirectory,
            IReadOnlyList<string> arguments, CancellationToken cancellationToken, bool requireSuccess = true)
        {
            string call = string.Join(' ', arguments);
            Calls.Enqueue(call);
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
                response = new[] { Enumerable.Range(0, repositoryCount).Select(index => new
                {
                    id = 42 + index, full_name = index == 0 ? "example/repository" : "example/repository-" + index.ToString(CultureInfo.InvariantCulture),
                    default_branch = "main",
                }).ToArray() };
            }
            else if (call.Contains("&author=selected", StringComparison.Ordinal))
            {
                response = new[] { new[] { Commit(implementation, [baseline], "2026-01-19T17:35:17Z", "2026-03-13T11:47:19Z",
                    email: crossRepositoryAliases && !call.Contains("repository-1/", StringComparison.Ordinal) ? "alternate@example.invalid" : "selected@example.invalid") } };
            }
            else if (call.Contains("commits?sha=main", StringComparison.Ordinal))
            {
                response = new[] { new[] { Commit(CurrentDefaultHead, [], "2025-01-01T12:00:00Z", "2025-01-01T12:00:00Z", login: crossRepositoryAliases ? "unrelated" : "selected", email: crossRepositoryAliases ? "unrelated@example.invalid" : "selected@example.invalid") } };
            }
            else if (call.Contains("graphql", StringComparison.Ordinal))
            {
                response = new[] { new { data = new { user = new { pullRequests = new { totalCount = crossRepositoryAliases ? 0 : 1,
                    nodes = Enumerable.Repeat(new { number = 7, state = "MERGED", author = new { login = "selected" },
                        repository = new { nameWithOwner = "example/repository" } }, crossRepositoryAliases ? 0 : 1).ToArray(),
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

        private static object Commit(string sha, string[] parents, string authorDate, string committerDate, string login = "selected", string email = "selected@example.invalid") => new
        {
            sha,
            parents = parents.Select(parent => new { sha = parent }),
            author = new { login },
            commit = new
            {
                author = new { name = "Selected Contributor", email, date = authorDate },
                committer = new { name = "Integrator", email = "integrator@example.invalid", date = committerDate },
                message = "synthetic"
            },
        };
    }
}
