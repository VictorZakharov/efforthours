using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class ChangePortfolioGroupNormalizer
{
    private static void AllocateRewritePairs(IReadOnlyList<ChangePortfolioItemDraft> drafts)
    {
        foreach (IGrouping<string, ChangePortfolioItemDraft> pair in drafts
            .Where(draft => draft.Candidate.Attribution.Rewrite is not null)
            .GroupBy(draft => draft.Candidate.Attribution.Rewrite!.Evidence.OriginalObjectId, StringComparer.Ordinal))
        {
            ChangePortfolioItemDraft original = pair.Single(draft => draft.Candidate.Attribution.Rewrite!.Role == "original");
            ChangePortfolioItemDraft rewritten = pair.Single(draft => draft.Candidate.Attribution.Rewrite!.Role == "rewritten");
            decimal budget = original.AllocatedExpectedHours + rewritten.AllocatedExpectedHours;
            if (original.Suppressed || rewritten.Suppressed) continue;
            original.AllocatedExpectedHours = Math.Min(original.Candidate.Report.TotalEffort.Expected, budget);
            rewritten.AllocatedExpectedHours = budget - original.AllocatedExpectedHours;
        }
    }

    private static CategoryEstimate[] RemoveRewriteSupport(
        IReadOnlyList<ChangePortfolioItemDraft> drafts,
        CategoryEstimate[] categories,
        decimal total)
    {
        foreach (ChangePortfolioItemDraft draft in drafts.Where(value =>
            value.Candidate.Attribution.Rewrite?.SupportOnly == true))
            draft.AllocatedExpectedHours = 0m;
        decimal retained = drafts.Sum(draft => draft.AllocatedExpectedHours);
        if (total == 0m || retained == 0m) return [];
        decimal ratio = retained / total;
        CategoryEstimate[] scaled = [.. categories.Select(category => category with
        {
            Hours = Round(new EffortRange
            {
                Low = category.Hours.Low * ratio,
                Expected = category.Hours.Expected * ratio,
                High = category.Hours.High * ratio,
            }),
        })];
        decimal residual = retained - scaled.Sum(category => category.Hours.Expected);
        int largest = Array.FindIndex(scaled, category => category.Hours.Expected ==
            scaled.Max(value => value.Hours.Expected));
        EffortRange hours = scaled[largest].Hours;
        decimal expected = hours.Expected + residual;
        scaled[largest] = scaled[largest] with
        {
            Hours = hours with
            {
                Low = Math.Min(hours.Low, expected),
                Expected = expected,
                High = Math.Max(hours.High, expected),
            }
        };
        return scaled;
    }
}
