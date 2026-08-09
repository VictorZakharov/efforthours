namespace EffortHours.Contracts.V1;

public static class HostReviewProtocol
{
    public const int MaximumPacketCapabilities = 12;
    public const int MaximumPacketEvidenceFacts = 48;
    public const int MaximumQueryEvidenceFacts = 48;
    public const int MaximumScopeCapabilities = 20;
    public const int MaximumSourceLines = 200;
    public const int MaximumSourceCharacters = 65_536;
    public const int MaximumSourceBytes = 1_048_576;
}

public sealed record HostReviewModelIdentity
{
    public required bool IsAvailable { get; init; }

    public string? Provider { get; init; }

    public string? Model { get; init; }

    public string? Version { get; init; }
}

public sealed record HostReviewStructuralLimits
{
    public required int PacketCapabilities { get; init; }

    public required int PacketEvidenceFacts { get; init; }

    public required int QueryEvidenceFacts { get; init; }

    public required int ScopeCapabilities { get; init; }

    public required int SourceLines { get; init; }

    public required int SourceCharacters { get; init; }

    public required int SourceBytes { get; init; }
}

public sealed record HostReviewCandidate
{
    public required CapabilityViewEntry Capability { get; init; }

    public IReadOnlyList<string> EstimationReasons { get; init; } = [];

    public IReadOnlyList<string> EvidenceIds { get; init; } = [];

    public IReadOnlyList<EstimatorReference> Estimators { get; init; } = [];

    public IReadOnlyList<string> Assumptions { get; init; } = [];

    public IReadOnlyList<string> Exclusions { get; init; } = [];

    public IReadOnlyList<string> CorrelationGroups { get; init; } = [];

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];
}

public sealed record HostReviewPacketOmissions
{
    public required int CapabilityCount { get; init; }

    public required decimal CapabilityExpectedHours { get; init; }

    public required int EvidenceFactCount { get; init; }

    public required int MissingEvidenceFactCount { get; init; }
}

public sealed record HostReviewPacket
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public string ProtocolVersion { get; init; } = HostReviewProtocolVersions.V1;

    public required string InputDigest { get; init; }

    public required string EstimatorVersion { get; init; }

    public required RepositoryDescriptor Repository { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required EstimationBaseline Baseline { get; init; }

    public required EffortRange TotalEffort { get; init; }

    public IReadOnlyList<CategoryViewEntry> Categories { get; init; } = [];

    public IReadOnlyList<EstimatorReference> Estimators { get; init; } = [];

    public IReadOnlyList<HostReviewCandidate> Candidates { get; init; } = [];

    public IReadOnlyList<EvidenceFact> EvidenceFacts { get; init; } = [];

    public IReadOnlyList<string> MissingEvidenceIds { get; init; } = [];

    public required HostReviewPacketOmissions Omissions { get; init; }

    public required HostReviewModelIdentity ReviewerModel { get; init; }

    public IReadOnlyList<HostReviewQueryKind> AvailableQueries { get; init; } = [];

    public required HostReviewStructuralLimits StructuralLimits { get; init; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    public IReadOnlyList<string> Assumptions { get; init; } = [];

    public required VerificationSummary Verification { get; init; }

    public required string ReviewObjective { get; init; }

    public required bool LocalBaselineComplete { get; init; }

    public required bool PricingIncluded { get; init; }

    public IReadOnlyList<string> CallerResponsibilities { get; init; } = [];

    public required string StatusDisclaimer { get; init; }
}

public sealed record HostReviewQuery
{
    public required HostReviewQueryKind Kind { get; init; }

    public required string Selector { get; init; }

    public required string Reason { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string InputDigest { get; init; }

    public int? Offset { get; init; }

    public int? Limit { get; init; }

    public int? StartLine { get; init; }

    public int? LineCount { get; init; }
}

public sealed record HostReviewSourceLine
{
    public required int Line { get; init; }

    public required string Text { get; init; }

    public required bool Truncated { get; init; }
}

public sealed record HostReviewSourceExcerpt
{
    public required string Path { get; init; }

    public required string EvidenceId { get; init; }

    public required string FileDigest { get; init; }

    public required int TotalLines { get; init; }

    public required int RequestedStartLine { get; init; }

    public required int RequestedLineCount { get; init; }

    public IReadOnlyList<HostReviewSourceLine> Lines { get; init; } = [];

    public required bool ContentTruncated { get; init; }
}

public sealed record HostReviewQueryOmissions
{
    public required int CapabilityCount { get; init; }

    public required int EvidenceFactCount { get; init; }

    public required int SourceLineCount { get; init; }

    public required int SourceCharacterCount { get; init; }
}

public sealed record HostReviewQueryResult
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public string ProtocolVersion { get; init; } = HostReviewProtocolVersions.V1;

    public required string InputDigest { get; init; }

    public required RepositoryDescriptor Repository { get; init; }

    public required HostReviewQuery Query { get; init; }

    public IReadOnlyList<HostReviewCandidate> Capabilities { get; init; } = [];

    public IReadOnlyList<EvidenceFact> EvidenceFacts { get; init; } = [];

    public HostReviewSourceExcerpt? SourceExcerpt { get; init; }

    public required HostReviewQueryOmissions Omissions { get; init; }

    public required bool ContainsSourceExcerpt { get; init; }

    public required string DisclosureNotice { get; init; }
}

public sealed record HostReviewReplacement
{
    public required EffortCategory Category { get; init; }

    public required EffortRange Hours { get; init; }

    public required decimal Confidence { get; init; }

    public IReadOnlyList<string> Assumptions { get; init; } = [];

    public IReadOnlyList<string> Exclusions { get; init; } = [];

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];
}

public sealed record HostReviewAdjustment
{
    public required string TargetId { get; init; }

    public required HostReviewDecision Decision { get; init; }

    public required EffortRange OriginalHours { get; init; }

    public HostReviewReplacement? Replacement { get; init; }

    public IReadOnlyList<string> EvidenceIds { get; init; } = [];

    public required string Reason { get; init; }
}

public sealed record HostReviewAdjustmentLedger
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public string ProtocolVersion { get; init; } = HostReviewProtocolVersions.V1;

    public required string InputDigest { get; init; }

    public required HostReviewModelIdentity ReviewerModel { get; init; }

    public IReadOnlyList<HostReviewAdjustment> Adjustments { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed record HostReviewValidationReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public string ProtocolVersion { get; init; } = HostReviewProtocolVersions.V1;

    public required string InputDigest { get; init; }

    public required bool IsValid { get; init; }

    public required int AdjustmentCount { get; init; }

    public required bool AdjustmentsApplied { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}
