using System.Threading.Channels;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class ChangeEstimator
{
    public const int PortfolioSnapshotAnalysisRetentionLimit = 16;
    public const int MaximumConcurrentPortfolioRepositories = 2;
    public const int MaximumConcurrentPortfolioChangesPerRepository = 4;
    public const int PortfolioDeltaPrimeChunkSize = 16;

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
        Lock progressGate = new();
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
                        using CancellationTokenSource pipelineCancellation =
                            CancellationTokenSource.CreateLinkedTokenSource(
                                repositoryCancellationToken);
                        Channel<PreparedPortfolioPlan> prepared = Channel.CreateBounded<
                            PreparedPortfolioPlan>(new BoundedChannelOptions(
                                MaximumConcurrentPortfolioChangesPerRepository)
                            {
                                FullMode = BoundedChannelFullMode.Wait,
                                SingleReader = false,
                                SingleWriter = true,
                            });
                        Task[] consumers =
                        [
                            .. Enumerable.Range(
                                0,
                                MaximumConcurrentPortfolioChangesPerRepository)
                                .Select(_ => Task.Run(async () =>
                                {
                                    try
                                    {
                                        await foreach (PreparedPortfolioPlan entry in prepared.Reader
                                            .ReadAllAsync(pipelineCancellation.Token)
                                            .ConfigureAwait(false))
                                        {
                                            reports[entry.Entry.Index] = await EstimateCoreAsync(
                                                entry.Input,
                                                profile,
                                                rateCard: null,
                                                snapshotAnalyses,
                                                entry.Entry.CacheNamespace,
                                                executionTelemetry,
                                                pipelineCancellation.Token).ConfigureAwait(false);
                                            lock (progressGate)
                                            {
                                                SnapshotAnalysisCache.SnapshotAnalysisCacheStatistics
                                                    cacheStatistics = snapshotAnalyses.GetStatistics();
                                                int requestDelta = cacheStatistics.Requests -
                                                    previousAnalysisRequests;
                                                int hitDelta = cacheStatistics.Hits -
                                                    previousAnalysisHits;
                                                previousAnalysisRequests = cacheStatistics.Requests;
                                                previousAnalysisHits = cacheStatistics.Hits;
                                                progressAnalysisRequests += requestDelta;
                                                progressAnalysisHits += hitDelta;
                                                completedPlans++;
                                                if (executionTelemetry is not null &&
                                                    (completedPlans == 1 ||
                                                        completedPlans % 16 == 0 ||
                                                        completedPlans == indexed.Length))
                                                {
                                                    executionTelemetry.ReportProgress(
                                                        ChangePortfolioExecutionPhases.StaticAnalysis,
                                                        completedPlans,
                                                        indexed.Length,
                                                        progressAnalysisRequests,
                                                        progressAnalysisHits);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception exception)
                                    {
                                        prepared.Writer.TryComplete(exception);
                                        await pipelineCancellation.CancelAsync().ConfigureAwait(false);
                                        throw;
                                    }
                                }, pipelineCancellation.Token)),
                        ];
                        try
                        {
                            foreach (IndexedPortfolioPlan[] chunk in repository.Chunk(
                                PortfolioDeltaPrimeChunkSize))
                            {
                                using (executionTelemetry?.Measure(
                                    ChangePortfolioExecutionPhases.SnapshotAndDiffConstruction))
                                {
                                    foreach (GitSnapshotSession session in chunk
                                        .Select(entry => entry.Plan.SnapshotSession)
                                        .OfType<GitSnapshotSession>()
                                        .Distinct())
                                    {
                                        await session.PrimeIncrementalDeltasAsync(
                                            chunk.Select(entry =>
                                                entry.Plan.Selection.Head.ObjectId),
                                            pipelineCancellation.Token).ConfigureAwait(false);
                                    }
                                }

                                foreach (IndexedPortfolioPlan entry in chunk)
                                {
                                    IChangeSnapshot baseSnapshot;
                                    using (executionTelemetry?.Measure(
                                        ChangePortfolioExecutionPhases.SnapshotAndDiffConstruction))
                                    {
                                        baseSnapshot = await entry.Plan.OpenBaseAsync(
                                            pipelineCancellation.Token).ConfigureAwait(false);
                                    }

                                    IChangeSnapshot headSnapshot;
                                    try
                                    {
                                        using (executionTelemetry?.Measure(
                                            ChangePortfolioExecutionPhases.SnapshotAndDiffConstruction))
                                        {
                                            headSnapshot = await entry.Plan.OpenHeadAsync(
                                                pipelineCancellation.Token).ConfigureAwait(false);
                                        }
                                    }
                                    catch
                                    {
                                        await baseSnapshot.DisposeAsync().ConfigureAwait(false);
                                        throw;
                                    }

                                    try
                                    {
                                        ChangeEstimateInput input = CreateInput(entry.Plan) with
                                        {
                                            OpenBaseAsync = _ => Task.FromResult(baseSnapshot),
                                            OpenHeadAsync = _ => Task.FromResult(headSnapshot),
                                        };
                                        await prepared.Writer.WriteAsync(
                                            new PreparedPortfolioPlan(
                                                entry,
                                                input,
                                                baseSnapshot,
                                                headSnapshot),
                                            pipelineCancellation.Token).ConfigureAwait(false);
                                    }
                                    catch
                                    {
                                        await baseSnapshot.DisposeAsync().ConfigureAwait(false);
                                        await headSnapshot.DisposeAsync().ConfigureAwait(false);
                                        throw;
                                    }
                                }
                            }

                            prepared.Writer.TryComplete();
                            await Task.WhenAll(consumers).ConfigureAwait(false);
                        }
                        catch
                        {
                            await pipelineCancellation.CancelAsync().ConfigureAwait(false);
                            prepared.Writer.TryComplete();
                            while (prepared.Reader.TryRead(out PreparedPortfolioPlan? abandoned))
                            {
                                await abandoned.BaseSnapshot.DisposeAsync().ConfigureAwait(false);
                                await abandoned.HeadSnapshot.DisposeAsync().ConfigureAwait(false);
                            }

                            try
                            {
                                await Task.WhenAll(consumers).ConfigureAwait(false);
                            }
                            catch
                            {
                                // Preserve the producer or first consumer failure.
                            }

                            throw;
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

    private sealed class ExecutionStatisticsAccumulator(int repositoryCount)
    {
        private readonly Lock _gate = new();
        private int _analysisRequests;
        private int _analysisHits;
        private int _analysisUniqueKeys;
        private int _analysisRevisitMisses;
        private int _analysisEvictions;
        private int _peakAnalyses;
        private int _artifactRequests;
        private int _artifactHits;
        private int _artifactUniqueKeys;
        private int _artifactRevisitMisses;
        private int _artifactEvictions;
        private int _peakArtifacts;
        private int _inventoryRequests;
        private int _inventoryHits;
        private int _inventoryUniqueObjects;
        private int _inventoryRevisitMisses;
        private int _fullInventories;
        private int _incrementalInventories;
        private int _batchedIncrementalInventories;
        private TimeSpan _fullInventoryReadTime;
        private TimeSpan _fullInventoryProjectionTime;
        private TimeSpan _incrementalInventoryProjectionTime;
        private int _inventoryEvictions;
        private int _peakInventories;
        private int _peakInventoryRoots;
        private int _objectReaders;
        private int _metadataReaders;
        private int _metadataRequests;
        private int _metadataHits;
        private int _uniqueMetadataObjects;
        private int _metadataEvictions;
        private int _peakMetadataLengths;
        private long _blobRequests;
        private long _blobHits;
        private long _blobEvictions;
        private long _uniqueBlobObjects;
        private long _blobRequestedBytes;
        private long _blobCacheHitBytes;
        private long _blobReadBytes;
        private long _uniqueBlobBytes;
        private long _retainedBlobBytes;
        private long _peakBlobBytes;
        private TimeSpan _objectReaderCpuTime;
        private TimeSpan _objectReaderOccupiedTime;
        private TimeSpan _objectReaderWaitTime;

        public void Add(SnapshotAnalysisCache.SnapshotAnalysisCacheStatistics value)
        {
            lock (_gate)
            {
                _analysisRequests += value.Requests;
                _analysisHits += value.Hits;
                _analysisUniqueKeys += value.UniqueKeys;
                _analysisRevisitMisses += value.RevisitMisses;
                _analysisEvictions += value.Evictions;
                _peakAnalyses = Math.Max(_peakAnalyses, value.PeakEntries);
            }
        }

        public void Add(GitSnapshotSessionStatistics value)
        {
            lock (_gate)
            {
                _artifactRequests += value.AnalysisArtifactRequests;
                _artifactHits += value.AnalysisArtifactHits;
                _artifactUniqueKeys += value.UniqueAnalysisArtifactKeys;
                _artifactRevisitMisses += value.AnalysisArtifactRevisitMisses;
                _artifactEvictions += value.AnalysisArtifactEvictions;
                _peakArtifacts = Math.Max(
                    _peakArtifacts,
                    value.PeakRetainedAnalysisArtifacts);
                _inventoryRequests += value.InventoryRequests;
                _inventoryHits += value.InventoryHits;
                _inventoryUniqueObjects += value.UniqueInventoryObjects;
                _inventoryRevisitMisses += value.InventoryRevisitMisses;
                _fullInventories += value.FullInventoryLoads;
                _incrementalInventories += value.IncrementalInventoryLoads;
                _batchedIncrementalInventories += value.BatchedIncrementalInventoryLoads;
                _fullInventoryReadTime += value.FullInventoryReadTime;
                _fullInventoryProjectionTime += value.FullInventoryProjectionTime;
                _incrementalInventoryProjectionTime +=
                    value.IncrementalInventoryProjectionTime;
                _inventoryEvictions += value.InventoryEvictions;
                _peakInventories = Math.Max(_peakInventories, value.PeakRetainedInventories);
                _peakInventoryRoots = Math.Max(
                    _peakInventoryRoots,
                    value.PeakRetainedInventoryRoots);
                _objectReaders += value.ObjectReaderStarts;
                _metadataReaders += value.ObjectMetadataReaderStarts;
                _metadataRequests += value.ObjectMetadataRequests;
                _metadataHits += value.ObjectMetadataCacheHits;
                _uniqueMetadataObjects += value.UniqueObjectMetadataObjects;
                _metadataEvictions += value.ObjectMetadataCacheEvictions;
                _peakMetadataLengths = Math.Max(
                    _peakMetadataLengths,
                    value.PeakCachedObjectMetadataLengths);
                _blobRequests += value.BlobRequests;
                _blobHits += value.BlobCacheHits;
                _blobEvictions += value.BlobCacheEvictions;
                _uniqueBlobObjects += value.UniqueBlobObjects;
                _blobRequestedBytes += value.BlobRequestedBytes;
                _blobCacheHitBytes += value.BlobCacheHitBytes;
                _blobReadBytes += value.BlobReadBytes;
                _uniqueBlobBytes += value.UniqueBlobBytes;
                _retainedBlobBytes = Math.Max(_retainedBlobBytes, value.RetainedBlobBytes);
                _peakBlobBytes = Math.Max(_peakBlobBytes, value.PeakCachedBlobBytes);
                _objectReaderCpuTime += value.ObjectReaderCpuTime;
                _objectReaderOccupiedTime += value.ObjectReaderOccupiedTime;
                _objectReaderWaitTime += value.ObjectReaderWaitTime;
            }
        }

        public ChangePortfolioExecutionStatistics Build() => new()
        {
            RepositoryCount = repositoryCount,
            MaximumActiveRepositories = Math.Min(
                repositoryCount,
                MaximumConcurrentPortfolioRepositories),
            MaximumConcurrentFileAnalysesPerRepository =
                RepositoryAnalysisConcurrency.MaximumFileAnalyses,
            MaximumConcurrentCpuWorkItems =
                RepositoryAnalysisConcurrency.MaximumCpuWorkItems,
            SnapshotAnalysisRequests = _analysisRequests,
            SnapshotAnalysisHits = _analysisHits,
            UniqueSnapshotAnalysisKeys = _analysisUniqueKeys,
            SnapshotAnalysisRevisitMisses = _analysisRevisitMisses,
            SnapshotAnalysisEvictions = _analysisEvictions,
            PeakRetainedSnapshotAnalyses = _peakAnalyses,
            AnalysisArtifactRequests = _artifactRequests,
            AnalysisArtifactHits = _artifactHits,
            UniqueAnalysisArtifactKeys = _artifactUniqueKeys,
            AnalysisArtifactRevisitMisses = _artifactRevisitMisses,
            AnalysisArtifactEvictions = _artifactEvictions,
            PeakRetainedAnalysisArtifacts = _peakArtifacts,
            SnapshotInventoryRequests = _inventoryRequests,
            SnapshotInventoryHits = _inventoryHits,
            UniqueSnapshotInventoryObjects = _inventoryUniqueObjects,
            SnapshotInventoryRevisitMisses = _inventoryRevisitMisses,
            FullSnapshotInventoryLoads = _fullInventories,
            IncrementalSnapshotInventoryLoads = _incrementalInventories,
            BatchedIncrementalSnapshotInventoryLoads = _batchedIncrementalInventories,
            FullSnapshotInventoryReadTime = _fullInventoryReadTime,
            FullSnapshotInventoryProjectionTime = _fullInventoryProjectionTime,
            IncrementalSnapshotInventoryProjectionTime = _incrementalInventoryProjectionTime,
            SnapshotInventoryEvictions = _inventoryEvictions,
            PeakRetainedSnapshotInventories = _peakInventories,
            PeakRetainedSnapshotInventoryRoots = _peakInventoryRoots,
            ObjectDatabaseReaders = _objectReaders,
            ObjectMetadataReaders = _metadataReaders,
            ObjectMetadataRequests = _metadataRequests,
            ObjectMetadataCacheHits = _metadataHits,
            UniqueObjectMetadataObjects = _uniqueMetadataObjects,
            ObjectMetadataCacheEvictions = _metadataEvictions,
            PeakCachedObjectMetadataLengthsPerRepository = _peakMetadataLengths,
            BlobRequests = _blobRequests,
            BlobCacheHits = _blobHits,
            BlobCacheEvictions = _blobEvictions,
            UniqueBlobObjects = _uniqueBlobObjects,
            BlobRequestedBytes = _blobRequestedBytes,
            BlobCacheHitBytes = _blobCacheHitBytes,
            BlobReadBytes = _blobReadBytes,
            UniqueBlobBytes = _uniqueBlobBytes,
            RetainedBlobBytesPerRepository = _retainedBlobBytes,
            PeakCachedBlobBytesPerRepository = _peakBlobBytes,
            ObjectReaderCpuTime = _objectReaderCpuTime,
            ObjectReaderOccupiedTime = _objectReaderOccupiedTime,
            ObjectReaderWaitTime = _objectReaderWaitTime,
            SnapshotAnalysisRetentionLimit = PortfolioSnapshotAnalysisRetentionLimit,
            AnalysisArtifactRetentionLimit =
                RepositoryAnalysisArtifactCache.DefaultMaximumEntries,
            SnapshotInventoryRetentionLimit = GitSnapshotSession.MaximumSnapshotInventories,
            SnapshotInventoryRootRetentionLimit =
                GitSnapshotSession.MaximumSnapshotInventoryRoots,
            SnapshotDeltaBatchOutputByteLimit = GitSnapshotDeltaBatch.MaximumBatchOutputBytes,
            BlobCacheByteLimitPerRepository = GitBatchObjectReader.MaximumCacheBytes,
            ObjectMetadataCacheEntryLimitPerRepository =
                GitBatchObjectMetadataReader.MaximumCachedLengths,
        };
    }
}
