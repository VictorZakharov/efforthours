using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationUncertaintyStructuralEvaluationMatcher
{
    public static CalibrationUncertaintyMatchedData Match(
        IReadOnlyList<(CalibrationRecord Record,
            CalibrationUncertaintyStructuralFeatureReport Report)> matches,
        CalibrationUncertaintyStructuralEvaluationPolicy policy)
    {
        List<CalibrationUncertaintyObservation> observations = [];
        List<CalibrationUncertaintyRepositoryMatch> repositories = [];
        int targetCount = 0;
        int sourceReferenceCount = 0;
        int matchedSourceReferenceCount = 0;

        foreach ((CalibrationRecord record,
                     CalibrationUncertaintyStructuralFeatureReport report) in matches)
        {
            Dictionary<string, CalibrationUncertaintyStructuralWorkItemFeatures> items =
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
                CalibrationUncertaintyStructuralWorkItemFeatures[] resolved =
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
                    Features = AggregateFeatures(resolved, policy),
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
        IReadOnlyList<CalibrationUncertaintyStructuralWorkItemFeatures> items,
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
        IReadOnlyList<CalibrationUncertaintyStructuralWorkItemFeatures> items,
        CalibrationUncertaintyStructuralEvaluationPolicy policy)
    {
        Dictionary<string, CalibrationUncertaintyAggregatedFeature> result =
            new(StringComparer.Ordinal);
        foreach (CalibrationUncertaintyStructuralEvaluationRule rule in policy.Rules)
        {
            CalibrationUncertaintyFeatureValue[] values =
            [
                .. items.Select(item => item.Features.Single(value =>
                    value.FeatureId == rule.FeatureId)),
            ];
            if (values.Any(value => value.Availability ==
                    CalibrationUncertaintyFeatureAvailability.Unavailable))
            {
                result.Add(rule.FeatureId, new CalibrationUncertaintyAggregatedFeature
                {
                    Availability = CalibrationUncertaintyFeatureAvailability.Unavailable,
                });
                continue;
            }

            CalibrationUncertaintyFeatureValue[] available =
            [
                .. values.Where(value => value.Availability ==
                    CalibrationUncertaintyFeatureAvailability.Available),
            ];
            if (available.Length == 0)
            {
                result.Add(rule.FeatureId, new CalibrationUncertaintyAggregatedFeature
                {
                    Availability = CalibrationUncertaintyFeatureAvailability.NotApplicable,
                });
                continue;
            }

            decimal value = rule.TargetAggregation switch
            {
                CalibrationUncertaintyStructuralTargetAggregation.Maximum =>
                    available.Max(candidate => candidate.Value!.Value),
                CalibrationUncertaintyStructuralTargetAggregation.Minimum =>
                    available.Min(candidate => candidate.Value!.Value),
                _ => throw new CalibrationEvaluationException(
                    [$"Unsupported structural target aggregation '{rule.TargetAggregation}'."]),
            };
            result.Add(rule.FeatureId, new CalibrationUncertaintyAggregatedFeature
            {
                Availability = CalibrationUncertaintyFeatureAvailability.Available,
                Value = value,
            });
        }

        return result;
    }
}
