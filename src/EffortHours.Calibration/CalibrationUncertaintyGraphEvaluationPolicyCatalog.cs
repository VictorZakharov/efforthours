using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class CalibrationUncertaintyGraphEvaluationPolicyCatalog
{
    public const string Version = CalibrationUncertaintyVersions.GraphEvaluationPolicyV1;

    public static CalibrationUncertaintyGraphEvaluationPolicy Current { get; } = new()
    {
        Version = Version,
        EffectiveDate = "2026-08-16",
        LabelIndependent = true,
        Rules =
        [
            Count(CalibrationUncertaintyGraphFeatureIds.FanInP50,
                CalibrationUncertaintyGraphTargetAggregation.UniqueNodeNearestRankP50),
            Count(CalibrationUncertaintyGraphFeatureIds.FanInP90,
                CalibrationUncertaintyGraphTargetAggregation.UniqueNodeNearestRankP90),
            Count(CalibrationUncertaintyGraphFeatureIds.FanInMaximum,
                CalibrationUncertaintyGraphTargetAggregation.UniqueNodeMaximum),
            Ratio(CalibrationUncertaintyGraphFeatureIds.HighFanInShare,
                CalibrationUncertaintyGraphTargetAggregation.UniqueNodeShareAboveThreshold),
            Count(CalibrationUncertaintyGraphFeatureIds.FanOutP50,
                CalibrationUncertaintyGraphTargetAggregation.UniqueNodeNearestRankP50),
            Count(CalibrationUncertaintyGraphFeatureIds.FanOutP90,
                CalibrationUncertaintyGraphTargetAggregation.UniqueNodeNearestRankP90),
            Count(CalibrationUncertaintyGraphFeatureIds.FanOutMaximum,
                CalibrationUncertaintyGraphTargetAggregation.UniqueNodeMaximum),
            Ratio(CalibrationUncertaintyGraphFeatureIds.HighFanOutShare,
                CalibrationUncertaintyGraphTargetAggregation.UniqueNodeShareAboveThreshold),
            Ratio(CalibrationUncertaintyGraphFeatureIds.CyclicNodeShare,
                CalibrationUncertaintyGraphTargetAggregation.UniqueNodeCyclicShare),
            Ratio(CalibrationUncertaintyGraphFeatureIds.LargestCyclicComponentShare,
                CalibrationUncertaintyGraphTargetAggregation
                    .MaximumTouchedCyclicComponentShare),
            Ratio(CalibrationUncertaintyGraphFeatureIds.PublicInterfaceP50,
                CalibrationUncertaintyGraphTargetAggregation.AvailableInterfaceNearestRankP50),
            Ratio(CalibrationUncertaintyGraphFeatureIds.PublicInterfaceP90,
                CalibrationUncertaintyGraphTargetAggregation.AvailableInterfaceNearestRankP90),
            Ratio(CalibrationUncertaintyGraphFeatureIds.PublicInterfaceMaximum,
                CalibrationUncertaintyGraphTargetAggregation.AvailableInterfaceMaximum),
            Ratio(CalibrationUncertaintyGraphFeatureIds.HighPublicInterfaceShare,
                CalibrationUncertaintyGraphTargetAggregation
                    .AvailableInterfaceShareAboveThreshold),
        ],
    };

    internal static CalibrationUncertaintyBucket Bucket(string featureId, decimal value)
    {
        CalibrationUncertaintyGraphEvaluationRule rule = Current.Rules.Single(candidate =>
            candidate.FeatureId == featureId);
        CalibrationUncertaintyGraphBucketDefinition bucket = rule.Buckets.First(candidate =>
            candidate.InclusiveMaximum is null || value <= candidate.InclusiveMaximum.Value);
        return new CalibrationUncertaintyBucket(bucket.Id, bucket.Order);
    }

    private static CalibrationUncertaintyGraphEvaluationRule Count(
        string id,
        CalibrationUncertaintyGraphTargetAggregation aggregation) => Rule(
            id,
            CalibrationUncertaintyFeatureValueKind.Count,
            aggregation,
            ("zero", 0m),
            ("one", 1m),
            ("two-to-three", 3m),
            ("four-to-seven", 7m),
            ("eight-plus", null));

    private static CalibrationUncertaintyGraphEvaluationRule Ratio(
        string id,
        CalibrationUncertaintyGraphTargetAggregation aggregation) => Rule(
            id,
            CalibrationUncertaintyFeatureValueKind.Ratio,
            aggregation,
            ("zero", 0m),
            ("up-to-0.25", 0.25m),
            ("0.25-to-0.50", 0.50m),
            ("0.50-to-0.75", 0.75m),
            ("above-0.75", null));

    private static CalibrationUncertaintyGraphEvaluationRule Rule(
        string id,
        CalibrationUncertaintyFeatureValueKind kind,
        CalibrationUncertaintyGraphTargetAggregation aggregation,
        params (string Id, decimal? InclusiveMaximum)[] buckets) => new()
        {
            FeatureId = id,
            ValueKind = kind,
            TargetAggregation = aggregation,
            ExpectedResidualDirection =
                CalibrationUncertaintyGraphResidualDirection.HigherValueHigherResidual,
            Buckets =
            [
                .. buckets.Select((bucket, index) =>
                    new CalibrationUncertaintyGraphBucketDefinition
                    {
                        Id = bucket.Id,
                        Order = index,
                        InclusiveMaximum = bucket.InclusiveMaximum,
                    }),
            ],
        };
}
