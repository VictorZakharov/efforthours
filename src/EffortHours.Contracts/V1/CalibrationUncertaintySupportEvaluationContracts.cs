namespace EffortHours.Contracts.V1;

public static class CalibrationUncertaintySupportSignalIds
{
    public const string FallbackDepth = "model.support-fallback-depth";

    public const string MinimumRepositoryCount = "model.support-minimum-repository-count";

    public const string WeightedMeanOutOfDistribution =
        "model.out-of-distribution-weighted-mean";

    public const string MaximumOutOfDistribution =
        "model.out-of-distribution-maximum";
}

public sealed record CalibrationUncertaintySupportEvaluationProtocol
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

    public required string TargetAggregationVersion { get; init; }

    public required string OutOfDistributionBucketPolicy { get; init; }

    public required bool RepositoryIsolated { get; init; }

    public required bool SupportProfileLabelIndependent { get; init; }

    public required bool DevelopmentOnly { get; init; }

    public required bool FitsProductionModel { get; init; }

    public required bool FormalProbabilityInterval { get; init; }
}

public sealed record CalibrationUncertaintySupportTargetEvaluation
{
    public required CalibrationUncertaintyTargetEvaluation Source { get; init; }

    public required int SourceWorkItemCount { get; init; }

    public required CalibrationUncertaintySupportLevel WorstSupportLevel { get; init; }

    public required int WorstSupportDepth { get; init; }

    public required bool SupportSufficient { get; init; }

    public required int MinimumSelectedSupportObservationCount { get; init; }

    public required int MinimumSelectedSupportRepositoryCount { get; init; }

    public required decimal ExpectedWeightedOutOfDistributionScore { get; init; }

    public required decimal MaximumOutOfDistributionScore { get; init; }
}

public sealed record CalibrationUncertaintySupportEvaluationSummary
{
    public required int RecordCount { get; init; }

    public required int RepositoryCount { get; init; }

    public required int FeatureReportCount { get; init; }

    public required int TargetCount { get; init; }

    public required int MatchedTargetCount { get; init; }

    public required int SourceWorkItemReferenceCount { get; init; }

    public required int MatchedSourceWorkItemReferenceCount { get; init; }

    public required int SupportProfileWorkItemCount { get; init; }

    public required int MatchedSupportWorkItemReferenceCount { get; init; }

    public required int SignalCount { get; init; }
}

public sealed record CalibrationUncertaintySupportEvaluationReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string EvaluatorVersion { get; init; }

    public required string MetricVersion { get; init; }

    public required string CorpusId { get; init; }

    public required string CorpusVersion { get; init; }

    public required string CorpusDigest { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required string SourceEvaluationDigest { get; init; }

    public required string SupportProfileDigest { get; init; }

    public required string SupportProfilerVersion { get; init; }

    public required string SupportPolicyVersion { get; init; }

    public required string SupportPopulationId { get; init; }

    public required string SupportPopulationVersion { get; init; }

    public required string SupportPopulationDigest { get; init; }

    public required string FeatureContractVersion { get; init; }

    public required string FeatureContractDigest { get; init; }

    public required string IntervalPolicyVersion { get; init; }

    public IReadOnlyList<string> ProjectorVersions { get; init; } = [];

    public IReadOnlyList<string> EstimatorVersions { get; init; } = [];

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public required CalibrationUncertaintySupportEvaluationProtocol Protocol { get; init; }

    public required CalibrationUncertaintySupportEvaluationSummary Summary { get; init; }

    public required CalibrationUncertaintyIntervalPerformance CurrentIntervals { get; init; }

    public required CalibrationUncertaintyIntervalPerformance CrossValidatedBaseline { get; init; }

    public IReadOnlyList<CalibrationUncertaintyFeatureEvaluation> Signals { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintyRepositoryEvaluation> Repositories { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintySupportTargetEvaluation> Targets { get; init; } = [];
}
