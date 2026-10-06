using EffortHours.Change;
using EffortHours.ChangeBenchmarks;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class GitHubLargeHistoricalInventoryTests
{
    [Fact]
    public async Task LargeUnrelatedPopulationMatchesTrustedSmallInventoryColdAndWarmWithoutFallback()
    {
        var reference = await HistoricalPullProviderFixture.DiscoverRestrictedAsync(new(258), new());
        MemoryPullMetadataCache cache = new();
        foreach (bool warm in new[] { false, true })
        {
            HistoricalPullProviderFixture runner = new(258) { UnrelatedPullCount = 10000 };
            ProviderQueryCounters counters = new() { PullMetadataCache = cache };
            var actual = await HistoricalPullProviderFixture.DiscoverRestrictedAsync(runner, counters);
            Assert.Equal(reference!.Single().Heads, actual!.Single().Heads);
            Assert.Equal(warm ? 103 : 125, counters.QueryCount);
            Assert.Equal(258, counters.Diagnostics("missing").HistoricalPullRequests!.CandidateCount);
            Assert.Equal(warm ? 258 : 0, counters.Diagnostics("missing").HistoricalPullRequests!.CacheHitCount);
            Assert.All(runner.Calls, call => Assert.DoesNotContain("--paginate", call));
        }
    }

    [Theory]
    [InlineData(10000)]
    [InlineData(10042)]
    public async Task RestPagesRetainOldPrDatesAndRequireTerminalEmptyPageForExactHundreds(int unrelated)
    {
        HistoricalPullProviderFixture runner = new(258) { UnrelatedPullCount = unrelated };
        ProviderQueryCounters counters = new();
        var result = await GitHubAuthorPeriodDiscoveryJson.DiscoverHeadsAsync(runner, "in-memory-fixture",
            new("42", "owner/project", "main"), ["selected@example.invalid"], "selected",
            HistoricalPullProviderFixture.Since, HistoricalPullProviderFixture.Since.AddDays(5),
            ChangePortfolioDateField.Author, ChangePortfolioMergePolicy.Exclude, ChangePortfolioCoauthorPolicy.Include,
            true, counters, CancellationToken.None, includeDefaultHead: false, includeHistoricalPullRequests: true);
        Assert.Equal(HistoricalPullProviderFixture.Id(7), Assert.Single(result!.Heads).ObjectId);
        var inventories = runner.Calls.Where(call => call.Any(value => value.Contains("pulls?", StringComparison.Ordinal))).ToArray();
        Assert.Equal((258 + unrelated) / 100 + 1, inventories.Length);
        Assert.All(inventories, call => { Assert.Contains("--jq", call); Assert.DoesNotContain("--paginate", call); });
    }

    [Fact]
    public async Task RestInventoryAndMetadataReadersShareOneProcessWideFourReaderGate()
    {
        HistoricalPullProviderFixture runner = new(1, 1) { UnrelatedPullCount = 1000 };
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => GitHubAuthorPeriodDiscoveryJson.DiscoverHeadsAsync(runner, "in-memory-fixture",
            new("42", "owner/project", "main"), ["selected@example.invalid"], "selected", HistoricalPullProviderFixture.Since,
            HistoricalPullProviderFixture.Since.AddDays(5), ChangePortfolioDateField.Author, ChangePortfolioMergePolicy.Exclude,
            ChangePortfolioCoauthorPolicy.Include, true, new(), CancellationToken.None, includeDefaultHead: false, includeHistoricalPullRequests: true)));
        Assert.InRange(runner.Peak, 1, 4);
    }

    [Fact]
    public async Task AuthoredCandidateBoundNeverReturnsTruncatedHeads()
    {
        ProviderQueryCounters counters = new();
        GitHubProviderException failure = await Assert.ThrowsAsync<GitHubProviderException>(() =>
            HistoricalPullProviderFixture.DiscoverRestrictedAsync(new(1001), counters));
        Assert.Contains("authored-candidate", failure.Message, StringComparison.Ordinal);
        Assert.Contains("1001", failure.Message, StringComparison.Ordinal);
        Assert.Equal("inspect-pr-discovery-or-use-pinned-manifest", failure.Action.SuggestedAction);
        Assert.Equal(0, counters.Diagnostics("missing").HistoricalPullRequests?.CompletedCount ?? 0);
    }
}
