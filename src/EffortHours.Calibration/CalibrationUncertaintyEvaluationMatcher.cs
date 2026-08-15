using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationUncertaintyEvaluationMatcher
{
    public static CalibrationUncertaintyMatchedData Match(
        IReadOnlyList<(CalibrationRecord Record, CalibrationUncertaintyFeatureReport Report)> matches)
    {
        List<CalibrationUncertaintyObservation> observations = [];
        List<CalibrationUncertaintyRepositoryMatch> repositories = [];
        int targetCount = 0;
        int sourceReferenceCount = 0;
        int matchedSourceReferenceCount = 0;

        foreach ((CalibrationRecord record, CalibrationUncertaintyFeatureReport report) in matches)
        {
            Dictionary<string, CalibrationUncertaintyWorkItemFeatures> items =
                report.WorkItems.ToDictionary(item => item.WorkItemId, StringComparer.Ordinal);
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
                CalibrationUncertaintyWorkItemFeatures[] resolved =
                [
                    .. target.SourceWorkItemIds
                        .Where(items.ContainsKey)
                        .Select(id => items[id]),
                ];
                recordMatchedReferences += resolved.Length;
                bool complete = resolved.Length == target.SourceWorkItemIds.Count;
                bool categoryMatches = complete && resolved.All(
                    item => item.Category == target.Category);
                if (!categoryMatches)
                {
                    unmatchedTargetIds.Add(target.Id);
                    if (complete)
                    {
                        categoryMismatchTargetIds.Add(target.Id);
                    }

                    continue;
                }

                EffortRange candidateRange = ContractValidation.Sum(
                    resolved.Select(item => item.SourceRange));
                decimal residual = decimal.Abs(candidateRange.Expected - target.Hours.Expected);
                observations.Add(new CalibrationUncertaintyObservation
                {
                    RecordId = record.Id,
                    RepositoryId = record.Repository.Id,
                    TargetId = target.Id,
                    Category = target.Category,
                    Ecosystems = ResolveEcosystems(resolved, report.Ecosystems),
                    ExpectedSizeBand = CalibrationUncertaintyEvaluationMath.SizeBand(
                        candidateRange.Expected),
                    CandidateRange = candidateRange,
                    ReviewedRange = target.Hours,
                    AbsoluteResidualHours = residual,
                    NormalizedAbsoluteResidual =
                        CalibrationUncertaintyEvaluationMath.NormalizeResidual(
                            residual,
                            candidateRange.Expected),
                    Features = AggregateFeatures(resolved, report.FeatureContract.Features),
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
        IReadOnlyList<CalibrationUncertaintyWorkItemFeatures> items,
        IReadOnlyList<string> repositoryEcosystems)
    {
        string[] ecosystems =
        [
            .. items.SelectMany(item => item.Ecosystems)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        return ecosystems.Length > 0
            ? ecosystems
            : [.. repositoryEcosystems.Order(StringComparer.Ordinal)];
    }

    private static Dictionary<string, CalibrationUncertaintyAggregatedFeature> AggregateFeatures(
            IReadOnlyList<CalibrationUncertaintyWorkItemFeatures> items,
            IReadOnlyList<CalibrationUncertaintyFeatureDefinition> definitions)
    {
        Dictionary<string, CalibrationUncertaintyAggregatedFeature> result =
            new(StringComparer.Ordinal);
        foreach (CalibrationUncertaintyFeatureDefinition definition in definitions)
        {
            (CalibrationUncertaintyWorkItemFeatures Item,
                CalibrationUncertaintyFeatureValue Feature)[] values =
            [
                .. items.Select(item => (
                    item,
                    item.Features.Single(value => value.FeatureId == definition.Id))),
            ];
            if (values.Any(value =>
                    value.Feature.Availability ==
                    CalibrationUncertaintyFeatureAvailability.Unavailable))
            {
                result.Add(definition.Id, new CalibrationUncertaintyAggregatedFeature
                {
                    Availability = CalibrationUncertaintyFeatureAvailability.Unavailable,
                });
                continue;
            }

            (CalibrationUncertaintyWorkItemFeatures Item,
                CalibrationUncertaintyFeatureValue Feature)[] available =
            [
                .. values.Where(value =>
                    value.Feature.Availability ==
                    CalibrationUncertaintyFeatureAvailability.Available),
            ];
            if (available.Length == 0)
            {
                result.Add(definition.Id, new CalibrationUncertaintyAggregatedFeature
                {
                    Availability = CalibrationUncertaintyFeatureAvailability.NotApplicable,
                });
                continue;
            }

            result.Add(definition.Id, new CalibrationUncertaintyAggregatedFeature
            {
                Availability = CalibrationUncertaintyFeatureAvailability.Available,
                Value = AggregateValue(definition.ValueKind, available),
            });
        }

        return result;
    }

    private static decimal AggregateValue(
        CalibrationUncertaintyFeatureValueKind kind,
        IReadOnlyList<(CalibrationUncertaintyWorkItemFeatures Item,
            CalibrationUncertaintyFeatureValue Feature)> values) => kind switch
            {
                CalibrationUncertaintyFeatureValueKind.Count =>
                    values.Sum(value => value.Feature.Value!.Value),
                CalibrationUncertaintyFeatureValueKind.Ordinal =>
                    values.Max(value => value.Feature.Value!.Value),
                CalibrationUncertaintyFeatureValueKind.Ratio or
                CalibrationUncertaintyFeatureValueKind.Rate => WeightedMean(values),
                _ => throw new CalibrationEvaluationException(
                    [$"Active distribution feature values are not supported by evaluation protocol v1."]),
            };

    private static decimal WeightedMean(
        IReadOnlyList<(CalibrationUncertaintyWorkItemFeatures Item,
            CalibrationUncertaintyFeatureValue Feature)> values)
    {
        decimal weight = values.Sum(value => value.Item.ExpectedHours);
        decimal result = weight > 0m
            ? values.Sum(value => value.Feature.Value!.Value * value.Item.ExpectedHours) / weight
            : values.Average(value => value.Feature.Value!.Value);
        return CalibrationUncertaintyEvaluationMath.Round6(result);
    }
}
