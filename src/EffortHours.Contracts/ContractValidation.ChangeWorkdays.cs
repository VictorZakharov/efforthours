using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(ChangeWorkdayManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        List<string> errors = [];
        RequireVersion(manifest.SchemaVersion, "change workday manifest", errors);
        ValidateDigest(manifest.SourceSemanticDigest, "sourceSemanticDigest", errors);
        if (manifest.Workdays.Count is < 1 or > 512) errors.Add("Declare between 1 and 512 workdays.");
        HashSet<string> dates = new(StringComparer.Ordinal);
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (ChangeDeclaredWorkday day in manifest.Workdays)
        {
            ValidatePublicId(day.RecordId, "recordId", errors);
            if (!DateOnly.TryParseExact(day.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                errors.Add("Workday dates must be valid yyyy-MM-dd dates.");
            if (!dates.Add(day.Date) || !ids.Add(day.RecordId)) errors.Add("Workday dates and record IDs must be unique.");
            if (day.LoggedHours is < 0 or > 24) errors.Add("Optional logged hours must be between zero and 24; they are not allocation weights.");
        }
        return errors;
    }

    public static IReadOnlyList<string> Validate(ChangeWorkdayAllocationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "change workday allocation report", errors);
        ValidateDigest(report.SourceSemanticDigest, "sourceSemanticDigest", errors);
        ValidateDigest(report.SourcePortfolioDigest, "sourcePortfolioDigest", errors);
        ValidateDigest(report.WorkdayInputDigest, "workdayInputDigest", errors);
        ValidatePublicId(report.ContributorId, "contributorId", errors);
        RequireCanonicalText(report.SourceEstimatorVersion, "sourceEstimatorVersion", 256, errors);
        RequireCanonicalText(report.TimeZone, "timeZone", 128, errors);
        if (report.Boundary != ChangeWorkdayPolicies.Boundary) errors.Add("Allocation must retain its interpretation boundary.");
        if (report.Policy != ChangeWorkdayPolicies.EqualDeclaredDaysV1 || report.Status != "allocated")
            errors.Add("Workday output must use the explicit allocated policy and status.");
        if (report.Days.Count is < 1 or > 512 || !report.Days.Any(day => day.RecordId is not null))
            errors.Add("Allocation requires bounded days and at least one declared workday.");
        if (report.Days.Select(day => day.Date).Distinct(StringComparer.Ordinal).Count() != report.Days.Count ||
            report.Days.Select(day => day.BucketId).Distinct(StringComparer.Ordinal).Count() != report.Days.Count)
            errors.Add("Allocation dates and buckets must be unique.");
        if (report.Days.Any(day => day.Status != "allocated" || day.OriginalWorkdayStatus != ChangeWorkdayPolicies.Unresolved))
            errors.Add("Allocated dates must retain unresolved original-workday status.");
        if (WorkdaySum(report.Days.Select(day => day.AllocatedEffort)) != report.TotalEffort ||
            WorkdaySum(report.Categories.Select(category => category.Hours)) != report.TotalEffort ||
            WorkdaySum(report.Days.Select(day => day.SourceAttributedEffort)) != report.TotalEffort)
            errors.Add("Allocated day and category totals must conserve source EHE exactly.");
        if (report.Days.Any(day => WorkdaySum(day.Categories.Select(category => category.Hours)) != day.AllocatedEffort ||
            day.RecordId is null && day.AllocatedEffort != new EffortRange { Low = 0, Expected = 0, High = 0 }))
            errors.Add("Day categories must conserve allocations; undeclared days cannot receive effort.");
        HashSet<string> recordIds = new(StringComparer.Ordinal);
        foreach (ChangeWorkdayAllocationDay day in report.Days)
        {
            ValidatePublicId(day.BucketId, "day.bucketId", errors);
            if (!DateOnly.TryParseExact(day.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                errors.Add("Allocation dates must be real yyyy-MM-dd dates.");
            if (day.RecordId is { } recordId)
            {
                ValidatePublicId(recordId, "day.recordId", errors);
                if (!recordIds.Add(recordId)) errors.Add("Allocation record IDs must be unique.");
            }
            if (day.CapacityHours is <= 0 || day.CapacityHours is null && day.CapacityRatio is not null)
                errors.Add("Allocation capacity and ratios must remain consistent.");
            if (day.CapacityHours is > 0 and { } capacity)
            {
                ChangePortfolioRatioRange expected = new()
                {
                    Low = decimal.Round(day.AllocatedEffort.Low / capacity, 6, MidpointRounding.AwayFromZero),
                    Expected = decimal.Round(day.AllocatedEffort.Expected / capacity, 6, MidpointRounding.AwayFromZero),
                    High = decimal.Round(day.AllocatedEffort.High / capacity, 6, MidpointRounding.AwayFromZero),
                };
                if (day.CapacityRatio != expected) errors.Add("Allocation ratios must use preserved reference capacity.");
            }
            if (day.Categories.Select(category => category.Category).Distinct().Count() != day.Categories.Count ||
                day.Categories.Any(category => !report.Categories.Any(value => value.Category == category.Category)))
                errors.Add("Day category membership must match source categories without duplication.");
        }
        if (report.Categories.Select(category => category.Category).Distinct().Count() != report.Categories.Count)
            errors.Add("Allocation categories must be unique.");
        ChangeWorkdayAllocationDay[] declared = [.. report.Days.Where(day => day.RecordId is not null).OrderBy(day => day.Date, StringComparer.Ordinal)];
        for (int rank = 0; rank < declared.Length; rank++)
        {
            foreach (CategoryEstimate category in report.Categories)
            {
                EffortRange? actual = declared[rank].Categories.FirstOrDefault(value => value.Category == category.Category)?.Hours;
                EffortRange expected = new()
                {
                    Low = ExpectedPart(category.Hours.Low, declared.Length, rank),
                    Expected = ExpectedPart(category.Hours.Expected, declared.Length, rank),
                    High = ExpectedPart(category.Hours.High, declared.Length, rank),
                };
                if (actual != expected) errors.Add("Equal declared-day allocation must use canonical cent-hour remainders.");
            }
        }
        foreach (CategoryEstimate category in report.Categories)
            if (WorkdaySum(report.Days.SelectMany(day => day.Categories).Where(value => value.Category == category.Category)
                .Select(value => value.Hours)) != category.Hours) errors.Add("Each category must be conserved across allocated days.");
        if (report.TotalCapacityHours != (report.Days.All(day => day.CapacityHours is not null)
            ? report.Days.Sum(day => day.CapacityHours) : null)) errors.Add("Reference capacity must remain conserved independently.");
        foreach (EffortRange range in report.Days.SelectMany(day => new[] { day.AllocatedEffort, day.SourceAttributedEffort })
            .Concat(report.Days.SelectMany(day => day.Categories).Select(category => category.Hours)).Concat(report.Categories.Select(category => category.Hours)).Append(report.TotalEffort))
            if (range.Low < 0 || range.Low > range.Expected || range.Expected > range.High)
                errors.Add("Allocation ranges must be nonnegative and ordered.");
        return errors;
    }

    private static decimal ExpectedPart(decimal value, int count, int rank)
    {
        if (value < 0 || value > decimal.MaxValue / 100 || value != decimal.Round(value, 2)) return -1;
        decimal cents = value * 100;
        return (decimal.Floor(cents / count) + (rank < cents % count ? 1 : 0)) / 100;
    }

    private static EffortRange WorkdaySum(IEnumerable<EffortRange> values) => new()
    {
        Low = values.Sum(value => value.Low),
        Expected = values.Sum(value => value.Expected),
        High = values.Sum(value => value.High),
    };
}
