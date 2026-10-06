using EffortHours.Change;
using EffortHours.ChangeBenchmarks;

namespace EffortHours.Tests;

public sealed class GitHubHistoricalPullBatchTests
{
    [Fact]
    public async Task NarrowHistoryBatches258PrsAndWarmRunRefreshesOnlyLiveInventory()
    {
        HistoricalPullProviderFixture runner = new(258, 1);
        MemoryPullMetadataCache cache = new();
        ProviderQueryCounters cold = new() { PullMetadataCache = cache };
        IReadOnlyList<DiscoveredRepository>? first = await HistoricalPullProviderFixture.DiscoverAsync(runner, cold);
        Assert.Equal(HistoricalPullProviderFixture.Id(7), Assert.Single(Assert.Single(first!).Heads).ObjectId);
        Assert.Equal(23, cold.QueryCount);
        Assert.Equal(25, cold.PageCount);
        Assert.Equal(23, cold.ProcessCount);
        Assert.Equal(258, cold.Diagnostics("missing").HistoricalPullRequests!.CandidateCount);
        Assert.Equal(22, cold.Diagnostics("missing").HistoricalPullRequests!.BatchCount);
        Assert.Equal(258, cold.Diagnostics("missing").HistoricalPullRequests!.CompletedCount);
        Assert.Equal(0, cold.Diagnostics("missing").HistoricalPullRequests!.PendingCount);
        Assert.InRange(runner.Peak, 1, 4);
        ProviderQueryCounters warm = new() { PullMetadataCache = cache };
        IReadOnlyList<DiscoveredRepository>? second = await HistoricalPullProviderFixture.DiscoverAsync(runner, warm);
        Assert.Equal(first!.Single().Heads, second!.Single().Heads);
        Assert.Equal(1, warm.QueryCount);
        Assert.Equal(3, warm.PageCount);
        Assert.Equal(258, warm.Diagnostics("hit").HistoricalPullRequests!.CacheHitCount);
        Assert.Equal(0, warm.Diagnostics("hit").HistoricalPullRequests!.BatchCount);
        Assert.DoesNotContain(runner.Calls, call => call.Any(value => value.Contains("other/private", StringComparison.Ordinal)));
        runner.BaseHead = HistoricalPullProviderFixture.Id(20000);
        ProviderQueryCounters advanced = new() { PullMetadataCache = cache };
        await HistoricalPullProviderFixture.DiscoverAsync(runner, advanced);
        Assert.Equal(0, advanced.Diagnostics("hit").HistoricalPullRequests!.CacheHitCount);
        Assert.Equal(23, advanced.QueryCount);
    }

    [Fact]
    public async Task BroadAnnualHistoryKeepsEarlierAuthorDatesAcrossSixteenRepositoriesWithBoundedReuse()
    {
        HistoricalPullProviderFixture runner = new(258, 1) { RepositoryCount = 16 };
        MemoryPullMetadataCache cache = new();
        ProviderQueryCounters cold = new() { PullMetadataCache = cache };
        IReadOnlyList<DiscoveredRepository>? first = await HistoricalPullProviderFixture.DiscoverScopeAsync(runner, cold, true, 16);
        Assert.Equal(16, first!.Count);
        Assert.Equal(257, first.Sum(repository => repository.Heads.Count));
        Assert.Equal(33, cold.QueryCount);
        Assert.Equal(35, cold.PageCount);
        Assert.Equal(258, cold.Diagnostics("missing").HistoricalPullRequests!.CompletedCount);
        Assert.Equal(257, cold.Diagnostics("missing").HistoricalPullRequests!.SelectedCount);
        Assert.InRange(runner.Peak, 1, 4);
        ProviderQueryCounters warm = new() { PullMetadataCache = cache };
        IReadOnlyList<DiscoveredRepository>? second = await HistoricalPullProviderFixture.DiscoverScopeAsync(runner, warm, true, 16);
        Assert.Equal(1, warm.QueryCount);
        Assert.Equal(258, warm.Diagnostics("hit").HistoricalPullRequests!.CacheHitCount);
        Assert.Equal(first.SelectMany(repository => repository.Heads).Select(head => head.ObjectId).Order(),
            second!.SelectMany(repository => repository.Heads).Select(head => head.ObjectId).Order());
        ProviderQueryCounters restricted = new() { PullMetadataCache = cache };
        IReadOnlyList<DiscoveredRepository>? one = await HistoricalPullProviderFixture.DiscoverScopeAsync(runner, restricted, true, 1);
        Assert.Equal(17, Assert.Single(one!).Heads.Count);
        Assert.Equal(17, restricted.Diagnostics("hit").HistoricalPullRequests!.CandidateCount);
        Assert.Equal(17, restricted.Diagnostics("hit").HistoricalPullRequests!.CacheHitCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncompleteOrUnavailableBatchUsesCompleteRestAndCachesOnlyFinishedMetadata(bool failBatch)
    {
        HistoricalPullProviderFixture runner = new(2) { IncompleteBatch = !failBatch, FailBatch = failBatch };
        ProviderQueryCounters counters = new() { PullMetadataCache = new MemoryPullMetadataCache() };
        Assert.Empty((await HistoricalPullProviderFixture.DiscoverAsync(runner, counters))!);
        Assert.Equal(6, counters.QueryCount);
        Assert.Equal(2, counters.Diagnostics("missing").HistoricalPullRequests!.FallbackCount);
        ProviderQueryCounters warm = new() { PullMetadataCache = counters.PullMetadataCache };
        await HistoricalPullProviderFixture.DiscoverAsync(runner, warm);
        Assert.Equal(1, warm.QueryCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialAliasErrorOrMissingParentRefreshesOnlyAffectedPr(bool aliasError)
    {
        HistoricalPullProviderFixture runner = new(2) { PartialBatchError = aliasError, IncompleteParents = !aliasError };
        ProviderQueryCounters counters = new() { PullMetadataCache = new MemoryPullMetadataCache() };
        await HistoricalPullProviderFixture.DiscoverAsync(runner, counters);
        Assert.Equal(4, counters.QueryCount);
        Assert.Equal(1, counters.Diagnostics("missing").HistoricalPullRequests!.FallbackCount);
        Assert.Equal(2, counters.Diagnostics("missing").HistoricalPullRequests!.CompletedCount);
        ProviderQueryCounters warm = new() { PullMetadataCache = counters.PullMetadataCache };
        await HistoricalPullProviderFixture.DiscoverAsync(runner, warm);
        Assert.Equal(1, warm.QueryCount);
    }

    [Fact]
    public async Task ChangedLiveHeadFailsInsteadOfSelectingOrCachingDifferentEvidence()
    {
        HistoricalPullProviderFixture runner = new(12) { ChangedDuringBatch = true };
        ProviderQueryCounters counters = new() { PullMetadataCache = new MemoryPullMetadataCache() };
        await Assert.ThrowsAsync<GitHubProviderException>(() => HistoricalPullProviderFixture.DiscoverAsync(runner, counters));
        Assert.Equal(0, counters.Diagnostics("missing").HistoricalPullRequests!.CompletedCount);
    }

    [Fact]
    public void OverlappingRepositoryRequestsDiscloseCumulativeAndWallTimeAndPreserveInterruptedContext()
    {
        ProviderQueryCounters counters = new();
        counters.AddQuery(GitHubProviderFailure.OpenPullRequestPhase);
        using (ProviderQueryCounters.RequestObservation first = counters.ObserveRequest(["api", "repos/owner/project/pulls/7"], GitHubProviderFailure.OpenPullRequestPhase))
        {
            counters.AddQuery(GitHubProviderFailure.OpenPullRequestPhase);
            using (ProviderQueryCounters.RequestObservation second = counters.ObserveRequest(["api", "repos/owner/project/pulls/8/commits"], GitHubProviderFailure.OpenPullRequestPhase)) { }
            first.Complete(1);
        }
        var diagnostics = counters.Diagnostics("missing");
        var observation = Assert.Single(diagnostics.RepositoryObservations!);
        Assert.Equal("cumulative-request", observation.ElapsedKind);
        Assert.NotNull(observation.WallElapsedMilliseconds);
        Assert.True(observation.ElapsedMilliseconds + .002m >= observation.WallElapsedMilliseconds);
        Assert.Equal("pull-commits", diagnostics.LastRequest!.Operation);
        Assert.Equal("incomplete", diagnostics.LastRequest.State);
        Assert.DoesNotContain("owner/project", diagnostics.LastRequest.RepositoryDigest, StringComparison.Ordinal);
    }

    [Fact]
    public void DeadlineUsesProviderSubphaseAndScopeAwareSuggestion()
    {
        using CancellationTokenSource deadline = new();
        ProviderQueryCounters counters = new();
        counters.AddQuery(GitHubProviderFailure.OpenPullRequestPhase);
        GitHubDiscoveryAcquisitionBudget budget = new(new()
        {
            Owner = "owner",
            Repositories = ["owner/project"],
            AsOf = DateTimeOffset.UtcNow,
            TimeZone = "UTC",
            Scope = "engineering"
        }, deadline, CancellationToken.None)
        { Counters = counters };
        GitHubProviderException failure = Assert.IsType<GitHubProviderException>(budget.Failure(new OperationCanceledException()));
        Assert.Equal("open-pr-discovery", failure.Action.Phase);
        Assert.Equal("inspect-pr-discovery-or-use-pinned-manifest", failure.Action.SuggestedAction);
        Assert.Equal(0, failure.Action.RetryLimit);
    }
}
