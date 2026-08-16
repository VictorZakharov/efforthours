namespace EffortHours.Contracts.V1;

public sealed record ManualQaReviewPolicy
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string PolicyVersion { get; init; }

    public required string Id { get; init; }

    public required string AuthoringVersion { get; init; }

    public required string LicenseExpression { get; init; }

    public required string Maturity { get; init; }

    public required CalibrationRubricReference Rubric { get; init; }

    public required CalibrationCorpusReference SourceCorpus { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public required CalibrationCandidateVisibility CandidateVisibility { get; init; }

    public IReadOnlyList<EffortCategory> EligibleCategories { get; init; } = [];

    public required int ExpectedRecordCount { get; init; }

    public required int ExpectedTargetCount { get; init; }

    public IReadOnlyList<string> HiddenInputs { get; init; } = [];

    public IReadOnlyList<string> RequiredReviewPractices { get; init; } = [];

    public IReadOnlyList<string> Limitations { get; init; } = [];
}

public sealed record ManualQaReviewPolicyReference
{
    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Digest { get; init; }
}

public sealed record ManualQaReviewSourceReference
{
    public required CalibrationDataClassification DataClassification { get; init; }

    public required string SourceReference { get; init; }

    public required string Revision { get; init; }

    public required string LicenseExpression { get; init; }

    public required bool RedistributionAllowed { get; init; }
}

public sealed record ManualQaReviewTarget
{
    public required string SourceTargetId { get; init; }

    public required string SourceLineageDigest { get; init; }

    public required EffortCategory SourceCategory { get; init; }

    public required string Title { get; init; }

    public required string Scope { get; init; }

    public required string OverlapGroupId { get; init; }

    public IReadOnlyList<string> EvidenceIds { get; init; } = [];
}

public sealed record ManualQaReviewPacket
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string AuthoringVersion { get; init; }

    public required CalibrationAuthoringStatus Status { get; init; }

    public required string Warning { get; init; }

    public required ManualQaReviewPolicyReference Policy { get; init; }

    public required CalibrationCorpusReference SourceCorpus { get; init; }

    public required CalibrationRubricReference Rubric { get; init; }

    public required CalibrationCandidateVisibility CandidateVisibility { get; init; }

    public required string SourceRecordId { get; init; }

    public required CalibrationRepositoryReference Repository { get; init; }

    public required ManualQaReviewSourceReference Source { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public IReadOnlyList<ManualQaReviewTarget> Targets { get; init; } = [];

    public IReadOnlyList<string> Instructions { get; init; } = [];
}

public sealed record ManualQaReviewManifestPacket
{
    public required string SourceRecordId { get; init; }

    public required string RepositoryId { get; init; }

    public required string RepositorySourceDigest { get; init; }

    public required string FileName { get; init; }

    public required string PacketDigest { get; init; }

    public required string LineageDigest { get; init; }

    public required int TargetCount { get; init; }
}

public sealed record ManualQaReviewManifest
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string ManifestVersion { get; init; }

    public required string AuthoringVersion { get; init; }

    public required ManualQaReviewPolicyReference Policy { get; init; }

    public required CalibrationCorpusReference SourceCorpus { get; init; }

    public required CalibrationRubricReference Rubric { get; init; }

    public required CalibrationCandidateVisibility CandidateVisibility { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required int RecordCount { get; init; }

    public required int TargetCount { get; init; }

    public IReadOnlyList<ManualQaReviewManifestPacket> Packets { get; init; } = [];

    public IReadOnlyList<string> Instructions { get; init; } = [];
}
