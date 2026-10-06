using System.Text.Json;
using System.Text.Json.Nodes;
using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class GitHubAuthorPeriodDiscoveryTests
{
    [Theory]
    [InlineData("open", 1, 0)]
    [InlineData("closed", 0, 1)]
    public async Task HistoricalDiscoveryIncludesRetainedClosedHeadsWithoutTimestampPruning(
        string state, int openCount, int historicalCount)
    {
        string defaultHead = new('a', 40);
        string pullHead = new('b', 40);
        string parent = new('c', 40);
        JsonNode page = JsonNode.Parse(CommitPage(pullHead, parent, "Target", "target@example.test",
            "2026-01-19T17:35:17Z"))!;
        page[0]![0]!["commit"]!["committer"]!["date"] = "2026-03-13T11:47:19Z";
        QueueRunner runner = new(
            CommitPage(defaultHead, parent, "Other", "other@example.test", "2026-03-17T12:00:00Z"),
            JsonSerializer.Serialize(new[]
            {
                new { number = 7, state, user = new { login = "target" }, head = new { sha = pullHead } },
            }),
            "{\"data\":null}",
            JsonSerializer.Serialize(new { commits = 1, head = new { sha = pullHead }, @base = new { sha = parent } }),
            "{\"data\":null}",
            JsonSerializer.Serialize(new { commits = 1, head = new { sha = pullHead }, @base = new { sha = parent } }),
            page.ToJsonString());
        ProviderQueryCounters counters = new();
        DiscoveredRepository result = Assert.IsType<DiscoveredRepository>(
            await GitHubAuthorPeriodDiscoveryJson.DiscoverHeadsAsync(runner, "virtual-workspace",
                new GitHubDiscoveryRepository("42", "owner/repository", "main"),
                ["target@example.test"], "target",
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
                ChangePortfolioDateField.Author, ChangePortfolioMergePolicy.Exclude,
                ChangePortfolioCoauthorPolicy.Include, true, counters, CancellationToken.None,
                includeHistoricalPullRequests: true));

        Assert.Equal(pullHead, Assert.Single(result.Heads).ObjectId);
        Assert.Equal(openCount, counters.OpenPullRequestCount);
        Assert.Equal(historicalCount, counters.HistoricalPullRequestCount);
        Assert.Contains(runner.Calls[1], argument => argument.Contains("state=all", StringComparison.Ordinal));
        Assert.All(runner.Calls[0], argument => Assert.DoesNotContain("since=", argument, StringComparison.Ordinal));
        Assert.All(runner.Calls[0], argument => Assert.DoesNotContain("until=", argument, StringComparison.Ordinal));
    }
}
