using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static partial class CalibrationUncertaintyEvaluator
{
    private static IReadOnlyList<CalibrationUncertaintyFeatureEvaluation>
        BuildFeatureEvaluations(
            IReadOnlyList<CalibrationUncertaintyObservation> observations,
            IReadOnlyList<CalibrationUncertaintyFeatureDefinition> definitions,
            CalibrationUncertaintyIntervalPerformance baseline) =>
    [
        .. definitions.Select(definition => BuildFeatureEvaluation(
            observations,
            definition,
            baseline)),
    ];

    private static CalibrationUncertaintyFeatureEvaluation BuildFeatureEvaluation(
        IReadOnlyList<CalibrationUncertaintyObservation> observations,
        CalibrationUncertaintyFeatureDefinition definition,
        CalibrationUncertaintyIntervalPerformance baseline)
    {
        CalibrationUncertaintyObservation[] available = [.. observations.Where(observation =>
            observation.Features[definition.Id].Availability ==
                CalibrationUncertaintyFeatureAvailability.Available)];
        Dictionary<(string RepositoryId, CalibrationUncertaintyBucket Bucket), decimal>
            supportedFactors = BuildSupportedFeatureFactors(available, definition);
        Dictionary<ObservationKey, CalibrationUncertaintyPrediction> predictions = [];
        foreach (CalibrationUncertaintyObservation observation in observations)
        {
            CalibrationUncertaintyAggregatedFeature value = observation.Features[definition.Id];
            CalibrationUncertaintyBucket? bucket = value.Availability ==
                CalibrationUncertaintyFeatureAvailability.Available
                    ? CalibrationUncertaintyEvaluationMath.Bucket(
                        definition.ValueKind,
                        value.Value!.Value)
                    : null;
            decimal factor = 0m;
            bool supported = bucket is not null && supportedFactors.TryGetValue(
                (observation.RepositoryId, bucket.Value),
                out factor);
            CalibrationUncertaintyPrediction prediction = supported
                ? new CalibrationUncertaintyPrediction(
                    CalibrationUncertaintyEvaluationMath.PredictRange(
                        observation.CandidateRange.Expected,
                        factor),
                    FeatureConditioned: true)
                : new CalibrationUncertaintyPrediction(
                    observation.BaselineRange!,
                    FeatureConditioned: false);
            predictions.Add(Key(observation), prediction);
        }

        CalibrationUncertaintyIntervalPerformance performance =
            CalibrationUncertaintyEvaluationMath.Performance(
                observations,
                observation => predictions[Key(observation)].Range);
        CalibrationUncertaintyFeatureBucketEvaluation[] buckets =
            BuildBuckets(available, definition);
        int conditioned = predictions.Values.Count(prediction => prediction.FeatureConditioned);
        return new CalibrationUncertaintyFeatureEvaluation
        {
            FeatureId = definition.Id,
            ValueKind = definition.ValueKind,
            Monotonicity = definition.Monotonicity,
            WidthDriver = definition.Monotonicity !=
                CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly,
            Availability = new CalibrationUncertaintyFeatureAvailabilitySummary
            {
                ObservationCount = observations.Count,
                AvailableCount = available.Length,
                NotApplicableCount = observations.Count(observation =>
                    observation.Features[definition.Id].Availability ==
                    CalibrationUncertaintyFeatureAvailability.NotApplicable),
                UnavailableCount = observations.Count(observation =>
                    observation.Features[definition.Id].Availability ==
                    CalibrationUncertaintyFeatureAvailability.Unavailable),
                AvailableRepositoryCount = available.Select(observation => observation.RepositoryId)
                    .Distinct(StringComparer.Ordinal).Count(),
            },
            ExpectedHoursSpearman = Correlate(
                available,
                definition.Id,
                observation => observation.CandidateRange.Expected),
            AbsoluteResidualSpearman = Correlate(
                available,
                definition.Id,
                observation => observation.AbsoluteResidualHours),
            NormalizedAbsoluteResidualSpearman = Correlate(
                available,
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

    private static IReadOnlyList<CalibrationUncertaintyFeatureRepositoryFold>
        BuildFeatureRepositoryFolds(
            IReadOnlyList<CalibrationUncertaintyObservation> observations,
            IReadOnlyDictionary<ObservationKey, CalibrationUncertaintyPrediction> predictions) =>
    [
        .. observations.GroupBy(observation => (
                observation.RecordId,
                observation.RepositoryId))
            .OrderBy(group => group.Key.RecordId, StringComparer.Ordinal)
            .Select(group =>
            {
                CalibrationUncertaintyObservation[] members = [.. group];
                int conditioned = members.Count(member =>
                    predictions[Key(member)].FeatureConditioned);
                return new CalibrationUncertaintyFeatureRepositoryFold
                {
                    RecordId = group.Key.RecordId,
                    RepositoryId = group.Key.RepositoryId,
                    ConditionedPredictionCount = conditioned,
                    BaselineFallbackCount = members.Length - conditioned,
                    Intervals = CalibrationUncertaintyEvaluationMath.Performance(
                        members,
                        member => predictions[Key(member)].Range),
                };
            }),
    ];

    private static Dictionary<(string RepositoryId, CalibrationUncertaintyBucket Bucket), decimal>
        BuildSupportedFeatureFactors(
            IReadOnlyList<CalibrationUncertaintyObservation> available,
            CalibrationUncertaintyFeatureDefinition definition)
    {
        Dictionary<(string RepositoryId, CalibrationUncertaintyBucket Bucket), decimal> result = [];
        foreach (IGrouping<CalibrationUncertaintyBucket, CalibrationUncertaintyObservation> group in
                 available.GroupBy(observation => CalibrationUncertaintyEvaluationMath.Bucket(
                     definition.ValueKind,
                     observation.Features[definition.Id].Value!.Value)))
        {
            CalibrationUncertaintyObservation[] members = [.. group];
            foreach (string repositoryId in members.Select(member => member.RepositoryId)
                         .Distinct(StringComparer.Ordinal))
            {
                CalibrationUncertaintyObservation[] training = [.. members.Where(member =>
                    !string.Equals(
                        member.RepositoryId,
                        repositoryId,
                        StringComparison.Ordinal))];
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

    private static CalibrationUncertaintyFeatureBucketEvaluation[] BuildBuckets(
        IReadOnlyList<CalibrationUncertaintyObservation> available,
        CalibrationUncertaintyFeatureDefinition definition) =>
    [
        .. available.GroupBy(observation => CalibrationUncertaintyEvaluationMath.Bucket(
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

    private static int CountMonotonicViolations(
        CalibrationUncertaintyFeatureBucketEvaluation[] buckets,
        CalibrationUncertaintyFeatureMonotonicity monotonicity)
    {
        if (monotonicity == CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly)
        {
            return 0;
        }

        int violations = 0;
        for (int index = 1; index < buckets.Length; index++)
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
            if (violation)
            {
                violations++;
            }
        }

        return violations;
    }

    private static decimal? Correlate(
        IEnumerable<CalibrationUncertaintyObservation> observations,
        string featureId,
        Func<CalibrationUncertaintyObservation, decimal> selector) =>
        CalibrationUncertaintyEvaluationMath.Spearman(
        [
            .. observations.Select(observation => (
                observation.Features[featureId].Value!.Value,
                selector(observation))),
        ]);

    private static IReadOnlyList<CalibrationUncertaintySliceEvaluation> BuildSlices(
        IReadOnlyList<CalibrationUncertaintyObservation> observations)
    {
        List<(CalibrationUncertaintySliceDimension Dimension, string Value,
            CalibrationUncertaintyObservation Observation)> rows = [];
        foreach (CalibrationUncertaintyObservation observation in observations)
        {
            rows.Add((
                CalibrationUncertaintySliceDimension.Category,
                JsonNamingPolicy.KebabCaseLower.ConvertName(observation.Category.ToString()),
                observation));
            rows.Add((
                CalibrationUncertaintySliceDimension.ExpectedSizeBand,
                observation.ExpectedSizeBand,
                observation));
            string[] ecosystems = observation.Ecosystems.Count == 0
                ? ["unknown"]
                : [.. observation.Ecosystems];
            rows.AddRange(ecosystems.Select(ecosystem => (
                CalibrationUncertaintySliceDimension.Ecosystem,
                ecosystem,
                observation)));
        }

        return [.. rows.GroupBy(row => (row.Dimension, row.Value))
            .OrderBy(group => group.Key.Dimension)
            .ThenBy(group => group.Key.Value, StringComparer.Ordinal)
            .Select(group =>
            {
                CalibrationUncertaintyObservation[] members =
                    [.. group.Select(row => row.Observation)];
                return new CalibrationUncertaintySliceEvaluation
                {
                    Dimension = group.Key.Dimension,
                    Value = group.Key.Value,
                    ObservationCount = members.Length,
                    RepositoryCount = members.Select(member => member.RepositoryId)
                        .Distinct(StringComparer.Ordinal).Count(),
                    CurrentIntervals = CalibrationUncertaintyEvaluationMath.Performance(
                        members,
                        member => member.CandidateRange),
                    CrossValidatedBaseline = CalibrationUncertaintyEvaluationMath.Performance(
                        members,
                        member => member.BaselineRange!),
                };
            })];
    }

    private static IReadOnlyList<CalibrationUncertaintyRepositoryEvaluation>
        BuildRepositoryEvaluations(
            IReadOnlyList<CalibrationUncertaintyRepositoryMatch> matches,
            IReadOnlyList<CalibrationUncertaintyObservation> observations) =>
    [
        .. matches.OrderBy(match => match.RecordId, StringComparer.Ordinal).Select(match =>
        {
            CalibrationUncertaintyObservation[] members = [.. observations.Where(observation =>
                observation.RecordId == match.RecordId)];
            return new CalibrationUncertaintyRepositoryEvaluation
            {
                RecordId = match.RecordId,
                RepositoryId = match.RepositoryId,
                SourceDigest = match.SourceDigest,
                FeatureReportDigest = match.FeatureReportDigest,
                EstimateDigest = match.EstimateDigest,
                TargetCount = match.TargetCount,
                MatchedTargetCount = match.MatchedTargetCount,
                SourceWorkItemReferenceCount = match.SourceWorkItemReferenceCount,
                MatchedSourceWorkItemReferenceCount = match.MatchedSourceWorkItemReferenceCount,
                CurrentIntervals = CalibrationUncertaintyEvaluationMath.Performance(
                    members,
                    member => member.CandidateRange),
                CrossValidatedBaseline = CalibrationUncertaintyEvaluationMath.Performance(
                    members,
                    member => member.BaselineRange!),
                UnmatchedTargetIds = match.UnmatchedTargetIds,
                CategoryMismatchTargetIds = match.CategoryMismatchTargetIds,
            };
        }),
    ];

    private static ObservationKey Key(CalibrationUncertaintyObservation observation) => new(
        observation.RecordId,
        observation.TargetId);

    private readonly record struct ObservationKey(string RecordId, string TargetId);
}
