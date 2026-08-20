using EffortHours.Analysis;
using EffortHours.Change;

namespace EffortHours.ChangeBenchmarks;

internal static partial class ChangePortfolioBenchmarkRunner
{
    private static RepositoryAnalysisWorkStatistics Difference(
        RepositoryAnalysisWorkStatistics after,
        RepositoryAnalysisWorkStatistics before) => new(
            after.Acquisitions - before.Acquisitions,
            after.OccupiedTime - before.OccupiedTime,
            after.WaitTime - before.WaitTime,
            after.ElapsedTime,
            after.MaximumOccupiedTime,
            after.ExternalProcessCpuTime - before.ExternalProcessCpuTime,
            after.OutputBytes - before.OutputBytes,
            after.MaximumActive);

    private static ChangePortfolioExecutionStatistics CombineStatistics(
        IReadOnlyList<ChangePortfolioExecutionStatistics> statistics) => new()
        {
            RepositoryCount = statistics.Sum(value => value.RepositoryCount),
            MaximumActiveRepositories = statistics.Max(value => value.MaximumActiveRepositories),
            MaximumConcurrentFileAnalysesPerRepository = statistics.Max(
                value => value.MaximumConcurrentFileAnalysesPerRepository),
            MaximumConcurrentCpuWorkItems = statistics.Max(
                value => value.MaximumConcurrentCpuWorkItems),
            MaximumConcurrentGitTreeReads = statistics.Max(
                value => value.MaximumConcurrentGitTreeReads),
            SnapshotAnalysisRequests = statistics.Sum(value => value.SnapshotAnalysisRequests),
            SnapshotAnalysisHits = statistics.Sum(value => value.SnapshotAnalysisHits),
            UniqueSnapshotAnalysisKeys = statistics.Sum(value => value.UniqueSnapshotAnalysisKeys),
            SnapshotAnalysisRevisitMisses = statistics.Sum(value => value.SnapshotAnalysisRevisitMisses),
            AnalysisArtifactRequests = statistics.Sum(value => value.AnalysisArtifactRequests),
            AnalysisArtifactHits = statistics.Sum(value => value.AnalysisArtifactHits),
            UniqueAnalysisArtifactKeys = statistics.Sum(value => value.UniqueAnalysisArtifactKeys),
            AnalysisArtifactRevisitMisses = statistics.Sum(value => value.AnalysisArtifactRevisitMisses),
            AnalysisArtifactEvictions = statistics.Sum(value => value.AnalysisArtifactEvictions),
            PeakRetainedAnalysisArtifacts = statistics.Max(value => value.PeakRetainedAnalysisArtifacts),
            SnapshotAnalysisEvictions = statistics.Sum(value => value.SnapshotAnalysisEvictions),
            PeakRetainedSnapshotAnalyses = statistics.Max(value => value.PeakRetainedSnapshotAnalyses),
            SnapshotInventoryRequests = statistics.Sum(value => value.SnapshotInventoryRequests),
            SnapshotInventoryHits = statistics.Sum(value => value.SnapshotInventoryHits),
            UniqueSnapshotInventoryObjects = statistics.Sum(value => value.UniqueSnapshotInventoryObjects),
            SnapshotInventoryRevisitMisses = statistics.Sum(value => value.SnapshotInventoryRevisitMisses),
            FullSnapshotInventoryLoads = statistics.Sum(value => value.FullSnapshotInventoryLoads),
            IncrementalSnapshotInventoryLoads = statistics.Sum(value => value.IncrementalSnapshotInventoryLoads),
            BatchedIncrementalSnapshotInventoryLoads = statistics.Sum(
                value => value.BatchedIncrementalSnapshotInventoryLoads),
            FullSnapshotInventoryReadTime = TimeSpan.FromTicks(
                statistics.Sum(value => value.FullSnapshotInventoryReadTime.Ticks)),
            FullSnapshotInventoryProjectionTime = TimeSpan.FromTicks(
                statistics.Sum(value => value.FullSnapshotInventoryProjectionTime.Ticks)),
            IncrementalSnapshotInventoryProjectionTime = TimeSpan.FromTicks(
                statistics.Sum(value => value.IncrementalSnapshotInventoryProjectionTime.Ticks)),
            SnapshotInventoryEvictions = statistics.Sum(value => value.SnapshotInventoryEvictions),
            PeakRetainedSnapshotInventories = statistics.Max(value => value.PeakRetainedSnapshotInventories),
            PeakRetainedSnapshotInventoryRoots = statistics.Max(
                value => value.PeakRetainedSnapshotInventoryRoots),
            ObjectDatabaseReaders = statistics.Sum(value => value.ObjectDatabaseReaders),
            ObjectMetadataReaders = statistics.Sum(value => value.ObjectMetadataReaders),
            ObjectMetadataRequests = statistics.Sum(value => value.ObjectMetadataRequests),
            ObjectMetadataCacheHits = statistics.Sum(value => value.ObjectMetadataCacheHits),
            UniqueObjectMetadataObjects = statistics.Sum(value => value.UniqueObjectMetadataObjects),
            ObjectMetadataCacheEvictions = statistics.Sum(value => value.ObjectMetadataCacheEvictions),
            PeakCachedObjectMetadataLengthsPerRepository = statistics.Max(
                value => value.PeakCachedObjectMetadataLengthsPerRepository),
            BlobRequests = statistics.Sum(value => value.BlobRequests),
            BlobCacheHits = statistics.Sum(value => value.BlobCacheHits),
            BlobCacheEvictions = statistics.Sum(value => value.BlobCacheEvictions),
            UniqueBlobObjects = statistics.Sum(value => value.UniqueBlobObjects),
            BlobRequestedBytes = statistics.Sum(value => value.BlobRequestedBytes),
            BlobCacheHitBytes = statistics.Sum(value => value.BlobCacheHitBytes),
            BlobReadBytes = statistics.Sum(value => value.BlobReadBytes),
            UniqueBlobBytes = statistics.Sum(value => value.UniqueBlobBytes),
            RetainedBlobBytesPerRepository = statistics.Max(value => value.RetainedBlobBytesPerRepository),
            PeakCachedBlobBytesPerRepository = statistics.Max(value => value.PeakCachedBlobBytesPerRepository),
            ObjectReaderCpuTime = TimeSpan.FromTicks(
                statistics.Sum(value => value.ObjectReaderCpuTime.Ticks)),
            ObjectReaderOccupiedTime = TimeSpan.FromTicks(
                statistics.Sum(value => value.ObjectReaderOccupiedTime.Ticks)),
            ObjectReaderWaitTime = TimeSpan.FromTicks(
                statistics.Sum(value => value.ObjectReaderWaitTime.Ticks)),
            SnapshotAnalysisRetentionLimit = statistics.Max(value => value.SnapshotAnalysisRetentionLimit),
            AnalysisArtifactRetentionLimit = statistics.Max(value => value.AnalysisArtifactRetentionLimit),
            SnapshotInventoryRetentionLimit = statistics.Max(value => value.SnapshotInventoryRetentionLimit),
            SnapshotInventoryRootRetentionLimit = statistics.Max(
                value => value.SnapshotInventoryRootRetentionLimit),
            SnapshotDeltaBatchOutputByteLimit = statistics.Max(
                value => value.SnapshotDeltaBatchOutputByteLimit),
            BlobCacheByteLimitPerRepository = statistics.Max(value => value.BlobCacheByteLimitPerRepository),
            ObjectMetadataCacheEntryLimitPerRepository = statistics.Max(
                value => value.ObjectMetadataCacheEntryLimitPerRepository),
        };
}
