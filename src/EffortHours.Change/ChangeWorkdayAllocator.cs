using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public static class ChangeWorkdayAllocator
{
    public static ChangeWorkdayAllocationReport Allocate(ChangePortfolioComparisonReport source,
        ChangeWorkdayManifest manifest, string policy)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(manifest);
        if (policy != ChangeWorkdayPolicies.EqualDeclaredDaysV1)
            throw new ArgumentException("Explicit --policy equal-declared-days/1.0.0 is required.");
        RequireValid(ContractValidation.Validate(manifest));
        ChangeWorkdaySource input = ChangeWorkdaySource.Read(source, manifest.SourceSemanticDigest);
        ChangePortfolioReport portfolio = input.Portfolio;
        ChangePortfolioAuthorPeriodManifestSelection selection = input.Selection;
        ChangePortfolioComparisonSeries series = input.Series;
        string digest = input.Digest;
        IReadOnlyList<(ChangePortfolioComparisonBucket Bucket, string Date)> geometry = input.Geometry;
        Dictionary<string, ChangeDeclaredWorkday> declared = manifest.Workdays.ToDictionary(day => day.Date, StringComparer.Ordinal);
        if (declared.Keys.Except(geometry.Select(value => value.Date), StringComparer.Ordinal).Any())
            throw new ArgumentException("Every declared workday must belong to the source period; declarations outside it require a new complete source report.");
        string[] allocatedDates = [.. declared.Keys.Order(StringComparer.Ordinal)];
        List<ChangeWorkdayAllocationDay> days = [];
        foreach ((ChangePortfolioComparisonBucket bucket, string date) in geometry)
        {
            ChangePortfolioComparisonPoint point = series.Points.Single(value => value.BucketId == bucket.Id);
            int rank = Array.IndexOf(allocatedDates, date);
            CategoryEstimate[] categories = [.. portfolio.Categories.Select(category => category with
            {
                Hours = rank < 0 ? Zero : Divide(category.Hours, allocatedDates.Length, rank),
            })];
            EffortRange allocated = Sum(categories.Select(category => category.Hours));
            days.Add(new()
            {
                Date = date,
                BucketId = bucket.Id,
                RecordId = declared.GetValueOrDefault(date)?.RecordId,
                SourceAttributedEffort = point.Effort,
                AllocatedEffort = allocated,
                CapacityHours = point.CapacityHours,
                CapacityRatio = Ratio(allocated, point.CapacityHours),
                Categories = categories,
            });
        }
        ChangeWorkdayAllocationReport report = new()
        {
            SourceSemanticDigest = digest,
            SourcePortfolioDigest = source.Verification.SourcePortfolioDigest!,
            WorkdayInputDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(ContractJson.SerializeCompact(manifest with
            { Workdays = [.. manifest.Workdays.OrderBy(day => day.Date, StringComparer.Ordinal)] })),
            TimeZone = selection.TimeZone,
            ContributorId = selection.ContributorIds[0],
            SourceEstimatorVersion = source.EstimatorVersion,
            TotalEffort = portfolio.TotalEffort,
            TotalCapacityHours = series.TotalCapacityHours,
            Categories = portfolio.Categories,
            Days = days,
        };
        RequireValid(ContractValidation.Validate(report));
        return report;
    }

    private static void RequireValid(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0) throw new ArgumentException("Invalid allocation input/output: " + string.Join(" ", errors));
    }

    private static readonly EffortRange Zero = new() { Low = 0, Expected = 0, High = 0 };
    private static EffortRange Sum(IEnumerable<EffortRange> ranges) => new()
    { Low = ranges.Sum(value => value.Low), Expected = ranges.Sum(value => value.Expected), High = ranges.Sum(value => value.High) };
    private static EffortRange Divide(EffortRange value, int count, int rank) => new()
    { Low = Divide(value.Low, count, rank), Expected = Divide(value.Expected, count, rank), High = Divide(value.High, count, rank) };

    private static decimal Divide(decimal value, int count, int rank)
    {
        if (value > decimal.MaxValue / 100) throw new ArgumentException("Source effort exceeds the cent-hour arithmetic bound.");
        decimal cents = value * 100;
        if (cents != decimal.Truncate(cents)) throw new ArgumentException("Allocation requires exact cent-hour source values.");
        return (decimal.Floor(cents / count) + (rank < cents % count ? 1 : 0)) / 100;
    }

    private static ChangePortfolioRatioRange? Ratio(EffortRange value, decimal? hours) => hours is > 0 ? new()
    {
        Low = decimal.Round(value.Low / hours.Value, 6, MidpointRounding.AwayFromZero),
        Expected = decimal.Round(value.Expected / hours.Value, 6, MidpointRounding.AwayFromZero),
        High = decimal.Round(value.High / hours.Value, 6, MidpointRounding.AwayFromZero),
    } : null;
}
