using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(
        CalibrationUncertaintyStructuralEvaluationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "calibration structural uncertainty evaluation", errors);
        if (report.EvaluatorVersion != CalibrationUncertaintyVersions.StructuralEvaluatorV1)
        {
            errors.Add(
                $"Unsupported structural uncertainty evaluator '{report.EvaluatorVersion}'.");
        }

        if (report.MetricVersion != CalibrationUncertaintyVersions.EvaluationMetricV1)
        {
            errors.Add(
                $"Unsupported structural uncertainty metric '{report.MetricVersion}'.");
        }

        RequireText(report.CorpusId, "corpusId", errors);
        RequireText(report.CorpusVersion, "corpusVersion", errors);
        RequireDigest(report.CorpusDigest, "corpusDigest", errors);
        RequireDigest(report.FeatureContractDigest, "featureContractDigest", errors);
        RequireDigest(report.EvaluationPolicyDigest, "evaluationPolicyDigest", errors);
        if (report.Partition != CalibrationPartition.Development)
        {
            errors.Add("Structural uncertainty evaluation v1 is development-only.");
        }

        if (report.FeatureContractVersion !=
                CalibrationUncertaintyVersions.StructuralFeatureContractV1 ||
            report.FeatureContractDigest !=
                CalibrationUncertaintyVersions.StructuralFeatureContractDigestV1 ||
            report.IntervalPolicyVersion != CalibrationUncertaintyVersions.IntervalPolicyV1)
        {
            errors.Add(
                "Structural uncertainty evaluation does not pin the frozen feature and interval " +
                "contracts.");
        }

        if (report.EvaluationPolicyDigest !=
            CalibrationUncertaintyVersions.StructuralEvaluationPolicyDigestV1)
        {
            errors.Add(
                "Structural uncertainty evaluation does not pin the frozen evaluation policy " +
                "digest.");
        }

        RequireUniqueText(report.ProjectorVersions, "projectorVersions", errors);
        RequireUniqueText(report.EstimatorVersions, "estimatorVersions", errors);
        if (report.ProjectorVersions.Count == 0 || report.EstimatorVersions.Count == 0)
        {
            errors.Add(
                "Structural uncertainty evaluation must identify projector and estimator versions.");
        }

        ValidateStructuralEvaluationProtocol(report.Protocol, errors);
        ValidateStructuralEvaluationPolicy(report.EvaluationPolicy, errors);
        CalibrationUncertaintyEvaluationReport source = StructuralEvaluationSource(report);
        ValidateUncertaintyEvaluationRepositories(source, errors);
        ValidateUncertaintyEvaluationTargets(source, errors);
        ValidateUncertaintyEvaluationFeatures(
            source,
            StructuralEvaluationFeatureBoundary,
            "The frozen v1 structural uncertainty evaluation",
            errors);
        ValidateUncertaintyEvaluationSlices(source, errors);
        ValidateUncertaintyEvaluationSummary(source, errors);
        ValidateStructuralEvaluationFeatures(report, source, errors);
        ValidateStructuralEvaluationTargets(report, errors);

        CalibrationUncertaintyIntervalPerformance current =
            BuildUncertaintyEvaluationPerformance(source.Targets, useBaseline: false);
        CalibrationUncertaintyIntervalPerformance baseline =
            BuildUncertaintyEvaluationPerformance(source.Targets, useBaseline: true);
        if (report.CurrentIntervals != current)
        {
            errors.Add("currentIntervals does not reconcile to structural target rows.");
        }

        if (report.CrossValidatedBaseline != baseline)
        {
            errors.Add("crossValidatedBaseline does not reconcile to structural target rows.");
        }

        return errors;
    }

    private static void ValidateStructuralEvaluationProtocol(
        CalibrationUncertaintyStructuralEvaluationProtocol protocol,
        List<string> errors)
    {
        if (protocol.Version !=
                CalibrationUncertaintyVersions.StructuralEvaluationProtocolV1 ||
            protocol.Partition != CalibrationPartition.Development ||
            protocol.FoldUnit != "repository" ||
            protocol.TargetMetric != "absolute-reviewed-expected-residual" ||
            protocol.Normalization != "candidate-expected-with-0.5-hour-floor" ||
            protocol.NormalizationFloorHours != 0.5m ||
            protocol.IntendedCoverageTarget != 0.80m ||
            protocol.QuantileMethod != "nearest-rank" ||
            protocol.MinimumBucketObservationCount != 3 ||
            protocol.MinimumBucketRepositoryCount != 2 ||
            protocol.EvaluationPolicyVersion !=
                CalibrationUncertaintyVersions.StructuralEvaluationPolicyV1 ||
            protocol.BucketAssignment != "fixed-feature-specific-inclusive-maximum" ||
            !protocol.RepositoryIsolated ||
            !protocol.DevelopmentOnly ||
            protocol.FitsProductionModel ||
            protocol.FormalProbabilityInterval)
        {
            errors.Add(
                "Structural uncertainty evaluation protocol does not satisfy v1 " +
                "development-only invariants.");
        }
    }

    private static void ValidateStructuralEvaluationFeatures(
        CalibrationUncertaintyStructuralEvaluationReport report,
        CalibrationUncertaintyEvaluationReport source,
        List<string> errors)
    {
        Dictionary<string, decimal?> baselineByRecord = report.Repositories.ToDictionary(
            repository => repository.RecordId,
            repository => repository.CrossValidatedBaseline.ReviewedExpectedCoverage,
            StringComparer.Ordinal);
        for (int index = 0; index < report.Features.Count; index++)
        {
            CalibrationUncertaintyStructuralFeatureEvaluation feature = report.Features[index];
            string path = $"structuralFeature[{feature.FeatureId}]";
            if (index >= report.EvaluationPolicy.Rules.Count)
            {
                errors.Add($"{path} has no corresponding evaluation-policy rule.");
                continue;
            }

            CalibrationUncertaintyStructuralEvaluationRule rule =
                report.EvaluationPolicy.Rules[index];
            if (feature.FeatureId != rule.FeatureId ||
                feature.Evaluation.FeatureId != rule.FeatureId ||
                feature.Evaluation.ValueKind != rule.ValueKind ||
                feature.TargetAggregation != rule.TargetAggregation ||
                feature.ExpectedResidualDirection != rule.ExpectedResidualDirection)
            {
                errors.Add($"{path} does not match its frozen evaluation-policy rule.");
            }

            bool correlationMatches = ExpectedStructuralDirectionMatches(
                feature.Evaluation.NormalizedAbsoluteResidualSpearman,
                rule.ExpectedResidualDirection);
            int bucketViolations = CountStructuralDirectionViolations(
                feature.Evaluation.Buckets,
                rule.ExpectedResidualDirection);
            int repositoryRegressions = feature.Evaluation.RepositoryFolds.Count(fold =>
                baselineByRecord.TryGetValue(fold.RecordId, out decimal? baseline) &&
                fold.Intervals.ReviewedExpectedCoverage is decimal conditioned &&
                baseline is decimal baselineValue &&
                conditioned < baselineValue);
            bool pooledGate = feature.Evaluation.ConditionedPredictionCount > 0 &&
                feature.Evaluation.CoverageDeltaFromBaseline is >= 0m &&
                feature.Evaluation.MeanNormalizedWidthDeltaFromBaseline <= 0m &&
                feature.Evaluation.MeanIntervalMissDeltaFromBaseline <= 0m;
            if (feature.ExpectedDirectionCorrelationMatches != correlationMatches ||
                feature.ExpectedDirectionBucketViolationCount != bucketViolations ||
                feature.RepositoryCoverageRegressionCount != repositoryRegressions ||
                feature.MeetsPooledIncrementalGate != pooledGate)
            {
                errors.Add($"{path} diagnostics do not reconcile to its measurements.");
            }

            CalibrationUncertaintyStructuralTargetEvaluation[] available =
            [
                .. report.Targets.Where(target => target.Features.Count > index &&
                    target.Features[index].Availability ==
                    CalibrationUncertaintyFeatureAvailability.Available),
            ];
            if (feature.Evaluation.Availability.AvailableCount != available.Length ||
                feature.Evaluation.Availability.AvailableRepositoryCount != available
                    .Select(target => target.Source.RepositoryId)
                    .Distinct(StringComparer.Ordinal).Count())
            {
                errors.Add($"{path} availability does not reconcile to structural target rows.");
            }

            foreach (CalibrationUncertaintyFeatureBucketEvaluation bucket in
                     feature.Evaluation.Buckets)
            {
                CalibrationUncertaintyStructuralBucketDefinition? definition =
                    rule.Buckets.SingleOrDefault(candidate => candidate.Id == bucket.Id);
                if (definition is null || definition.Order != bucket.Order)
                {
                    errors.Add($"{path}.bucket[{bucket.Id}] is not declared by its policy rule.");
                }
            }
        }

        if (report.Features.Count != source.Features.Count)
        {
            errors.Add("Structural feature wrappers do not reconcile to source feature rows.");
        }
    }

    private static void ValidateStructuralEvaluationTargets(
        CalibrationUncertaintyStructuralEvaluationReport report,
        List<string> errors)
    {
        foreach (CalibrationUncertaintyStructuralTargetEvaluation target in report.Targets)
        {
            string path = $"structuralTarget[{target.Source.RecordId}/{target.Source.TargetId}]";
            if (target.Features.Count != report.EvaluationPolicy.Rules.Count)
            {
                errors.Add($"{path}.features must match the evaluation-policy rule count.");
                continue;
            }

            for (int index = 0; index < target.Features.Count; index++)
            {
                CalibrationUncertaintyFeatureValue value = target.Features[index];
                CalibrationUncertaintyStructuralEvaluationRule rule =
                    report.EvaluationPolicy.Rules[index];
                if (value.FeatureId != rule.FeatureId || value.ReasonCode is not null ||
                    value.EvidenceIds.Count != 0 ||
                    (value.Availability == CalibrationUncertaintyFeatureAvailability.Available &&
                        (value.Value is null || value.Value < 0m ||
                            (rule.ValueKind == CalibrationUncertaintyFeatureValueKind.Ratio &&
                                value.Value > 1m))) ||
                    (value.Availability != CalibrationUncertaintyFeatureAvailability.Available &&
                        value.Value is not null))
                {
                    errors.Add($"{path}.feature[{value.FeatureId}] is invalid or out of order.");
                }
            }
        }
    }

    private static CalibrationUncertaintyEvaluationReport StructuralEvaluationSource(
        CalibrationUncertaintyStructuralEvaluationReport report) => new()
        {
            EvaluatorVersion = "uncertainty-feature-evaluator/1.0.0",
            MetricVersion = report.MetricVersion,
            CorpusId = report.CorpusId,
            CorpusVersion = report.CorpusVersion,
            CorpusDigest = report.CorpusDigest,
            Partition = report.Partition,
            FeatureContractVersion = CalibrationUncertaintyVersions.FeatureContractV1,
            FeatureContractDigest = CalibrationUncertaintyVersions.FeatureContractDigestV1,
            IntervalPolicyVersion = report.IntervalPolicyVersion,
            ProjectorVersions = report.ProjectorVersions,
            EstimatorVersions = report.EstimatorVersions,
            Protocol = new CalibrationUncertaintyEvaluationProtocol
            {
                Version = CalibrationUncertaintyVersions.EvaluationProtocolV1,
                Partition = report.Protocol.Partition,
                FoldUnit = report.Protocol.FoldUnit,
                TargetMetric = report.Protocol.TargetMetric,
                Normalization = report.Protocol.Normalization,
                NormalizationFloorHours = report.Protocol.NormalizationFloorHours,
                IntendedCoverageTarget = report.Protocol.IntendedCoverageTarget,
                QuantileMethod = report.Protocol.QuantileMethod,
                MinimumBucketObservationCount = report.Protocol.MinimumBucketObservationCount,
                MinimumBucketRepositoryCount = report.Protocol.MinimumBucketRepositoryCount,
                RepositoryIsolated = report.Protocol.RepositoryIsolated,
                DevelopmentOnly = report.Protocol.DevelopmentOnly,
                FitsProductionModel = report.Protocol.FitsProductionModel,
                FormalProbabilityInterval = report.Protocol.FormalProbabilityInterval,
            },
            Summary = report.Summary,
            CurrentIntervals = report.CurrentIntervals,
            CrossValidatedBaseline = report.CrossValidatedBaseline,
            Features = [.. report.Features.Select(feature => feature.Evaluation)],
            Slices = report.Slices,
            Repositories = report.Repositories,
            Targets = [.. report.Targets.Select(target => target.Source)],
        };

    private static bool ExpectedStructuralDirectionMatches(
        decimal? correlation,
        CalibrationUncertaintyStructuralResidualDirection direction) => direction switch
        {
            CalibrationUncertaintyStructuralResidualDirection.HigherValueHigherResidual =>
                correlation > 0m,
            CalibrationUncertaintyStructuralResidualDirection.HigherValueLowerResidual =>
                correlation < 0m,
            _ => false,
        };

    private static int CountStructuralDirectionViolations(
        IReadOnlyList<CalibrationUncertaintyFeatureBucketEvaluation> buckets,
        CalibrationUncertaintyStructuralResidualDirection direction)
    {
        int result = 0;
        for (int index = 1; index < buckets.Count; index++)
        {
            decimal previous = buckets[index - 1].Empirical80thPercentileNormalizedResidual;
            decimal current = buckets[index].Empirical80thPercentileNormalizedResidual;
            if ((direction ==
                    CalibrationUncertaintyStructuralResidualDirection.HigherValueHigherResidual &&
                    current < previous) ||
                (direction ==
                    CalibrationUncertaintyStructuralResidualDirection.HigherValueLowerResidual &&
                    current > previous))
            {
                result++;
            }
        }

        return result;
    }
}
