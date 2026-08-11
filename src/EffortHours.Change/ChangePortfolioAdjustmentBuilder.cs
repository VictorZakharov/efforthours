using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static class ChangePortfolioAdjustmentBuilder
{
    public static IReadOnlyList<ChangePortfolioAdjustment> Build(
        string repositoryId,
        EffortRange isolated,
        EffortRange normalized,
        IReadOnlyList<ChangePortfolioAdjustmentCause> structuralCauses,
        IReadOnlyList<string> allItemIds)
    {
        SignedEffortRange delta = new()
        {
            Low = normalized.Low - isolated.Low,
            Expected = normalized.Expected - isolated.Expected,
            High = normalized.High - isolated.High,
        };
        if (delta.Low == 0m && delta.Expected == 0m && delta.High == 0m)
        {
            return [];
        }

        List<ChangePortfolioAdjustmentCause> causes = [.. structuralCauses];
        if (causes.Count == 0)
        {
            causes.Add(new ChangePortfolioAdjustmentCause(
                ChangePortfolioAdjustmentKind.Interaction,
                1m,
                "Residual interaction reconciles structural portfolio normalization with the isolated item ledger.",
                allItemIds,
                0));
        }

        decimal weight = causes.Sum(cause => cause.Weight);
        decimal usedLow = 0m;
        decimal usedExpected = 0m;
        decimal usedHigh = 0m;
        List<ChangePortfolioAdjustment> adjustments = [];
        for (int index = 0; index < causes.Count; index++)
        {
            ChangePortfolioAdjustmentCause cause = causes[index];
            bool last = index == causes.Count - 1;
            decimal low = last
                ? delta.Low - usedLow
                : Round(delta.Low * cause.Weight / weight);
            decimal expected = last
                ? delta.Expected - usedExpected
                : Round(delta.Expected * cause.Weight / weight);
            decimal high = last
                ? delta.High - usedHigh
                : Round(delta.High * cause.Weight / weight);
            usedLow += low;
            usedExpected += expected;
            usedHigh += high;
            adjustments.Add(new ChangePortfolioAdjustment
            {
                Id = ChangePortfolioIdentity.AdjustmentId(
                    repositoryId,
                    cause.Kind,
                    index,
                    cause.ItemIds),
                Kind = cause.Kind,
                EffortDelta = new SignedEffortRange { Low = low, Expected = expected, High = high },
                Reason = cause.Reason +
                    " Point-level attribution is a deterministic structural heuristic, not causal credit or historical labor accounting.",
                ItemIds = [.. cause.ItemIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
                AffectedPathCount = cause.AffectedPathCount,
            });
        }

        return adjustments;
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
