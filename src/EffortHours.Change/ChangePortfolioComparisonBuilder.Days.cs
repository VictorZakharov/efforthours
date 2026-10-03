using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public static partial class ChangePortfolioComparisonBuilder
{
    private static IReadOnlyList<ChangePortfolioComparisonSeries> BuildDailyAdditiveSeries(
        ChangePortfolioReport source, ChangePortfolioComparisonBuildOptions options,
        Dictionary<string, ChangePortfolioItemEstimate> items)
    {
        if (options.BucketKind != ChangePortfolioBucketPolicyKind.CalendarDay ||
            options.ContributorNormalization != ChangePortfolioContributorNormalization.Joint)
        {
            throw new ArgumentException("Independent daily reports require calendar-day buckets and joint contributor allocation within each day.");
        }
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(source.DailyNormalization!.TimeZone);
        Dictionary<string, ChangePortfolioComparisonBucket> byDate = options.Buckets.ToDictionary(bucket =>
            TimeZoneInfo.ConvertTime(bucket.SinceInclusive, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), StringComparer.Ordinal);
        Dictionary<string, List<ChangePortfolioComparisonPoint>> points = new(StringComparer.Ordinal);
        Dictionary<string, ChangePortfolioComparisonSeries> templates = BuildAdditiveSeries(source, options, items)
            .ToDictionary(series => series.Id, StringComparer.Ordinal);
        foreach (ChangePortfolioDayEstimate day in source.DailyNormalization.Days)
        {
            ChangePortfolioComparisonBucket bucket = byDate[day.Date];
            ChangePortfolioItemEstimate[] selected = [.. day.RepositoryGroups.SelectMany(group => group.ItemIds).Select(id => items[id])];
            ChangePortfolioReport partition = source with
            {
                Items = selected,
                RepositoryGroups = day.RepositoryGroups,
                Aggregation = ChangePortfolioAggregationBuilder.Build(source.Selection, selected, day.RepositoryGroups, day.Adjustments),
            };
            ChangePortfolioComparisonBuildOptions single = options with { Buckets = [bucket] };
            foreach (ChangePortfolioComparisonSeries series in BuildAdditiveSeries(partition, single, items))
            {
                if (!points.TryGetValue(series.Id, out List<ChangePortfolioComparisonPoint>? values))
                {
                    values = []; points.Add(series.Id, values);
                }
                values.Add(series.Points.Single());
            }
        }
        return [.. templates.Values.OrderBy(template => template.Id, StringComparer.Ordinal).Select(template =>
        {
            Dictionary<string, ChangePortfolioComparisonPoint> selected = (points.GetValueOrDefault(template.Id) ?? [])
                .ToDictionary(point => point.BucketId, StringComparer.Ordinal);
            return CreateSeries(template.Id, template.Kind, template.ContributorIds, template.AdditiveToPortfolio,
                [.. template.Points.Select(point => selected.GetValueOrDefault(point.BucketId) ??
                    CreatePoint(point.BucketId, 0, Zero(), point.CapacityHours))], template.TotalCapacityHours is not null);
        })];
    }
}
