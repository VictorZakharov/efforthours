using EffortHours.Change;
using EffortHours.ChangeBenchmarks;

namespace EffortHours.Tests;

public sealed class GitHubScopedHistoricalPullTests
{
    [Fact]
    public async Task ExplicitScopeReadsOnlyRequestedRepositoriesAndReusesExactLiveMetadata()
    {
        HistoricalPullProviderFixture runner = new(258) { RepositoryCount = 2 };
        MemoryPullMetadataCache cache = new();
        ProviderQueryCounters cold = new() { PullMetadataCache = cache };
        var first = await HistoricalPullProviderFixture.DiscoverRestrictedAsync(runner, cold);
        Assert.Equal(HistoricalPullProviderFixture.Id(7), Assert.Single(Assert.Single(first!).Heads).ObjectId);
        Assert.Equal(13, cold.QueryCount); // Two scoped pages and eleven metadata batches.
        Assert.Equal(129, cold.Diagnostics("missing").HistoricalPullRequests!.CandidateCount);
        Assert.Equal(0, cold.Diagnostics("missing").OpenPullRequestAccountQueryCount);
        Assert.All(runner.Calls, call =>
        {
            Assert.DoesNotContain("--paginate", call);
            Assert.DoesNotContain(call, value => value.Contains("user(login:", StringComparison.Ordinal));
            Assert.DoesNotContain(call, value => value.Contains("project-1", StringComparison.Ordinal));
        });
        ProviderQueryCounters warm = new() { PullMetadataCache = cache };
        var second = await HistoricalPullProviderFixture.DiscoverRestrictedAsync(runner, warm);
        Assert.Equal(first!.Single().Heads, second!.Single().Heads);
        Assert.Equal(2, warm.QueryCount);
        Assert.Equal(2, warm.PageCount);
        Assert.Equal(129, warm.Diagnostics("hit").HistoricalPullRequests!.CacheHitCount);
        Assert.Equal(0, warm.Diagnostics("hit").HistoricalPullRequests!.BatchCount);
        Assert.Equal("pull-inventory", warm.Diagnostics("hit").LastRequest!.Operation);
        Assert.NotNull(warm.Diagnostics("hit").LastRequest!.RepositoryDigest);
        runner.BaseHead = HistoricalPullProviderFixture.Id(20000);
        ProviderQueryCounters changed = new() { PullMetadataCache = cache };
        await HistoricalPullProviderFixture.DiscoverRestrictedAsync(runner, changed);
        Assert.Equal(0, changed.Diagnostics("hit").HistoricalPullRequests!.CacheHitCount);
        Assert.Equal(13, changed.QueryCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OverBoundOrRepeatedCursorRequestsCompleteFallbackWithoutAdmittingPartialEvidence(bool overBound)
    {
        HistoricalPullProviderFixture runner = new(258)
        { InvalidScopedTotal = overBound, RepeatedScopedCursor = !overBound };
        ProviderQueryCounters counters = new();
        Assert.Null(await HistoricalPullProviderFixture.DiscoverRestrictedAsync(runner, counters));
        Assert.Equal(overBound ? 1 : 2, counters.QueryCount);
        Assert.Equal(0, counters.Diagnostics("missing").HistoricalPullRequests?.CandidateCount ?? 0);
        Assert.DoesNotContain(runner.Calls, call => call.Any(value => value.Contains("pullRequest(number:", StringComparison.Ordinal)));
    }
    [Fact]
    public async Task InventoryFailureCancelsAndDrainsSiblingWithoutAdmittingPartialEvidence()
    {
        DrainedInventoryRunner runner = new();
        ProviderQueryCounters counters = new();
        await Assert.ThrowsAsync<GitHubProviderException>(() =>
            GitHubAuthorPeriodDiscoveryJson.DiscoverHistoricalPullHeadsInScopeAsync(runner, "in-memory-fixture",
                [new("1", "owner/project", "main"), new("2", "owner/project-1", "main")],
                "selected", ["selected@example.invalid"], HistoricalPullProviderFixture.Since,
                HistoricalPullProviderFixture.Since.AddDays(5), EffortHours.Contracts.V1.ChangePortfolioDateField.Author,
                EffortHours.Contracts.V1.ChangePortfolioMergePolicy.Exclude, EffortHours.Contracts.V1.ChangePortfolioCoauthorPolicy.Include,
                counters, CancellationToken.None));
        Assert.Equal(0, runner.Active);
        Assert.True(runner.SiblingCancelled);
        Assert.Equal(2, counters.QueryCount);
        Assert.Equal("incomplete", counters.Diagnostics("missing").LastRequest!.State);
    }

    [Fact]
    public async Task HeadBudgetFailureReportsActualPrPhaseAndCompleteProgressWithoutSilentTruncation()
    {
        HistoricalPullProviderFixture runner = new(70);
        ProviderQueryCounters counters = new();
        GitHubProviderException failure = await Assert.ThrowsAsync<GitHubProviderException>(() =>
            HistoricalPullProviderFixture.DiscoverScopeAsync(runner, counters, true, 1));
        Assert.Equal("github-discovery-budget-exceeded", failure.Action.FailureCode);
        Assert.Equal("open-pr-discovery", failure.Action.Phase);
        Assert.Equal("inspect-head-scope-or-use-pinned-manifest", failure.Action.SuggestedAction);
        Assert.Equal(0, failure.Action.RetryLimit);
        Assert.Equal(70, counters.Diagnostics("missing").HistoricalPullRequests!.CompletedCount);
        Assert.Equal(69, counters.Diagnostics("missing").HistoricalPullRequests!.SelectedCount);
        Assert.Contains("32-head bound", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScopedPaginationChargesOneBoundAcrossPagesBeforeAdmittingMetadata()
    {
        HistoricalPullProviderFixture runner = new(258) { ScopedPaddingCharacters = 8 * 1024 * 1024 };
        ProviderQueryCounters counters = new();
        GitHubProviderException failure = await Assert.ThrowsAsync<GitHubProviderException>(() =>
            HistoricalPullProviderFixture.DiscoverRestrictedAsync(runner, counters));
        Assert.Equal("github-discovery-budget-exceeded", failure.Action.FailureCode);
        Assert.Equal("open-pr-discovery", failure.Action.Phase);
        Assert.Equal(2, counters.QueryCount);
        Assert.Equal(2, counters.PageCount);
        Assert.Equal(0, counters.Diagnostics("missing").HistoricalPullRequests?.CandidateCount ?? 0);
    }

    private sealed class DrainedInventoryRunner : IExternalCommandRunner
    {
        private readonly TaskCompletionSource _bothStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Active;
        public bool SiblingCancelled;
        public async Task<ExternalCommandResult> RunAsync(string executable, string workingDirectory,
            IReadOnlyList<string> arguments, CancellationToken cancellationToken, bool requireSuccess = true)
        {
            if (Interlocked.Increment(ref Active) == 2) _bothStarted.SetResult();
            try
            {
                await _bothStarted.Task.WaitAsync(cancellationToken);
                if (arguments.Contains("name=project")) return new(1, "", "HTTP 429 synthetic failure");
                await Task.Delay(Timeout.Infinite, cancellationToken);
                throw new InvalidOperationException("Unreachable synthetic response.");
            }
            catch (OperationCanceledException) { SiblingCancelled = true; throw; }
            finally { Interlocked.Decrement(ref Active); }
        }
    }

}
