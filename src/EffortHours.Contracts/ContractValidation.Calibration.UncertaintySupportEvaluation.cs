using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(
        CalibrationUncertaintySupportEvaluationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "uncertainty support evaluation", errors);
        if (report.EvaluatorVersion != "uncertainty-support-evaluator/1.0.0")
        {
            errors.Add($"Unsupported uncertainty support evaluator '{report.EvaluatorVersion}'.");
        }

        if (report.MetricVersion != CalibrationUncertaintyVersions.SupportEvaluationMetricV1)
        {
            errors.Add($"Unsupported uncertainty support metric '{report.MetricVersion}'.");
        }

        RequireText(report.CorpusId, "corpusId", errors);
        RequireText(report.CorpusVersion, "corpusVersion", errors);
        RequireDigest(report.CorpusDigest, "corpusDigest", errors);
        RequireDigest(report.SourceEvaluationDigest, "sourceEvaluationDigest", errors);
        RequireDigest(report.SupportProfileDigest, "supportProfileDigest", errors);
        RequireText(report.SupportProfilerVersion, "supportProfilerVersion", errors);
        RequireText(report.SupportPolicyVersion, "supportPolicyVersion", errors);
        RequireText(report.SupportPopulationId, "supportPopulationId", errors);
        RequireText(report.SupportPopulationVersion, "supportPopulationVersion", errors);
        RequireDigest(report.SupportPopulationDigest, "supportPopulationDigest", errors);
        RequireDigest(report.FeatureContractDigest, "featureContractDigest", errors);
        RequireText(report.BaselineId, "baselineId", errors);
        RequireUniqueText(report.ProjectorVersions, "projectorVersions", errors);
        RequireUniqueText(report.EstimatorVersions, "estimatorVersions", errors);
        if (report.Partition != CalibrationPartition.Development ||
            report.FeatureContractVersion != CalibrationUncertaintyVersions.FeatureContractV1 ||
            report.FeatureContractDigest !=
                CalibrationUncertaintyVersions.FeatureContractDigestV1 ||
            report.IntervalPolicyVersion != CalibrationUncertaintyVersions.IntervalPolicyV1 ||
            report.SupportProfilerVersion != CalibrationUncertaintyVersions.SupportProfilerV1 ||
            report.SupportPolicyVersion != CalibrationUncertaintyVersions.SupportPolicyV1 ||
            report.ProjectorVersions.Count == 0 || report.EstimatorVersions.Count == 0)
        {
            errors.Add("Uncertainty support evaluation does not pin its frozen development lineage.");
        }

        ValidateUncertaintySupportEvaluationProtocol(report.Protocol, errors);
        CalibrationUncertaintyEvaluationReport source = SupportEvaluationSourceView(report);
        ValidateUncertaintyEvaluationRepositories(source, errors);
        ValidateUncertaintyEvaluationTargets(source, errors);
        ValidateUncertaintySupportEvaluationTargets(report, errors);
        ValidateUncertaintySupportEvaluationSignals(report, source, errors);
        ValidateUncertaintySupportEvaluationSummary(report, errors);

        CalibrationUncertaintyTargetEvaluation[] targets =
            [.. report.Targets.Select(target => target.Source)];
        if (report.CurrentIntervals !=
                BuildUncertaintyEvaluationPerformance(targets, useBaseline: false) ||
            report.CrossValidatedBaseline !=
                BuildUncertaintyEvaluationPerformance(targets, useBaseline: true))
        {
            errors.Add("Support evaluation interval metrics do not reconcile to source targets.");
        }

        return errors;
    }

    private static void ValidateUncertaintySupportEvaluationProtocol(
        CalibrationUncertaintySupportEvaluationProtocol protocol,
        List<string> errors)
    {
        if (protocol.Version != CalibrationUncertaintyVersions.SupportEvaluationV1 ||
            protocol.Partition != CalibrationPartition.Development ||
            protocol.FoldUnit != "repository" ||
            protocol.TargetMetric != "absolute-reviewed-expected-residual" ||
            protocol.Normalization != "candidate-expected-with-0.5-hour-floor" ||
            protocol.NormalizationFloorHours != 0.5m ||
            protocol.IntendedCoverageTarget != 0.80m ||
            protocol.QuantileMethod != "nearest-rank" ||
            protocol.MinimumBucketObservationCount != 3 ||
            protocol.MinimumBucketRepositoryCount != 2 ||
            protocol.TargetAggregationVersion !=
                CalibrationUncertaintyVersions.SupportTargetAggregationV1 ||
            protocol.OutOfDistributionBucketPolicy !=
                "exact-0;very-near-0.008333;near-0.025;moderate-0.05;elevated-0.1;high" ||
            !protocol.RepositoryIsolated || !protocol.SupportProfileLabelIndependent ||
            !protocol.DevelopmentOnly || protocol.FitsProductionModel ||
            protocol.FormalProbabilityInterval)
        {
            errors.Add("Uncertainty support evaluation protocol violates frozen v1 invariants.");
        }
    }

    private static CalibrationUncertaintyEvaluationReport SupportEvaluationSourceView(
        CalibrationUncertaintySupportEvaluationReport report) => new()
        {
            EvaluatorVersion = "uncertainty-feature-evaluator/1.0.0",
            MetricVersion = CalibrationUncertaintyVersions.EvaluationMetricV1,
            CorpusId = report.CorpusId,
            CorpusVersion = report.CorpusVersion,
            CorpusDigest = report.CorpusDigest,
            Partition = report.Partition,
            FeatureContractVersion = report.FeatureContractVersion,
            FeatureContractDigest = report.FeatureContractDigest,
            IntervalPolicyVersion = report.IntervalPolicyVersion,
            ProjectorVersions = report.ProjectorVersions,
            EstimatorVersions = report.EstimatorVersions,
            Protocol = new CalibrationUncertaintyEvaluationProtocol
            {
                Version = CalibrationUncertaintyVersions.EvaluationProtocolV1,
                Partition = CalibrationPartition.Development,
                FoldUnit = "repository",
                TargetMetric = "absolute-reviewed-expected-residual",
                Normalization = "candidate-expected-with-0.5-hour-floor",
                NormalizationFloorHours = 0.5m,
                IntendedCoverageTarget = 0.80m,
                QuantileMethod = "nearest-rank",
                MinimumBucketObservationCount = 3,
                MinimumBucketRepositoryCount = 2,
                RepositoryIsolated = true,
                DevelopmentOnly = true,
                FitsProductionModel = false,
                FormalProbabilityInterval = false,
            },
            Summary = new CalibrationUncertaintyEvaluationSummary
            {
                RecordCount = report.Summary.RecordCount,
                RepositoryCount = report.Summary.RepositoryCount,
                FeatureReportCount = report.Summary.FeatureReportCount,
                IgnoredFeatureReportCount = 0,
                TargetCount = report.Summary.TargetCount,
                MatchedTargetCount = report.Summary.MatchedTargetCount,
                SourceWorkItemReferenceCount = report.Summary.SourceWorkItemReferenceCount,
                MatchedSourceWorkItemReferenceCount =
                report.Summary.MatchedSourceWorkItemReferenceCount,
            },
            CurrentIntervals = report.CurrentIntervals,
            CrossValidatedBaseline = report.CrossValidatedBaseline,
            Repositories = report.Repositories,
            Targets = [.. report.Targets.Select(target => target.Source)],
        };
}
