using EffortHours.Change;
using EffortHours.ChangeBenchmarks;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class GitHubHistoricalResumeTests
{
    [Fact]
    public async Task SmallerCompleteAccountInventoryAvoidsUnrelatedRepositoryPopulationAndWarmPrefix()
    {
        MemoryPullMetadataCache cache = new();
        var reference = await HistoricalPullProviderFixture.DiscoverRestrictedAsync(new(440), new());
        foreach (bool warm in new[] { false, true })
        {
            HistoricalPullProviderFixture runner = new(440) { UnrelatedPullCount = 15944 };
            ProviderQueryCounters counters = new() { PullMetadataCache = cache };
            var result = await HistoricalPullProviderFixture.DiscoverOptimizedAsync(runner, counters);
            Assert.Equal(reference!.Single().Heads, result!.Single().Heads);
            Assert.Equal(warm ? 6 : 43, counters.QueryCount);
            var plan = counters.Diagnostics("missing").HistoricalPullRequests!;
            Assert.Equal("account-connection", plan.InventoryStrategy);
            Assert.True(plan.InventoryComplete);
            Assert.True(plan.MetadataComplete);
            Assert.Equal(6, plan.InventoryQueryCount);
            Assert.Equal(0, plan.HeaderQueryCount);
            Assert.Equal(warm ? 0 : 37, plan.MetadataQueryCount);
            Assert.Equal(warm ? 440 : 0, plan.CacheHitCount);
            Assert.Equal(warm ? 0 : 440, plan.CacheWriteCount);
            Assert.Equal(0, plan.PendingCount);
            Assert.All(runner.Calls, call => Assert.DoesNotContain("--slurp", call));
            Assert.InRange(runner.Peak, 1, 4);
        }
    }

    [Fact]
    public async Task LargeAccountWithSmallRequestedRepositoryKeepsScopedInventoryAndSkipsNullAuthors()
    {
        HistoricalPullProviderFixture runner = new(258)
        { OutsideAccountPullCount = 12000, UnrelatedPullCount = 1, NullUnrelatedAuthor = true };
        ProviderQueryCounters counters = new();
        var result = await HistoricalPullProviderFixture.DiscoverOptimizedAsync(runner, counters);
        Assert.Equal(HistoricalPullProviderFixture.Id(7), Assert.Single(Assert.Single(result!).Heads).ObjectId);
        Assert.Equal(26, counters.QueryCount);
        Assert.Equal("scoped-connections", counters.Diagnostics("missing").HistoricalPullRequests!.InventoryStrategy);
        Assert.Empty(counters.Diagnostics("missing").Fallbacks);
    }

    [Fact]
    public async Task UnavailableRepositoryFallsBackWithoutRepeatingCompletedSiblingInventory()
    {
        HistoricalPullProviderFixture runner = new(24) { RepositoryCount = 2, UnavailableScopedRepositoryIndex = 0, OutsideAccountPullCount = 1000 };
        ProviderQueryCounters counters = new();
        var result = await HistoricalPullProviderFixture.DiscoverOptimizedAsync(runner, counters, 2);
        Assert.Equal(HistoricalPullProviderFixture.Id(7), Assert.Single(Assert.Single(result!).Heads).ObjectId);
        Assert.Equal(7, counters.QueryCount);
        var plan = counters.Diagnostics("missing").HistoricalPullRequests!;
        Assert.Equal(24, plan.CompletedCount);
        Assert.Equal(1, plan.HeaderBatchCount);
        Assert.Equal(2, plan.BatchCount);
        Assert.True(plan.InventoryComplete);
        Assert.Equal(1, Assert.Single(counters.Diagnostics("missing").Fallbacks).RepositoryCount);
        Assert.DoesNotContain(runner.Calls, call => call.Any(value => value.StartsWith("repos/owner/project-1/pulls?", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task HeaderAliasFailureRetainsCompleteSiblingsAndCountsSeparatelyFromMetadataFallback()
    {
        HistoricalPullProviderFixture runner = new(2) { InvalidScopedTotal = true, PartialBatchError = true };
        ProviderQueryCounters counters = new();
        await HistoricalPullProviderFixture.DiscoverRestrictedAsync(runner, counters);
        var plan = counters.Diagnostics("missing").HistoricalPullRequests!;
        Assert.Equal(1, plan.HeaderBatchCount);
        Assert.Equal(1, plan.HeaderFallbackCount);
        Assert.Equal(1, plan.BatchCount);
        Assert.Equal(1, plan.FallbackCount);
        Assert.Equal(2, plan.HeaderQueryCount);
        Assert.Equal(3, plan.MetadataQueryCount);
        Assert.Equal(2, plan.CompletedCount);
    }

    [Fact]
    public async Task MoreThan1000OutOfScopeAuthoredPrsDoNotRejectCompleteIncludedAccountEvidence()
    {
        HistoricalPullProviderFixture runner = new(258) { OutsideAccountPullCount = 1200 };
        ProviderQueryCounters counters = new();
        var result = await HistoricalPullProviderFixture.DiscoverAsync(runner, counters);
        Assert.Equal(HistoricalPullProviderFixture.Id(7), Assert.Single(Assert.Single(result!).Heads).ObjectId);
        Assert.Equal(37, counters.QueryCount);
        Assert.Equal(258, counters.Diagnostics("missing").HistoricalPullRequests!.CompletedCount);
        Assert.DoesNotContain(runner.Calls, call => call.Contains("--slurp"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InconsistentAccountTotalOrCursorRequiresCompleteScopedFallbackBeforeMetadata(bool cursor)
    {
        HistoricalPullProviderFixture runner = new(258)
        { UnrelatedPullCount = 1000, InvalidAccountTotal = !cursor, RepeatedAccountCursor = cursor };
        ProviderQueryCounters counters = new();
        var result = await HistoricalPullProviderFixture.DiscoverOptimizedAsync(runner, counters);
        Assert.Equal(HistoricalPullProviderFixture.Id(7), Assert.Single(Assert.Single(result!).Heads).ObjectId);
        Assert.Equal("scoped-connections", counters.Diagnostics("missing").HistoricalPullRequests!.InventoryStrategy);
        Assert.Equal(258, counters.Diagnostics("missing").HistoricalPullRequests!.CompletedCount);
        Assert.Equal("account-connection-unavailable", Assert.Single(counters.Diagnostics("missing").Fallbacks).Reason);
        Assert.Equal(cursor ? 38 : 39, counters.QueryCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelled440CandidateAttemptPersists204CompletedRowsAndResumesOnly236Misses(bool restHeaders)
    {
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(10));
        using StopAfterCache cache = new(cancellation, 204);
        HistoricalPullProviderFixture fixture = new(440, 1) { InvalidScopedTotal = restHeaders };
        FenceRunner runner = new(fixture);
        ProviderQueryCounters interrupted = new() { PullMetadataCache = cache, CancellationOwner = () => "caller" };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => restHeaders
            ? GitHubAuthorPeriodDiscoveryJson.DiscoverHistoricalPullHeadsInScopeAsync(runner, "in-memory-fixture",
                [new("42", "owner/project", "main")], "selected", ["selected@example.invalid"],
                HistoricalPullProviderFixture.Since, HistoricalPullProviderFixture.Since.AddDays(5), ChangePortfolioDateField.Author,
                ChangePortfolioMergePolicy.Exclude, ChangePortfolioCoauthorPolicy.Include, interrupted, cancellation.Token)
            : HistoricalPullProviderFixture.DiscoverAsync(runner, interrupted, cancellation.Token));
        var pending = interrupted.Diagnostics("missing").HistoricalPullRequests!;
        Assert.True(pending.InventoryComplete);
        Assert.False(pending.MetadataComplete);
        Assert.Equal(440, pending.CandidateCount);
        Assert.Equal(204, pending.CompletedCount);
        Assert.Equal(236, pending.PendingCount);
        Assert.Equal(204, pending.CacheWriteCount);
        Assert.Equal("completed-metadata-reusable", pending.ResumeState);
        Assert.Equal(0, runner.Active);
        // Seventeen completed chunks plus at most four admitted, cancellable requests.
        if (restHeaders) Assert.InRange(pending.HeaderBatchCount!.Value, 17, 21);
        ProviderQueryCounters resumed = new() { PullMetadataCache = cache.Inner };
        var actual = restHeaders
            ? await HistoricalPullProviderFixture.DiscoverRestrictedAsync(new(440) { InvalidScopedTotal = true }, resumed)
            : await HistoricalPullProviderFixture.DiscoverAsync(new HistoricalPullProviderFixture(440), resumed);
        var plan = resumed.Diagnostics("hit").HistoricalPullRequests!;
        Assert.Equal(204, plan.CacheHitCount);
        Assert.Equal(236, plan.CacheWriteCount);
        Assert.Equal(20, plan.BatchCount);
        Assert.Equal(440, plan.CompletedCount);
        Assert.True(plan.MetadataComplete);
        Assert.Equal(HistoricalPullProviderFixture.Id(7), Assert.Single(Assert.Single(actual!).Heads).ObjectId);
        Assert.All(fixture.Calls.Where(call => call.Any(value => value.Contains("pullRequest(number:", StringComparison.Ordinal))),
            call => Assert.InRange(call.Count(value => value.StartsWith("number", StringComparison.Ordinal)), 1, 12));
    }

    [Theory]
    [InlineData(false, false, 2047, "historical-pr-metadata", 0, 0)]
    [InlineData(true, false, 2046, "historical-pr-headers", 0, 0)]
    [InlineData(true, true, 2045, "historical-pr-headers", 1, 0)]
    public async Task ExhaustedRequestBudgetNeverCountsAnUnsentHeaderOrMetadataBatch(bool rest, bool aliasError,
        int charged, string phase, int headerBatches, int headerFallbacks)
    {
        HistoricalPullProviderFixture runner = new(2) { InvalidScopedTotal = rest, PartialBatchError = aliasError };
        ProviderQueryCounters counters = new();
        for (int index = 0; index < charged; index++) counters.AddQuery(GitHubProviderFailure.HistoricalInventoryPhase);
        var failure = await Assert.ThrowsAsync<GitHubProviderException>(() =>
            HistoricalPullProviderFixture.DiscoverRestrictedAsync(runner, counters));
        Assert.Equal("github-discovery-budget-exceeded", failure.Action.FailureCode);
        Assert.Equal(phase, failure.Action.Phase);
        Assert.Equal(2048, counters.QueryCount);
        var plan = counters.Diagnostics("missing").HistoricalPullRequests!;
        Assert.True(plan.InventoryComplete);
        Assert.False(plan.MetadataComplete);
        Assert.Equal(2, plan.PendingCount);
        Assert.Equal(0, plan.BatchCount);
        Assert.Equal(0, plan.MetadataQueryCount);
        Assert.Equal(headerBatches, plan.HeaderBatchCount);
        Assert.Equal(headerFallbacks, plan.HeaderFallbackCount);
        Assert.Equal(headerBatches, plan.HeaderQueryCount);
        Assert.Equal(0, plan.CacheWriteCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptedWorkerPhaseAndReusableProgressControlManualResumeGuidance(bool multipleRepositories)
    {
        using CancellationTokenSource deadline = new();
        ProviderQueryCounters counters = new();
        counters.AddQuery(GitHubProviderFailure.HistoricalHeaderPhase);
        counters.PlanHistoricalPulls(440);
        counters.HistoricalCacheWrite();
        counters.CompleteHistoricalPull(false);
        GitHubDiscoveryAcquisitionBudget budget = new(new()
        {
            Owner = "owner",
            Repositories = multipleRepositories ? ["owner/project", "owner/second"] : ["owner/project"],
            AsOf = DateTimeOffset.UnixEpoch,
            TimeZone = "UTC",
            Scope = "engineering"
        }, deadline, CancellationToken.None)
        { Counters = counters };
        OperationCanceledException interrupted = new();
        interrupted.Data[GitHubProviderFailure.InterruptedPhaseKey] = GitHubProviderFailure.HistoricalSelectionPhase;
        var failure = Assert.IsType<GitHubProviderException>(budget.Failure(interrupted));
        Assert.Equal("historical-pr-selection", failure.Action.Phase);
        Assert.Equal("resume-same-scope-or-use-pinned-manifest", failure.Action.SuggestedAction);
        Assert.Equal(0, failure.Action.RetryLimit);
        Assert.DoesNotContain("owner/project", ContractJson.Serialize(counters.Diagnostics("missing")), StringComparison.Ordinal);
    }

    private sealed class StopAfterCache(CancellationTokenSource cancellation, int maximum) : IGitHubPullMetadataCache, IDisposable
    {
        private int _written;
        private readonly SemaphoreSlim _writes = new(1);
        public MemoryPullMetadataCache Inner { get; } = new();
        public void Dispose() => _writes.Dispose();
        public Task<GitHubPullMetadata?> ReadAsync(string repository, int number, string head, string baseHead, int count, CancellationToken token) =>
            Inner.ReadAsync(repository, number, head, baseHead, count, token);
        public async Task<bool> WriteAsync(string repository, int number, GitHubPullMetadata metadata, CancellationToken token)
        {
            await _writes.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                await Inner.WriteAsync(repository, number, metadata, token);
                if (++_written == maximum) cancellation.Cancel();
                return true;
            }
            finally { _writes.Release(); }
        }
    }

    private sealed class FenceRunner(IExternalCommandRunner inner) : IExternalCommandRunner
    {
        public int Active;
        public async Task<ExternalCommandResult> RunAsync(string executable, string directory, IReadOnlyList<string> arguments,
            CancellationToken token, bool requireSuccess = true)
        {
            Interlocked.Increment(ref Active);
            try
            {
                if (arguments.Any(value => value.StartsWith("number0=", StringComparison.Ordinal) && int.Parse(value[8..], System.Globalization.CultureInfo.InvariantCulture) > 204))
                    await Task.Delay(Timeout.Infinite, token);
                return await inner.RunAsync(executable, directory, arguments, token, requireSuccess);
            }
            finally { Interlocked.Decrement(ref Active); }
        }
    }
}
