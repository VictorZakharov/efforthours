using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static class ChangeNormalizationCalculator
{
    internal const string CalculationMethod = "expected-point/gross-isolated-v1";

    public static ChangeNormalizationSummary? Calculate(
        ChangeSelection selection,
        EffortRange grossIsolatedEffort,
        EffortRange normalizedFinalDeltaEffort,
        IReadOnlyList<ChangeComponentEstimate> components,
        IReadOnlyList<ChangeAdjustment> adjustments)
    {
        if (selection.Kind != ChangeSelectionKind.Range ||
            components.Count < 2 ||
            components.Any(component => component.Kind != ChangeComponentKind.Commit))
        {
            return null;
        }

        decimal normalizationHours = Math.Max(
            0m,
            grossIsolatedEffort.Expected - normalizedFinalDeltaEffort.Expected);
        decimal sharedHours = NegativeExpectedHours(adjustments, ChangeAdjustmentKind.SharedSetup);
        decimal overlapHours = NegativeExpectedHours(adjustments, ChangeAdjustmentKind.Overlap);
        decimal revertHours = NegativeExpectedHours(adjustments, ChangeAdjustmentKind.Revert);
        decimal residualHours = NegativeExpectedHours(adjustments, ChangeAdjustmentKind.Interaction);
        decimal reworkLikeHours = Math.Min(normalizationHours, overlapHours + revertHours);
        decimal otherNormalizationHours = normalizationHours - reworkLikeHours;
        decimal positiveInteractionHours = PositiveExpectedHours(
            adjustments,
            ChangeAdjustmentKind.Interaction);
        bool hasGrossDenominator = grossIsolatedEffort.Expected > 0m;

        return new ChangeNormalizationSummary
        {
            Id = StableId(selection, components),
            Status = hasGrossDenominator
                ? ChangeNormalizationStatus.Calculated
                : ChangeNormalizationStatus.NotApplicableZeroGross,
            CalculationMethod = CalculationMethod,
            GrossIsolatedEffort = grossIsolatedEffort,
            NormalizedFinalDeltaEffort = normalizedFinalDeltaEffort,
            ExpectedGrossToFinalNormalizationHours = normalizationHours,
            ExpectedGrossToFinalNormalizationShare = Share(normalizationHours, grossIsolatedEffort.Expected),
            ExpectedReworkLikeHours = reworkLikeHours,
            ExpectedReworkLikeShare = Share(reworkLikeHours, grossIsolatedEffort.Expected),
            ExpectedOtherNormalizationHours = otherNormalizationHours,
            ExpectedOtherNormalizationShare = Share(otherNormalizationHours, grossIsolatedEffort.Expected),
            ExpectedSharedOrRepeatedHours = sharedHours,
            ExpectedOverlapHours = overlapHours,
            ExpectedRevertHours = revertHours,
            ExpectedResidualInteractionHours = residualHours,
            ExpectedPositiveInteractionHours = positiveInteractionHours,
            SharedOrRepeatedAdjustmentIds = NegativeAdjustmentIds(
                adjustments,
                ChangeAdjustmentKind.SharedSetup),
            OverlapAdjustmentIds = NegativeAdjustmentIds(adjustments, ChangeAdjustmentKind.Overlap),
            RevertAdjustmentIds = NegativeAdjustmentIds(adjustments, ChangeAdjustmentKind.Revert),
            ResidualInteractionAdjustmentIds = NegativeAdjustmentIds(
                adjustments,
                ChangeAdjustmentKind.Interaction),
            PositiveInteractionAdjustmentIds = PositiveAdjustmentIds(
                adjustments,
                ChangeAdjustmentKind.Interaction),
        };
    }

    private static decimal NegativeExpectedHours(
        IEnumerable<ChangeAdjustment> adjustments,
        ChangeAdjustmentKind kind) => adjustments
        .Where(adjustment => adjustment.Kind == kind && adjustment.EffortDelta.Expected < 0m)
        .Sum(adjustment => -adjustment.EffortDelta.Expected);

    private static decimal PositiveExpectedHours(
        IEnumerable<ChangeAdjustment> adjustments,
        ChangeAdjustmentKind kind) => adjustments
        .Where(adjustment => adjustment.Kind == kind && adjustment.EffortDelta.Expected > 0m)
        .Sum(adjustment => adjustment.EffortDelta.Expected);

    private static string[] NegativeAdjustmentIds(
        IEnumerable<ChangeAdjustment> adjustments,
        ChangeAdjustmentKind kind) =>
        [.. adjustments
            .Where(adjustment => adjustment.Kind == kind && adjustment.EffortDelta.Expected < 0m)
            .Select(adjustment => adjustment.Id)
            .Order(StringComparer.Ordinal)];

    private static string[] PositiveAdjustmentIds(
        IEnumerable<ChangeAdjustment> adjustments,
        ChangeAdjustmentKind kind) =>
        [.. adjustments
            .Where(adjustment => adjustment.Kind == kind && adjustment.EffortDelta.Expected > 0m)
            .Select(adjustment => adjustment.Id)
            .Order(StringComparer.Ordinal)];

    private static decimal? Share(decimal numerator, decimal denominator) => denominator <= 0m
        ? null
        : decimal.Round(numerator / denominator, 4, MidpointRounding.AwayFromZero);

    private static string StableId(
        ChangeSelection selection,
        IEnumerable<ChangeComponentEstimate> components)
    {
        string[] identity =
        [
            CalculationMethod,
            selection.Base.ObjectId,
            selection.Head.ObjectId,
            .. components.Select(component => component.Id).Order(StringComparer.Ordinal),
        ];
        string digest = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\n', identity))))
            .ToLowerInvariant();
        return $"change-normalization:{digest[..20]}";
    }
}
