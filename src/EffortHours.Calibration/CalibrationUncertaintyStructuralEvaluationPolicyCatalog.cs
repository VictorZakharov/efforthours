using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class CalibrationUncertaintyStructuralEvaluationPolicyCatalog
{
    public const string Version = CalibrationUncertaintyVersions.StructuralEvaluationPolicyV1;

    public static CalibrationUncertaintyStructuralEvaluationPolicy Current { get; } = new()
    {
        Version = Version,
        EffectiveDate = "2026-08-16",
        LabelIndependent = true,
        Rules =
        [
            Size(CalibrationUncertaintyStructuralFeatureIds.CallableSizeP50),
            Size(CalibrationUncertaintyStructuralFeatureIds.CallableSizeP90),
            Size(CalibrationUncertaintyStructuralFeatureIds.CallableSizeMaximum),
            HigherRatio(CalibrationUncertaintyStructuralFeatureIds.OversizedCallableShare),
            Complexity(CalibrationUncertaintyStructuralFeatureIds.DecisionComplexityP50),
            Complexity(CalibrationUncertaintyStructuralFeatureIds.DecisionComplexityP90),
            Complexity(CalibrationUncertaintyStructuralFeatureIds.DecisionComplexityMaximum),
            HigherRatio(
                CalibrationUncertaintyStructuralFeatureIds.HighDecisionComplexityShare),
            Nesting(CalibrationUncertaintyStructuralFeatureIds.NestingDepthP50),
            Nesting(CalibrationUncertaintyStructuralFeatureIds.NestingDepthP90),
            Nesting(CalibrationUncertaintyStructuralFeatureIds.NestingDepthMaximum),
            HigherRatio(CalibrationUncertaintyStructuralFeatureIds.DeepNestingShare),
            Ratio(
                CalibrationUncertaintyStructuralFeatureIds.CallableMeasurementCoverage,
                CalibrationUncertaintyStructuralTargetAggregation.Minimum,
                CalibrationUncertaintyStructuralResidualDirection.HigherValueLowerResidual),
            HigherRatio(
                CalibrationUncertaintyStructuralFeatureIds.AnalyzerAmbiguityConcentration),
        ],
    };

    internal static CalibrationUncertaintyBucket Bucket(string featureId, decimal value)
    {
        CalibrationUncertaintyStructuralEvaluationRule rule = Current.Rules.Single(candidate =>
            candidate.FeatureId == featureId);
        CalibrationUncertaintyStructuralBucketDefinition bucket = rule.Buckets.First(candidate =>
            candidate.InclusiveMaximum is null || value <= candidate.InclusiveMaximum.Value);
        return new CalibrationUncertaintyBucket(bucket.Id, bucket.Order);
    }

    private static CalibrationUncertaintyStructuralEvaluationRule Size(string id) => Rule(
        id,
        CalibrationUncertaintyFeatureValueKind.Count,
        CalibrationUncertaintyStructuralTargetAggregation.Maximum,
        CalibrationUncertaintyStructuralResidualDirection.HigherValueHigherResidual,
        ("up-to-25", 25m),
        ("26-to-50", 50m),
        ("51-to-100", 100m),
        ("101-to-200", 200m),
        ("above-200", null));

    private static CalibrationUncertaintyStructuralEvaluationRule Complexity(string id) => Rule(
        id,
        CalibrationUncertaintyFeatureValueKind.Count,
        CalibrationUncertaintyStructuralTargetAggregation.Maximum,
        CalibrationUncertaintyStructuralResidualDirection.HigherValueHigherResidual,
        ("one", 1m),
        ("two-to-five", 5m),
        ("six-to-ten", 10m),
        ("eleven-to-twenty", 20m),
        ("above-twenty", null));

    private static CalibrationUncertaintyStructuralEvaluationRule Nesting(string id) => Rule(
        id,
        CalibrationUncertaintyFeatureValueKind.Count,
        CalibrationUncertaintyStructuralTargetAggregation.Maximum,
        CalibrationUncertaintyStructuralResidualDirection.HigherValueHigherResidual,
        ("zero", 0m),
        ("one-to-two", 2m),
        ("three-to-four", 4m),
        ("five-to-six", 6m),
        ("above-six", null));

    private static CalibrationUncertaintyStructuralEvaluationRule HigherRatio(string id) => Ratio(
        id,
        CalibrationUncertaintyStructuralTargetAggregation.Maximum,
        CalibrationUncertaintyStructuralResidualDirection.HigherValueHigherResidual);

    private static CalibrationUncertaintyStructuralEvaluationRule Ratio(
        string id,
        CalibrationUncertaintyStructuralTargetAggregation aggregation,
        CalibrationUncertaintyStructuralResidualDirection direction) => Rule(
            id,
            CalibrationUncertaintyFeatureValueKind.Ratio,
            aggregation,
            direction,
            ("zero", 0m),
            ("up-to-0.25", 0.25m),
            ("0.25-to-0.50", 0.50m),
            ("0.50-to-0.75", 0.75m),
            ("above-0.75", null));

    private static CalibrationUncertaintyStructuralEvaluationRule Rule(
        string id,
        CalibrationUncertaintyFeatureValueKind kind,
        CalibrationUncertaintyStructuralTargetAggregation aggregation,
        CalibrationUncertaintyStructuralResidualDirection direction,
        params (string Id, decimal? InclusiveMaximum)[] buckets) => new()
        {
            FeatureId = id,
            ValueKind = kind,
            TargetAggregation = aggregation,
            ExpectedResidualDirection = direction,
            Buckets =
            [
                .. buckets.Select((bucket, index) =>
                    new CalibrationUncertaintyStructuralBucketDefinition
                    {
                        Id = bucket.Id,
                        Order = index,
                        InclusiveMaximum = bucket.InclusiveMaximum,
                    }),
            ],
        };
}
