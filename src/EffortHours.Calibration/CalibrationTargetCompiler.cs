using System.Text.RegularExpressions;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationTargetCompiler
{
    private static readonly Regex TitlePartSuffix = new(
        @" \(part \d+ of \d+\)$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static IReadOnlyList<CalibrationTarget> Compile(
        string recordId,
        IReadOnlyList<CalibrationCapabilityReviewDecision> plannedCapabilities,
        IReadOnlyList<WorkItem> sourceItems,
        List<string> errors)
    {
        Dictionary<string, WorkItem[]> capabilities = sourceItems
            .GroupBy(item => CalibrationAuthoring.GetSourceCapabilityId(item.Id))
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        Dictionary<string, CalibrationCapabilityReviewDecision> decisions = plannedCapabilities
            .ToDictionary(decision => decision.SourceCapabilityId, StringComparer.Ordinal);

        foreach (string missing in capabilities.Keys
                     .Except(decisions.Keys, StringComparer.Ordinal)
                     .OrderBy(id => id, StringComparer.Ordinal))
        {
            errors.Add(
                $"Review-plan record '{recordId}' has no decision for source capability '{missing}'.");
        }

        foreach (string unknown in decisions.Keys
                     .Except(capabilities.Keys, StringComparer.Ordinal)
                     .OrderBy(id => id, StringComparer.Ordinal))
        {
            errors.Add(
                $"Review-plan record '{recordId}' references unknown source capability '{unknown}'.");
        }

        if (errors.Count > 0)
        {
            return [];
        }

        List<CalibrationTarget> targets = [];
        foreach (CalibrationCapabilityReviewDecision decision in plannedCapabilities
                     .OrderBy(item => item.SourceCapabilityId, StringComparer.Ordinal))
        {
            WorkItem[] items = capabilities[decision.SourceCapabilityId];
            if (decision.Targets.Count > items.Length)
            {
                errors.Add(
                    $"Capability '{decision.SourceCapabilityId}' in '{recordId}' has " +
                    $"{decision.Targets.Count} reviewed targets but only {items.Length} source work items.");
                continue;
            }

            EffortCategory category = items[0].Category;
            string scope = items[0].Scope;
            if (items.Any(item => item.Category != category ||
                                  !string.Equals(item.Scope, scope, StringComparison.Ordinal)))
            {
                errors.Add(
                    $"Source capability '{decision.SourceCapabilityId}' in '{recordId}' " +
                    "contains mixed categories or scopes and cannot be compiled automatically.");
                continue;
            }

            string title = TitlePartSuffix.Replace(items[0].Title, string.Empty);
            int cursor = 0;
            for (int index = 0; index < decision.Targets.Count; index++)
            {
                int remainingItems = items.Length - cursor;
                int remainingTargets = decision.Targets.Count - index;
                int take = (remainingItems + remainingTargets - 1) / remainingTargets;
                WorkItem[] assigned = items[cursor..(cursor + take)];
                cursor += take;

                CalibrationReviewTargetDecision reviewed = decision.Targets[index];
                targets.Add(new CalibrationTarget
                {
                    Id = $"target:{decision.SourceCapabilityId}:review-{index + 1:D4}",
                    Category = category,
                    Title = decision.Targets.Count == 1
                        ? title
                        : $"{title} (review target {index + 1} of {decision.Targets.Count})",
                    Scope = scope,
                    SourceWorkItemIds = [.. assigned.Select(item => item.Id)],
                    EvidenceIds = [.. assigned
                        .SelectMany(item => item.EvidenceIds)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(id => id, StringComparer.Ordinal)],
                    Hours = reviewed.Hours,
                    Rationale = decision.Rationale,
                    UncertaintyReasons = reviewed.UncertaintyReasons,
                    SizeException = reviewed.SizeException,
                });
            }
        }

        return targets;
    }
}
