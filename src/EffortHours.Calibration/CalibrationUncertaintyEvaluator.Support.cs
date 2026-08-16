using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static partial class CalibrationUncertaintyEvaluator
{
    public const string SupportEvaluatorVersion = "uncertainty-support-evaluator/1.0.0";

    public static CalibrationUncertaintySupportEvaluationReport EvaluateSupportDevelopment(
        CalibrationCorpus corpus,
        IReadOnlyList<CalibrationUncertaintyFeatureReport> featureReports,
        CalibrationUncertaintySupportProfile supportProfile)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(featureReports);
        ArgumentNullException.ThrowIfNull(supportProfile);

        CalibrationUncertaintyEvaluationReport source = EvaluateDevelopment(
            corpus,
            featureReports);
        ValidateSupportEvaluationInputs(source, featureReports, supportProfile);
        CalibrationUncertaintySupportEvaluationData data = BuildSupportEvaluationData(
            corpus,
            featureReports,
            supportProfile,
            source);
        CalibrationUncertaintyIntervalPerformance baseline = source.CrossValidatedBaseline;
        CalibrationUncertaintySupportEvaluationReport result = new()
        {
            EvaluatorVersion = SupportEvaluatorVersion,
            MetricVersion = CalibrationUncertaintyVersions.SupportEvaluationMetricV1,
            CorpusId = source.CorpusId,
            CorpusVersion = source.CorpusVersion,
            CorpusDigest = source.CorpusDigest,
            Partition = source.Partition,
            SourceEvaluationDigest = CalibrationDigest.Compute(source),
            SupportProfileDigest = CalibrationDigest.Compute(supportProfile),
            SupportProfilerVersion = supportProfile.ProfilerVersion,
            SupportPolicyVersion = supportProfile.Policy.Version,
            SupportPopulationId = supportProfile.PopulationId,
            SupportPopulationVersion = supportProfile.PopulationVersion,
            SupportPopulationDigest = supportProfile.PopulationDigest,
            FeatureContractVersion = source.FeatureContractVersion,
            FeatureContractDigest = source.FeatureContractDigest,
            IntervalPolicyVersion = source.IntervalPolicyVersion,
            ProjectorVersions = source.ProjectorVersions,
            EstimatorVersions = source.EstimatorVersions,
            Profile = supportProfile.Profile,
            BaselineId = supportProfile.BaselineId,
            Protocol = SupportProtocol,
            Summary = new CalibrationUncertaintySupportEvaluationSummary
            {
                RecordCount = source.Summary.RecordCount,
                RepositoryCount = source.Summary.RepositoryCount,
                FeatureReportCount = source.Summary.FeatureReportCount,
                TargetCount = source.Summary.TargetCount,
                MatchedTargetCount = source.Summary.MatchedTargetCount,
                SourceWorkItemReferenceCount = source.Summary.SourceWorkItemReferenceCount,
                MatchedSourceWorkItemReferenceCount =
                    source.Summary.MatchedSourceWorkItemReferenceCount,
                SupportProfileWorkItemCount = supportProfile.WorkItems.Count,
                MatchedSupportWorkItemReferenceCount =
                    data.MatchedSupportWorkItemReferenceCount,
                SignalCount = SupportSignalDefinitions.Length,
            },
            CurrentIntervals = source.CurrentIntervals,
            CrossValidatedBaseline = baseline,
            Signals = BuildSupportSignalEvaluations(data.Observations, baseline),
            Repositories = source.Repositories,
            Targets = data.Targets,
        };
        IReadOnlyList<string> errors = ContractValidation.Validate(result);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Uncertainty support evaluator produced an invalid report: " +
                string.Join("; ", errors));
        }

        return result;
    }

    private static readonly CalibrationUncertaintySupportEvaluationProtocol SupportProtocol =
        new()
        {
            Version = CalibrationUncertaintyVersions.SupportEvaluationV1,
            Partition = CalibrationPartition.Development,
            FoldUnit = "repository",
            TargetMetric = "absolute-reviewed-expected-residual",
            Normalization = "candidate-expected-with-0.5-hour-floor",
            NormalizationFloorHours = CalibrationUncertaintyEvaluationMath.NormalizationFloorHours,
            IntendedCoverageTarget = CalibrationUncertaintyEvaluationMath.CoverageTarget,
            QuantileMethod = "nearest-rank",
            MinimumBucketObservationCount =
                CalibrationUncertaintyEvaluationMath.MinimumBucketObservationCount,
            MinimumBucketRepositoryCount =
                CalibrationUncertaintyEvaluationMath.MinimumBucketRepositoryCount,
            TargetAggregationVersion =
                CalibrationUncertaintyVersions.SupportTargetAggregationV1,
            OutOfDistributionBucketPolicy =
                "exact-0;very-near-0.008333;near-0.025;moderate-0.05;elevated-0.1;high",
            RepositoryIsolated = true,
            SupportProfileLabelIndependent = true,
            DevelopmentOnly = true,
            FitsProductionModel = false,
            FormalProbabilityInterval = false,
        };

    private static readonly CalibrationUncertaintyFeatureDefinition[] SupportSignalDefinitions =
    [
        Signal(
            CalibrationUncertaintySupportSignalIds.FallbackDepth,
            CalibrationUncertaintyFeatureValueKind.Ordinal,
            CalibrationUncertaintyFeatureMonotonicity.HigherMustNotNarrow,
            "Worst hierarchical support fallback depth across source work items."),
        Signal(
            CalibrationUncertaintySupportSignalIds.MinimumRepositoryCount,
            CalibrationUncertaintyFeatureValueKind.Count,
            CalibrationUncertaintyFeatureMonotonicity.LowerMustNotNarrow,
            "Minimum selected-cell cross-family repository count."),
        Signal(
            CalibrationUncertaintySupportSignalIds.WeightedMeanOutOfDistribution,
            CalibrationUncertaintyFeatureValueKind.Ratio,
            CalibrationUncertaintyFeatureMonotonicity.HigherMustNotNarrow,
            "Candidate-expected-hour-weighted mean work-item OOD distance."),
        Signal(
            CalibrationUncertaintySupportSignalIds.MaximumOutOfDistribution,
            CalibrationUncertaintyFeatureValueKind.Ratio,
            CalibrationUncertaintyFeatureMonotonicity.HigherMustNotNarrow,
            "Maximum work-item OOD distance."),
    ];

    private static CalibrationUncertaintyFeatureDefinition Signal(
        string id,
        CalibrationUncertaintyFeatureValueKind kind,
        CalibrationUncertaintyFeatureMonotonicity monotonicity,
        string description) => new()
        {
            Id = id,
            Stage = CalibrationUncertaintyFeatureStage.AvailableOffline,
            ValueKind = kind,
            Monotonicity = monotonicity,
            OfflineSource = "uncertainty-support-profile/1.0.0",
            Description = description,
        };
}
