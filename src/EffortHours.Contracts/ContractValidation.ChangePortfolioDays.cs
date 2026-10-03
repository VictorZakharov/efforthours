using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateDailyComparison(ChangePortfolioComparisonReport report, List<string> errors)
    {
        if (report.SourcePortfolio?.DailyNormalization is not { } daily) return;
        if (report.BucketPolicy.Kind != ChangePortfolioBucketPolicyKind.CalendarDay ||
            report.BucketPolicy.ContributorNormalization != ChangePortfolioContributorNormalization.Joint)
            errors.Add("Independent daily normalization requires calendar-day buckets and joint contributor allocation within each day.");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(daily.TimeZone); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException) { return; }
        HashSet<DateOnly> dates = [];
        foreach (ChangePortfolioComparisonBucket bucket in report.Buckets)
        {
            DateTime start = TimeZoneInfo.ConvertTime(bucket.SinceInclusive, zone).DateTime;
            DateTime end = TimeZoneInfo.ConvertTime(bucket.UntilExclusive, zone).DateTime;
            if (!dates.Add(DateOnly.FromDateTime(start)) || end > start.Date.AddDays(1) ||
                (!bucket.PartialStart && start.TimeOfDay != TimeSpan.Zero) ||
                (!bucket.PartialEnd && end != start.Date.AddDays(1)))
                errors.Add("Independent daily bucket geometry is inconsistent with local dates.");
        }
    }

    private static void ValidateDailyNormalization(ChangePortfolioReport report, List<string> errors)
    {
        if (report.DailyNormalization is not { } daily) return;
        if (daily.Protocol != ChangePortfolioDailyNormalization.Policy ||
            report.Selection.AuthorPeriodManifest?.TimeZone != daily.TimeZone || daily.Days.Count > 512)
        {
            errors.Add("Independent daily normalization has invalid selection, identity, or bounds.");
            return;
        }
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(daily.TimeZone); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            errors.Add("Independent daily timezone is unavailable."); return;
        }
        Dictionary<string, ChangePortfolioItemEstimate> items = report.Items.DistinctBy(item => item.Id)
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        HashSet<string> visited = new(StringComparer.Ordinal);
        Dictionary<string, ChangePortfolioAdjustment> batchAdjustments = report.Adjustments.DistinctBy(adjustment => adjustment.Id)
            .ToDictionary(adjustment => adjustment.Id, StringComparer.Ordinal);
        if (!daily.Days.Select(day => day.Date).SequenceEqual(daily.Days.Select(day => day.Date).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)))
            errors.Add("Independent dates must be unique and ordered.");
        foreach (ChangePortfolioDayEstimate day in daily.Days)
        {
            ChangePortfolioReport partition = report with
            {
                Items = [.. day.RepositoryGroups.SelectMany(group => group.ItemIds).Distinct(StringComparer.Ordinal)
                    .Where(items.ContainsKey).Select(id => items[id])],
                RepositoryGroups = day.RepositoryGroups,
                Adjustments = day.Adjustments,
                TotalEffort = Sum(day.RepositoryGroups.Select(group => group.NormalizedEffort)),
                IsolatedEffort = Sum(day.RepositoryGroups.Select(group => group.IsolatedEffort)),
                Categories = [.. day.RepositoryGroups.SelectMany(group => group.Categories)
                    .GroupBy(category => category.Category).Select(group => group.First() with
                    {
                        Hours = Sum(group.Select(category => category.Hours)),
                    })],
            };
            ValidatePortfolioGroups(partition, partition.Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal), errors);
            if (day.Adjustments.Any(adjustment => !batchAdjustments.TryGetValue(adjustment.Id, out ChangePortfolioAdjustment? batch) ||
                !ReferenceEquals(batch, adjustment) && ContractJson.SerializeCompact(batch) != ContractJson.SerializeCompact(adjustment)))
                errors.Add("Independent day adjustment lineage differs from the batch.");
            if (!DateOnly.TryParseExact(day.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                errors.Add("Independent date has an invalid format.");
            foreach (ChangePortfolioRepositoryGroup group in day.RepositoryGroups)
            {
                ValidatePortfolioCategories(group.Categories, group.NormalizedEffort, "independentDay.categories", errors);
                foreach (string id in group.ItemIds)
                {
                    if (!visited.Add(id) || !items.TryGetValue(id, out ChangePortfolioItemEstimate? item) ||
                        item.RepositoryId != group.RepositoryId || item.Attribution.SelectedTimestamp is null ||
                        TimeZoneInfo.ConvertTime(item.Attribution.SelectedTimestamp.Value, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) != day.Date)
                        errors.Add("Independent day contains duplicate, unknown, or mismatched input lineage.");
                }
                if (Sum(group.Categories.Select(category => category.Hours)) != group.NormalizedEffort ||
                    group.ItemIds.Where(items.ContainsKey).Sum(id => items[id].AllocatedExpectedHours) != group.NormalizedEffort.Expected)
                    errors.Add("Independent day category or row sums disagree.");
            }
        }
        foreach (ChangePortfolioRepositoryGroup group in report.RepositoryGroups)
        {
            ChangePortfolioRepositoryGroup[] parts = [.. daily.Days.SelectMany(day => day.RepositoryGroups)
                .Where(part => part.RepositoryId == group.RepositoryId)];
            if (Sum(parts.Select(part => part.NormalizedEffort)) != group.NormalizedEffort ||
                Sum(parts.Select(part => part.IsolatedEffort)) != group.IsolatedEffort ||
                group.Categories.Any(category => Sum(parts.SelectMany(part => part.Categories)
                    .Where(part => part.Category == category.Category).Select(part => part.Hours)) != category.Hours))
                errors.Add("Independent repository/category totals disagree with the batch.");
        }
        if (!visited.SetEquals(items.Keys) || Sum(daily.Days.SelectMany(day => day.RepositoryGroups).Select(group => group.NormalizedEffort)) != report.TotalEffort)
            errors.Add("Independent day totals or item coverage disagree with the batch.");
        string[] adjustments = [.. daily.Days.SelectMany(day => day.Adjustments).Select(adjustment => adjustment.Id)];
        if (adjustments.Length != adjustments.Distinct(StringComparer.Ordinal).Count() ||
            !adjustments.ToHashSet(StringComparer.Ordinal).SetEquals(report.Adjustments.Select(adjustment => adjustment.Id)))
            errors.Add("Independent adjustment coverage disagrees with the batch.");
    }
}
