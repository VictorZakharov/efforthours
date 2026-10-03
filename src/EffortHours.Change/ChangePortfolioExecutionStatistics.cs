using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record ChangePortfolioExecutionStatistics
{
    public int RepositoryCount { get; init; }

    public int MaximumActiveRepositories { get; init; }

    public int MaximumConcurrentFileAnalysesPerRepository { get; init; }

    public int MaximumConcurrentCpuWorkItems { get; init; }

    public int MaximumConcurrentGitTreeReads { get; init; }

    public int SnapshotAnalysisRequests { get; init; }

    public int SnapshotAnalysisHits { get; init; }

    public int UniqueSnapshotAnalysisKeys { get; init; }

    public int SnapshotAnalysisRevisitMisses { get; init; }

    public int AnalysisArtifactRequests { get; init; }

    public int AnalysisArtifactHits { get; init; }

    public int UniqueAnalysisArtifactKeys { get; init; }

    public int AnalysisArtifactRevisitMisses { get; init; }

    public int AnalysisArtifactEvictions { get; init; }

    public int PeakRetainedAnalysisArtifacts { get; init; }

    public int SnapshotAnalysisEvictions { get; init; }

    public int PeakRetainedSnapshotAnalyses { get; init; }

    public int SnapshotInventoryRequests { get; init; }

    public int SnapshotInventoryHits { get; init; }

    public int UniqueSnapshotInventoryObjects { get; init; }

    public int SnapshotInventoryRevisitMisses { get; init; }

    public int FullSnapshotInventoryLoads { get; init; }

    public int IncrementalSnapshotInventoryLoads { get; init; }

    public int BatchedIncrementalSnapshotInventoryLoads { get; init; }

    public TimeSpan FullSnapshotInventoryReadTime { get; init; }

    public TimeSpan FullSnapshotInventoryProjectionTime { get; init; }

    public TimeSpan IncrementalSnapshotInventoryProjectionTime { get; init; }

    public int SnapshotInventoryEvictions { get; init; }

    public int PeakRetainedSnapshotInventories { get; init; }

    public int PeakRetainedSnapshotInventoryRoots { get; init; }

    public int ObjectDatabaseReaders { get; init; }

    public int ObjectMetadataReaders { get; init; }

    public int ObjectMetadataRequests { get; init; }

    public int ObjectMetadataCacheHits { get; init; }

    public int UniqueObjectMetadataObjects { get; init; }

    public int ObjectMetadataCacheEvictions { get; init; }

    public int PeakCachedObjectMetadataLengthsPerRepository { get; init; }

    public long BlobRequests { get; init; }

    public long BlobCacheHits { get; init; }

    public long BlobCacheEvictions { get; init; }

    public long UniqueBlobObjects { get; init; }

    public long BlobRequestedBytes { get; init; }

    public long BlobCacheHitBytes { get; init; }

    public long BlobReadBytes { get; init; }

    public long UniqueBlobBytes { get; init; }

    public long RetainedBlobBytesPerRepository { get; init; }

    public long PeakCachedBlobBytesPerRepository { get; init; }

    public TimeSpan ObjectReaderCpuTime { get; init; }

    public TimeSpan ObjectReaderOccupiedTime { get; init; }

    public TimeSpan ObjectReaderWaitTime { get; init; }

    public int SnapshotAnalysisRetentionLimit { get; init; }

    public int AnalysisArtifactRetentionLimit { get; init; }

    public int SnapshotInventoryRetentionLimit { get; init; }

    public int SnapshotInventoryRootRetentionLimit { get; init; }

    public int SnapshotDeltaBatchOutputByteLimit { get; init; }

    public long BlobCacheByteLimitPerRepository { get; init; }

    public int ObjectMetadataCacheEntryLimitPerRepository { get; init; }

    internal static ChangePortfolioExecutionStatistics Combine(
        ChangePortfolioExecutionStatistics left, ChangePortfolioExecutionStatistics right) => new()
        {
            RepositoryCount = Math.Max(left.RepositoryCount, right.RepositoryCount),
            MaximumActiveRepositories = Math.Max(left.MaximumActiveRepositories, right.MaximumActiveRepositories),
            MaximumConcurrentFileAnalysesPerRepository = Math.Max(left.MaximumConcurrentFileAnalysesPerRepository, right.MaximumConcurrentFileAnalysesPerRepository),
            MaximumConcurrentCpuWorkItems = Math.Max(left.MaximumConcurrentCpuWorkItems, right.MaximumConcurrentCpuWorkItems),
            MaximumConcurrentGitTreeReads = Math.Max(left.MaximumConcurrentGitTreeReads, right.MaximumConcurrentGitTreeReads),
            SnapshotAnalysisRequests = left.SnapshotAnalysisRequests + right.SnapshotAnalysisRequests,
            SnapshotAnalysisHits = left.SnapshotAnalysisHits + right.SnapshotAnalysisHits,
            UniqueSnapshotAnalysisKeys = left.UniqueSnapshotAnalysisKeys + right.UniqueSnapshotAnalysisKeys,
            SnapshotAnalysisRevisitMisses = left.SnapshotAnalysisRevisitMisses + right.SnapshotAnalysisRevisitMisses,
            AnalysisArtifactRequests = left.AnalysisArtifactRequests + right.AnalysisArtifactRequests,
            AnalysisArtifactHits = left.AnalysisArtifactHits + right.AnalysisArtifactHits,
            UniqueAnalysisArtifactKeys = left.UniqueAnalysisArtifactKeys + right.UniqueAnalysisArtifactKeys,
            AnalysisArtifactRevisitMisses = left.AnalysisArtifactRevisitMisses + right.AnalysisArtifactRevisitMisses,
            AnalysisArtifactEvictions = left.AnalysisArtifactEvictions + right.AnalysisArtifactEvictions,
            PeakRetainedAnalysisArtifacts = Math.Max(left.PeakRetainedAnalysisArtifacts, right.PeakRetainedAnalysisArtifacts),
            SnapshotAnalysisEvictions = left.SnapshotAnalysisEvictions + right.SnapshotAnalysisEvictions,
            PeakRetainedSnapshotAnalyses = Math.Max(left.PeakRetainedSnapshotAnalyses, right.PeakRetainedSnapshotAnalyses),
            SnapshotInventoryRequests = left.SnapshotInventoryRequests + right.SnapshotInventoryRequests,
            SnapshotInventoryHits = left.SnapshotInventoryHits + right.SnapshotInventoryHits,
            UniqueSnapshotInventoryObjects = left.UniqueSnapshotInventoryObjects + right.UniqueSnapshotInventoryObjects,
            SnapshotInventoryRevisitMisses = left.SnapshotInventoryRevisitMisses + right.SnapshotInventoryRevisitMisses,
            FullSnapshotInventoryLoads = left.FullSnapshotInventoryLoads + right.FullSnapshotInventoryLoads,
            IncrementalSnapshotInventoryLoads = left.IncrementalSnapshotInventoryLoads + right.IncrementalSnapshotInventoryLoads,
            BatchedIncrementalSnapshotInventoryLoads = left.BatchedIncrementalSnapshotInventoryLoads + right.BatchedIncrementalSnapshotInventoryLoads,
            FullSnapshotInventoryReadTime = left.FullSnapshotInventoryReadTime + right.FullSnapshotInventoryReadTime,
            FullSnapshotInventoryProjectionTime = left.FullSnapshotInventoryProjectionTime + right.FullSnapshotInventoryProjectionTime,
            IncrementalSnapshotInventoryProjectionTime = left.IncrementalSnapshotInventoryProjectionTime + right.IncrementalSnapshotInventoryProjectionTime,
            SnapshotInventoryEvictions = left.SnapshotInventoryEvictions + right.SnapshotInventoryEvictions,
            PeakRetainedSnapshotInventories = Math.Max(left.PeakRetainedSnapshotInventories, right.PeakRetainedSnapshotInventories),
            PeakRetainedSnapshotInventoryRoots = Math.Max(left.PeakRetainedSnapshotInventoryRoots, right.PeakRetainedSnapshotInventoryRoots),
            ObjectDatabaseReaders = left.ObjectDatabaseReaders + right.ObjectDatabaseReaders,
            ObjectMetadataReaders = left.ObjectMetadataReaders + right.ObjectMetadataReaders,
            ObjectMetadataRequests = left.ObjectMetadataRequests + right.ObjectMetadataRequests,
            ObjectMetadataCacheHits = left.ObjectMetadataCacheHits + right.ObjectMetadataCacheHits,
            UniqueObjectMetadataObjects = left.UniqueObjectMetadataObjects + right.UniqueObjectMetadataObjects,
            ObjectMetadataCacheEvictions = left.ObjectMetadataCacheEvictions + right.ObjectMetadataCacheEvictions,
            PeakCachedObjectMetadataLengthsPerRepository = Math.Max(left.PeakCachedObjectMetadataLengthsPerRepository, right.PeakCachedObjectMetadataLengthsPerRepository),
            BlobRequests = left.BlobRequests + right.BlobRequests,
            BlobCacheHits = left.BlobCacheHits + right.BlobCacheHits,
            BlobCacheEvictions = left.BlobCacheEvictions + right.BlobCacheEvictions,
            UniqueBlobObjects = left.UniqueBlobObjects + right.UniqueBlobObjects,
            BlobRequestedBytes = left.BlobRequestedBytes + right.BlobRequestedBytes,
            BlobCacheHitBytes = left.BlobCacheHitBytes + right.BlobCacheHitBytes,
            BlobReadBytes = left.BlobReadBytes + right.BlobReadBytes,
            UniqueBlobBytes = left.UniqueBlobBytes + right.UniqueBlobBytes,
            RetainedBlobBytesPerRepository = Math.Max(left.RetainedBlobBytesPerRepository, right.RetainedBlobBytesPerRepository),
            PeakCachedBlobBytesPerRepository = Math.Max(left.PeakCachedBlobBytesPerRepository, right.PeakCachedBlobBytesPerRepository),
            ObjectReaderCpuTime = left.ObjectReaderCpuTime + right.ObjectReaderCpuTime,
            ObjectReaderOccupiedTime = left.ObjectReaderOccupiedTime + right.ObjectReaderOccupiedTime,
            ObjectReaderWaitTime = left.ObjectReaderWaitTime + right.ObjectReaderWaitTime,
            SnapshotAnalysisRetentionLimit = Math.Max(left.SnapshotAnalysisRetentionLimit, right.SnapshotAnalysisRetentionLimit),
            AnalysisArtifactRetentionLimit = Math.Max(left.AnalysisArtifactRetentionLimit, right.AnalysisArtifactRetentionLimit),
            SnapshotInventoryRetentionLimit = Math.Max(left.SnapshotInventoryRetentionLimit, right.SnapshotInventoryRetentionLimit),
            SnapshotInventoryRootRetentionLimit = Math.Max(left.SnapshotInventoryRootRetentionLimit, right.SnapshotInventoryRootRetentionLimit),
            SnapshotDeltaBatchOutputByteLimit = Math.Max(left.SnapshotDeltaBatchOutputByteLimit, right.SnapshotDeltaBatchOutputByteLimit),
            BlobCacheByteLimitPerRepository = Math.Max(left.BlobCacheByteLimitPerRepository, right.BlobCacheByteLimitPerRepository),
            ObjectMetadataCacheEntryLimitPerRepository = Math.Max(left.ObjectMetadataCacheEntryLimitPerRepository, right.ObjectMetadataCacheEntryLimitPerRepository),
        };

    public Diagnostic CreateDiagnostic() => new()
    {
        Code = "FB5325",
        Severity = DiagnosticSeverity.Information,
        Message = $"Repository-scoped portfolio reuse served {SnapshotAnalysisHits} of " +
            $"{SnapshotAnalysisRequests} snapshot-analysis request(s) across " +
            $"{UniqueSnapshotAnalysisKeys} unique key(s), with " +
            $"{SnapshotAnalysisRevisitMisses} revisit miss(es); served {AnalysisArtifactHits} of " +
            $"{AnalysisArtifactRequests} immutable file-analysis artifact request(s) across " +
            $"{UniqueAnalysisArtifactKeys} unique key(s), with " +
            $"{AnalysisArtifactRevisitMisses} revisit miss(es); served {SnapshotInventoryHits} of " +
            $"{SnapshotInventoryRequests} immutable-inventory request(s) across " +
            $"{UniqueSnapshotInventoryObjects} unique object(s), with " +
            $"{SnapshotInventoryRevisitMisses} revisit miss(es); built " +
            $"{FullSnapshotInventoryLoads} full and {IncrementalSnapshotInventoryLoads} incremental " +
            $"inventories, including {BatchedIncrementalSnapshotInventoryLoads} from repository-level " +
            $"batch diffs, while retaining at most {PeakRetainedSnapshotInventoryRoots} full-tree " +
            $"root lineage(s); evicted {SnapshotAnalysisEvictions} analysis entries and " +
            $"{SnapshotInventoryEvictions} inventory entries; " +
            $"{ObjectDatabaseReaders} Git object reader(s) served {BlobRequests} blob request(s) over " +
            $"{UniqueBlobObjects} unique object(s) with {BlobCacheHits} cache hit(s), " +
            $"{BlobCacheEvictions} eviction(s), and {BlobReadBytes} byte(s) read from Git. At most " +
            $"{ObjectMetadataReaders} Git metadata reader(s) served {ObjectMetadataRequests} " +
            $"length request(s) over {UniqueObjectMetadataObjects} unique object(s), with " +
            $"{ObjectMetadataCacheHits} cache hit(s) and {ObjectMetadataCacheEvictions} eviction(s). " +
            "At most " +
            $"{MaximumActiveRepositories} repository session(s) were active concurrently, " +
            $"with separate process-wide budgets of {MaximumConcurrentGitTreeReads} Git tree " +
            $"read(s) and {MaximumConcurrentCpuWorkItems} independent common/semantic " +
            "file-analysis and thread-safe estimation work item(s), and " +
            "retention limits of " +
            $"{SnapshotAnalysisRetentionLimit} analyses, " +
            $"{AnalysisArtifactRetentionLimit} file-analysis artifacts, " +
            $"{SnapshotInventoryRetentionLimit} structurally shared inventories across " +
            $"{SnapshotInventoryRootRetentionLimit} full-tree root lineages, and " +
            $"{BlobCacheByteLimitPerRepository / 1024 / 1024} MiB of blobs plus " +
            $"{ObjectMetadataCacheEntryLimitPerRepository} object-length entries per repository; " +
            $"repository batch output is capped at " +
            $"{SnapshotDeltaBatchOutputByteLimit / 1024 / 1024} MiB.",
    };
}

public sealed record ChangePortfolioEstimateBatch
{
    public IReadOnlyList<ChangeEstimateReport> Reports { get; init; } = [];

    public required ChangePortfolioExecutionStatistics Statistics { get; init; }
}
