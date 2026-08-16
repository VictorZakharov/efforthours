using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateGraphEvaluationPolicy(
        CalibrationUncertaintyGraphEvaluationPolicy policy,
        List<string> errors)
    {
        if (policy.Version != CalibrationUncertaintyVersions.GraphEvaluationPolicyV1 ||
            policy.EffectiveDate != "2026-08-16" ||
            !policy.LabelIndependent)
        {
            errors.Add("Graph uncertainty evaluation policy does not satisfy frozen v1 lineage.");
        }

        if (policy.Rules.Count != GraphPolicyBoundary.Length)
        {
            errors.Add(
                $"The frozen graph evaluation policy must contain all " +
                $"{GraphPolicyBoundary.Length} rules.");
        }

        HashSet<string> featureIds = new(StringComparer.Ordinal);
        for (int index = 0; index < policy.Rules.Count; index++)
        {
            CalibrationUncertaintyGraphEvaluationRule rule = policy.Rules[index];
            string path = $"evaluationPolicy.rule[{rule.FeatureId}]";
            RequireText(rule.FeatureId, $"{path}.featureId", errors);
            if (!featureIds.Add(rule.FeatureId))
            {
                errors.Add($"{path} is duplicated.");
            }

            if (index >= GraphPolicyBoundary.Length)
            {
                continue;
            }

            GraphPolicyRule expected = GraphPolicyBoundary[index];
            if (rule.FeatureId != expected.FeatureId ||
                rule.ValueKind != expected.ValueKind ||
                rule.TargetAggregation != expected.TargetAggregation ||
                rule.ExpectedResidualDirection !=
                    CalibrationUncertaintyGraphResidualDirection.HigherValueHigherResidual ||
                rule.Buckets.Count != expected.Buckets.Count)
            {
                errors.Add($"{path} does not match the frozen v1 rule.");
                continue;
            }

            for (int bucketIndex = 0; bucketIndex < rule.Buckets.Count; bucketIndex++)
            {
                CalibrationUncertaintyGraphBucketDefinition bucket = rule.Buckets[bucketIndex];
                GraphPolicyBucket expectedBucket = expected.Buckets[bucketIndex];
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

    private static readonly GraphPolicyRule[] GraphPolicyBoundary =
    [
        Count("graph.local-fan-in-p50",
            CalibrationUncertaintyGraphTargetAggregation.UniqueNodeNearestRankP50),
        Count("graph.local-fan-in-p90",
            CalibrationUncertaintyGraphTargetAggregation.UniqueNodeNearestRankP90),
        Count("graph.local-fan-in-maximum",
            CalibrationUncertaintyGraphTargetAggregation.UniqueNodeMaximum),
        Ratio("graph.local-high-fan-in-share",
            CalibrationUncertaintyGraphTargetAggregation.UniqueNodeShareAboveThreshold),
        Count("graph.local-fan-out-p50",
            CalibrationUncertaintyGraphTargetAggregation.UniqueNodeNearestRankP50),
        Count("graph.local-fan-out-p90",
            CalibrationUncertaintyGraphTargetAggregation.UniqueNodeNearestRankP90),
        Count("graph.local-fan-out-maximum",
            CalibrationUncertaintyGraphTargetAggregation.UniqueNodeMaximum),
        Ratio("graph.local-high-fan-out-share",
            CalibrationUncertaintyGraphTargetAggregation.UniqueNodeShareAboveThreshold),
        Ratio("graph.local-cyclic-node-share",
            CalibrationUncertaintyGraphTargetAggregation.UniqueNodeCyclicShare),
        Ratio(
            "graph.local-largest-cyclic-component-share",
            CalibrationUncertaintyGraphTargetAggregation.MaximumTouchedCyclicComponentShare),
        Ratio("shape.local-public-interface-p50",
            CalibrationUncertaintyGraphTargetAggregation.AvailableInterfaceNearestRankP50),
        Ratio("shape.local-public-interface-p90",
            CalibrationUncertaintyGraphTargetAggregation.AvailableInterfaceNearestRankP90),
        Ratio("shape.local-public-interface-maximum",
            CalibrationUncertaintyGraphTargetAggregation.AvailableInterfaceMaximum),
        Ratio(
            "shape.local-high-public-interface-share",
            CalibrationUncertaintyGraphTargetAggregation.AvailableInterfaceShareAboveThreshold),
    ];

    private static GraphPolicyRule Count(
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

    private static GraphPolicyRule Ratio(
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

    private static GraphPolicyRule Rule(
        string id,
        CalibrationUncertaintyFeatureValueKind kind,
        CalibrationUncertaintyGraphTargetAggregation aggregation,
        params (string Id, decimal? InclusiveMaximum)[] buckets) => new(
            id,
            kind,
            aggregation,
            [.. buckets.Select((bucket, index) => new GraphPolicyBucket(
                bucket.Id,
                index,
                bucket.InclusiveMaximum))]);

    private sealed record GraphPolicyRule(
        string FeatureId,
        CalibrationUncertaintyFeatureValueKind ValueKind,
        CalibrationUncertaintyGraphTargetAggregation TargetAggregation,
        IReadOnlyList<GraphPolicyBucket> Buckets);

    private sealed record GraphPolicyBucket(
        string Id,
        int Order,
        decimal? InclusiveMaximum);
}
