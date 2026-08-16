using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationUncertaintyGraphEvaluationMatcher
{
    public static CalibrationUncertaintyMatchedData Match(
        IReadOnlyList<(CalibrationRecord Record,
            CalibrationUncertaintyGraphFeatureReport Report)> matches,
        CalibrationUncertaintyGraphEvaluationPolicy policy)
    {
        List<CalibrationUncertaintyObservation> observations = [];
        List<CalibrationUncertaintyRepositoryMatch> repositories = [];
        int targetCount = 0;
        int sourceReferenceCount = 0;
        int matchedSourceReferenceCount = 0;

        foreach ((CalibrationRecord record, CalibrationUncertaintyGraphFeatureReport report) in
                 matches)
        {
            Dictionary<string, CalibrationUncertaintyGraphWorkItemMapping> items =
                report.WorkItems.ToDictionary(item => item.WorkItemId, StringComparer.Ordinal);
            Dictionary<string, CalibrationUncertaintyGraphNode> nodes =
                report.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
            List<string> unmatchedTargetIds = [];
            List<string> categoryMismatchTargetIds = [];
            int recordSourceReferences = 0;
            int recordMatchedReferences = 0;
            int recordMatchedTargets = 0;

            foreach (CalibrationTarget target in record.Targets.OrderBy(
                         target => target.Id,
                         StringComparer.Ordinal))
            {
                targetCount++;
                recordSourceReferences += target.SourceWorkItemIds.Count;
                CalibrationUncertaintyGraphWorkItemMapping[] resolved =
                [
                    .. target.SourceWorkItemIds.Where(items.ContainsKey).Select(id => items[id]),
                ];
                recordMatchedReferences += resolved.Length;
                bool complete = resolved.Length == target.SourceWorkItemIds.Count;
                bool categoryMatches = complete && resolved.All(item =>
                    item.Category == target.Category);
                if (!categoryMatches)
                {
                    unmatchedTargetIds.Add(target.Id);
                    if (complete)
                    {
                        categoryMismatchTargetIds.Add(target.Id);
                    }

                    continue;
                }

                string[] nodeIds =
                [
                    .. resolved.SelectMany(item => item.NodeIds)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal),
                ];
                CalibrationUncertaintyGraphNode[] selectedNodes =
                    [.. nodeIds.Select(id => nodes[id])];
                EffortRange candidateRange = ContractValidation.Sum(
                    resolved.Select(item => item.SourceRange));
                decimal residual = decimal.Abs(candidateRange.Expected - target.Hours.Expected);
                observations.Add(new CalibrationUncertaintyObservation
                {
                    RecordId = record.Id,
                    RepositoryId = record.Repository.Id,
                    TargetId = target.Id,
                    Category = target.Category,
                    Ecosystems = ResolveEcosystems(selectedNodes, report.Ecosystems),
                    ExpectedSizeBand = CalibrationUncertaintyEvaluationMath.SizeBand(
                        candidateRange.Expected),
                    CandidateRange = candidateRange,
                    ReviewedRange = target.Hours,
                    AbsoluteResidualHours = residual,
                    NormalizedAbsoluteResidual =
                        CalibrationUncertaintyEvaluationMath.NormalizeResidual(
                            residual,
                            candidateRange.Expected),
                    Features = AggregateFeatures(selectedNodes, policy),
                    GraphNodeIds = nodeIds,
                });
                recordMatchedTargets++;
            }

            sourceReferenceCount += recordSourceReferences;
            matchedSourceReferenceCount += recordMatchedReferences;
            repositories.Add(new CalibrationUncertaintyRepositoryMatch
            {
                RecordId = record.Id,
                RepositoryId = record.Repository.Id,
                SourceDigest = record.Repository.SourceDigest,
                FeatureReportDigest = CalibrationDigest.Compute(report),
                EstimateDigest = report.EstimateDigest,
                TargetCount = record.Targets.Count,
                MatchedTargetCount = recordMatchedTargets,
                SourceWorkItemReferenceCount = recordSourceReferences,
                MatchedSourceWorkItemReferenceCount = recordMatchedReferences,
                UnmatchedTargetIds = [.. unmatchedTargetIds.Order(StringComparer.Ordinal)],
                CategoryMismatchTargetIds =
                    [.. categoryMismatchTargetIds.Order(StringComparer.Ordinal)],
            });
        }

        return new CalibrationUncertaintyMatchedData
        {
            Observations = observations,
            Repositories = repositories,
            TargetCount = targetCount,
            SourceWorkItemReferenceCount = sourceReferenceCount,
            MatchedSourceWorkItemReferenceCount = matchedSourceReferenceCount,
        };
    }

    private static string[] ResolveEcosystems(
        IReadOnlyList<CalibrationUncertaintyGraphNode> nodes,
        IReadOnlyList<string> repositoryEcosystems)
    {
        string[] ecosystems =
        [
            .. nodes.Select(node => node.Ecosystem)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        return ecosystems.Length > 0
            ? ecosystems
            : [.. repositoryEcosystems.Order(StringComparer.Ordinal)];
    }

    private static Dictionary<string, CalibrationUncertaintyAggregatedFeature> AggregateFeatures(
        IReadOnlyList<CalibrationUncertaintyGraphNode> nodes,
        CalibrationUncertaintyGraphEvaluationPolicy policy)
    {
        Dictionary<string, CalibrationUncertaintyAggregatedFeature> result =
            new(StringComparer.Ordinal);
        foreach (CalibrationUncertaintyGraphEvaluationRule rule in policy.Rules)
        {
            result.Add(rule.FeatureId, AggregateFeature(nodes, rule));
        }

        return result;
    }

    private static CalibrationUncertaintyAggregatedFeature AggregateFeature(
        IReadOnlyList<CalibrationUncertaintyGraphNode> nodes,
        CalibrationUncertaintyGraphEvaluationRule rule)
    {
        if (nodes.Count == 0)
        {
            return Feature(CalibrationUncertaintyFeatureAvailability.NotApplicable);
        }

        bool interfaceFeature = IsInterfaceFeature(rule.FeatureId);
        if (interfaceFeature && nodes.Any(node => node.PublicInterfaceAvailability ==
                CalibrationUncertaintyFeatureAvailability.Unavailable))
        {
            return Feature(CalibrationUncertaintyFeatureAvailability.Unavailable);
        }

        decimal[] interfaceValues =
        [
            .. nodes.Where(node => node.PublicInterfaceAvailability ==
                    CalibrationUncertaintyFeatureAvailability.Available)
                .Select(node => node.PublicInterfaceConcentration!.Value),
        ];
        if (interfaceFeature && interfaceValues.Length == 0)
        {
            return Feature(CalibrationUncertaintyFeatureAvailability.NotApplicable);
        }

        decimal value = rule.TargetAggregation switch
        {
            CalibrationUncertaintyGraphTargetAggregation.UniqueNodeNearestRankP50 =>
                NearestRank(Degrees(nodes, rule.FeatureId), 0.50m),
            CalibrationUncertaintyGraphTargetAggregation.UniqueNodeNearestRankP90 =>
                NearestRank(Degrees(nodes, rule.FeatureId), 0.90m),
            CalibrationUncertaintyGraphTargetAggregation.UniqueNodeMaximum =>
                Degrees(nodes, rule.FeatureId).Max(),
            CalibrationUncertaintyGraphTargetAggregation.UniqueNodeShareAboveThreshold =>
                HighDegreeShare(nodes, rule.FeatureId),
            CalibrationUncertaintyGraphTargetAggregation.UniqueNodeCyclicShare =>
                Round6((decimal)nodes.Count(node => node.Cyclic) / nodes.Count),
            CalibrationUncertaintyGraphTargetAggregation
                    .MaximumTouchedCyclicComponentShare =>
                nodes.Max(node => node.CyclicComponentNodeShare),
            CalibrationUncertaintyGraphTargetAggregation.AvailableInterfaceNearestRankP50 =>
                NearestRank(interfaceValues, 0.50m),
            CalibrationUncertaintyGraphTargetAggregation.AvailableInterfaceNearestRankP90 =>
                NearestRank(interfaceValues, 0.90m),
            CalibrationUncertaintyGraphTargetAggregation.AvailableInterfaceMaximum =>
                interfaceValues.Max(),
            CalibrationUncertaintyGraphTargetAggregation
                    .AvailableInterfaceShareAboveThreshold =>
                Round6((decimal)interfaceValues.Count(value => value >
                    CalibrationUncertaintyGraphFeatureCatalog.HighPublicInterfaceThreshold) /
                    interfaceValues.Length),
            _ => throw new CalibrationEvaluationException(
                [$"Unsupported graph target aggregation '{rule.TargetAggregation}'."]),
        };
        return Feature(CalibrationUncertaintyFeatureAvailability.Available, value);
    }

    private static decimal[] Degrees(
        IReadOnlyList<CalibrationUncertaintyGraphNode> nodes,
        string featureId) => featureId switch
        {
            CalibrationUncertaintyGraphFeatureIds.FanInP50 or
            CalibrationUncertaintyGraphFeatureIds.FanInP90 or
            CalibrationUncertaintyGraphFeatureIds.FanInMaximum =>
                [.. nodes.Select(node => (decimal)node.FanIn)],
            CalibrationUncertaintyGraphFeatureIds.FanOutP50 or
            CalibrationUncertaintyGraphFeatureIds.FanOutP90 or
            CalibrationUncertaintyGraphFeatureIds.FanOutMaximum =>
                [.. nodes.Select(node => (decimal)node.FanOut)],
            _ => throw new CalibrationEvaluationException(
                [$"Graph degree aggregation does not support feature '{featureId}'."]),
        };

    private static decimal HighDegreeShare(
        IReadOnlyList<CalibrationUncertaintyGraphNode> nodes,
        string featureId)
    {
        int count = featureId switch
        {
            CalibrationUncertaintyGraphFeatureIds.HighFanInShare =>
                nodes.Count(node => node.FanIn >
                    CalibrationUncertaintyGraphFeatureCatalog.HighFanDegreeThreshold),
            CalibrationUncertaintyGraphFeatureIds.HighFanOutShare =>
                nodes.Count(node => node.FanOut >
                    CalibrationUncertaintyGraphFeatureCatalog.HighFanDegreeThreshold),
            _ => throw new CalibrationEvaluationException(
                [$"Graph degree-share aggregation does not support feature '{featureId}'."]),
        };
        return Round6((decimal)count / nodes.Count);
    }

    private static bool IsInterfaceFeature(string featureId) => featureId is
        CalibrationUncertaintyGraphFeatureIds.PublicInterfaceP50 or
        CalibrationUncertaintyGraphFeatureIds.PublicInterfaceP90 or
        CalibrationUncertaintyGraphFeatureIds.PublicInterfaceMaximum or
        CalibrationUncertaintyGraphFeatureIds.HighPublicInterfaceShare;

    private static decimal NearestRank(decimal[] values, decimal quantile)
    {
        decimal[] sorted = [.. values.Order()];
        int rank = (int)decimal.Ceiling(quantile * sorted.Length);
        return sorted[Math.Max(0, rank - 1)];
    }

    private static CalibrationUncertaintyAggregatedFeature Feature(
        CalibrationUncertaintyFeatureAvailability availability,
        decimal? value = null) => new()
        {
            Availability = availability,
            Value = value,
        };

    private static decimal Round6(decimal value) =>
        decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}
