namespace EffortHours.Contracts.V1;

public static class ChangePortfolioComparisonLimits
{
    public const int MaximumBuckets = 512;

    public const int MaximumCapacityEntries =
        MaximumBuckets * ChangeAuthorPeriodManifestLimits.MaximumContributors;

    public const int MaximumTitleLength = 256;

    public const int MaximumCalendarPolicyLength = 512;
}

public static class ChangePortfolioComparisonPolicies
{
    public const string CalendarMonthV1 = "calendar-month/1.0.0";

    public const string CalendarWeekV1 = "calendar-week-monday/1.0.0";

    public const string CustomClosedBucketsV1 = "custom-closed-buckets/1.0.0";

    public const string RepositoryEvidenceShardsV1 = "repository-evidence-shards/1.0.0";

    public const string ExclusiveContributorSeriesV1 =
        "exclusive-contributor-match-sets-with-shared-groups/1.0.0";

    public const string IsolatedContributorSeriesV1 =
        "membership-stable-isolated-contributor-series/1.0.0";

    public const int RollingWindowBucketCount = 3;
}

public sealed record ChangePortfolioBucketManifest
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public IReadOnlyList<ChangePortfolioBucketDefinition> Buckets { get; init; } = [];
}

public sealed record ChangePortfolioBucketDefinition
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public required DateTimeOffset SinceInclusive { get; init; }

    public required DateTimeOffset UntilExclusive { get; init; }
}

public sealed record ChangePortfolioCapacityManifest
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string CalendarPolicy { get; init; }

    public IReadOnlyList<ChangePortfolioCapacityEntry> Entries { get; init; } = [];
}

public sealed record ChangePortfolioCapacityEntry
{
    public required string BucketId { get; init; }

    public required string ContributorId { get; init; }

    public decimal Hours { get; init; }
}

public sealed record ChangePortfolioComparisonReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required ChangePortfolioComparisonStatus Status { get; init; }

    public required ChangePortfolioComparisonView View { get; init; }

    public required string Title { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }

    public required string CliVersion { get; init; }

    public required string EstimatorVersion { get; init; }

    public required string SourceChangeEstimatorVersion { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required ChangePortfolioSelection Selection { get; init; }

    public required ChangePortfolioComparisonBucketPolicy BucketPolicy { get; init; }

    public ChangePortfolioReport? SourcePortfolio { get; init; }

    public IReadOnlyList<ChangePortfolioComparisonBucket> Buckets { get; init; } = [];

    public IReadOnlyList<ChangePortfolioComparisonSeries> Series { get; init; } = [];

    public required ChangePortfolioComparisonExecution Execution { get; init; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    public required ChangePortfolioComparisonVerification Verification { get; init; }
}

public sealed record ChangePortfolioComparisonBucketPolicy
{
    public required ChangePortfolioBucketPolicyKind Kind { get; init; }

    public required string Policy { get; init; }

    public required string InputDigest { get; init; }

    public ChangePortfolioContributorNormalization ContributorNormalization { get; init; } =
        ChangePortfolioContributorNormalization.Joint;

    public string? CapacityCalendarPolicy { get; init; }

    public string? CapacityInputDigest { get; init; }

    public int RollingWindowBucketCount { get; init; } =
        ChangePortfolioComparisonPolicies.RollingWindowBucketCount;
}

public sealed record ChangePortfolioComparisonBucket
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public required DateTimeOffset SinceInclusive { get; init; }

    public required DateTimeOffset UntilExclusive { get; init; }

    public bool PartialStart { get; init; }

    public bool PartialEnd { get; init; }
}

public sealed record ChangePortfolioComparisonSeries
{
    public required string Id { get; init; }

    public required ChangePortfolioSeriesKind Kind { get; init; }

    public IReadOnlyList<string> ContributorIds { get; init; } = [];

    public bool AdditiveToPortfolio { get; init; }

    public IReadOnlyList<ChangePortfolioComparisonPoint> Points { get; init; } = [];

    public required EffortRange TotalEffort { get; init; }

    public decimal? TotalCapacityHours { get; init; }

    public ChangePortfolioRatioRange? TotalCapacityRatio { get; init; }

    public ChangePortfolioTrendStatistics? Trend { get; init; }
}

public sealed record ChangePortfolioComparisonPoint
{
    public required string BucketId { get; init; }

    public int SelectedChangeCount { get; init; }

    public required EffortRange Effort { get; init; }

    public decimal? CapacityHours { get; init; }

    public ChangePortfolioRatioRange? CapacityRatio { get; init; }
}

public sealed record ChangePortfolioRatioRange
{
    public decimal Low { get; init; }

    public decimal Expected { get; init; }

    public decimal High { get; init; }
}

public sealed record ChangePortfolioTrendStatistics
{
    public int ObservationCount { get; init; }

    public required string FirstBucketId { get; init; }

    public required string LatestBucketId { get; init; }

    public decimal FirstExpectedRatio { get; init; }

    public decimal LatestExpectedRatio { get; init; }

    public decimal? PercentageChange { get; init; }

    public decimal? OrdinaryLeastSquaresSlope { get; init; }

    public decimal? RSquared { get; init; }

    public IReadOnlyList<ChangePortfolioRollingPoint> CapacityWeightedRollingWindow { get; init; } = [];
}

public sealed record ChangePortfolioRollingPoint
{
    public required string BucketId { get; init; }

    public int WindowBucketCount { get; init; }

    public decimal ExpectedRatio { get; init; }
}

public sealed record ChangePortfolioComparisonExecution
{
    public required string RuntimeFramework { get; init; }

    public required string OperatingSystemFamily { get; init; }

    public required string ProcessArchitecture { get; init; }

    public int LogicalProcessorCount { get; init; }

    public required string ShardPolicy { get; init; }

    public required ChangePortfolioComparisonCheckpoint Checkpoint { get; init; }

    public int RepositoryShardCount { get; init; }

    public IReadOnlyList<ChangePortfolioComparisonRepositoryExecution> Repositories { get; init; } = [];

    public IReadOnlyList<ChangePortfolioComparisonPhaseTiming> PhaseTimings { get; init; } = [];

    public ChangePortfolioComparisonProgress? LastProgress { get; init; }

    public required ChangePortfolioComparisonReuse Reuse { get; init; }

    public IReadOnlyList<ChangePortfolioComparisonFailure> Failures { get; init; } = [];
}

public sealed record ChangePortfolioComparisonRepositoryExecution
{
    public required string RepositoryId { get; init; }

    public required ChangePortfolioRepositoryExecutionStatus Status { get; init; }

    public required ChangePortfolioCheckpointDisposition CheckpointDisposition { get; init; }

    public int SelectedChangeCount { get; init; }

    public required decimal ElapsedMilliseconds { get; init; }

    public required string InputDigest { get; init; }

    public IReadOnlyList<ChangePortfolioComparisonPhaseTiming> PhaseTimings { get; init; } = [];

    public ChangePortfolioComparisonProgress? LastProgress { get; init; }
}

public sealed record ChangePortfolioComparisonCheckpoint
{
    public string Protocol { get; init; } = "repository-evidence-checkpoint/1.0.0";

    public bool Enabled { get; init; }

    public int HitCount { get; init; }

    public int MissCount { get; init; }

    public int WriteCount { get; init; }

    public int FailureCount { get; init; }
}

public sealed record ChangePortfolioComparisonPhaseTiming
{
    public required string Phase { get; init; }

    public decimal ElapsedMilliseconds { get; init; }
}

public sealed record ChangePortfolioComparisonProgress
{
    public required DateTimeOffset ObservedAt { get; init; }

    public required string Phase { get; init; }

    public int ProcessedUnits { get; init; }

    public int TotalUnits { get; init; }

    public int AnalysisCacheRequests { get; init; }

    public int AnalysisCacheHits { get; init; }

    public decimal ElapsedMilliseconds { get; init; }

    public long WorkingSetBytes { get; init; }

    public long PeakWorkingSetBytes { get; init; }
}

public sealed record ChangePortfolioComparisonReuse
{
    public int SnapshotAnalysisRequests { get; init; }

    public int SnapshotAnalysisHits { get; init; }

    public int UniqueSnapshotAnalysisKeys { get; init; }

    public int AnalysisArtifactRequests { get; init; }

    public int AnalysisArtifactHits { get; init; }

    public int UniqueAnalysisArtifactKeys { get; init; }

    public int SnapshotInventoryRequests { get; init; }

    public int SnapshotInventoryHits { get; init; }

    public int UniqueSnapshotInventoryObjects { get; init; }

    public long BlobRequests { get; init; }

    public long BlobCacheHits { get; init; }

    public long UniqueBlobObjects { get; init; }

    public long BlobReadBytes { get; init; }

    public long PeakWorkingSetBytes { get; init; }
}

public sealed record ChangePortfolioComparisonFailure
{
    public required string RepositoryId { get; init; }

    public string? BucketId { get; init; }

    public required string Phase { get; init; }

    public required string Category { get; init; }

    public required string Message { get; init; }

    public required string MessageDigest { get; init; }
}

public sealed record ChangePortfolioComparisonVerification
{
    public required string SemanticDigest { get; init; }

    public string? SourcePortfolioDigest { get; init; }

    public required string BucketAllocationPolicy { get; init; }

    public bool CompleteAggregates { get; init; }

    public bool ExecutionOnlyPathsExcluded { get; init; }

    public bool RawAliasesExcluded { get; init; }

    public string? Note { get; init; }
}
