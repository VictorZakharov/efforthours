using System.Globalization;
using System.Text.Json.Nodes;
using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class GitHubProviderBatchingTests
{
    [Theory]
    [InlineData("incomplete-history", 1)]
    [InlineData("branch-changed", 1)]
    [InlineData("malformed-response", 1)]
    [InlineData("repository-unavailable", 1)]
    [InlineData("provider-errors", 12)]
    public async Task DefaultFallbackPreservesOtherRepositoriesAndEarlierAndLaterBatches(
        string reason, int expectedFallback)
    {
        ProviderQueryCounters counters = new();
        BatchRunner runner = new(reason);
        DefaultHeadBatchResult result = await DiscoverBatches(runner, 25, counters);

        Assert.Equal(3, counters.QueryCount);
        Assert.Equal(25 - expectedFallback, result.Repositories.Count);
        Assert.Equal(expectedFallback, result.FallbackRepositories.Count);
        ChangePortfolioProviderFallback fallback = Assert.Single(counters.Diagnostics("missing").Fallbacks);
        Assert.Equal(reason, fallback.Reason);
        Assert.Equal(expectedFallback, fallback.RepositoryCount);
        Assert.Contains(result.Repositories, repository => repository.RepositoryIdentity == "owner/repository-0");
        Assert.Contains(result.Repositories, repository => repository.RepositoryIdentity == "owner/repository-24");
    }

    [Fact]
    public async Task DefaultBatchesAllowFourConcurrentRequestsAndNoMore()
    {
        BlockingBatchRunner runner = new();
        Task<DefaultHeadBatchResult> pending = DiscoverBatches(runner, 96, new());
        await runner.FourStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(4, runner.Active);
        runner.Release.SetResult();
        DefaultHeadBatchResult result = await pending;

        Assert.Empty(result.FallbackRepositories);
        Assert.Equal(96, result.Repositories.Count);
        Assert.Equal(4, runner.Peak);
    }

    [Theory]
    [InlineData("@me", "viewer")]
    [InlineData("viewer", "viewer")]
    [InlineData("another-user", "another-user")]
    [InlineData("selected@example.test", null)]
    [InlineData("Full Name", null)]
    public void AccountInventoryUsesTheRequestedSingleLogin(string alias, string? expected)
    {
        Assert.Equal(expected, GitHubAuthorPeriodDiscovery.SingleContributorLogin(new()
        {
            Owner = "owner",
            AuthorAliases = [alias],
            AsOf = Until,
            TimeZone = "UTC",
            Scope = "engineering",
        }, "viewer"));
    }

    [Fact]
    public async Task ProviderLinkedEmailWorksWithBothGraphQlAndRestForAnotherUser()
    {
        GitHubDiscoveryRepository repository = new("42", "owner/repository", "main");
        string graphCommit = GraphDefaultHead(new('a', 40), new('b', 40))
            .Replace("Selected", "Different Git display name", StringComparison.Ordinal);
        string restCommit = RestCommitPage(new('a', 40), new('b', 40))
            .Replace("Selected", "Different Git display name", StringComparison.Ordinal);
        ProviderQueryCounters graphCounters = new() { ContributorIdentity = new("selected") };
        DefaultHeadBatchResult graph = await GitHubAuthorPeriodDiscoveryJson.DiscoverDefaultHeadsBatchedAsync(
            new QueueRunner(graphCommit),
            "unused", [repository], ["selected"], Since, Until, ChangePortfolioDateField.Author,
            ChangePortfolioMergePolicy.Exclude, ChangePortfolioCoauthorPolicy.Include, graphCounters, default);
        ProviderQueryCounters restCounters = new() { ContributorIdentity = new("selected") };
        DiscoveredRepository? rest = await GitHubAuthorPeriodDiscoveryJson.DiscoverHeadsAsync(
            new QueueRunner(restCommit),
            "unused", repository, ["selected"], "different-viewer", Since, Until,
            ChangePortfolioDateField.Author, ChangePortfolioMergePolicy.Exclude,
            ChangePortfolioCoauthorPolicy.Include, false, restCounters, default);

        Assert.Equal(Assert.Single(graph.Repositories).Heads, rest!.Heads);
        ResolvedDiscoveryContributors resolved = new(
            [new("person", "selected", ["selected"])],
            new ChangePortfolioContributorSelection
            {
                Mode = ChangePortfolioContributorSelectionMode.SingleContributor,
                InputDigest = "placeholder",
            }, []);
        foreach (ProviderQueryCounters counters in new[] { graphCounters, restCounters })
        {
            Assert.Equal(["selected", "selected@example.test"],
                Assert.Single(counters.ContributorIdentity!.Apply(resolved).Contributors).Aliases);
        }
    }

    private static Task<DefaultHeadBatchResult> DiscoverBatches(
        IExternalCommandRunner runner, int count, ProviderQueryCounters counters) =>
        GitHubAuthorPeriodDiscoveryJson.DiscoverDefaultHeadsBatchedAsync(
            runner, "unused",
            [.. Enumerable.Range(0, count).Select(index =>
                new GitHubDiscoveryRepository(index.ToString(CultureInfo.InvariantCulture), $"owner/repository-{index}", "main"))],
            ["selected@example.test"], Since, Until, ChangePortfolioDateField.Author,
            ChangePortfolioMergePolicy.Exclude, ChangePortfolioCoauthorPolicy.Include, counters, default);

    private static string BatchResponse(IReadOnlyList<string> arguments, string? reason = null)
    {
        bool damagedBatch = arguments.Contains("name0=repository-12");
        if (damagedBatch && reason == "provider-errors")
        {
            return """{"errors":[{"message":"redacted"}]}""";
        }

        JsonObject data = [];
        int count = arguments.Count(value => value.StartsWith("name", StringComparison.Ordinal));
        for (int index = 0; index < count; index++)
        {
            JsonNode repository = JsonNode.Parse(GraphDefaultHead(new('a', 40), new('b', 40)))!["data"]!["r0"]!
                .DeepClone();
            if (damagedBatch && index == 3)
            {
                JsonNode branch = repository["defaultBranchRef"]!;
                if (reason == "incomplete-history")
                {
                    branch["target"]!["history"]!["pageInfo"]!["hasNextPage"] = true;
                }
                else if (reason == "branch-changed")
                {
                    branch["name"] = "changed";
                }
                else if (reason == "malformed-response")
                {
                    branch.AsObject().Remove("target");
                }
                else if (reason == "repository-unavailable")
                {
                    repository = null!;
                }
            }

            data["r" + index] = repository;
        }

        return new JsonObject { ["data"] = data }.ToJsonString();
    }

    private sealed class BatchRunner(string reason) : IExternalCommandRunner
    {
        public Task<ExternalCommandResult> RunAsync(
            string executable, string workingDirectory, IReadOnlyList<string> arguments,
            CancellationToken cancellationToken, bool requireSuccess = true) =>
            Task.FromResult(new ExternalCommandResult(0, BatchResponse(arguments, reason), ""));
    }

    private sealed class BlockingBatchRunner : IExternalCommandRunner
    {
        public TaskCompletionSource FourStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Active;
        public int Peak;

        public async Task<ExternalCommandResult> RunAsync(
            string executable, string workingDirectory, IReadOnlyList<string> arguments,
            CancellationToken cancellationToken, bool requireSuccess = true)
        {
            int active = Interlocked.Increment(ref Active);
            Interlocked.Exchange(ref Peak, Math.Max(active, Peak));
            if (active == 4)
            {
                FourStarted.TrySetResult();
            }

            await Release.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref Active);
            return new(0, BatchResponse(arguments), "");
        }
    }
}
