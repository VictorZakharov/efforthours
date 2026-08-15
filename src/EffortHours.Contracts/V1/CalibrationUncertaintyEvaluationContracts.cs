namespace EffortHours.Contracts.V1;

public enum CalibrationUncertaintySliceDimension
{
    Category,
    Ecosystem,
    ExpectedSizeBand,
}

public sealed record CalibrationUncertaintyEvaluationProtocol
{
    public required string Version { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required string FoldUnit { get; init; }

    public required string TargetMetric { get; init; }

    public required string Normalization { get; init; }

    public required decimal NormalizationFloorHours { get; init; }

    public required decimal IntendedCoverageTarget { get; init; }

    public required string QuantileMethod { get; init; }

    public required int MinimumBucketObservationCount { get; init; }

    public required int MinimumBucketRepositoryCount { get; init; }

    public required bool RepositoryIsolated { get; init; }

    public required bool DevelopmentOnly { get; init; }

    public required bool FitsProductionModel { get; init; }

    public required bool FormalProbabilityInterval { get; init; }
}

public sealed record CalibrationUncertaintyIntervalPerformance
{
    public required int ObservationCount { get; init; }

    public required int ReviewedExpectedCoveredCount { get; init; }

    public decimal? ReviewedExpectedCoverage { get; init; }

    public required decimal MeanWidthHours { get; init; }

    public required decimal MeanNormalizedWidth { get; init; }

    public required decimal MeanAbsoluteResidualHours { get; init; }

    public required decimal MeanIntervalMissHours { get; init; }
}

public sealed record CalibrationUncertaintyFeatureAvailabilitySummary
{
    public required int ObservationCount { get; init; }

    public required int AvailableCount { get; init; }

    public required int NotApplicableCount { get; init; }

    public required int UnavailableCount { get; init; }

    public required int AvailableRepositoryCount { get; init; }
}

public sealed record CalibrationUncertaintyFeatureBucketEvaluation
{
    public required string Id { get; init; }

    public required int Order { get; init; }

    public required int ObservationCount { get; init; }

    public required int RepositoryCount { get; init; }

    public required decimal MinimumValue { get; init; }

    public required decimal MaximumValue { get; init; }

    public required decimal MeanValue { get; init; }

    public required decimal MeanNormalizedAbsoluteResidual { get; init; }

    public required decimal Empirical80thPercentileNormalizedResidual { get; init; }

    public required CalibrationUncertaintyIntervalPerformance CurrentIntervals { get; init; }
}

public sealed record CalibrationUncertaintyFeatureRepositoryFold
{
    public required string RecordId { get; init; }

    public required string RepositoryId { get; init; }

    public required int ConditionedPredictionCount { get; init; }

    public required int BaselineFallbackCount { get; init; }

    public required CalibrationUncertaintyIntervalPerformance Intervals { get; init; }
}

public sealed record CalibrationUncertaintyFeatureEvaluation
{
    public required string FeatureId { get; init; }

    public required CalibrationUncertaintyFeatureValueKind ValueKind { get; init; }

    public required CalibrationUncertaintyFeatureMonotonicity Monotonicity { get; init; }

    public required bool WidthDriver { get; init; }

    public required CalibrationUncertaintyFeatureAvailabilitySummary Availability { get; init; }

    public decimal? ExpectedHoursSpearman { get; init; }

    public decimal? AbsoluteResidualSpearman { get; init; }

    public decimal? NormalizedAbsoluteResidualSpearman { get; init; }

    public required CalibrationUncertaintyIntervalPerformance CrossValidatedIntervals { get; init; }

    public decimal? CoverageDeltaFromBaseline { get; init; }

    public required decimal MeanNormalizedWidthDeltaFromBaseline { get; init; }

    public required decimal MeanIntervalMissDeltaFromBaseline { get; init; }

    public required int ConditionedPredictionCount { get; init; }

    public required int BaselineFallbackCount { get; init; }

    public required int MonotonicBucketViolationCount { get; init; }

    public IReadOnlyList<CalibrationUncertaintyFeatureBucketEvaluation> Buckets { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintyFeatureRepositoryFold> RepositoryFolds { get; init; } = [];
}

public sealed record CalibrationUncertaintySliceEvaluation
{
    public required CalibrationUncertaintySliceDimension Dimension { get; init; }

    public required string Value { get; init; }

    public required int ObservationCount { get; init; }

    public required int RepositoryCount { get; init; }

    public required CalibrationUncertaintyIntervalPerformance CurrentIntervals { get; init; }

    public required CalibrationUncertaintyIntervalPerformance CrossValidatedBaseline { get; init; }
}

public sealed record CalibrationUncertaintyTargetEvaluation
{
    public required string RecordId { get; init; }

    public required string RepositoryId { get; init; }

    public required string TargetId { get; init; }

    public required EffortCategory Category { get; init; }

    public IReadOnlyList<string> Ecosystems { get; init; } = [];

    public required string ExpectedSizeBand { get; init; }

    public required EffortRange CandidateRange { get; init; }

    public required EffortRange ReviewedRange { get; init; }

    public required decimal AbsoluteExpectedResidualHours { get; init; }

    public required decimal NormalizedAbsoluteResidual { get; init; }

    public required bool CurrentReviewedExpectedCovered { get; init; }

    public required EffortRange CrossValidatedBaselineRange { get; init; }

    public required bool BaselineReviewedExpectedCovered { get; init; }
}

public sealed record CalibrationUncertaintyRepositoryEvaluation
{
    public required string RecordId { get; init; }

    public required string RepositoryId { get; init; }

    public required string SourceDigest { get; init; }

    public required string FeatureReportDigest { get; init; }

    public required string EstimateDigest { get; init; }

    public required int TargetCount { get; init; }

    public required int MatchedTargetCount { get; init; }

    public required int SourceWorkItemReferenceCount { get; init; }

    public required int MatchedSourceWorkItemReferenceCount { get; init; }

    public required CalibrationUncertaintyIntervalPerformance CurrentIntervals { get; init; }

    public required CalibrationUncertaintyIntervalPerformance CrossValidatedBaseline { get; init; }

    public IReadOnlyList<string> UnmatchedTargetIds { get; init; } = [];

    public IReadOnlyList<string> CategoryMismatchTargetIds { get; init; } = [];
}

public sealed record CalibrationUncertaintyEvaluationSummary
{
    public required int RecordCount { get; init; }

    public required int RepositoryCount { get; init; }

    public required int FeatureReportCount { get; init; }

    public required int IgnoredFeatureReportCount { get; init; }

    public required int TargetCount { get; init; }

    public required int MatchedTargetCount { get; init; }

    public required int SourceWorkItemReferenceCount { get; init; }

    public required int MatchedSourceWorkItemReferenceCount { get; init; }
}

public sealed record CalibrationUncertaintyEvaluationReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string EvaluatorVersion { get; init; }

    public required string MetricVersion { get; init; }

    public required string CorpusId { get; init; }

    public required string CorpusVersion { get; init; }

    public required string CorpusDigest { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required string FeatureContractVersion { get; init; }

    public required string FeatureContractDigest { get; init; }

    public required string IntervalPolicyVersion { get; init; }

    public IReadOnlyList<string> ProjectorVersions { get; init; } = [];

    public IReadOnlyList<string> EstimatorVersions { get; init; } = [];

    public required CalibrationUncertaintyEvaluationProtocol Protocol { get; init; }

    public required CalibrationUncertaintyEvaluationSummary Summary { get; init; }

    public required CalibrationUncertaintyIntervalPerformance CurrentIntervals { get; init; }

    public required CalibrationUncertaintyIntervalPerformance CrossValidatedBaseline { get; init; }

    public IReadOnlyList<CalibrationUncertaintyFeatureEvaluation> Features { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintySliceEvaluation> Slices { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintyRepositoryEvaluation> Repositories { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintyTargetEvaluation> Targets { get; init; } = [];
}
