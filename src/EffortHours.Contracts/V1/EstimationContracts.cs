namespace EffortHours.Contracts.V1;

public sealed record EffortRange
{
    public required decimal Low { get; init; }

    public required decimal Expected { get; init; }

    public required decimal High { get; init; }
}

public sealed record CostRange
{
    public required decimal Low { get; init; }

    public required decimal Expected { get; init; }

    public required decimal High { get; init; }

    public required string Currency { get; init; }
}

public sealed record EstimatorReference
{
    public required string Id { get; init; }

    public required string Version { get; init; }

    public required EstimatorKind Kind { get; init; }
}

public sealed record WorkItem
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string Id { get; init; }

    public required EffortCategory Category { get; init; }

    public required string Title { get; init; }

    public required string Scope { get; init; }

    public IReadOnlyList<string> EvidenceIds { get; init; } = [];

    public decimal Quantity { get; init; } = 1m;

    public required ComplexityLevel Complexity { get; init; }

    public required EffortRange Hours { get; init; }

    public required decimal Confidence { get; init; }

    public required string Reason { get; init; }

    public required EstimatorReference Estimator { get; init; }

    public IReadOnlyList<EstimationProfile> Profiles { get; init; } = [];

    public string? ParentId { get; init; }

    public IReadOnlyList<string> DependencyIds { get; init; } = [];

    public string? CorrelationGroup { get; init; }

    public IReadOnlyList<string> Assumptions { get; init; } = [];

    public IReadOnlyList<string> Exclusions { get; init; } = [];

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];
}

public sealed record EstimationBaseline
{
    public required string Id { get; init; }

    public required string WorkerProfile { get; init; }

    public required int TechnologyBaselineYear { get; init; }

    public required bool BusinessDomainFamiliar { get; init; }

    public required bool UsesAi { get; init; }

    public required string Description { get; init; }
}

public sealed record CategoryEstimate
{
    public required EffortCategory Category { get; init; }

    public required EffortRange Hours { get; init; }
}

public sealed record VerificationSummary
{
    public required VerificationMode Mode { get; init; }

    public required WorkingState WorkingState { get; init; }

    public required bool TestsAssumedPassing { get; init; }

    public string? Note { get; init; }
}

public sealed record EstimateReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public string EvidenceSchemaVersion { get; init; } = ContractVersions.V1;

    public required string EstimatorVersion { get; init; }

    public required RepositoryDescriptor Repository { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required EstimationBaseline Baseline { get; init; }

    public required EffortRange TotalEffort { get; init; }

    public RateCard? RateCard { get; init; }

    public CostRange? TotalCost { get; init; }

    public IReadOnlyList<CategoryEstimate> Categories { get; init; } = [];

    public IReadOnlyList<WorkItem> WorkItems { get; init; } = [];

    public IReadOnlyList<WorkItem> ProfessionalizationGap { get; init; } = [];

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    public IReadOnlyList<string> Assumptions { get; init; } = [];

    public required VerificationSummary Verification { get; init; }
}
