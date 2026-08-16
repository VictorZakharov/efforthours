namespace EffortHours.Contracts.V1;

public enum CalibrationUncertaintyStructuralTargetAggregation
{
    Maximum,
    Minimum,
}

public enum CalibrationUncertaintyStructuralResidualDirection
{
    HigherValueHigherResidual,
    HigherValueLowerResidual,
}

public sealed record CalibrationUncertaintyStructuralBucketDefinition
{
    public required string Id { get; init; }

    public required int Order { get; init; }

    public decimal? InclusiveMaximum { get; init; }
}

public sealed record CalibrationUncertaintyStructuralEvaluationRule
{
    public required string FeatureId { get; init; }

    public required CalibrationUncertaintyFeatureValueKind ValueKind { get; init; }

    public required CalibrationUncertaintyStructuralTargetAggregation TargetAggregation
    { get; init; }

    public required CalibrationUncertaintyStructuralResidualDirection ExpectedResidualDirection
    { get; init; }

    public IReadOnlyList<CalibrationUncertaintyStructuralBucketDefinition> Buckets { get; init; } = [];
}

public sealed record CalibrationUncertaintyStructuralEvaluationPolicy
{
    public required string Version { get; init; }

    public required string EffectiveDate { get; init; }

    public required bool LabelIndependent { get; init; }

    public IReadOnlyList<CalibrationUncertaintyStructuralEvaluationRule> Rules { get; init; } = [];
}

public sealed record CalibrationUncertaintyStructuralEvaluationProtocol
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

    public required string EvaluationPolicyVersion { get; init; }

    public required string BucketAssignment { get; init; }

    public required bool RepositoryIsolated { get; init; }

    public required bool DevelopmentOnly { get; init; }

    public required bool FitsProductionModel { get; init; }

    public required bool FormalProbabilityInterval { get; init; }
}

public sealed record CalibrationUncertaintyStructuralFeatureEvaluation
{
    public required string FeatureId { get; init; }

    public required CalibrationUncertaintyStructuralTargetAggregation TargetAggregation
    { get; init; }

    public required CalibrationUncertaintyStructuralResidualDirection ExpectedResidualDirection
    { get; init; }

    public required CalibrationUncertaintyFeatureEvaluation Evaluation { get; init; }

    public required bool ExpectedDirectionCorrelationMatches { get; init; }

    public required int ExpectedDirectionBucketViolationCount { get; init; }

    public required int RepositoryCoverageRegressionCount { get; init; }

    public required bool MeetsPooledIncrementalGate { get; init; }
}

public sealed record CalibrationUncertaintyStructuralTargetEvaluation
{
    public required CalibrationUncertaintyTargetEvaluation Source { get; init; }

    public IReadOnlyList<CalibrationUncertaintyFeatureValue> Features { get; init; } = [];
}

public sealed record CalibrationUncertaintyStructuralEvaluationReport
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

    public required CalibrationUncertaintyStructuralEvaluationPolicy EvaluationPolicy
    { get; init; }

    public required string EvaluationPolicyDigest { get; init; }

    public IReadOnlyList<string> ProjectorVersions { get; init; } = [];

    public IReadOnlyList<string> EstimatorVersions { get; init; } = [];

    public required CalibrationUncertaintyStructuralEvaluationProtocol Protocol { get; init; }

    public required CalibrationUncertaintyEvaluationSummary Summary { get; init; }

    public required CalibrationUncertaintyIntervalPerformance CurrentIntervals { get; init; }

    public required CalibrationUncertaintyIntervalPerformance CrossValidatedBaseline { get; init; }

    public IReadOnlyList<CalibrationUncertaintyStructuralFeatureEvaluation> Features
    { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintySliceEvaluation> Slices { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintyRepositoryEvaluation> Repositories { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintyStructuralTargetEvaluation> Targets { get; init; } = [];
}
