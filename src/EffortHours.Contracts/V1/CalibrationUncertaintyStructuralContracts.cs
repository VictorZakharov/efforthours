namespace EffortHours.Contracts.V1;

public enum CalibrationUncertaintyStructuralCoverageStatus
{
    Complete,
    Partial,
    NotApplicable,
    Unavailable,
}

public sealed record CalibrationUncertaintyStructuralWorkItemFeatures
{
    public required string WorkItemId { get; init; }

    public required EffortCategory Category { get; init; }

    public required ComplexityLevel SourceComplexity { get; init; }

    public IReadOnlyList<string> Ecosystems { get; init; } = [];

    public required decimal ExpectedHours { get; init; }

    public required EffortRange SourceRange { get; init; }

    public string? ParentId { get; init; }

    public string? CorrelationGroup { get; init; }

    public required CalibrationUncertaintyStructuralCoverageStatus CoverageStatus { get; init; }

    public IReadOnlyList<string> ResolvedEvidenceIds { get; init; } = [];

    public IReadOnlyList<string> UnresolvedEvidenceIds { get; init; } = [];

    public IReadOnlyList<string> StructuralEvidenceIds { get; init; } = [];

    public IReadOnlyList<string> IncompatibleStructuralEvidenceIds { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintyFeatureValue> Features { get; init; } = [];
}

public sealed record CalibrationUncertaintyStructuralFeatureSummary
{
    public required int WorkItemCount { get; init; }

    public required int FeatureCount { get; init; }

    public required int CompleteWorkItemCount { get; init; }

    public required int PartialWorkItemCount { get; init; }

    public required int NotApplicableWorkItemCount { get; init; }

    public required int UnavailableWorkItemCount { get; init; }

    public required int ResolvedEvidenceReferenceCount { get; init; }

    public required int UnresolvedEvidenceReferenceCount { get; init; }

    public required int StructuralEvidenceReferenceCount { get; init; }

    public required int IncompatibleStructuralEvidenceReferenceCount { get; init; }
}

public sealed record CalibrationUncertaintyStructuralFeatureReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string ProjectorVersion { get; init; }

    public required CalibrationUncertaintyFeatureContract FeatureContract { get; init; }

    public required string FeatureContractDigest { get; init; }

    public required string EstimateDigest { get; init; }

    public required string EvidenceDigest { get; init; }

    public required string RepositorySourceDigest { get; init; }

    public IReadOnlyList<string> Ecosystems { get; init; } = [];

    public required string EstimatorVersion { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public required CalibrationUncertaintyStructuralFeatureSummary Summary { get; init; }

    public IReadOnlyList<CalibrationUncertaintyStructuralWorkItemFeatures> WorkItems { get; init; } = [];
}
