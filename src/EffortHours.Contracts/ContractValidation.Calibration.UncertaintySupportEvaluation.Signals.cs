using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateUncertaintySupportEvaluationSignals(
        CalibrationUncertaintySupportEvaluationReport report,
        CalibrationUncertaintyEvaluationReport source,
        List<string> errors)
    {
        if (report.Signals.Count != SupportEvaluationSignalBoundary.Length)
        {
            errors.Add("The frozen v1 support evaluation must contain all four signals.");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach ((CalibrationUncertaintyFeatureEvaluation signal, int index) in
                 report.Signals.Select((signal, index) => (signal, index)))
        {
            string path = $"signal[{signal.FeatureId}]";
            if (!ids.Add(signal.FeatureId) || index >= SupportEvaluationSignalBoundary.Length ||
                (signal.FeatureId, signal.ValueKind, signal.Monotonicity) !=
                    SupportEvaluationSignalBoundary[index])
            {
                errors.Add($"{path} does not match the frozen v1 signal order and semantics.");
            }

            CalibrationUncertaintyFeatureAvailabilitySummary availability = signal.Availability;
            if (!signal.WidthDriver || availability.ObservationCount != report.Targets.Count ||
                availability.AvailableCount != report.Targets.Count ||
                availability.NotApplicableCount != 0 || availability.UnavailableCount != 0 ||
                availability.AvailableRepositoryCount != report.Summary.RepositoryCount ||
                signal.ConditionedPredictionCount < 0 || signal.BaselineFallbackCount < 0 ||
                signal.ConditionedPredictionCount + signal.BaselineFallbackCount !=
                    report.Targets.Count ||
                signal.Buckets.Sum(bucket => bucket.ObservationCount) != report.Targets.Count)
            {
                errors.Add($"{path} contains inconsistent availability or prediction counts.");
            }

            ValidateUncertaintyEvaluationPerformance(
                signal.CrossValidatedIntervals,
                $"{path}.crossValidatedIntervals",
                errors);
            decimal? coverageDelta = signal.CrossValidatedIntervals.ReviewedExpectedCoverage is null ||
                report.CrossValidatedBaseline.ReviewedExpectedCoverage is null
                    ? null
                    : RoundUncertaintyEvaluation(
                        signal.CrossValidatedIntervals.ReviewedExpectedCoverage.Value -
                        report.CrossValidatedBaseline.ReviewedExpectedCoverage.Value);
            if (signal.CrossValidatedIntervals.ObservationCount != report.Targets.Count ||
                signal.CoverageDeltaFromBaseline != coverageDelta ||
                signal.MeanNormalizedWidthDeltaFromBaseline != RoundUncertaintyEvaluation(
                    signal.CrossValidatedIntervals.MeanNormalizedWidth -
                    report.CrossValidatedBaseline.MeanNormalizedWidth) ||
                signal.MeanIntervalMissDeltaFromBaseline != RoundUncertaintyEvaluation(
                    signal.CrossValidatedIntervals.MeanIntervalMissHours -
                    report.CrossValidatedBaseline.MeanIntervalMissHours))
            {
                errors.Add($"{path} interval metrics do not reconcile to the held-out baseline.");
            }

            ValidateCorrelation(signal.ExpectedHoursSpearman, $"{path}.expectedHoursSpearman", errors);
            ValidateCorrelation(
                signal.AbsoluteResidualSpearman,
                $"{path}.absoluteResidualSpearman",
                errors);
            ValidateCorrelation(
                signal.NormalizedAbsoluteResidualSpearman,
                $"{path}.normalizedAbsoluteResidualSpearman",
                errors);
            if (signal.MonotonicBucketViolationCount !=
                CountSupportEvaluationMonotonicViolations(
                    signal.Buckets,
                    signal.Monotonicity))
            {
                errors.Add($"{path}.monotonicBucketViolationCount does not reconcile to buckets.");
            }

            ValidateSupportSignalBuckets(signal, report.Targets, path, errors);
            ValidateUncertaintyEvaluationFeatureFolds(signal, source, path, errors);
        }
    }

    private static void ValidateSupportSignalBuckets(
        CalibrationUncertaintyFeatureEvaluation signal,
        IReadOnlyList<CalibrationUncertaintySupportTargetEvaluation> targets,
        string path,
        List<string> errors)
    {
        Dictionary<(string Id, int Order), CalibrationUncertaintySupportTargetEvaluation[]> expected =
            targets.GroupBy(target => SupportEvaluationBucket(
                    signal.FeatureId,
                    signal.ValueKind,
                    SupportEvaluationSignalValue(target, signal.FeatureId)))
                .ToDictionary(
                    group => group.Key,
                    group => group.ToArray());
        HashSet<(string Id, int Order)> seen = [];
        int priorOrder = -1;
        foreach (CalibrationUncertaintyFeatureBucketEvaluation bucket in signal.Buckets)
        {
            string bucketPath = $"{path}.bucket[{bucket.Id}]";
            (string Id, int Order) key = (bucket.Id, bucket.Order);
            if (!seen.Add(key) || bucket.Order <= priorOrder ||
                !expected.TryGetValue(key, out CalibrationUncertaintySupportTargetEvaluation[]? members))
            {
                errors.Add($"{bucketPath} is unexpected, duplicated, or out of order.");
                priorOrder = bucket.Order;
                continue;
            }

            decimal[] values = [.. members.Select(target =>
                SupportEvaluationSignalValue(target, signal.FeatureId))];
            CalibrationUncertaintyTargetEvaluation[] sourceTargets =
                [.. members.Select(target => target.Source)];
            if (bucket.ObservationCount != members.Length ||
                bucket.RepositoryCount != members.Select(target => target.Source.RepositoryId)
                    .Distinct(StringComparer.Ordinal).Count() ||
                bucket.MinimumValue != RoundUncertaintyEvaluation(values.Min()) ||
                bucket.MaximumValue != RoundUncertaintyEvaluation(values.Max()) ||
                bucket.MeanValue != MeanUncertaintyEvaluation(values) ||
                bucket.MeanNormalizedAbsoluteResidual != MeanUncertaintyEvaluation(
                    sourceTargets.Select(target => target.NormalizedAbsoluteResidual)) ||
                bucket.Empirical80thPercentileNormalizedResidual !=
                    SupportEvaluationQuantile80(sourceTargets.Select(target =>
                        target.NormalizedAbsoluteResidual)) ||
                bucket.CurrentIntervals != BuildUncertaintyEvaluationPerformance(
                    sourceTargets,
                    useBaseline: false))
            {
                errors.Add($"{bucketPath} does not reconcile to target rows.");
            }

            priorOrder = bucket.Order;
        }

        if (seen.Count != expected.Count || signal.Buckets.Count != expected.Count)
        {
            errors.Add($"{path}.buckets do not cover every observed signal bucket.");
        }
    }

    private static (string Id, int Order) SupportEvaluationBucket(
        string signalId,
        CalibrationUncertaintyFeatureValueKind kind,
        decimal value)
    {
        if (signalId == CalibrationUncertaintySupportSignalIds.FallbackDepth)
        {
            return value switch
            {
                0m => ("exact", 0),
                1m => ("category-size-ecosystem", 1),
                2m => ("category-size", 2),
                3m => ("category", 3),
                4m => ("global", 4),
                5m => ("insufficient", 5),
                _ => ("invalid", int.MaxValue),
            };
        }

        if (signalId is CalibrationUncertaintySupportSignalIds.WeightedMeanOutOfDistribution or
            CalibrationUncertaintySupportSignalIds.MaximumOutOfDistribution)
        {
            return value switch
            {
                <= 0m => ("exact", 0),
                <= 0.008333m => ("very-near", 1),
                <= 0.025m => ("near", 2),
                <= 0.05m => ("moderate", 3),
                <= 0.1m => ("elevated", 4),
                _ => ("high", 5),
            };
        }

        return kind == CalibrationUncertaintyFeatureValueKind.Count
            ? value switch
            {
                <= 0m => ("zero", 0),
                <= 1m => ("one", 1),
                <= 3m => ("two-to-three", 2),
                <= 7m => ("four-to-seven", 3),
                _ => ("eight-plus", 4),
            }
            : ("invalid", int.MaxValue);
    }

    private static decimal SupportEvaluationQuantile80(IEnumerable<decimal> values)
    {
        decimal[] ordered = [.. values.Order()];
        int rank = (int)decimal.Ceiling(0.80m * ordered.Length);
        return RoundUncertaintyEvaluation(ordered[Math.Max(0, rank - 1)]);
    }

    private static int CountSupportEvaluationMonotonicViolations(
        IReadOnlyList<CalibrationUncertaintyFeatureBucketEvaluation> buckets,
        CalibrationUncertaintyFeatureMonotonicity monotonicity)
    {
        int violations = 0;
        for (int index = 1; index < buckets.Count; index++)
        {
            decimal previous = buckets[index - 1].Empirical80thPercentileNormalizedResidual;
            decimal current = buckets[index].Empirical80thPercentileNormalizedResidual;
            bool violation = monotonicity switch
            {
                CalibrationUncertaintyFeatureMonotonicity.HigherMustNotNarrow =>
                    current < previous,
                CalibrationUncertaintyFeatureMonotonicity.HigherMustWiden =>
                    current <= previous,
                CalibrationUncertaintyFeatureMonotonicity.LowerMustNotNarrow =>
                    current > previous,
                _ => false,
            };
            violations += violation ? 1 : 0;
        }

        return violations;
    }

    private static readonly (string Id, CalibrationUncertaintyFeatureValueKind ValueKind,
        CalibrationUncertaintyFeatureMonotonicity Monotonicity)[]
        SupportEvaluationSignalBoundary =
        [
            (CalibrationUncertaintySupportSignalIds.FallbackDepth,
                CalibrationUncertaintyFeatureValueKind.Ordinal,
                CalibrationUncertaintyFeatureMonotonicity.HigherMustNotNarrow),
            (CalibrationUncertaintySupportSignalIds.MinimumRepositoryCount,
                CalibrationUncertaintyFeatureValueKind.Count,
                CalibrationUncertaintyFeatureMonotonicity.LowerMustNotNarrow),
            (CalibrationUncertaintySupportSignalIds.WeightedMeanOutOfDistribution,
                CalibrationUncertaintyFeatureValueKind.Ratio,
                CalibrationUncertaintyFeatureMonotonicity.HigherMustNotNarrow),
            (CalibrationUncertaintySupportSignalIds.MaximumOutOfDistribution,
                CalibrationUncertaintyFeatureValueKind.Ratio,
                CalibrationUncertaintyFeatureMonotonicity.HigherMustNotNarrow),
        ];
}
