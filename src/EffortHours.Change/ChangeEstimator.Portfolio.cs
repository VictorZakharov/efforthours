using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class ChangeEstimator
{
    public const int PortfolioSnapshotAnalysisRetentionLimit = 16;

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
        ExecutionStatisticsAccumulator statistics = new(repositories.Length);
        try
        {
            foreach (IndexedPortfolioPlan[] repository in repositories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SnapshotAnalysisCache snapshotAnalyses = new(
                    PortfolioSnapshotAnalysisRetentionLimit);
                try
                {
                    foreach (IndexedPortfolioPlan entry in repository)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        reports[entry.Index] = await EstimateCoreAsync(
                            CreateInput(entry.Plan),
                            profile,
                            rateCard: null,
                            snapshotAnalyses,
                            entry.CacheNamespace,
                            executionTelemetry,
                            cancellationToken).ConfigureAwait(false);
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
                        undisposedSessions.Remove(session);
                    }
                }
            }
        }
        finally
        {
            foreach (GitSnapshotSession session in undisposedSessions)
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
            _analysisRequests += value.Requests;
            _analysisHits += value.Hits;
            _analysisEvictions += value.Evictions;
            _peakAnalyses = Math.Max(_peakAnalyses, value.PeakEntries);
        }

        public void Add(GitSnapshotSessionStatistics value)
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

        public ChangePortfolioExecutionStatistics Build() => new()
        {
            RepositoryCount = repositoryCount,
            MaximumActiveRepositories = repositoryCount == 0 ? 0 : 1,
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
