using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static partial class CalibrationUncertaintyEvaluator
{
    private static IReadOnlyList<CalibrationUncertaintyFeatureEvaluation>
        BuildSupportSignalEvaluations(
            IReadOnlyList<CalibrationUncertaintyObservation> observations,
            CalibrationUncertaintyIntervalPerformance baseline) =>
    [
        .. SupportSignalDefinitions.Select(definition => BuildSupportSignalEvaluation(
            observations,
            definition,
            baseline)),
    ];

    private static CalibrationUncertaintyFeatureEvaluation BuildSupportSignalEvaluation(
        IReadOnlyList<CalibrationUncertaintyObservation> observations,
        CalibrationUncertaintyFeatureDefinition definition,
        CalibrationUncertaintyIntervalPerformance baseline)
    {
        Dictionary<(string RepositoryId, CalibrationUncertaintyBucket Bucket), decimal>
            supportedFactors = BuildSupportSignalFactors(observations, definition);
        Dictionary<ObservationKey, CalibrationUncertaintyPrediction> predictions = [];
        foreach (CalibrationUncertaintyObservation observation in observations)
        {
            CalibrationUncertaintyBucket bucket = SupportSignalBucket(
                definition.Id,
                definition.ValueKind,
                observation.Features[definition.Id].Value!.Value);
            bool supported = supportedFactors.TryGetValue(
                (observation.RepositoryId, bucket),
                out decimal factor);
            predictions.Add(
                Key(observation),
                supported
                    ? new CalibrationUncertaintyPrediction(
                        CalibrationUncertaintyEvaluationMath.PredictRange(
                            observation.CandidateRange.Expected,
                            factor),
                        FeatureConditioned: true)
                    : new CalibrationUncertaintyPrediction(
                        observation.BaselineRange!,
                        FeatureConditioned: false));
        }

        CalibrationUncertaintyIntervalPerformance performance =
            CalibrationUncertaintyEvaluationMath.Performance(
                observations,
                observation => predictions[Key(observation)].Range);
        CalibrationUncertaintyFeatureBucketEvaluation[] buckets = BuildSupportSignalBuckets(
            observations,
            definition);
        int conditioned = predictions.Values.Count(prediction => prediction.FeatureConditioned);
        return new CalibrationUncertaintyFeatureEvaluation
        {
            FeatureId = definition.Id,
            ValueKind = definition.ValueKind,
            Monotonicity = definition.Monotonicity,
            WidthDriver = true,
            Availability = new CalibrationUncertaintyFeatureAvailabilitySummary
            {
                ObservationCount = observations.Count,
                AvailableCount = observations.Count,
                NotApplicableCount = 0,
                UnavailableCount = 0,
                AvailableRepositoryCount = observations.Select(
                        observation => observation.RepositoryId)
                    .Distinct(StringComparer.Ordinal).Count(),
            },
            ExpectedHoursSpearman = Correlate(
                observations,
                definition.Id,
                observation => observation.CandidateRange.Expected),
            AbsoluteResidualSpearman = Correlate(
                observations,
                definition.Id,
                observation => observation.AbsoluteResidualHours),
            NormalizedAbsoluteResidualSpearman = Correlate(
                observations,
                definition.Id,
                observation => observation.NormalizedAbsoluteResidual),
            CrossValidatedIntervals = performance,
            CoverageDeltaFromBaseline = CalibrationUncertaintyEvaluationMath.Difference(
                performance.ReviewedExpectedCoverage,
                baseline.ReviewedExpectedCoverage),
            MeanNormalizedWidthDeltaFromBaseline =
                CalibrationUncertaintyEvaluationMath.Round4(
                    performance.MeanNormalizedWidth - baseline.MeanNormalizedWidth),
            MeanIntervalMissDeltaFromBaseline =
                CalibrationUncertaintyEvaluationMath.Round4(
                    performance.MeanIntervalMissHours - baseline.MeanIntervalMissHours),
            ConditionedPredictionCount = conditioned,
            BaselineFallbackCount = observations.Count - conditioned,
            MonotonicBucketViolationCount = CountMonotonicViolations(
                buckets,
                definition.Monotonicity),
            Buckets = buckets,
            RepositoryFolds = BuildFeatureRepositoryFolds(observations, predictions),
        };
    }

    private static Dictionary<(string RepositoryId, CalibrationUncertaintyBucket Bucket), decimal>
        BuildSupportSignalFactors(
            IReadOnlyList<CalibrationUncertaintyObservation> observations,
            CalibrationUncertaintyFeatureDefinition definition)
    {
        Dictionary<(string RepositoryId, CalibrationUncertaintyBucket Bucket), decimal> result = [];
        foreach (IGrouping<CalibrationUncertaintyBucket, CalibrationUncertaintyObservation> group in
                 observations.GroupBy(observation => SupportSignalBucket(
                     definition.Id,
                     definition.ValueKind,
                     observation.Features[definition.Id].Value!.Value)))
        {
            CalibrationUncertaintyObservation[] members = [.. group];
            foreach (string repositoryId in members.Select(member => member.RepositoryId)
                         .Distinct(StringComparer.Ordinal))
            {
                CalibrationUncertaintyObservation[] training = [.. members.Where(member =>
                    !string.Equals(member.RepositoryId, repositoryId, StringComparison.Ordinal))];
                bool supported = training.Length >=
                        CalibrationUncertaintyEvaluationMath.MinimumBucketObservationCount &&
                    training.Select(candidate => candidate.RepositoryId)
                        .Distinct(StringComparer.Ordinal).Count() >=
                        CalibrationUncertaintyEvaluationMath.MinimumBucketRepositoryCount;
                if (supported)
                {
                    result.Add(
                        (repositoryId, group.Key),
                        CalibrationUncertaintyEvaluationMath.Quantile80(training.Select(
                            candidate => candidate.NormalizedAbsoluteResidual)));
                }
            }
        }

        return result;
    }

    private static CalibrationUncertaintyFeatureBucketEvaluation[] BuildSupportSignalBuckets(
        IReadOnlyList<CalibrationUncertaintyObservation> observations,
        CalibrationUncertaintyFeatureDefinition definition) =>
    [
        .. observations.GroupBy(observation => SupportSignalBucket(
                definition.Id,
                definition.ValueKind,
                observation.Features[definition.Id].Value!.Value))
            .OrderBy(group => group.Key.Order)
            .Select(group =>
            {
                CalibrationUncertaintyObservation[] members = [.. group];
                decimal[] values = [.. members.Select(observation =>
                    observation.Features[definition.Id].Value!.Value)];
                return new CalibrationUncertaintyFeatureBucketEvaluation
                {
                    Id = group.Key.Id,
                    Order = group.Key.Order,
                    ObservationCount = members.Length,
                    RepositoryCount = members.Select(member => member.RepositoryId)
                        .Distinct(StringComparer.Ordinal).Count(),
                    MinimumValue = CalibrationUncertaintyEvaluationMath.Round4(values.Min()),
                    MaximumValue = CalibrationUncertaintyEvaluationMath.Round4(values.Max()),
                    MeanValue = CalibrationUncertaintyEvaluationMath.Mean(values),
                    MeanNormalizedAbsoluteResidual =
                        CalibrationUncertaintyEvaluationMath.Mean(members.Select(
                            member => member.NormalizedAbsoluteResidual)),
                    Empirical80thPercentileNormalizedResidual =
                        CalibrationUncertaintyEvaluationMath.Round4(
                            CalibrationUncertaintyEvaluationMath.Quantile80(members.Select(
                                member => member.NormalizedAbsoluteResidual))),
                    CurrentIntervals = CalibrationUncertaintyEvaluationMath.Performance(
                        members,
                        member => member.CandidateRange),
                };
            }),
    ];

    private static CalibrationUncertaintyBucket SupportSignalBucket(
        string signalId,
        CalibrationUncertaintyFeatureValueKind kind,
        decimal value)
    {
        if (signalId == CalibrationUncertaintySupportSignalIds.FallbackDepth)
        {
            return value switch
            {
                0m => new("exact", 0),
                1m => new("category-size-ecosystem", 1),
                2m => new("category-size", 2),
                3m => new("category", 3),
                4m => new("global", 4),
                5m => new("insufficient", 5),
                _ => throw new InvalidOperationException(
                    $"Unsupported target support depth '{value}'."),
            };
        }

        if (signalId is CalibrationUncertaintySupportSignalIds.WeightedMeanOutOfDistribution or
            CalibrationUncertaintySupportSignalIds.MaximumOutOfDistribution)
        {
            return value switch
            {
                <= 0m => new("exact", 0),
                <= 0.008333m => new("very-near", 1),
                <= 0.025m => new("near", 2),
                <= 0.05m => new("moderate", 3),
                <= 0.1m => new("elevated", 4),
                _ => new("high", 5),
            };
        }

        return CalibrationUncertaintyEvaluationMath.Bucket(kind, value);
    }
}
