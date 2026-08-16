using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateStructuralEvaluationPolicy(
        CalibrationUncertaintyStructuralEvaluationPolicy policy,
        List<string> errors)
    {
        if (policy.Version != CalibrationUncertaintyVersions.StructuralEvaluationPolicyV1 ||
            policy.EffectiveDate != "2026-08-16" ||
            !policy.LabelIndependent)
        {
            errors.Add(
                "Structural uncertainty evaluation policy does not satisfy frozen v1 lineage.");
        }

        if (policy.Rules.Count != StructuralPolicyBoundary.Length)
        {
            errors.Add(
                $"The frozen structural evaluation policy must contain all " +
                $"{StructuralPolicyBoundary.Length} rules.");
        }

        HashSet<string> featureIds = new(StringComparer.Ordinal);
        for (int index = 0; index < policy.Rules.Count; index++)
        {
            CalibrationUncertaintyStructuralEvaluationRule rule = policy.Rules[index];
            string path = $"evaluationPolicy.rule[{rule.FeatureId}]";
            RequireText(rule.FeatureId, $"{path}.featureId", errors);
            if (!featureIds.Add(rule.FeatureId))
            {
                errors.Add($"{path} is duplicated.");
            }

            if (index >= StructuralPolicyBoundary.Length)
            {
                continue;
            }

            StructuralPolicyRule expected = StructuralPolicyBoundary[index];
            if (rule.FeatureId != expected.FeatureId ||
                rule.ValueKind != expected.ValueKind ||
                rule.TargetAggregation != expected.TargetAggregation ||
                rule.ExpectedResidualDirection != expected.ResidualDirection ||
                rule.Buckets.Count != expected.Buckets.Count)
            {
                errors.Add($"{path} does not match the frozen v1 rule.");
                continue;
            }

            for (int bucketIndex = 0; bucketIndex < rule.Buckets.Count; bucketIndex++)
            {
                CalibrationUncertaintyStructuralBucketDefinition bucket =
                    rule.Buckets[bucketIndex];
                StructuralPolicyBucket expectedBucket = expected.Buckets[bucketIndex];
                if (bucket.Id != expectedBucket.Id ||
                    bucket.Order != expectedBucket.Order ||
                    bucket.InclusiveMaximum != expectedBucket.InclusiveMaximum)
                {
                    errors.Add(
                        $"{path}.bucket[{bucket.Id}] does not match the frozen v1 boundary.");
                }
            }
        }
    }

    private static readonly StructuralPolicyRule[] StructuralPolicyBoundary =
    [
        Size("shape.local-callable-size-p50"),
        Size("shape.local-callable-size-p90"),
        Size("shape.local-callable-size-maximum"),
        HigherRatio("shape.local-oversized-callable-share"),
        Complexity("shape.local-decision-complexity-p50"),
        Complexity("shape.local-decision-complexity-p90"),
        Complexity("shape.local-decision-complexity-maximum"),
        HigherRatio("shape.local-high-decision-complexity-share"),
        Nesting("shape.local-nesting-depth-p50"),
        Nesting("shape.local-nesting-depth-p90"),
        Nesting("shape.local-nesting-depth-maximum"),
        HigherRatio("shape.local-deep-nesting-share"),
        Ratio(
            "analysis.callable-structural-measurement-coverage",
            CalibrationUncertaintyStructuralTargetAggregation.Minimum,
            CalibrationUncertaintyStructuralResidualDirection.HigherValueLowerResidual),
        HigherRatio("analysis.structural-analyzer-ambiguity-concentration"),
    ];

    private static StructuralPolicyRule Size(string id) => PolicyRule(
        id,
        CalibrationUncertaintyFeatureValueKind.Count,
        CalibrationUncertaintyStructuralTargetAggregation.Maximum,
        CalibrationUncertaintyStructuralResidualDirection.HigherValueHigherResidual,
        ("up-to-25", 25m),
        ("26-to-50", 50m),
        ("51-to-100", 100m),
        ("101-to-200", 200m),
        ("above-200", null));

    private static StructuralPolicyRule Complexity(string id) => PolicyRule(
        id,
        CalibrationUncertaintyFeatureValueKind.Count,
        CalibrationUncertaintyStructuralTargetAggregation.Maximum,
        CalibrationUncertaintyStructuralResidualDirection.HigherValueHigherResidual,
        ("one", 1m),
        ("two-to-five", 5m),
        ("six-to-ten", 10m),
        ("eleven-to-twenty", 20m),
        ("above-twenty", null));

    private static StructuralPolicyRule Nesting(string id) => PolicyRule(
        id,
        CalibrationUncertaintyFeatureValueKind.Count,
        CalibrationUncertaintyStructuralTargetAggregation.Maximum,
        CalibrationUncertaintyStructuralResidualDirection.HigherValueHigherResidual,
        ("zero", 0m),
        ("one-to-two", 2m),
        ("three-to-four", 4m),
        ("five-to-six", 6m),
        ("above-six", null));

    private static StructuralPolicyRule HigherRatio(string id) => Ratio(
        id,
        CalibrationUncertaintyStructuralTargetAggregation.Maximum,
        CalibrationUncertaintyStructuralResidualDirection.HigherValueHigherResidual);

    private static StructuralPolicyRule Ratio(
        string id,
        CalibrationUncertaintyStructuralTargetAggregation aggregation,
        CalibrationUncertaintyStructuralResidualDirection direction) => PolicyRule(
            id,
            CalibrationUncertaintyFeatureValueKind.Ratio,
            aggregation,
            direction,
            ("zero", 0m),
            ("up-to-0.25", 0.25m),
            ("0.25-to-0.50", 0.50m),
            ("0.50-to-0.75", 0.75m),
            ("above-0.75", null));

    private static StructuralPolicyRule PolicyRule(
        string id,
        CalibrationUncertaintyFeatureValueKind kind,
        CalibrationUncertaintyStructuralTargetAggregation aggregation,
        CalibrationUncertaintyStructuralResidualDirection direction,
        params (string Id, decimal? InclusiveMaximum)[] buckets) => new(
            id,
            kind,
            aggregation,
            direction,
            [.. buckets.Select((bucket, index) => new StructuralPolicyBucket(
                bucket.Id,
                index,
                bucket.InclusiveMaximum))]);

    private sealed record StructuralPolicyRule(
        string FeatureId,
        CalibrationUncertaintyFeatureValueKind ValueKind,
        CalibrationUncertaintyStructuralTargetAggregation TargetAggregation,
        CalibrationUncertaintyStructuralResidualDirection ResidualDirection,
        IReadOnlyList<StructuralPolicyBucket> Buckets);

    private sealed record StructuralPolicyBucket(
        string Id,
        int Order,
        decimal? InclusiveMaximum);
}
