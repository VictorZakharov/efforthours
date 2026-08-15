namespace EffortHours.Contracts.V1;

public enum CalibrationIntervalStatus
{
    Covered,
    CandidateTooHigh,
    CandidateTooLow,
}

public enum CalibrationResidualComponentKind
{
    ReviewedTarget,
    UnmatchedCandidateWorkItem,
}

public enum CalibrationResidualMappingStatus
{
    Matched,
    Incomplete,
    CategoryMismatch,
    UnmatchedCandidate,
}

public sealed record CalibrationSignedEffortRange
{
    public required decimal Low { get; init; }

    public required decimal Expected { get; init; }

    public required decimal High { get; init; }
}

public sealed record CalibrationRangeDiagnostic
{
    public required EffortRange Reviewed { get; init; }

    public required EffortRange Candidate { get; init; }

    public required CalibrationSignedEffortRange SignedError { get; init; }

    public required decimal ExpectedAbsoluteErrorHours { get; init; }

    public required CalibrationIntervalStatus ReviewedExpectedStatus { get; init; }

    public required decimal IntervalMissHours { get; init; }

    public required decimal CandidateWidthHours { get; init; }

    public required decimal ReviewedWidthHours { get; init; }

    public required decimal CandidateLowerDistanceHours { get; init; }

    public required decimal CandidateUpperDistanceHours { get; init; }

    public required decimal CandidateAbsoluteAsymmetryHours { get; init; }

    public required decimal CandidateRelativeAsymmetry { get; init; }

    public required bool CandidateSymmetric { get; init; }
}

public sealed record CalibrationCandidateLeafDiagnostic
{
    public required string Id { get; init; }

    public required EffortCategory Category { get; init; }

    public required string Title { get; init; }

    public required string Scope { get; init; }

    public required ComplexityLevel Complexity { get; init; }

    public required EffortRange Hours { get; init; }

    public required decimal Confidence { get; init; }

    public required int EvidenceCount { get; init; }

    public required string EvidenceDigest { get; init; }

    public required int ReasonIndex { get; init; }

    public required int AssumptionCount { get; init; }

    public required int ExclusionCount { get; init; }

    public required int UncertaintyReasonCount { get; init; }
}

public sealed record CalibrationResidualContribution
{
    public required int Rank { get; init; }

    public required CalibrationResidualComponentKind Kind { get; init; }

    public required CalibrationResidualMappingStatus MappingStatus { get; init; }

    public required string Id { get; init; }

    public string? ReviewedTargetId { get; init; }

    public required EffortCategory Category { get; init; }

    public required string Title { get; init; }

    public required string Scope { get; init; }

    public string? ReviewedRationale { get; init; }

    public string? ReviewedSizeException { get; init; }

    public required int CandidateLeafCount { get; init; }

    public IReadOnlyList<CalibrationCandidateLeafDiagnostic> CandidateLeaves { get; init; } = [];

    public IReadOnlyList<string> MissingCandidateWorkItemIds { get; init; } = [];

    public IReadOnlyList<string> ReviewedEvidenceIds { get; init; } = [];

    public IReadOnlyList<string> CandidateEvidenceIds { get; init; } = [];

    public IReadOnlyList<string> CandidateReasons { get; init; } = [];

    public required CalibrationRangeDiagnostic Range { get; init; }

    public decimal? CandidateConfidence { get; init; }

    public IReadOnlyList<string> ReviewedUncertaintyReasons { get; init; } = [];

    public IReadOnlyList<string> CandidateUncertaintyReasons { get; init; } = [];

    public required decimal AbsoluteErrorShare { get; init; }

    public required decimal CumulativeAbsoluteErrorShare { get; init; }

    public required bool MaterialContributor { get; init; }
}

public sealed record CalibrationCategoryResidual
{
    public required int Rank { get; init; }

    public required EffortCategory Category { get; init; }

    public required CalibrationRangeDiagnostic Range { get; init; }

    public required decimal AbsoluteErrorShare { get; init; }

    public required decimal CumulativeAbsoluteErrorShare { get; init; }

    public required bool MaterialContributor { get; init; }
}

public sealed record CalibrationRepositoryDiagnostic
{
    public required int Rank { get; init; }

    public required string RecordId { get; init; }

    public required string RepositoryId { get; init; }

    public required string SourceDigest { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public required string CandidateEstimatorVersion { get; init; }

    public required string CandidateEstimateDigest { get; init; }

    public required CalibrationRangeDiagnostic Range { get; init; }

    public required int TargetCount { get; init; }

    public required int CandidateWorkItemCount { get; init; }

    public required int MatchedTargetCount { get; init; }

    public required int IncompleteTargetCount { get; init; }

    public required int CategoryMismatchTargetCount { get; init; }

    public required int UnmatchedCandidateWorkItemCount { get; init; }

    public required int ReviewedSizeExceptionTargetCount { get; init; }

    public required int OversizedReviewedTargetCount { get; init; }

    public required decimal GrossCategoryExpectedErrorHours { get; init; }

    public required decimal CategoryCancellationHours { get; init; }

    public required decimal GrossComponentExpectedErrorHours { get; init; }

    public required decimal ComponentCancellationHours { get; init; }

    public required int MaterialCategoryCount { get; init; }

    public required int MaterialComponentCount { get; init; }

    public required CalibrationSignedEffortRange CategoryReconciliationDelta { get; init; }

    public required CalibrationSignedEffortRange ComponentReconciliationDelta { get; init; }

    public IReadOnlyList<CalibrationCategoryResidual> Categories { get; init; } = [];

    public IReadOnlyList<CalibrationResidualContribution> Components { get; init; } = [];
}

public sealed record CalibrationDiagnosticSummary
{
    public required CalibrationRangeDiagnostic Range { get; init; }

    public required decimal GrossRepositoryExpectedErrorHours { get; init; }

    public required decimal RepositoryCancellationHours { get; init; }

    public required decimal GrossCategoryExpectedErrorHours { get; init; }

    public required decimal GrossComponentExpectedErrorHours { get; init; }

    public required int ComponentCount { get; init; }

    public required int MaterialComponentCount { get; init; }

    public required int ReviewedSizeExceptionTargetCount { get; init; }

    public required int OversizedReviewedTargetCount { get; init; }

    public required int ReviewedExpectedCoveredRepositoryCount { get; init; }

    public required int CandidateTooHighRepositoryCount { get; init; }

    public required int CandidateTooLowRepositoryCount { get; init; }

    public required int CandidateSymmetricRepositoryCount { get; init; }

    public required int RawRepositoryWidthCorrelationSampleCount { get; init; }

    public decimal? RawRepositoryWidthCorrelation { get; init; }

    public required int NormalizedRepositoryWidthCorrelationSampleCount { get; init; }

    public decimal? NormalizedRepositoryWidthCorrelation { get; init; }

    public required CalibrationSignedEffortRange RepositoryReconciliationDelta { get; init; }
}

public sealed record CalibrationDiagnosticReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string DiagnosticVersion { get; init; }

    public required string CorpusId { get; init; }

    public required string CorpusVersion { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required int RecordCount { get; init; }

    public required int RepositoryCount { get; init; }

    public required int IgnoredCandidateCount { get; init; }

    public IReadOnlyList<string> CandidateEstimatorVersions { get; init; } = [];

    public required decimal MaterialContributionThreshold { get; init; }

    public required CalibrationDiagnosticSummary Summary { get; init; }

    public IReadOnlyList<CalibrationRepositoryDiagnostic> Repositories { get; init; } = [];
}
