namespace Fairbill.Contracts.V1;

public sealed record EstimateViewCounts
{
    public required int RepresentedWorkItems { get; init; }

    public required int CapabilityGroups { get; init; }

    public required int Scopes { get; init; }

    public required int ProfessionalizationGapItems { get; init; }
}

public sealed record CategoryViewEntry
{
    public required EffortCategory Category { get; init; }

    public required EffortRange Hours { get; init; }

    public CostRange? Cost { get; init; }

    public required int WorkItemCount { get; init; }

    public required int CapabilityCount { get; init; }

    public required decimal Confidence { get; init; }
}

public sealed record ScopeViewEntry
{
    public required string Scope { get; init; }

    public required EffortRange Hours { get; init; }

    public CostRange? Cost { get; init; }

    public required int WorkItemCount { get; init; }

    public required int CapabilityCount { get; init; }

    public required decimal Confidence { get; init; }
}

public sealed record CapabilityViewEntry
{
    public required string Id { get; init; }

    public required EffortCategory Category { get; init; }

    public required string Title { get; init; }

    public required string Scope { get; init; }

    public required EffortRange Hours { get; init; }

    public CostRange? Cost { get; init; }

    public required decimal Confidence { get; init; }

    public required int WorkItemCount { get; init; }

    public required int EvidenceCount { get; init; }

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];

    public IReadOnlyList<string> ReviewReasons { get; init; } = [];
}

public sealed record ProjectionOmissions
{
    public required int ScopeCount { get; init; }

    public required decimal ScopeExpectedHours { get; init; }

    public required int CapabilityCount { get; init; }

    public required decimal CapabilityExpectedHours { get; init; }
}

public sealed record EstimateViewReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public string SourceEstimateSchemaVersion { get; init; } = ContractVersions.V1;

    public required EstimateViewKind View { get; init; }

    public required string EstimatorVersion { get; init; }

    public required RepositoryDescriptor Repository { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required EstimationBaseline Baseline { get; init; }

    public required EffortRange TotalEffort { get; init; }

    public RateCard? RateCard { get; init; }

    public CostRange? TotalCost { get; init; }

    public required EstimateViewCounts Counts { get; init; }

    public IReadOnlyList<CategoryViewEntry> Categories { get; init; } = [];

    public IReadOnlyList<ScopeViewEntry> Scopes { get; init; } = [];

    public IReadOnlyList<CapabilityViewEntry> Capabilities { get; init; } = [];

    public IReadOnlyList<CapabilityViewEntry> ReviewQueue { get; init; } = [];

    public IReadOnlyList<CapabilityViewEntry> ProfessionalizationGap { get; init; } = [];

    public required ProjectionOmissions Omissions { get; init; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    public IReadOnlyList<string> Assumptions { get; init; } = [];

    public required VerificationSummary Verification { get; init; }

    public string ProfessionalizationGapTreatment { get; init; } =
        "Excluded from represented EHE and replacement cost.";
}

public sealed record EstimateExplanation
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public string EvidenceSchemaVersion { get; init; } = ContractVersions.V1;

    public required string EstimatorVersion { get; init; }

    public required RepositoryDescriptor Repository { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string RequestedId { get; init; }

    public required ExplanationMatchKind MatchKind { get; init; }

    public required CapabilityViewEntry Capability { get; init; }

    public IReadOnlyList<WorkItem> WorkItems { get; init; } = [];

    public IReadOnlyList<EvidenceFact> EvidenceFacts { get; init; } = [];

    public IReadOnlyList<string> MissingEvidenceIds { get; init; } = [];

    public IReadOnlyList<EstimatorReference> Estimators { get; init; } = [];

    public IReadOnlyList<string> Assumptions { get; init; } = [];

    public IReadOnlyList<string> Exclusions { get; init; } = [];

    public IReadOnlyList<string> CorrelationGroups { get; init; } = [];

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    public required VerificationSummary Verification { get; init; }
}
