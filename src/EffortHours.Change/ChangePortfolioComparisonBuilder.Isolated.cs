using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public static partial class ChangePortfolioComparisonBuilder
{
    private static List<ChangePortfolioComparisonSeries> BuildIsolatedContributorSeries(
        ChangePortfolioReport source,
        ChangePortfolioComparisonBuildOptions options)
    {
        List<ChangePortfolioComparisonSeries> result = [];
        foreach (string contributorId in source.Selection.AuthorPeriodManifest!.ContributorIds
            .Order(StringComparer.Ordinal))
        {
            Dictionary<string, EffortRange> effort = options.Buckets.ToDictionary(
                bucket => bucket.Id,
                _ => Zero(),
                StringComparer.Ordinal);
            Dictionary<string, int> counts = options.Buckets.ToDictionary(
                bucket => bucket.Id,
                _ => 0,
                StringComparer.Ordinal);
            foreach (ChangePortfolioItemEstimate item in source.Items.Where(item =>
                item.Attribution.ContributorMatches?.Any(match =>
                    string.Equals(
                        match.ContributorId,
                        contributorId,
                        StringComparison.Ordinal)) == true))
            {
                DateTimeOffset timestamp = item.Attribution.SelectedTimestamp ??
                    throw new InvalidOperationException(
                        $"Portfolio item '{item.Id}' lacks its selected timestamp.");
                ChangePortfolioComparisonBucket bucket = options.Buckets.Single(candidate =>
                    timestamp >= candidate.SinceInclusive && timestamp < candidate.UntilExclusive);
                effort[bucket.Id] = Add(effort[bucket.Id], item.IsolatedEffort);
                counts[bucket.Id]++;
            }

            ChangePortfolioComparisonPoint[] points =
            [
                .. options.Buckets.Select(bucket => CreatePoint(
                    bucket.Id,
                    counts[bucket.Id],
                    effort[bucket.Id],
                    Capacity(options.CapacityManifest, bucket.Id, contributorId))),
            ];
            result.Add(CreateSeries(
                "contributor-" + contributorId,
                ChangePortfolioSeriesKind.ContributorIsolated,
                [contributorId],
                additiveToPortfolio: false,
                points,
                options.CapacityManifest is not null));
        }

        return result;
    }
}
