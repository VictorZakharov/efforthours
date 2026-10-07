using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed partial class ProviderQueryCounters
{
    public IGitHubPullMetadataCache? PullMetadataCache { get; set; }
    private string _lastPhase = GitHubProviderFailure.AuthenticationPhase;
    public string LastPhase => Volatile.Read(ref _lastPhase);
    public string HistoricalInventoryStrategy { get; set; } = "not-observed";
    private int _expectedInventories = 1, _completedInventories;
    private int _inventoryQueries, _headerQueries, _metadataQueries, _headerBatches, _headerFallbacks, _cacheWrites;
    public bool HasReusableHistoricalMetadata => Volatile.Read(ref _cachedPulls) + Volatile.Read(ref _cacheWrites) > 0;
    public void ExpectHistoricalInventories(int count) => _expectedInventories = count;
    public IDisposable? MeasureHistoricalPhase(string phase)
    {
        Volatile.Write(ref _lastPhase, phase);
        return _telemetry?.Measure(phase);
    }
    public void HistoricalHeaderBatch() => Interlocked.Increment(ref _headerBatches);
    public void HistoricalHeaderFallback() => Interlocked.Increment(ref _headerFallbacks);
    public void HistoricalCacheWrite() => Interlocked.Increment(ref _cacheWrites);
    private int _plannedPulls;
    private int _cachedPulls;
    private int _pullBatches;
    private int _fallbackPulls;
    private int _completedPulls;
    private int _selectedPulls;
    public void PlanHistoricalPulls(int count)
    {
        Interlocked.Add(ref _plannedPulls, count);
        Interlocked.Increment(ref _completedInventories);
    }
    public void HistoricalPullCacheHit() => Interlocked.Increment(ref _cachedPulls);
    public void HistoricalPullBatch() => Interlocked.Increment(ref _pullBatches);
    public void HistoricalPullFallback() => Interlocked.Increment(ref _fallbackPulls);
    public void CompleteHistoricalPull(bool selected)
    {
        if (selected) Interlocked.Increment(ref _selectedPulls);
        int completed = Interlocked.Increment(ref _completedPulls);
        _telemetry?.ReportProgress(ChangePortfolioExecutionPhases.HistoricalPullSelection,
            completed, Volatile.Read(ref _plannedPulls), 0, 0);
    }
    private ChangePortfolioHistoricalPullRequestPlan? HistoricalPullPlan() => Volatile.Read(ref _plannedPulls) == 0 && Volatile.Read(ref _inventoryQueries) == 0 && Volatile.Read(ref _completedInventories) == 0 ? null : new()
    {
        InventoryStrategy = HistoricalInventoryStrategy,
        InventoryComplete = Volatile.Read(ref _completedInventories) == _expectedInventories,
        MetadataComplete = Volatile.Read(ref _completedInventories) == _expectedInventories && Volatile.Read(ref _plannedPulls) == Volatile.Read(ref _completedPulls),
        InventoryQueryCount = Volatile.Read(ref _inventoryQueries),
        HeaderQueryCount = Volatile.Read(ref _headerQueries),
        MetadataQueryCount = Volatile.Read(ref _metadataQueries),
        HeaderBatchCount = Volatile.Read(ref _headerBatches),
        HeaderFallbackCount = Volatile.Read(ref _headerFallbacks),
        CacheWriteCount = Volatile.Read(ref _cacheWrites),
        ResumeState = HasReusableHistoricalMetadata ? "completed-metadata-reusable" : "no-completed-metadata",
        CandidateCount = Volatile.Read(ref _plannedPulls),
        CacheHitCount = Volatile.Read(ref _cachedPulls),
        BatchCount = Volatile.Read(ref _pullBatches),
        FallbackCount = Volatile.Read(ref _fallbackPulls),
        CompletedCount = Volatile.Read(ref _completedPulls),
        SelectedCount = Volatile.Read(ref _selectedPulls),
        PendingCount = Volatile.Read(ref _plannedPulls) - Volatile.Read(ref _completedPulls),
    };
}
