using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public static partial class ChangePortfolioComparisonBuilder
{
    private static ChangePortfolioNativePeriod? HistoricalDailyEvidence(
        ChangePortfolioReport source,
        ChangePortfolioComparisonBuildOptions options)
    {
        if (options.NativePeriod is not { RetainedHistory: true } native ||
            native.Breakdown != ChangePortfolioNativeBreakdown.CalendarDay)
        {
            return options.NativePeriod;
        }

        Dictionary<string, List<ChangePortfolioItemEstimate>> selected = options.Buckets.ToDictionary(
            bucket => bucket.Id, _ => new List<ChangePortfolioItemEstimate>(), StringComparer.Ordinal);
        foreach (ChangePortfolioItemEstimate item in source.Items)
        {
            DateTimeOffset timestamp = item.Attribution.SelectedTimestamp!.Value;
            ChangePortfolioComparisonBucket bucket = options.Buckets.Single(bucket =>
                timestamp >= bucket.SinceInclusive && timestamp < bucket.UntilExclusive);
            selected[bucket.Id].Add(item);
        }

        return native with
        {
            DailyEvidence = [.. options.Buckets.Select(bucket =>
            {
                List<ChangePortfolioItemEstimate> items = selected[bucket.Id];
                return new ChangePortfolioDailyEvidence
                {
                    BucketId = bucket.Id,
                    SelectedChangeCount = items.Count,
                    State = items.Count == 0 ? "no-retained-change" :
                        items.All(item => item.AnalyzedPathCount == 0) ? "scope-excluded" :
                        items.All(item => item.RepresentedPathCount == 0) ? "normalized-zero" :
                        items.Sum(item => item.AllocatedExpectedHours) == 0m ? "reconciled-zero" :
                        "measured-retained-change",
                };
            })],
        };
    }
}
