using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class ChangeEstimator
{
    public const int PortfolioSnapshotAnalysisRetentionLimit = 16;
    public const int MaximumConcurrentPortfolioRepositories = 2;

    public async Task<IReadOnlyList<ChangeEstimateReport>> EstimatePortfolioCandidatesAsync(
        IReadOnlyList<GitChangePlan> plans,
        EstimationProfile profile,
        CancellationToken cancellationToken = default) =>
        (await EstimatePortfolioCandidatesWithStatisticsAsync(
            plans,
            profile,
            cancellationToken).ConfigureAwait(false)).Reports;

    public Task<ChangePortfolioEstimateBatch> EstimatePortfolioCandidatesWithStatisticsAsync(
        IReadOnlyList<GitChangePlan> plans,
        EstimationProfile profile,
        CancellationToken cancellationToken = default) =>
        EstimatePortfolioCandidatesWithStatisticsCoreAsync(
            plans,
            profile,
            executionTelemetry: null,
            cancellationToken);

    public Task<ChangePortfolioEstimateBatch> EstimatePortfolioCandidatesWithStatisticsAsync(
        IReadOnlyList<GitChangePlan> plans,
        EstimationProfile profile,
        ChangePortfolioExecutionTelemetry executionTelemetry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executionTelemetry);
        return EstimatePortfolioCandidatesWithStatisticsCoreAsync(
            plans,
            profile,
            executionTelemetry,
            cancellationToken);
    }

    private async Task<ChangePortfolioEstimateBatch> EstimatePortfolioCandidatesWithStatisticsCoreAsync(
        IReadOnlyList<GitChangePlan> plans,
        EstimationProfile profile,
        ChangePortfolioExecutionTelemetry? executionTelemetry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plans);
        IndexedPortfolioPlan[] indexed = [.. plans.Select((plan, index) =>
            ValidatePlan(plan, index))];
        IndexedPortfolioPlan[][] repositories = [.. indexed
            .GroupBy(plan => plan.CacheNamespace, PathComparer)
            .Select(group => group.ToArray())];
        ChangeEstimateReport?[] reports = new ChangeEstimateReport?[plans.Count];
        HashSet<GitSnapshotSession> undisposedSessions = [.. indexed
            .Select(plan => plan.Plan.SnapshotSession)
            .OfType<GitSnapshotSession>()];
        Lock sessionGate = new();
        ExecutionStatisticsAccumulator statistics = new(repositories.Length);
        int completedPlans = 0;
        int progressAnalysisRequests = 0;
        int progressAnalysisHits = 0;
        try
        {
            await Parallel.ForEachAsync(
                repositories,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = MaximumConcurrentPortfolioRepositories,
                },
                async (repository, repositoryCancellationToken) =>
                {
                    SnapshotAnalysisCache snapshotAnalyses = new(
                        PortfolioSnapshotAnalysisRetentionLimit);
                    int previousAnalysisRequests = 0;
                    int previousAnalysisHits = 0;
                    try
                    {
                        foreach (IndexedPortfolioPlan entry in repository)
                        {
                            repositoryCancellationToken.ThrowIfCancellationRequested();
                            reports[entry.Index] = await EstimateCoreAsync(
                                CreateInput(entry.Plan),
                                profile,
                                rateCard: null,
                                snapshotAnalyses,
                                entry.CacheNamespace,
                                executionTelemetry,
                                repositoryCancellationToken).ConfigureAwait(false);
                            SnapshotAnalysisCache.SnapshotAnalysisCacheStatistics cacheStatistics =
                                snapshotAnalyses.GetStatistics();
                            _ = Interlocked.Add(
                                ref progressAnalysisRequests,
                                cacheStatistics.Requests - previousAnalysisRequests);
                            _ = Interlocked.Add(
                                ref progressAnalysisHits,
                                cacheStatistics.Hits - previousAnalysisHits);
                            previousAnalysisRequests = cacheStatistics.Requests;
                            previousAnalysisHits = cacheStatistics.Hits;
                            int completed = Interlocked.Increment(ref completedPlans);
                            if (executionTelemetry is not null &&
                                (completed == 1 || completed % 16 == 0 ||
                                    completed == indexed.Length))
                            {
                                executionTelemetry.ReportProgress(
                                    ChangePortfolioExecutionPhases.StaticAnalysis,
                                    completed,
                                    indexed.Length,
                                    Volatile.Read(ref progressAnalysisRequests),
                                    Volatile.Read(ref progressAnalysisHits));
                            }
                        }
                    }
                    finally
                    {
                        statistics.Add(snapshotAnalyses.GetStatistics());
                        foreach (GitSnapshotSession session in repository
                            .Select(entry => entry.Plan.SnapshotSession)
                            .OfType<GitSnapshotSession>()
                            .Distinct())
                        {
                            statistics.Add(session.GetStatistics());
                            await session.DisposeAsync().ConfigureAwait(false);
                            lock (sessionGate)
                            {
                                undisposedSessions.Remove(session);
                            }
                        }
                    }

                    return;
                }).ConfigureAwait(false);
        }
        finally
        {
            GitSnapshotSession[] remaining;
            lock (sessionGate)
            {
                remaining = [.. undisposedSessions];
            }

            foreach (GitSnapshotSession session in remaining)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }

        return new ChangePortfolioEstimateBatch
        {
            Reports = [.. reports.Select(report => report!)],
            Statistics = statistics.Build(),
        };
    }

    private static IndexedPortfolioPlan ValidatePlan(GitChangePlan? plan, int index)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Selection.Kind is not (ChangeSelectionKind.Commit or ChangeSelectionKind.PullRequest))
        {
            throw new ArgumentException(
                "Portfolio candidates must be immutable commit or pull-request changes.",
                nameof(plan));
        }

        return new IndexedPortfolioPlan(
            index,
            plan,
            Path.GetFullPath(plan.RepositoryPath));
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private sealed record IndexedPortfolioPlan(
        int Index,
        GitChangePlan Plan,
        string CacheNamespace);

    private sealed class ExecutionStatisticsAccumulator(int repositoryCount)
    {
        private readonly Lock _gate = new();
        private int _analysisRequests;
        private int _analysisHits;
        private int _analysisEvictions;
        private int _peakAnalyses;
        private int _inventoryRequests;
        private int _inventoryHits;
        private int _fullInventories;
        private int _incrementalInventories;
        private int _inventoryEvictions;
        private int _peakInventories;
        private int _objectReaders;
        private long _blobRequests;
        private long _blobHits;
        private long _blobEvictions;
        private long _peakBlobBytes;

        public void Add(SnapshotAnalysisCache.SnapshotAnalysisCacheStatistics value)
        {
            lock (_gate)
            {
                _analysisRequests += value.Requests;
                _analysisHits += value.Hits;
                _analysisEvictions += value.Evictions;
                _peakAnalyses = Math.Max(_peakAnalyses, value.PeakEntries);
            }
        }

        public void Add(GitSnapshotSessionStatistics value)
        {
            lock (_gate)
            {
                _inventoryRequests += value.InventoryRequests;
                _inventoryHits += value.InventoryHits;
                _fullInventories += value.FullInventoryLoads;
                _incrementalInventories += value.IncrementalInventoryLoads;
                _inventoryEvictions += value.InventoryEvictions;
                _peakInventories = Math.Max(_peakInventories, value.PeakRetainedInventories);
                _objectReaders += value.ObjectReaderStarts;
                _blobRequests += value.BlobRequests;
                _blobHits += value.BlobCacheHits;
                _blobEvictions += value.BlobCacheEvictions;
                _peakBlobBytes = Math.Max(_peakBlobBytes, value.PeakCachedBlobBytes);
            }
        }

        public ChangePortfolioExecutionStatistics Build() => new()
        {
            RepositoryCount = repositoryCount,
            MaximumActiveRepositories = Math.Min(
                repositoryCount,
                MaximumConcurrentPortfolioRepositories),
            SnapshotAnalysisRequests = _analysisRequests,
            SnapshotAnalysisHits = _analysisHits,
            SnapshotAnalysisEvictions = _analysisEvictions,
            PeakRetainedSnapshotAnalyses = _peakAnalyses,
            SnapshotInventoryRequests = _inventoryRequests,
            SnapshotInventoryHits = _inventoryHits,
            FullSnapshotInventoryLoads = _fullInventories,
            IncrementalSnapshotInventoryLoads = _incrementalInventories,
            SnapshotInventoryEvictions = _inventoryEvictions,
            PeakRetainedSnapshotInventories = _peakInventories,
            ObjectDatabaseReaders = _objectReaders,
            BlobRequests = _blobRequests,
            BlobCacheHits = _blobHits,
            BlobCacheEvictions = _blobEvictions,
            PeakCachedBlobBytesPerRepository = _peakBlobBytes,
            SnapshotAnalysisRetentionLimit = PortfolioSnapshotAnalysisRetentionLimit,
            SnapshotInventoryRetentionLimit = GitSnapshotSession.MaximumSnapshotInventories,
            BlobCacheByteLimitPerRepository = GitBatchObjectReader.MaximumCacheBytes,
        };
    }
}
