using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public static partial class ChangePortfolioComparisonBuilder
{
    /// <summary>Project filters are exact additive views of the same reconciled portfolio.</summary>
    public static IReadOnlyList<ChangePortfolioComparisonSeries> BuildRepositorySeries(
        ChangePortfolioComparisonReport report)
    {
        if (report.Status != ChangePortfolioComparisonStatus.Complete || report.SourcePortfolio?.Aggregation is null ||
            ContractValidation.Validate(report).Count != 0)
            throw new ArgumentException("Repository series require a valid complete comparison.", nameof(report));
        ChangePortfolioReport source = report.SourcePortfolio;
        Dictionary<string, ChangePortfolioItemEstimate> items = source.Items.ToDictionary(i => i.Id, StringComparer.Ordinal);
        List<ChangePortfolioComparisonSeries> result = [];
        foreach (ChangePortfolioRepositorySummary repository in source.Aggregation.Repositories)
        {
            Dictionary<string, EffortRange> effort = report.Buckets.ToDictionary(b => b.Id, _ => Zero(), StringComparer.Ordinal);
            Dictionary<string, int> counts = report.Buckets.ToDictionary(b => b.Id, _ => 0, StringComparer.Ordinal);
            if (source.DailyNormalization is null)
                foreach (ChangePortfolioContributorRepositoryAllocation allocation in source.Aggregation.ContributorGroups
                .SelectMany(g => g.RepositoryAllocations).Where(a => a.RepositoryId == repository.RepositoryId))
                    AllocateRepositoryGroup(report.Buckets, allocation, items, effort, counts);
            if (source.DailyNormalization is { } daily)
            {
                TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(daily.TimeZone);
                Dictionary<string, ChangePortfolioDayEstimate> days = daily.Days.ToDictionary(day => day.Date, StringComparer.Ordinal);
                foreach (ChangePortfolioComparisonBucket bucket in report.Buckets)
                {
                    string date = TimeZoneInfo.ConvertTime(bucket.SinceInclusive, zone).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                    ChangePortfolioRepositoryGroup? group = days.GetValueOrDefault(date)?.RepositoryGroups
                        .SingleOrDefault(group => group.RepositoryId == repository.RepositoryId);
                    effort[bucket.Id] = group?.NormalizedEffort ?? Zero();
                    counts[bucket.Id] = group?.ItemIds.Count ?? 0;
                }
            }
            result.Add(new ChangePortfolioComparisonSeries
            {
                Id = repository.RepositoryId,
                Kind = ChangePortfolioSeriesKind.Portfolio,
                AdditiveToPortfolio = true,
                TotalEffort = Sum(effort.Values),
                Points = [.. report.Buckets.Select(b => new ChangePortfolioComparisonPoint
                {
                    BucketId = b.Id, SelectedChangeCount = counts[b.Id], Effort = effort[b.Id],
                })],
            });
        }
        ChangePortfolioComparisonSeries portfolio = report.Series.Single(s => s.Kind == ChangePortfolioSeriesKind.Portfolio);
        for (int index = 0; index < report.Buckets.Count; index++)
            if (Sum(result.Select(s => s.Points[index].Effort)) != portfolio.Points[index].Effort)
                throw new InvalidOperationException("Repository calendar cells do not reconcile to the portfolio.");
        return result;
    }
}
