using System.Globalization;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class ChangePortfolioReconciler
{
    private static ChangePortfolioDailyNormalization NormalizeDays(
        ChangePortfolioSelection selection, IReadOnlyList<ChangePortfolioItemDraft> drafts)
    {
        ChangePortfolioAuthorPeriodManifestSelection manifest = selection.AuthorPeriodManifest ??
            throw new ArgumentException("Independent days require a manifest author-period selection.");
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(manifest.TimeZone);
        var days = drafts.GroupBy(draft => TimeZoneInfo.ConvertTime(
            draft.Candidate.Attribution.SelectedTimestamp!.Value, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();
        if (days.Length > ChangePortfolioComparisonLimits.MaximumBuckets)
        {
            throw new ArgumentException("Independent daily normalization exceeds the 512-day batch envelope.");
        }
        return new ChangePortfolioDailyNormalization
        {
            TimeZone = manifest.TimeZone,
            Days = [.. days.Select(day =>
            {
                ChangePortfolioGroupResult[] results = [.. day.GroupBy(draft => draft.Candidate.RepositoryId, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => ChangePortfolioGroupNormalizer.Normalize(selection, group.Key,
                        [.. group.OrderBy(draft => draft.Id, StringComparer.Ordinal)]))];
                return new ChangePortfolioDayEstimate
                {
                    Date = day.Key, RepositoryGroups = [.. results.Select(result => result.Group)],
                    Adjustments = [.. results.SelectMany(result => result.Adjustments).OrderBy(adjustment => adjustment.Id, StringComparer.Ordinal)],
                };
            })],
        };
    }

    private static ChangePortfolioGroupResult[] MergeDays(ChangePortfolioDailyNormalization daily)
    {
        Dictionary<string, ChangePortfolioAdjustment> adjustments = daily.Days.SelectMany(day => day.Adjustments)
            .ToDictionary(adjustment => adjustment.Id, StringComparer.Ordinal);
        return [.. daily.Days.SelectMany(day => day.RepositoryGroups)
            .GroupBy(group => group.RepositoryId, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(groups =>
            {
                ChangePortfolioRepositoryGroup[] parts = [.. groups];
                ChangePortfolioRepositoryGroup merged = parts[0] with
                {
                    BaseContexts = [.. parts.SelectMany(part => part.BaseContexts).GroupBy(context => context.Id, StringComparer.Ordinal)
                        .OrderBy(context => context.Key, StringComparer.Ordinal).Select(context => context.First() with
                        {
                            ItemIds = [.. context.SelectMany(value => value.ItemIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
                        })],
                    ItemIds = [.. parts.SelectMany(part => part.ItemIds).Order(StringComparer.Ordinal)],
                    IsolatedEffort = ContractValidation.Sum(parts.Select(part => part.IsolatedEffort)),
                    NormalizedEffort = ContractValidation.Sum(parts.Select(part => part.NormalizedEffort)),
                    Categories = AggregateCategories(parts), Assessment = "sum-of-independent-local-days",
                    AdjustmentIds = [.. parts.SelectMany(part => part.AdjustmentIds).Order(StringComparer.Ordinal)],
                    UncertaintyReasons = [.. parts.SelectMany(part => part.UncertaintyReasons).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
                };
                return new ChangePortfolioGroupResult(merged, [.. merged.AdjustmentIds.Select(id => adjustments[id])]);
            })];
    }
}
