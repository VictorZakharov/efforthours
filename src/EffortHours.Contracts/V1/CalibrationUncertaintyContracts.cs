namespace EffortHours.Contracts.V1;

public enum CalibrationUncertaintyFeatureStage
{
    AvailableOffline,
    DeferredEvidence,
}

public enum CalibrationUncertaintyFeatureValueKind
{
    Count,
    Ratio,
    Rate,
    Ordinal,
    Distribution,
}

public enum CalibrationUncertaintyFeatureMonotonicity
{
    DiagnosticOnly,
    HigherMustNotNarrow,
    LowerMustNotNarrow,
    HigherMustWiden,
}

public enum CalibrationUncertaintyFeatureAvailability
{
    Available,
    NotApplicable,
    Unavailable,
}

public enum CalibrationUncertaintyCoverageMetric
{
    ReviewedExpectedPoint,
}

public sealed record CalibrationUncertaintyIntervalPolicy
{
    public required string Version { get; init; }

    public required CalibrationUncertaintyCoverageMetric IntendedCoverageMetric { get; init; }

    public required decimal IntendedCoverageTarget { get; init; }

    public required bool FormalProbabilityInterval { get; init; }

    public required bool SymmetricAroundExpected { get; init; }

    public required bool ZeroHourFloor { get; init; }

    public required bool DirectionalContingenciesSeparate { get; init; }

    public required bool MaterialUnresolvedFactsMustWiden { get; init; }

    public required bool ComparableWeakerEvidenceMustNotNarrow { get; init; }

    public required bool MissingValuesWidenAutomatically { get; init; }
}

public sealed record CalibrationUncertaintyFeatureDefinition
{
    public required string Id { get; init; }

    public required CalibrationUncertaintyFeatureStage Stage { get; init; }

    public required CalibrationUncertaintyFeatureValueKind ValueKind { get; init; }

    public required CalibrationUncertaintyFeatureMonotonicity Monotonicity { get; init; }

    public required string OfflineSource { get; init; }

    public required string Description { get; init; }
}

public sealed record CalibrationUncertaintyFeatureContract
{
    public required string Version { get; init; }

    public required string EffectiveDate { get; init; }

    public required bool LabelIndependent { get; init; }

    public required CalibrationUncertaintyIntervalPolicy IntervalPolicy { get; init; }

    public IReadOnlyList<CalibrationUncertaintyFeatureDefinition> Features { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintyFeatureDefinition> DeferredCandidates { get; init; } = [];
}

public sealed record CalibrationUncertaintyFeatureValue
{
    public required string FeatureId { get; init; }

    public required CalibrationUncertaintyFeatureAvailability Availability { get; init; }

    public decimal? Value { get; init; }

    public string? ReasonCode { get; init; }

    public IReadOnlyList<string> EvidenceIds { get; init; } = [];
}

public sealed record CalibrationUncertaintyWorkItemFeatures
{
    public required string WorkItemId { get; init; }

    public required EffortCategory Category { get; init; }

    public required ComplexityLevel SourceComplexity { get; init; }

    public IReadOnlyList<string> Ecosystems { get; init; } = [];

    public required decimal ExpectedHours { get; init; }

    public required EffortRange SourceRange { get; init; }

    public required bool SourceRangePolicyCompliant { get; init; }

    public string? ParentId { get; init; }

    public string? CorrelationGroup { get; init; }

    public IReadOnlyList<string> ResolvedEvidenceIds { get; init; } = [];

    public IReadOnlyList<string> UnresolvedEvidenceIds { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintyFeatureValue> Features { get; init; } = [];
}

public sealed record CalibrationUncertaintyFeatureSummary
{
    public required int WorkItemCount { get; init; }

    public required int FeatureCount { get; init; }

    public required int WidthDriverFeatureCount { get; init; }

    public required int DeferredCandidateCount { get; init; }

    public required int ResolvedEvidenceReferenceCount { get; init; }

    public required int UnresolvedEvidenceReferenceCount { get; init; }

    public required int WidthDriverUnavailableWorkItemCount { get; init; }

    public required int MaterialAccessGapWorkItemCount { get; init; }

    public required int NonMaterialOfflineLimitationWorkItemCount { get; init; }

    public required int SourcePolicyCompliantWorkItemCount { get; init; }
}

public sealed record CalibrationUncertaintyFeatureReport
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

    public required CalibrationUncertaintyFeatureSummary Summary { get; init; }

    public IReadOnlyList<CalibrationUncertaintyWorkItemFeatures> WorkItems { get; init; } = [];
}
