using System.Text.Json.Serialization;

namespace EffortHours.Contracts.V1;

public sealed record ManualQaReviewManifestReference
{
    public required string Version { get; init; }

    public required string Digest { get; init; }
}

public sealed record ManualQaDecisionOutputCorpus
{
    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Description { get; init; }

    public required CalibrationRubricReference Rubric { get; init; }
}

public sealed record ManualQaDecisionCompilerPolicy
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string PolicyVersion { get; init; }

    public required string Id { get; init; }

    public required string CompilerVersion { get; init; }

    public required string AuthoringVersion { get; init; }

    public required string LicenseExpression { get; init; }

    public required string Maturity { get; init; }

    public required string PlanId { get; init; }

    public required string PlanVersion { get; init; }

    public required string PlanDescription { get; init; }

    public required CalibrationCorpusReference SourceCorpus { get; init; }

    public required ManualQaReviewPolicyReference ReviewPolicy { get; init; }

    public required ManualQaReviewManifestReference ReviewManifest { get; init; }

    public required CalibrationRubricReference ReviewRubric { get; init; }

    public required ManualQaDecisionOutputCorpus OutputCorpus { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public required EffortCategory ReplacedCategory { get; init; }

    public required string SourceWorkItemLineageVersion { get; init; }

    public required int ExpectedRecordCount { get; init; }

    public required int ExpectedDecisionCount { get; init; }

    public required int ExpectedRemovedTargetCount { get; init; }

    public required int ExpectedPreservedTargetCount { get; init; }

    public required int ExpectedOutputTargetCount { get; init; }

    public IReadOnlyList<string> RequiredDecisionPractices { get; init; } = [];

    public IReadOnlyList<string> Limitations { get; init; } = [];
}

public sealed record ManualQaDecision
{
    public required string SourceTargetId { get; init; }

    public required string SourceLineageDigest { get; init; }

    public required string OverlapGroupId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public ManualQaDecisionDisposition? Disposition { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public EffortRange? Hours { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Rationale { get; init; }

    public IReadOnlyList<string> EvidenceIds { get; init; } = [];

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? OverlapAllocation { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? DuplicateOfSourceTargetId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? SizeException { get; init; }
}

public sealed record ManualQaDecisionPlanRecord
{
    public required string SourceRecordId { get; init; }

    public required string PacketDigest { get; init; }

    public required string LineageDigest { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public CalibrationReviewProvenance? Review { get; init; }

    public IReadOnlyList<ManualQaDecision> Decisions { get; init; } = [];
}

public sealed record ManualQaDecisionPlan
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string PlanVersion { get; init; }

    public required string CompilerVersion { get; init; }

    public required ManualQaDecisionPlanStatus Status { get; init; }

    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Description { get; init; }

    public required ManualQaReviewPolicyReference Policy { get; init; }

    public required CalibrationCorpusReference SourceCorpus { get; init; }

    public required ManualQaReviewManifestReference ReviewManifest { get; init; }

    public required CalibrationRubricReference ReviewRubric { get; init; }

    public required ManualQaDecisionOutputCorpus OutputCorpus { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public required int RecordCount { get; init; }

    public required int DecisionCount { get; init; }

    public IReadOnlyList<ManualQaDecisionPlanRecord> Records { get; init; } = [];

    public IReadOnlyList<string> Instructions { get; init; } = [];
}
