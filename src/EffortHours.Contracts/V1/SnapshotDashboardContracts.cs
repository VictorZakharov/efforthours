namespace EffortHours.Contracts.V1;

public sealed record SnapshotDashboardStudies
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;
    public required IReadOnlyList<SnapshotDashboardStudy> Projects { get; init; }
}

public sealed record SnapshotDashboardStudy
{
    public required string Id { get; init; }
    public required string PublicRepositoryUrl { get; init; }
    public required string AreasDigest { get; init; }
    public required IReadOnlyList<SnapshotDashboardAreaStudy> Areas { get; init; }
}

public sealed record SnapshotDashboardAreaStudy
{
    public required string Id { get; init; }
    public required string Folder { get; init; }
    public required string ReviewedCommit { get; init; }
}

public sealed record SnapshotDashboardAsset
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;
    public string ProtocolVersion { get; init; } = "snapshot-dashboard-asset/1.0.0";
    public required string SourceSemanticDigest { get; init; }
    public required string MeasurementEpoch { get; init; }
    public required string DocumentationMarkdown { get; init; }
    public required IReadOnlyList<SnapshotDashboardProjectAsset> Projects { get; init; }
}

public sealed record SnapshotDashboardProjectAsset
{
    public required string Id { get; init; }
    public required IReadOnlyList<SnapshotDashboardPeriodAsset> Periods { get; init; }
}

public sealed record SnapshotDashboardPeriodAsset
{
    public required string Id { get; init; }
    public required string Status { get; init; }
    public string? CommitObjectId { get; init; }
    public EffortRange? Hours { get; init; }
    public required IReadOnlyList<SnapshotCategoryGroup> Categories { get; init; }
    public required IReadOnlyList<SnapshotDashboardAreaAsset> Areas { get; init; }
}

public sealed record SnapshotCategoryGroup(string Id, EffortRange Hours);
public sealed record SnapshotDashboardAreaAsset(string Id, decimal StandaloneExpectedHours,
    decimal AllocatedExpectedHours, string FolderLink, string ReceiptId);
