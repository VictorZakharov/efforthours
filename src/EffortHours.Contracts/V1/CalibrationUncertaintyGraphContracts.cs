namespace EffortHours.Contracts.V1;

public sealed record CalibrationUncertaintyGraphNode
{
    public required string NodeId { get; init; }

    public required string Ecosystem { get; init; }

    public required int FanIn { get; init; }

    public required int FanOut { get; init; }

    public required bool Cyclic { get; init; }

    public required int CyclicComponentSize { get; init; }

    public required decimal CyclicComponentNodeShare { get; init; }

    public required CalibrationUncertaintyFeatureAvailability PublicInterfaceAvailability
    {
        get;
        init;
    }

    public decimal? PublicInterfaceConcentration { get; init; }

    public string? PublicInterfaceReasonCode { get; init; }

    public IReadOnlyList<string> PublicInterfaceEvidenceIds { get; init; } = [];
}

public sealed record CalibrationUncertaintyGraphEdge
{
    public required string SourceNodeId { get; init; }

    public required string TargetNodeId { get; init; }

    public IReadOnlyList<string> EvidenceIds { get; init; } = [];
}

public sealed record CalibrationUncertaintyGraphWorkItemMapping
{
    public required string WorkItemId { get; init; }

    public required EffortCategory Category { get; init; }

    public required ComplexityLevel SourceComplexity { get; init; }

    public required decimal ExpectedHours { get; init; }

    public required EffortRange SourceRange { get; init; }

    public string? ParentId { get; init; }

    public string? CorrelationGroup { get; init; }

    public IReadOnlyList<string> ResolvedEvidenceIds { get; init; } = [];

    public IReadOnlyList<string> UnresolvedEvidenceIds { get; init; } = [];

    public IReadOnlyList<string> NodeIds { get; init; } = [];
}

public sealed record CalibrationUncertaintyGraphFeatureSummary
{
    public required int FeatureCount { get; init; }

    public required int NodeCount { get; init; }

    public required int EdgeCount { get; init; }

    public required int CandidateReferenceFactCount { get; init; }

    public required int ResolvedLocalReferenceFactCount { get; init; }

    public required int CyclicNodeCount { get; init; }

    public required int PublicInterfaceAvailableNodeCount { get; init; }

    public required int PublicInterfaceNotApplicableNodeCount { get; init; }

    public required int PublicInterfaceUnavailableNodeCount { get; init; }

    public required int WorkItemCount { get; init; }

    public required int MappedWorkItemCount { get; init; }

    public required int UnmappedWorkItemCount { get; init; }

    public required int ResolvedEvidenceReferenceCount { get; init; }

    public required int UnresolvedEvidenceReferenceCount { get; init; }
}

public sealed record CalibrationUncertaintyGraphFeatureReport
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

    public required CalibrationUncertaintyGraphFeatureSummary Summary { get; init; }

    public IReadOnlyList<CalibrationUncertaintyFeatureValue> Features { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintyGraphNode> Nodes { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintyGraphEdge> Edges { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintyGraphWorkItemMapping> WorkItems { get; init; } = [];
}
