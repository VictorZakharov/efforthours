namespace EffortHours.Contracts.V1;

public static class SnapshotPortfolioVersions
{
    public const string Manifest = "snapshot-portfolio-manifest/1.0.0";
    public const string Report = "snapshot-portfolio-report/1.0.0";
    public const string Receipt = "snapshot-measurement-receipt/1.0.0";
    public const string Snapshot = "git-archive/1.0.0";
    public const string Areas = "ordered-standalone-areas/1.0.0";
    public const string Categories = "snapshot-category-groups/1.0.0";
    public const string Daily = "daily-replacement-calendar/1.0.0";
}

public sealed record SnapshotPortfolioManifest
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;
    public string ProtocolVersion { get; init; } = SnapshotPortfolioVersions.Manifest;
    public required int Year { get; init; }
    public required string Timezone { get; init; }
    public required EstimationProfile Profile { get; init; }
    public string SnapshotPolicy { get; init; } = SnapshotPortfolioVersions.Snapshot;
    public string BaselineConvention { get; init; } = "january-1-zero";
    public string? CalendarPolicy { get; init; }
    public required IReadOnlyList<SnapshotProjectDefinition> Projects { get; init; }
}

public sealed record SnapshotProjectDefinition
{
    public required string Id { get; init; }
    public required string Ref { get; init; }
    public string? AreaMeasurementMode { get; init; }
    public required IReadOnlyList<SnapshotAreaDefinition> Areas { get; init; }
    public IReadOnlyList<SnapshotAreaRevision>? AreaRevisions { get; init; }
    public ReviewedVendorManifest? VendorManifest { get; init; }
}

public sealed record SnapshotAreaRevision(string CommitObjectId, IReadOnlyList<SnapshotAreaDefinition> Areas);

public sealed record SnapshotAreaDefinition
{
    public required string Id { get; init; }
    public required IReadOnlyList<string> Include { get; init; }
}

public sealed record SnapshotPortfolioLocalMap
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;
    public required IReadOnlyList<SnapshotProjectLocator> Projects { get; init; }
}

public sealed record SnapshotProjectLocator
{
    public required string Id { get; init; }
    public string? RepositoryPath { get; init; }
    public string? GitHubRepository { get; init; }
}

public sealed record MeasurementIdentity
{
    public string Contract { get; init; } = SnapshotPortfolioVersions.Receipt;
    public string Normalization { get; init; } = "repository-evidence/1.0.0";
    public required EstimationProfile Profile { get; init; }
    public required string ModelDigest { get; init; }
    public required string ImplementationDigest { get; init; }
    public string InventoryPolicy { get; init; } = "archive-relative-inventory/1.0.0";
    public string IgnorePolicy { get; init; } = "git-and-efforthours-ignore/1.0.0";
    public string SnapshotPolicy { get; init; } = SnapshotPortfolioVersions.Snapshot;
    public string AreaPolicy { get; init; } = SnapshotPortfolioVersions.Areas;
    public string AnalysisOptions { get; init; } = "default-static/1.0.0";
    public string? OwnershipDigest { get; init; }
}

public sealed record SnapshotMeasurementReceipt
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;
    public string ProtocolVersion { get; init; } = SnapshotPortfolioVersions.Receipt;
    public required string Id { get; init; }
    public required string InputDigest { get; init; }
    public required MeasurementIdentity Measurement { get; init; }
    public required string ProducerVersion { get; init; }
    public required EffortRange Hours { get; init; }
    public required IReadOnlyList<CategoryEstimate> Categories { get; init; }
    public required int SelectedFileCount { get; init; }
    public required int ContextFileCount { get; init; }
    public required string EvidenceDigest { get; init; }
    public IReadOnlyList<string> DirectoryIds { get; init; } = [];
    public IReadOnlyList<SnapshotBodyFingerprint> MaintainedBodies { get; init; } = [];
    public string Maturity { get; init; } = "experimental-uncalibrated";
}

public sealed record SnapshotPortfolioReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;
    public string ProtocolVersion { get; init; } = SnapshotPortfolioVersions.Report;
    public required string Status { get; init; }
    public required DateTimeOffset AsOf { get; init; }
    public required int Year { get; init; }
    public required string Timezone { get; init; }
    public required string ManifestDigest { get; init; }
    public required string MeasurementEpoch { get; init; }
    public string? PreviousEpochDigest { get; init; }
    public required string SemanticDigest { get; init; }
    public string CategoryMapping { get; init; } = SnapshotPortfolioVersions.Categories;
    public string? CalendarPolicy { get; init; }
    public RateCard? RateCard { get; init; }
    public IReadOnlyList<SnapshotSharedSourceReview> SharedSourceReviews { get; init; } = [];
    public required IReadOnlyList<SnapshotProjectResult> Projects { get; init; }
    public IReadOnlyList<SnapshotMeasurementReceipt> Receipts { get; init; } = [];
    public SnapshotPortfolioTelemetry Telemetry { get; init; } = new();
}

public sealed record SnapshotProjectResult
{
    public required string Id { get; init; }
    public string? HeadObjectId { get; init; }
    public DateTimeOffset? FirstAvailableCommitAt { get; init; }
    public required bool ShallowHistory { get; init; }
    public required string AreasDigest { get; init; }
    public required IReadOnlyList<SnapshotPeriodResult> Periods { get; init; }
    public string? PlanningIssue { get; init; }
    public string? AreaMeasurementMode { get; init; }
    public IReadOnlyList<SnapshotPeriodResult>? MonthlyEndpoints { get; init; }
    public int? SelectedSnapshotCount { get; init; }
    public int? DistinctSnapshotCount { get; init; }
}

public sealed record SnapshotPeriodResult
{
    public required string Id { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset Cutoff { get; init; }
    public string? CommitObjectId { get; init; }
    public string? TreeObjectId { get; init; }
    public DateTimeOffset? CommitAt { get; init; }
    public string? WholeReceiptId { get; init; }
    public EffortRange? Hours { get; init; }
    public CostRange? TotalCost { get; init; }
    public IReadOnlyList<SnapshotAreaResult> Areas { get; init; } = [];
    public IReadOnlyList<SnapshotAreaPlan> AreaPlans { get; init; } = [];
    public string? AreaDefinitionDigest { get; init; }
    public string? AreaDisposition { get; init; }
    public string? PlanningIssue { get; init; }
    public string? PlanningAreaId { get; init; }
    public string CacheDisposition { get; init; } = "not-requested";
    public decimal? PreviousExpectedHours { get; init; }
    public long? ExpectedChangeCentihours { get; init; }
    public int? ActiveCommitDateCount { get; init; }
    public long? BenchmarkHours { get; init; }
}

public sealed record SnapshotBodyFingerprint(string Digest, long Bytes);
public sealed record SnapshotAreaPlan(string Id, string Disposition);
public sealed record SnapshotSharedSourceReview(string FirstProjectId, string SecondProjectId,
    int SharedBodyCount, long SharedBytes, string Disposition = "review-required-no-exclusion");

public sealed record SnapshotAreaResult
{
    public required string Id { get; init; }
    public required string ReceiptId { get; init; }
    public required decimal StandaloneExpectedHours { get; init; }
    public required decimal AllocatedExpectedHours { get; init; }
    public required int OwnedFileCount { get; init; }
    public required int ContextFileCount { get; init; }
    public required string InputDigest { get; init; }
    public string? InventoryDigest { get; init; }
    public decimal? PreviousExpectedHours { get; init; }
    public string ReviewStatus { get; init; } = "reviewed-boundary";
}

public sealed record SnapshotPortfolioTelemetry
{
    public int? InventoryReads { get; init; }
    public int? AreaPlanningCalls { get; init; }
    public int? SelectorCompilations { get; init; }
    public int? PlanningReuseHits { get; init; }
    public int EstimatorCalls { get; init; }
    public int ReceiptHits { get; init; }
    public int ReceiptInvalidations { get; init; }
    public int Exports { get; init; }
    public int ArtifactRequests { get; init; }
    public int ArtifactHits { get; init; }
    public int ArtifactInvalidations { get; init; }
    public int ArtifactEvictions { get; init; }
    public long GitReadBytes { get; init; }
    public long PeakWorkingSetBytes { get; init; }
    public double ElapsedMilliseconds { get; init; }
    public int ConcurrencyLimit { get; init; } = 1;
    public int ArchiveMiBLimit { get; init; } = 256;
    public int CheckpointMiBLimit { get; init; } = 512;
    public int MemoryMiBLimit { get; init; } = 2048;
    public int OutputMiBLimit { get; init; } = 32;
    public int TimeoutSecondsLimit { get; init; } = 3600;
}
