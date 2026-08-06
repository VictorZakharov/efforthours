namespace Fairbill.Contracts.V1;

public sealed record CalibrationRubricReference
{
    public required string Id { get; init; }

    public required string Version { get; init; }
}

public sealed record CalibrationRepositoryReference
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string SourceDigest { get; init; }
}

public sealed record CalibrationSourceProvenance
{
    public required CalibrationDataClassification DataClassification { get; init; }

    public required string SourceReference { get; init; }

    public required string Revision { get; init; }

    public required string LicenseExpression { get; init; }

    public required bool RedistributionAllowed { get; init; }

    public string? Notes { get; init; }
}

public sealed record CalibrationReviewer
{
    public required string Id { get; init; }

    public required CalibrationReviewerKind Kind { get; init; }

    public required CalibrationReviewerRole Role { get; init; }

    public string? ModelId { get; init; }

    public string? ModelVersion { get; init; }
}

public sealed record CalibrationReviewProvenance
{
    public required CalibrationReviewStatus Status { get; init; }

    public required DateOnly CompletedOn { get; init; }

    public IReadOnlyList<CalibrationReviewer> Reviewers { get; init; } = [];

    public string? Notes { get; init; }
}

public sealed record CalibrationTarget
{
    public required string Id { get; init; }

    public required EffortCategory Category { get; init; }

    public required string Title { get; init; }

    public required string Scope { get; init; }

    public IReadOnlyList<string> SourceWorkItemIds { get; init; } = [];

    public IReadOnlyList<string> EvidenceIds { get; init; } = [];

    public required EffortRange Hours { get; init; }

    public required string Rationale { get; init; }

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];

    public string? SizeException { get; init; }
}

public sealed record CalibrationRecord
{
    public required string Id { get; init; }

    public required CalibrationRepositoryReference Repository { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required string SourceEstimatorVersion { get; init; }

    public required string SourceEstimateDigest { get; init; }

    public required CalibrationSourceProvenance Source { get; init; }

    public required CalibrationReviewProvenance Review { get; init; }

    public IReadOnlyList<CalibrationTarget> Targets { get; init; } = [];
}

public sealed record CalibrationCorpus
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Description { get; init; }

    public required CalibrationRubricReference Rubric { get; init; }

    public IReadOnlyList<CalibrationRecord> Records { get; init; } = [];
}

public sealed record CalibrationPartitionSummary
{
    public required CalibrationPartition Partition { get; init; }

    public required int RecordCount { get; init; }

    public required int RepositoryCount { get; init; }
}

public sealed record CalibrationValidationSummary
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string CorpusId { get; init; }

    public required string CorpusVersion { get; init; }

    public required bool Valid { get; init; }

    public required int RecordCount { get; init; }

    public required int RepositoryCount { get; init; }

    public IReadOnlyList<CalibrationPartitionSummary> Partitions { get; init; } = [];
}

public sealed record CalibrationPointMetrics
{
    public required int SampleCount { get; init; }

    public required decimal ReviewedHours { get; init; }

    public required decimal CandidateHours { get; init; }

    public required decimal MeanAbsoluteErrorHours { get; init; }

    public required decimal MedianAbsoluteErrorHours { get; init; }

    public required decimal RootMeanSquaredErrorHours { get; init; }

    public required decimal MeanSignedErrorHours { get; init; }

    public decimal? WeightedAbsolutePercentageError { get; init; }

    public decimal? AggregateBiasRate { get; init; }
}

public sealed record CalibrationIntervalMetrics
{
    public required int SampleCount { get; init; }

    public required int ReviewedExpectedCoveredCount { get; init; }

    public decimal? ReviewedExpectedCoverage { get; init; }

    public required int ReviewedRangeFullyCoveredCount { get; init; }

    public decimal? ReviewedRangeFullyCoveredRate { get; init; }

    public required decimal MeanCandidateWidthHours { get; init; }

    public required decimal MeanReviewedWidthHours { get; init; }
}

public sealed record CalibrationRangeMetrics
{
    public required CalibrationPointMetrics Low { get; init; }

    public required CalibrationPointMetrics Expected { get; init; }

    public required CalibrationPointMetrics High { get; init; }

    public required CalibrationIntervalMetrics Interval { get; init; }
}

public sealed record CalibrationCategoryMetrics
{
    public required EffortCategory Category { get; init; }

    public required CalibrationRangeMetrics Metrics { get; init; }
}

public sealed record CalibrationMatchSummary
{
    public required int TargetCount { get; init; }

    public required int MatchedTargetCount { get; init; }

    public decimal? TargetMatchRate { get; init; }

    public required int SourceWorkItemReferenceCount { get; init; }

    public required int MatchedSourceWorkItemReferenceCount { get; init; }

    public decimal? SourceWorkItemReferenceMatchRate { get; init; }

    public required int CandidateWorkItemCount { get; init; }

    public required int MatchedCandidateWorkItemCount { get; init; }

    public decimal? CandidateWorkItemMatchRate { get; init; }
}

public sealed record CalibrationRepositoryEvaluation
{
    public required string RecordId { get; init; }

    public required string RepositoryId { get; init; }

    public required string SourceDigest { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public required string CandidateEstimatorVersion { get; init; }

    public required string CandidateEstimateDigest { get; init; }

    public required EffortRange ReviewedTotal { get; init; }

    public required EffortRange CandidateTotal { get; init; }

    public required decimal ExpectedAbsoluteErrorHours { get; init; }

    public required decimal ExpectedSignedErrorHours { get; init; }

    public required bool ReviewedExpectedCovered { get; init; }

    public required bool ReviewedRangeFullyCovered { get; init; }

    public required int TargetCount { get; init; }

    public required int MatchedTargetCount { get; init; }

    public required int CandidateWorkItemCount { get; init; }

    public required int MatchedCandidateWorkItemCount { get; init; }

    public IReadOnlyList<string> UnmatchedTargetIds { get; init; } = [];

    public IReadOnlyList<string> UnmatchedCandidateWorkItemIds { get; init; } = [];

    public IReadOnlyList<string> CategoryMismatchTargetIds { get; init; } = [];
}

public sealed record CalibrationEvaluationReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string EvaluatorVersion { get; init; }

    public required string MetricVersion { get; init; }

    public required string CorpusId { get; init; }

    public required string CorpusVersion { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required int RecordCount { get; init; }

    public required int RepositoryCount { get; init; }

    public required int IgnoredCandidateCount { get; init; }

    public IReadOnlyList<string> CandidateEstimatorVersions { get; init; } = [];

    public required CalibrationRangeMetrics RepositoryTotals { get; init; }

    public IReadOnlyList<CalibrationCategoryMetrics> Categories { get; init; } = [];

    public required CalibrationRangeMetrics WorkItems { get; init; }

    public required CalibrationMatchSummary Match { get; init; }

    public IReadOnlyList<CalibrationRepositoryEvaluation> Repositories { get; init; } = [];
}
