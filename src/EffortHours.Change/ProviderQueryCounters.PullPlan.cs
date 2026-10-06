using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed partial class ProviderQueryCounters
{
    public IGitHubPullMetadataCache? PullMetadataCache { get; set; }
    public string LastPhase { get; private set; } = GitHubProviderFailure.AuthenticationPhase;
    private int _plannedPulls;
    private int _cachedPulls;
    private int _pullBatches;
    private int _fallbackPulls;
    private int _completedPulls;
    private int _selectedPulls;
    public void PlanHistoricalPulls(int count) => Interlocked.Add(ref _plannedPulls, count);
    public void HistoricalPullCacheHit() => Interlocked.Increment(ref _cachedPulls);
    public void HistoricalPullBatch() => Interlocked.Increment(ref _pullBatches);
    public void HistoricalPullFallback() => Interlocked.Increment(ref _fallbackPulls);
    public void CompleteHistoricalPull(bool selected)
    {
        if (selected) Interlocked.Increment(ref _selectedPulls);
        Interlocked.Increment(ref _completedPulls);
    }
    private ChangePortfolioHistoricalPullRequestPlan? HistoricalPullPlan() => Volatile.Read(ref _plannedPulls) == 0 ? null : new()
    {
        CandidateCount = Volatile.Read(ref _plannedPulls),
        CacheHitCount = Volatile.Read(ref _cachedPulls),
        BatchCount = Volatile.Read(ref _pullBatches),
        FallbackCount = Volatile.Read(ref _fallbackPulls),
        CompletedCount = Volatile.Read(ref _completedPulls),
        SelectedCount = Volatile.Read(ref _selectedPulls),
        PendingCount = Volatile.Read(ref _plannedPulls) - Volatile.Read(ref _completedPulls),
    };
}
