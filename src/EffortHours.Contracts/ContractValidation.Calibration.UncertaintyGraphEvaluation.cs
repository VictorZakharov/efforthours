using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(CalibrationUncertaintyGraphEvaluationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "calibration graph uncertainty evaluation", errors);
        if (report.EvaluatorVersion != CalibrationUncertaintyVersions.GraphEvaluatorV1)
        {
            errors.Add($"Unsupported graph uncertainty evaluator '{report.EvaluatorVersion}'.");
        }

        if (report.MetricVersion != CalibrationUncertaintyVersions.EvaluationMetricV1)
        {
            errors.Add($"Unsupported graph uncertainty metric '{report.MetricVersion}'.");
        }

        RequireText(report.CorpusId, "corpusId", errors);
        RequireText(report.CorpusVersion, "corpusVersion", errors);
        RequireDigest(report.CorpusDigest, "corpusDigest", errors);
        RequireDigest(report.FeatureContractDigest, "featureContractDigest", errors);
        RequireDigest(report.EvaluationPolicyDigest, "evaluationPolicyDigest", errors);
        if (report.Partition != CalibrationPartition.Development)
        {
            errors.Add("Graph uncertainty evaluation v1 is development-only.");
        }

        if (report.FeatureContractVersion !=
                CalibrationUncertaintyVersions.GraphFeatureContractV1 ||
            report.FeatureContractDigest !=
                CalibrationUncertaintyVersions.GraphFeatureContractDigestV1 ||
            report.IntervalPolicyVersion != CalibrationUncertaintyVersions.IntervalPolicyV1)
        {
            errors.Add(
                "Graph uncertainty evaluation does not pin the frozen feature and interval " +
                "contracts.");
        }

        if (report.EvaluationPolicyDigest !=
            CalibrationUncertaintyVersions.GraphEvaluationPolicyDigestV1)
        {
            errors.Add(
                "Graph uncertainty evaluation does not pin the frozen evaluation policy digest.");
        }

        RequireUniqueText(report.ProjectorVersions, "projectorVersions", errors);
        RequireUniqueText(report.EstimatorVersions, "estimatorVersions", errors);
        if (report.ProjectorVersions.Count == 0 || report.EstimatorVersions.Count == 0)
        {
            errors.Add("Graph uncertainty evaluation must identify projector and estimator versions.");
        }

        ValidateGraphEvaluationProtocol(report.Protocol, errors);
        ValidateGraphEvaluationPolicy(report.EvaluationPolicy, errors);
        CalibrationUncertaintyEvaluationReport source = GraphEvaluationSource(report);
        ValidateUncertaintyEvaluationRepositories(source, errors);
        ValidateUncertaintyEvaluationTargets(source, errors);
        ValidateUncertaintyEvaluationFeatures(
            source,
            GraphEvaluationFeatureBoundary,
            "The frozen v1 graph uncertainty evaluation",
            errors);
        ValidateUncertaintyEvaluationSlices(source, errors);
        ValidateUncertaintyEvaluationSummary(source, errors);
        ValidateGraphEvaluationFeatures(report, source, errors);
        ValidateGraphEvaluationTargets(report, errors);

        CalibrationUncertaintyIntervalPerformance current =
            BuildUncertaintyEvaluationPerformance(source.Targets, useBaseline: false);
        CalibrationUncertaintyIntervalPerformance baseline =
            BuildUncertaintyEvaluationPerformance(source.Targets, useBaseline: true);
        if (report.CurrentIntervals != current)
        {
            errors.Add("currentIntervals does not reconcile to graph target rows.");
        }

        if (report.CrossValidatedBaseline != baseline)
        {
            errors.Add("crossValidatedBaseline does not reconcile to graph target rows.");
        }

        return errors;
    }

    private static void ValidateGraphEvaluationProtocol(
        CalibrationUncertaintyGraphEvaluationProtocol protocol,
        List<string> errors)
    {
        if (protocol.Version != CalibrationUncertaintyVersions.GraphEvaluationProtocolV1 ||
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
                CalibrationUncertaintyVersions.GraphEvaluationPolicyV1 ||
            protocol.BucketAssignment != "fixed-feature-specific-inclusive-maximum" ||
            protocol.TargetNodePopulation != "union-of-unique-mapped-node-ids-per-target" ||
            protocol.UnmappedWorkItemPolicy != "retain-target-and-aggregate-mapped-nodes" ||
            protocol.PublicInterfaceAvailabilityPolicy !=
                "any-unavailable-selected-node-makes-interface-features-unavailable" ||
            !protocol.RepositoryIsolated ||
            !protocol.DevelopmentOnly ||
            protocol.FitsProductionModel ||
            protocol.FormalProbabilityInterval)
        {
            errors.Add(
                "Graph uncertainty evaluation protocol does not satisfy v1 development-only " +
                "invariants.");
        }
    }

    private static void ValidateGraphEvaluationFeatures(
        CalibrationUncertaintyGraphEvaluationReport report,
        CalibrationUncertaintyEvaluationReport source,
        List<string> errors)
    {
        Dictionary<string, decimal?> baselineByRecord = report.Repositories.ToDictionary(
            repository => repository.RecordId,
            repository => repository.CrossValidatedBaseline.ReviewedExpectedCoverage,
            StringComparer.Ordinal);
        for (int index = 0; index < report.Features.Count; index++)
        {
            CalibrationUncertaintyGraphFeatureEvaluation feature = report.Features[index];
            string path = $"graphFeature[{feature.FeatureId}]";
            if (index >= report.EvaluationPolicy.Rules.Count)
            {
                errors.Add($"{path} has no corresponding evaluation-policy rule.");
                continue;
            }

            CalibrationUncertaintyGraphEvaluationRule rule =
                report.EvaluationPolicy.Rules[index];
            if (feature.FeatureId != rule.FeatureId ||
                feature.Evaluation.FeatureId != rule.FeatureId ||
                feature.Evaluation.ValueKind != rule.ValueKind ||
                feature.TargetAggregation != rule.TargetAggregation ||
                feature.ExpectedResidualDirection != rule.ExpectedResidualDirection)
            {
                errors.Add($"{path} does not match its frozen evaluation-policy rule.");
            }

            bool correlationMatches = feature.Evaluation.NormalizedAbsoluteResidualSpearman > 0m;
            int bucketViolations = CountGraphDirectionViolations(feature.Evaluation.Buckets);
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

            CalibrationUncertaintyGraphTargetEvaluation[] available =
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
                errors.Add($"{path} availability does not reconcile to graph target rows.");
            }

            foreach (CalibrationUncertaintyFeatureBucketEvaluation bucket in
                     feature.Evaluation.Buckets)
            {
                CalibrationUncertaintyGraphBucketDefinition? definition =
                    rule.Buckets.SingleOrDefault(candidate => candidate.Id == bucket.Id);
                if (definition is null || definition.Order != bucket.Order)
                {
                    errors.Add($"{path}.bucket[{bucket.Id}] is not declared by its policy rule.");
                }
            }
        }

        if (report.Features.Count != source.Features.Count)
        {
            errors.Add("Graph feature wrappers do not reconcile to source feature rows.");
        }
    }

    private static void ValidateGraphEvaluationTargets(
        CalibrationUncertaintyGraphEvaluationReport report,
        List<string> errors)
    {
        foreach (CalibrationUncertaintyGraphTargetEvaluation target in report.Targets)
        {
            string path = $"graphTarget[{target.Source.RecordId}/{target.Source.TargetId}]";
            RequireUniqueText(target.NodeIds, $"{path}.nodeIds", errors);
            if (!target.NodeIds.SequenceEqual(target.NodeIds.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal))
            {
                errors.Add($"{path}.nodeIds must use deterministic ordinal order.");
            }

            if (target.Features.Count != report.EvaluationPolicy.Rules.Count)
            {
                errors.Add($"{path}.features must match the evaluation-policy rule count.");
                continue;
            }

            for (int index = 0; index < target.Features.Count; index++)
            {
                CalibrationUncertaintyFeatureValue value = target.Features[index];
                CalibrationUncertaintyGraphEvaluationRule rule =
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

                bool topology = index < 10;
                if ((target.NodeIds.Count == 0 && value.Availability !=
                        CalibrationUncertaintyFeatureAvailability.NotApplicable) ||
                    (target.NodeIds.Count > 0 && topology && value.Availability !=
                        CalibrationUncertaintyFeatureAvailability.Available))
                {
                    errors.Add($"{path}.feature[{value.FeatureId}] conflicts with node coverage.");
                }
            }
        }
    }

    private static CalibrationUncertaintyEvaluationReport GraphEvaluationSource(
        CalibrationUncertaintyGraphEvaluationReport report) => new()
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

    private static int CountGraphDirectionViolations(
        IReadOnlyList<CalibrationUncertaintyFeatureBucketEvaluation> buckets)
    {
        int result = 0;
        for (int index = 1; index < buckets.Count; index++)
        {
            if (buckets[index].Empirical80thPercentileNormalizedResidual <
                buckets[index - 1].Empirical80thPercentileNormalizedResidual)
            {
                result++;
            }
        }

        return result;
    }
}
