namespace EffortHours.Contracts.V1;

public sealed record ChangeSnapshotReference
{
    public required string Selector { get; init; }

    public required string ObjectId { get; init; }

    public required ChangeSnapshotKind Kind { get; init; }
}

public sealed record PullRequestReference
{
    public required string Input { get; init; }

    public required int Number { get; init; }

    public string? Repository { get; init; }

    public string? Url { get; init; }
}

public sealed record ChangeSelection
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required ChangeSelectionKind Kind { get; init; }

    public required ChangeSnapshotReference Base { get; init; }

    public required ChangeSnapshotReference Head { get; init; }

    public string? Commit { get; init; }

    public string? Parent { get; init; }

    public string? Range { get; init; }

    public PullRequestReference? PullRequest { get; init; }
}

public sealed record ChangePathEvidence
{
    public required string Id { get; init; }

    public required ChangePathStatus Status { get; init; }

    public required string Path { get; init; }

    public string? PreviousPath { get; init; }

    public string? BaseObjectId { get; init; }

    public string? HeadObjectId { get; init; }

    public long? BaseBytes { get; init; }

    public long? HeadBytes { get; init; }

    public int EditRegions { get; init; }

    public required ChangePathClassification Classification { get; init; }

    public required bool Represented { get; init; }

    public required string Reason { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}

public sealed record ChangeEvidence
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required ChangeSelection Selection { get; init; }

    public required RepositoryDescriptor Repository { get; init; }

    public required string BaseEvidenceDigest { get; init; }

    public required string HeadEvidenceDigest { get; init; }

    public int UnchangedContextPathCount { get; init; }

    public IReadOnlyList<ChangePathEvidence> Paths { get; init; } = [];

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];
}

public sealed record SignedEffortRange
{
    public required decimal Low { get; init; }

    public required decimal Expected { get; init; }

    public required decimal High { get; init; }
}

public sealed record ChangeComponentEstimate
{
    public required string Id { get; init; }

    public required ChangeComponentKind Kind { get; init; }

    public required string Selector { get; init; }

    public required string BaseObjectId { get; init; }

    public required string HeadObjectId { get; init; }

    public required EffortRange IsolatedEffort { get; init; }

    public required decimal AllocatedExpectedHours { get; init; }

    public IReadOnlyList<string> EvidenceIds { get; init; } = [];
}

public sealed record ChangeAdjustment
{
    public required string Id { get; init; }

    public required ChangeAdjustmentKind Kind { get; init; }

    public required SignedEffortRange EffortDelta { get; init; }

    public required string Reason { get; init; }

    public IReadOnlyList<string> ComponentIds { get; init; } = [];

    public IReadOnlyList<string> EvidenceIds { get; init; } = [];
}

public sealed record ChangeNormalizationSummary
{
    public required string Id { get; init; }

    public required ChangeNormalizationStatus Status { get; init; }

    public required string CalculationMethod { get; init; }

    public required EffortRange GrossIsolatedEffort { get; init; }

    public required EffortRange NormalizedFinalDeltaEffort { get; init; }

    public required decimal ExpectedGrossToFinalNormalizationHours { get; init; }

    public decimal? ExpectedGrossToFinalNormalizationShare { get; init; }

    public required decimal ExpectedReworkLikeHours { get; init; }

    public decimal? ExpectedReworkLikeShare { get; init; }

    public required decimal ExpectedOtherNormalizationHours { get; init; }

    public decimal? ExpectedOtherNormalizationShare { get; init; }

    public required decimal ExpectedSharedOrRepeatedHours { get; init; }

    public required decimal ExpectedOverlapHours { get; init; }

    public required decimal ExpectedRevertHours { get; init; }

    public required decimal ExpectedResidualInteractionHours { get; init; }

    public required decimal ExpectedPositiveInteractionHours { get; init; }

    public IReadOnlyList<string> SharedOrRepeatedAdjustmentIds { get; init; } = [];

    public IReadOnlyList<string> OverlapAdjustmentIds { get; init; } = [];

    public IReadOnlyList<string> RevertAdjustmentIds { get; init; } = [];

    public IReadOnlyList<string> ResidualInteractionAdjustmentIds { get; init; } = [];

    public IReadOnlyList<string> PositiveInteractionAdjustmentIds { get; init; } = [];
}

public sealed record ChangeReconciliation
{
    public required EffortRange IsolatedComponentSum { get; init; }

    public required EffortRange NormalizedEffort { get; init; }

    public required decimal AdditivityToleranceHours { get; init; }

    public required decimal ExpectedDifferenceHours { get; init; }

    public required string Assessment { get; init; }

    public required string AllocationMethod { get; init; }

    public IReadOnlyList<ChangeComponentEstimate> Components { get; init; } = [];

    public IReadOnlyList<ChangeAdjustment> Adjustments { get; init; } = [];

    public ChangeNormalizationSummary? Normalization { get; init; }
}

public sealed record ChangeEstimateReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public string ChangeEvidenceSchemaVersion { get; init; } = ContractVersions.V1;

    public required string EstimatorVersion { get; init; }

    public required string SourceEstimatorVersion { get; init; }

    public required RepositoryDescriptor Repository { get; init; }

    public required ChangeSelection Selection { get; init; }

    public required ChangeEvidence Evidence { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required EstimationBaseline Baseline { get; init; }

    public required EffortRange TotalEffort { get; init; }

    public RateCard? RateCard { get; init; }

    public CostRange? TotalCost { get; init; }

    public IReadOnlyList<CategoryEstimate> Categories { get; init; } = [];

    public IReadOnlyList<WorkItem> WorkItems { get; init; } = [];

    public IReadOnlyList<WorkItem> ProfessionalizationGap { get; init; } = [];

    public required ChangeReconciliation Reconciliation { get; init; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    public IReadOnlyList<string> Assumptions { get; init; } = [];

    public required VerificationSummary Verification { get; init; }
}

public sealed record ChangeEstimateExplanation
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public string SourceChangeEstimateSchemaVersion { get; init; } = ContractVersions.V1;

    public required string EstimatorVersion { get; init; }

    public required ChangeSelection Selection { get; init; }

    public required string RequestedId { get; init; }

    public IReadOnlyList<WorkItem> WorkItems { get; init; } = [];

    public ChangeNormalizationSummary? Normalization { get; init; }

    public IReadOnlyList<ChangeAdjustment>? Adjustments { get; init; }

    public IReadOnlyList<ChangePathEvidence> Evidence { get; init; } = [];

    public IReadOnlyList<string> UnresolvedEvidenceIds { get; init; } = [];
}
