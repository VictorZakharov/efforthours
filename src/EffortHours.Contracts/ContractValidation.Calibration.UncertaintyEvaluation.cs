using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(CalibrationUncertaintyEvaluationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "calibration uncertainty evaluation", errors);
        if (report.EvaluatorVersion != "uncertainty-feature-evaluator/1.0.0")
        {
            errors.Add($"Unsupported uncertainty feature evaluator '{report.EvaluatorVersion}'.");
        }

        if (report.MetricVersion != CalibrationUncertaintyVersions.EvaluationMetricV1)
        {
            errors.Add($"Unsupported uncertainty evaluation metric '{report.MetricVersion}'.");
        }

        RequireText(report.CorpusId, "corpusId", errors);
        RequireText(report.CorpusVersion, "corpusVersion", errors);
        RequireDigest(report.CorpusDigest, "corpusDigest", errors);
        RequireDigest(report.FeatureContractDigest, "featureContractDigest", errors);
        if (report.FeatureContractDigest != CalibrationUncertaintyVersions.FeatureContractDigestV1)
        {
            errors.Add("The uncertainty evaluation does not pin the canonical v1 feature contract digest.");
        }
        if (report.Partition != CalibrationPartition.Development)
        {
            errors.Add("Uncertainty feature evaluation v1 is development-only.");
        }

        if (report.FeatureContractVersion != CalibrationUncertaintyVersions.FeatureContractV1 ||
            report.IntervalPolicyVersion != CalibrationUncertaintyVersions.IntervalPolicyV1)
        {
            errors.Add("Uncertainty evaluation does not pin the frozen v1 feature and interval contracts.");
        }

        RequireUniqueText(report.ProjectorVersions, "projectorVersions", errors);
        RequireUniqueText(report.EstimatorVersions, "estimatorVersions", errors);
        if (report.ProjectorVersions.Count == 0 || report.EstimatorVersions.Count == 0)
        {
            errors.Add("Uncertainty evaluation must identify its projector and estimator versions.");
        }

        ValidateUncertaintyEvaluationProtocol(report.Protocol, errors);
        ValidateUncertaintyEvaluationRepositories(report, errors);
        ValidateUncertaintyEvaluationTargets(report, errors);
        ValidateUncertaintyEvaluationFeatures(
            report,
            UncertaintyEvaluationFeatureBoundary,
            "The frozen v1 uncertainty evaluation",
            errors);
        ValidateUncertaintyEvaluationSlices(report, errors);
        ValidateUncertaintyEvaluationSummary(report, errors);

        CalibrationUncertaintyIntervalPerformance current =
            BuildUncertaintyEvaluationPerformance(report.Targets, useBaseline: false);
        CalibrationUncertaintyIntervalPerformance baseline =
            BuildUncertaintyEvaluationPerformance(report.Targets, useBaseline: true);
        if (report.CurrentIntervals != current)
        {
            errors.Add("currentIntervals does not reconcile to target rows.");
        }

        if (report.CrossValidatedBaseline != baseline)
        {
            errors.Add("crossValidatedBaseline does not reconcile to target rows.");
        }

        return errors;
    }

    private static void ValidateUncertaintyEvaluationFeatures(
        CalibrationUncertaintyEvaluationReport report,
        (string Id, CalibrationUncertaintyFeatureValueKind ValueKind,
            CalibrationUncertaintyFeatureMonotonicity Monotonicity)[] boundary,
        string boundaryName,
        List<string> errors)
    {
        if (report.Features.Count != boundary.Length)
        {
            errors.Add($"{boundaryName} must contain all {boundary.Length} scalar features.");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach ((CalibrationUncertaintyFeatureEvaluation feature, int index) in
                 report.Features.Select((feature, index) => (feature, index)))
        {
            string path = $"feature[{feature.FeatureId}]";
            RequireText(feature.FeatureId, "feature.id", errors);
            if (!ids.Add(feature.FeatureId))
            {
                errors.Add($"Uncertainty evaluation feature '{feature.FeatureId}' is duplicated.");
            }

            if (index >= boundary.Length ||
                (feature.FeatureId, feature.ValueKind, feature.Monotonicity) !=
                boundary[index])
            {
                errors.Add($"{path} does not match the frozen v1 feature order and semantics.");
            }

            bool widthDriver = feature.Monotonicity !=
                CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly;
            CalibrationUncertaintyFeatureAvailabilitySummary availability = feature.Availability;
            if (feature.WidthDriver != widthDriver ||
                availability.ObservationCount != report.Targets.Count ||
                availability.AvailableCount < 0 ||
                availability.NotApplicableCount < 0 ||
                availability.UnavailableCount < 0 ||
                availability.AvailableCount + availability.NotApplicableCount +
                    availability.UnavailableCount != availability.ObservationCount ||
                availability.AvailableRepositoryCount < 0 ||
                availability.AvailableRepositoryCount > report.Summary.RepositoryCount ||
                feature.ConditionedPredictionCount < 0 ||
                feature.BaselineFallbackCount < 0 ||
                feature.ConditionedPredictionCount + feature.BaselineFallbackCount !=
                    report.Targets.Count ||
                feature.Buckets.Sum(bucket => bucket.ObservationCount) !=
                    availability.AvailableCount ||
                feature.MonotonicBucketViolationCount < 0 ||
                feature.MonotonicBucketViolationCount > Math.Max(0, feature.Buckets.Count - 1))
            {
                errors.Add($"{path} contains inconsistent availability or prediction counts.");
            }

            ValidateUncertaintyEvaluationPerformance(
                feature.CrossValidatedIntervals,
                $"{path}.crossValidatedIntervals",
                errors);
            if (feature.CrossValidatedIntervals.ObservationCount != report.Targets.Count)
            {
                errors.Add($"{path}.crossValidatedIntervals must cover every matched target.");
            }

            decimal? coverageDelta = feature.CrossValidatedIntervals.ReviewedExpectedCoverage is null ||
                report.CrossValidatedBaseline.ReviewedExpectedCoverage is null
                    ? null
                    : RoundUncertaintyEvaluation(
                        feature.CrossValidatedIntervals.ReviewedExpectedCoverage.Value -
                        report.CrossValidatedBaseline.ReviewedExpectedCoverage.Value);
            if (feature.CoverageDeltaFromBaseline != coverageDelta ||
                feature.MeanNormalizedWidthDeltaFromBaseline != RoundUncertaintyEvaluation(
                    feature.CrossValidatedIntervals.MeanNormalizedWidth -
                    report.CrossValidatedBaseline.MeanNormalizedWidth) ||
                feature.MeanIntervalMissDeltaFromBaseline != RoundUncertaintyEvaluation(
                    feature.CrossValidatedIntervals.MeanIntervalMissHours -
                    report.CrossValidatedBaseline.MeanIntervalMissHours))
            {
                errors.Add($"{path} deltas do not reconcile to the held-out baseline.");
            }

            ValidateCorrelation(feature.ExpectedHoursSpearman, $"{path}.expectedHoursSpearman", errors);
            ValidateCorrelation(
                feature.AbsoluteResidualSpearman,
                $"{path}.absoluteResidualSpearman",
                errors);
            ValidateCorrelation(
                feature.NormalizedAbsoluteResidualSpearman,
                $"{path}.normalizedAbsoluteResidualSpearman",
                errors);
            HashSet<string> bucketIds = new(StringComparer.Ordinal);
            int priorOrder = -1;
            foreach (CalibrationUncertaintyFeatureBucketEvaluation bucket in feature.Buckets)
            {
                RequireText(bucket.Id, $"{path}.bucket.id", errors);
                if (!bucketIds.Add(bucket.Id) || bucket.Order <= priorOrder ||
                    bucket.ObservationCount <= 0 || bucket.RepositoryCount <= 0 ||
                    bucket.RepositoryCount > report.Summary.RepositoryCount ||
                    bucket.MinimumValue < 0m || bucket.MaximumValue < bucket.MinimumValue ||
                    bucket.MeanValue < bucket.MinimumValue || bucket.MeanValue > bucket.MaximumValue ||
                    bucket.MeanNormalizedAbsoluteResidual < 0m ||
                    bucket.Empirical80thPercentileNormalizedResidual < 0m)
                {
                    errors.Add($"{path}.bucket[{bucket.Id}] is invalid or out of order.");
                }

                priorOrder = bucket.Order;
                ValidateUncertaintyEvaluationPerformance(
                    bucket.CurrentIntervals,
                    $"{path}.bucket[{bucket.Id}].currentIntervals",
                    errors);
                if (bucket.CurrentIntervals.ObservationCount != bucket.ObservationCount)
                {
                    errors.Add($"{path}.bucket[{bucket.Id}] performance count is inconsistent.");
                }
            }

            ValidateUncertaintyEvaluationFeatureFolds(feature, report, path, errors);
        }
    }

    private static void ValidateUncertaintyEvaluationSlices(
        CalibrationUncertaintyEvaluationReport report,
        List<string> errors)
    {
        HashSet<(CalibrationUncertaintySliceDimension Dimension, string Value)> keys = [];
        foreach (CalibrationUncertaintySliceEvaluation slice in report.Slices)
        {
            string path = $"slice[{slice.Dimension}/{slice.Value}]";
            RequireText(slice.Value, "slice.value", errors);
            if (!keys.Add((slice.Dimension, slice.Value)) ||
                slice.ObservationCount <= 0 ||
                slice.RepositoryCount <= 0 ||
                slice.RepositoryCount > report.Summary.RepositoryCount)
            {
                errors.Add($"{path} contains invalid counts or is duplicated.");
            }

            ValidateUncertaintyEvaluationPerformance(slice.CurrentIntervals, $"{path}.current", errors);
            ValidateUncertaintyEvaluationPerformance(
                slice.CrossValidatedBaseline,
                $"{path}.baseline",
                errors);
            if (slice.CurrentIntervals.ObservationCount != slice.ObservationCount ||
                slice.CrossValidatedBaseline.ObservationCount != slice.ObservationCount)
            {
                errors.Add($"{path} performance counts are inconsistent.");
            }
        }
    }

    private static void ValidateUncertaintyEvaluationSummary(
        CalibrationUncertaintyEvaluationReport report,
        List<string> errors)
    {
        CalibrationUncertaintyEvaluationSummary summary = report.Summary;
        if (summary.RecordCount != report.Repositories.Count ||
            summary.RepositoryCount != report.Repositories.Select(repository => repository.RepositoryId)
                .Distinct(StringComparer.Ordinal).Count() ||
            summary.RepositoryCount < 3 ||
            summary.FeatureReportCount < 1 ||
            summary.IgnoredFeatureReportCount < 0 ||
            summary.IgnoredFeatureReportCount > summary.FeatureReportCount ||
            summary.FeatureReportCount - summary.IgnoredFeatureReportCount > summary.RecordCount ||
            summary.TargetCount != report.Repositories.Sum(repository => repository.TargetCount) ||
            summary.MatchedTargetCount != report.Targets.Count ||
            summary.MatchedTargetCount !=
                report.Repositories.Sum(repository => repository.MatchedTargetCount) ||
            summary.SourceWorkItemReferenceCount != report.Repositories.Sum(
                repository => repository.SourceWorkItemReferenceCount) ||
            summary.MatchedSourceWorkItemReferenceCount != report.Repositories.Sum(
                repository => repository.MatchedSourceWorkItemReferenceCount))
        {
            errors.Add("Uncertainty evaluation summary does not reconcile to report rows.");
        }

        ValidateUncertaintyEvaluationPerformance(report.CurrentIntervals, "currentIntervals", errors);
        ValidateUncertaintyEvaluationPerformance(
            report.CrossValidatedBaseline,
            "crossValidatedBaseline",
            errors);
    }

    private static void ValidateUncertaintyEvaluationPerformance(
        CalibrationUncertaintyIntervalPerformance performance,
        string path,
        List<string> errors)
    {
        if (performance.ObservationCount < 0 ||
            performance.ReviewedExpectedCoveredCount < 0 ||
            performance.ReviewedExpectedCoveredCount > performance.ObservationCount ||
            performance.MeanWidthHours < 0m ||
            performance.MeanNormalizedWidth < 0m ||
            performance.MeanAbsoluteResidualHours < 0m ||
            performance.MeanIntervalMissHours < 0m)
        {
            errors.Add($"{path} contains invalid counts or metrics.");
        }

        ValidateUnitRatio(performance.ReviewedExpectedCoverage, $"{path}.coverage", errors);
        decimal? expectedCoverage = performance.ObservationCount == 0
            ? null
            : RoundUncertaintyEvaluation(
                (decimal)performance.ReviewedExpectedCoveredCount / performance.ObservationCount);
        if (performance.ReviewedExpectedCoverage != expectedCoverage)
        {
            errors.Add($"{path}.reviewedExpectedCoverage does not reconcile to counts.");
        }
    }

    private static CalibrationUncertaintyIntervalPerformance BuildUncertaintyEvaluationPerformance(
        IReadOnlyList<CalibrationUncertaintyTargetEvaluation> targets,
        bool useBaseline)
    {
        if (targets.Count == 0)
        {
            return new CalibrationUncertaintyIntervalPerformance
            {
                ObservationCount = 0,
                ReviewedExpectedCoveredCount = 0,
                ReviewedExpectedCoverage = null,
                MeanWidthHours = 0m,
                MeanNormalizedWidth = 0m,
                MeanAbsoluteResidualHours = 0m,
                MeanIntervalMissHours = 0m,
            };
        }

        EffortRange[] ranges = [.. targets.Select(target =>
            useBaseline ? target.CrossValidatedBaselineRange : target.CandidateRange)];
        int covered = targets.Select((target, index) =>
                ContainsReviewedExpected(ranges[index], target.ReviewedRange.Expected))
            .Count(value => value);
        return new CalibrationUncertaintyIntervalPerformance
        {
            ObservationCount = targets.Count,
            ReviewedExpectedCoveredCount = covered,
            ReviewedExpectedCoverage = RoundUncertaintyEvaluation((decimal)covered / targets.Count),
            MeanWidthHours = MeanUncertaintyEvaluation(
                ranges.Select(range => range.High - range.Low)),
            MeanNormalizedWidth = MeanUncertaintyEvaluation(targets.Select((target, index) =>
                (ranges[index].High - ranges[index].Low) /
                decimal.Max(0.5m, target.CandidateRange.Expected))),
            MeanAbsoluteResidualHours = MeanUncertaintyEvaluation(targets.Select(target =>
                decimal.Abs(target.CandidateRange.Expected - target.ReviewedRange.Expected))),
            MeanIntervalMissHours = MeanUncertaintyEvaluation(targets.Select((target, index) =>
                IntervalMiss(ranges[index], target.ReviewedRange.Expected))),
        };
    }

    private static void ValidateCorrelation(decimal? value, string path, List<string> errors)
    {
        if (value is < -1m or > 1m)
        {
            errors.Add($"{path} must be between -1 and 1 when present.");
        }
    }

    private static bool ContainsReviewedExpected(EffortRange range, decimal reviewedExpected) =>
        reviewedExpected >= range.Low && reviewedExpected <= range.High;

    private static decimal IntervalMiss(EffortRange range, decimal reviewedExpected) =>
        reviewedExpected < range.Low
            ? range.Low - reviewedExpected
            : reviewedExpected > range.High
                ? reviewedExpected - range.High
                : 0m;

    private static decimal MeanUncertaintyEvaluation(IEnumerable<decimal> values)
    {
        decimal[] materialized = [.. values];
        return materialized.Length == 0
            ? 0m
            : RoundUncertaintyEvaluation(materialized.Average());
    }

    private static decimal RoundUncertaintyEvaluation(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);

}
