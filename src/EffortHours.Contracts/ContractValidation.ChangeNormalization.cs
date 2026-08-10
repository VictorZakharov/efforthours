using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private const string ChangeNormalizationMethod = "expected-point/gross-isolated-v1";

    private static void ValidateChangeNormalization(
        ChangeEstimateReport report,
        HashSet<string> componentIds,
        HashSet<string> adjustmentIds,
        List<string> errors)
    {
        ChangeNormalizationSummary? summary = report.Reconciliation.Normalization;
        if (summary is null)
        {
            return;
        }

        RequireText(summary.Id, "reconciliation.normalization.id", errors);
        if (!string.Equals(
                summary.Id,
                StableNormalizationId(report.Selection, componentIds),
                StringComparison.Ordinal))
        {
            errors.Add("Change normalization ID is inconsistent with its immutable lineage.");
        }

        if (!string.Equals(summary.CalculationMethod, ChangeNormalizationMethod, StringComparison.Ordinal))
        {
            errors.Add("Change normalization calculationMethod is unsupported.");
        }

        bool eligible = report.Selection.Kind == ChangeSelectionKind.Range &&
            report.Reconciliation.Components.Count >= 2 &&
            report.Reconciliation.Components.All(component => component.Kind == ChangeComponentKind.Commit);
        if (!eligible)
        {
            errors.Add("Change normalization is only available for explicit multi-commit ranges.");
        }

        if (summary.GrossIsolatedEffort != report.Reconciliation.IsolatedComponentSum ||
            summary.NormalizedFinalDeltaEffort != report.TotalEffort)
        {
            errors.Add("Change normalization effort lineage does not match reconciliation.");
        }

        decimal gross = summary.GrossIsolatedEffort.Expected;
        decimal normalized = summary.NormalizedFinalDeltaEffort.Expected;
        decimal normalizationHours = Math.Max(0m, gross - normalized);
        decimal overlapHours = NegativeExpectedHours(
            report.Reconciliation.Adjustments,
            ChangeAdjustmentKind.Overlap);
        decimal revertHours = NegativeExpectedHours(
            report.Reconciliation.Adjustments,
            ChangeAdjustmentKind.Revert);
        decimal reworkLikeHours = Math.Min(normalizationHours, overlapHours + revertHours);
        decimal otherNormalizationHours = normalizationHours - reworkLikeHours;
        ValidateValue(
            summary.ExpectedGrossToFinalNormalizationHours,
            normalizationHours,
            "expectedGrossToFinalNormalizationHours",
            errors);
        ValidateValue(summary.ExpectedReworkLikeHours, reworkLikeHours, "expectedReworkLikeHours", errors);
        ValidateValue(
            summary.ExpectedOtherNormalizationHours,
            otherNormalizationHours,
            "expectedOtherNormalizationHours",
            errors);
        ValidateValue(
            summary.ExpectedSharedOrRepeatedHours,
            NegativeExpectedHours(report.Reconciliation.Adjustments, ChangeAdjustmentKind.SharedSetup),
            "expectedSharedOrRepeatedHours",
            errors);
        ValidateValue(summary.ExpectedOverlapHours, overlapHours, "expectedOverlapHours", errors);
        ValidateValue(summary.ExpectedRevertHours, revertHours, "expectedRevertHours", errors);
        ValidateValue(
            summary.ExpectedResidualInteractionHours,
            NegativeExpectedHours(report.Reconciliation.Adjustments, ChangeAdjustmentKind.Interaction),
            "expectedResidualInteractionHours",
            errors);
        ValidateValue(
            summary.ExpectedPositiveInteractionHours,
            PositiveExpectedHours(report.Reconciliation.Adjustments, ChangeAdjustmentKind.Interaction),
            "expectedPositiveInteractionHours",
            errors);

        bool hasGross = gross > 0m;
        ChangeNormalizationStatus expectedStatus = hasGross
            ? ChangeNormalizationStatus.Calculated
            : ChangeNormalizationStatus.NotApplicableZeroGross;
        if (summary.Status != expectedStatus)
        {
            errors.Add("Change normalization status is inconsistent with gross isolated effort.");
        }

        ValidateShare(
            summary.ExpectedGrossToFinalNormalizationShare,
            Share(normalizationHours, gross),
            "expectedGrossToFinalNormalizationShare",
            errors);
        ValidateShare(
            summary.ExpectedReworkLikeShare,
            Share(reworkLikeHours, gross),
            "expectedReworkLikeShare",
            errors);
        ValidateShare(
            summary.ExpectedOtherNormalizationShare,
            Share(otherNormalizationHours, gross),
            "expectedOtherNormalizationShare",
            errors);

        ValidateAdjustmentIds(
            summary.SharedOrRepeatedAdjustmentIds,
            report.Reconciliation.Adjustments,
            ChangeAdjustmentKind.SharedSetup,
            adjustmentIds,
            "sharedOrRepeatedAdjustmentIds",
            errors);
        ValidateAdjustmentIds(
            summary.OverlapAdjustmentIds,
            report.Reconciliation.Adjustments,
            ChangeAdjustmentKind.Overlap,
            adjustmentIds,
            "overlapAdjustmentIds",
            errors);
        ValidateAdjustmentIds(
            summary.RevertAdjustmentIds,
            report.Reconciliation.Adjustments,
            ChangeAdjustmentKind.Revert,
            adjustmentIds,
            "revertAdjustmentIds",
            errors);
        ValidateAdjustmentIds(
            summary.ResidualInteractionAdjustmentIds,
            report.Reconciliation.Adjustments,
            ChangeAdjustmentKind.Interaction,
            adjustmentIds,
            "residualInteractionAdjustmentIds",
            errors);
        ValidatePositiveAdjustmentIds(
            summary.PositiveInteractionAdjustmentIds,
            report.Reconciliation.Adjustments,
            ChangeAdjustmentKind.Interaction,
            adjustmentIds,
            "positiveInteractionAdjustmentIds",
            errors);

        if (componentIds.Count < 2)
        {
            errors.Add("Change normalization must retain at least two component identities.");
        }
    }

    private static void ValidateValue(decimal actual, decimal expected, string name, List<string> errors)
    {
        if (actual != expected || actual < 0m)
        {
            errors.Add($"Change normalization {name} is inconsistent.");
        }
    }

    private static void ValidateShare(
        decimal? actual,
        decimal? expected,
        string name,
        List<string> errors)
    {
        if (actual != expected || actual is < 0m or > 1m)
        {
            errors.Add($"Change normalization {name} is inconsistent.");
        }
    }

    private static void ValidateAdjustmentIds(
        IReadOnlyList<string> actual,
        IEnumerable<ChangeAdjustment> adjustments,
        ChangeAdjustmentKind kind,
        HashSet<string> knownIds,
        string name,
        List<string> errors)
    {
        string[] expected = [.. adjustments
            .Where(adjustment => adjustment.Kind == kind && adjustment.EffortDelta.Expected < 0m)
            .Select(adjustment => adjustment.Id)
            .Order(StringComparer.Ordinal)];
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal) ||
            actual.Any(id => !knownIds.Contains(id)))
        {
            errors.Add($"Change normalization {name} is inconsistent.");
        }
    }

    private static decimal NegativeExpectedHours(
        IEnumerable<ChangeAdjustment> adjustments,
        ChangeAdjustmentKind kind) => adjustments
        .Where(adjustment => adjustment.Kind == kind && adjustment.EffortDelta.Expected < 0m)
        .Sum(adjustment => -adjustment.EffortDelta.Expected);

    private static void ValidatePositiveAdjustmentIds(
        IReadOnlyList<string> actual,
        IEnumerable<ChangeAdjustment> adjustments,
        ChangeAdjustmentKind kind,
        HashSet<string> knownIds,
        string name,
        List<string> errors)
    {
        string[] expected = [.. adjustments
            .Where(adjustment => adjustment.Kind == kind && adjustment.EffortDelta.Expected > 0m)
            .Select(adjustment => adjustment.Id)
            .Order(StringComparer.Ordinal)];
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal) ||
            actual.Any(id => !knownIds.Contains(id)))
        {
            errors.Add($"Change normalization {name} is inconsistent.");
        }
    }

    private static decimal PositiveExpectedHours(
        IEnumerable<ChangeAdjustment> adjustments,
        ChangeAdjustmentKind kind) => adjustments
        .Where(adjustment => adjustment.Kind == kind && adjustment.EffortDelta.Expected > 0m)
        .Sum(adjustment => adjustment.EffortDelta.Expected);

    private static decimal? Share(decimal numerator, decimal denominator) => denominator <= 0m
        ? null
        : decimal.Round(numerator / denominator, 4, MidpointRounding.AwayFromZero);

    private static HashSet<string> NormalizationAdjustmentIds(ChangeNormalizationSummary summary) =>
        summary.SharedOrRepeatedAdjustmentIds
            .Concat(summary.OverlapAdjustmentIds)
            .Concat(summary.RevertAdjustmentIds)
            .Concat(summary.ResidualInteractionAdjustmentIds)
            .Concat(summary.PositiveInteractionAdjustmentIds)
            .ToHashSet(StringComparer.Ordinal);

    private static string StableNormalizationId(
        ChangeSelection selection,
        IEnumerable<string> componentIds)
    {
        string[] identity =
        [
            ChangeNormalizationMethod,
            selection.Base.ObjectId,
            selection.Head.ObjectId,
            .. componentIds.Order(StringComparer.Ordinal),
        ];
        string digest = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\n', identity))))
            .ToLowerInvariant();
        return $"change-normalization:{digest[..20]}";
    }
}
