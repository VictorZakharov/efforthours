using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(ChangeEstimateExplanation explanation)
    {
        ArgumentNullException.ThrowIfNull(explanation);

        List<string> errors = [];
        RequireVersion(explanation.SchemaVersion, "change estimate explanation", errors);
        RequireVersion(
            explanation.SourceChangeEstimateSchemaVersion,
            "source change estimate",
            errors);
        RequireText(explanation.EstimatorVersion, "estimatorVersion", errors);
        ValidateChangeSelection(explanation.Selection, errors);
        RequireText(explanation.RequestedId, "requestedId", errors);
        bool workItemMode = explanation.WorkItems.Count > 0;
        bool normalizationMode = explanation.Normalization is not null;
        if (workItemMode == normalizationMode)
        {
            errors.Add(
                "A change estimate explanation must contain either one work item or one normalization summary.");
        }

        HashSet<string> evidenceIds = explanation.Evidence
            .Select(evidence => evidence.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (WorkItem item in explanation.WorkItems)
        {
            if (!string.Equals(item.Id, explanation.RequestedId, StringComparison.Ordinal))
            {
                errors.Add($"Explanation work item '{item.Id}' does not match the requested ID.");
            }

            ValidateEvidenceReferences(
                item.EvidenceIds,
                explanation.UnresolvedEvidenceIds,
                evidenceIds,
                errors);
        }

        if (explanation.Normalization is not null)
        {
            if (!string.Equals(
                    explanation.Normalization.Id,
                    explanation.RequestedId,
                    StringComparison.Ordinal))
            {
                errors.Add("Explanation normalization ID does not match the requested ID.");
            }

            if (explanation.Adjustments is null)
            {
                errors.Add("A normalization explanation must contain its adjustment lineage.");
            }
            else
            {
                HashSet<string> expectedAdjustmentIds = NormalizationAdjustmentIds(
                    explanation.Normalization);
                HashSet<string> actualAdjustmentIds = explanation.Adjustments
                    .Select(adjustment => adjustment.Id)
                    .ToHashSet(StringComparer.Ordinal);
                if (!expectedAdjustmentIds.SetEquals(actualAdjustmentIds))
                {
                    errors.Add("Normalization explanation adjustments do not match its calculation lineage.");
                }

                foreach (ChangeAdjustment adjustment in explanation.Adjustments)
                {
                    ValidateEvidenceReferences(
                        adjustment.EvidenceIds,
                        explanation.UnresolvedEvidenceIds,
                        evidenceIds,
                        errors);
                }
            }
        }
        else if (explanation.Adjustments is not null)
        {
            errors.Add("Work-item explanations cannot contain normalization adjustments.");
        }

        return errors;
    }

    private static void ValidateEvidenceReferences(
        IEnumerable<string> referencedIds,
        IReadOnlyList<string> unresolvedIds,
        HashSet<string> resolvedIds,
        List<string> errors)
    {
        foreach (string evidenceId in referencedIds)
        {
            if (!resolvedIds.Contains(evidenceId) &&
                !unresolvedIds.Contains(evidenceId, StringComparer.Ordinal))
            {
                errors.Add($"Explanation omits referenced evidence '{evidenceId}'.");
            }
        }
    }
}
