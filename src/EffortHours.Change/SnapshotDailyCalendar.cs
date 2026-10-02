using EffortHours.Contracts.V1;

namespace EffortHours.Change;

/// <summary>Exact stock differences and month endpoints; never distributes Change EHE.</summary>
public static class SnapshotDailyCalendar
{
    public static IReadOnlyList<SnapshotPeriodResult> Differences(IReadOnlyList<SnapshotPeriodResult> periods) =>
        [.. periods.Select((period, index) => period with
        {
            ExpectedChangeCentihours = index == 0 || period.Hours is null || periods[index - 1].Hours is null
                ? null : Centihours(period.Hours.Expected - periods[index - 1].Hours!.Expected),
        })];

    public static IReadOnlyList<SnapshotPeriodResult> Endpoints(int year, string timezone, DateTimeOffset asOf,
        IReadOnlyList<SnapshotPeriodResult> periods)
    {
        IReadOnlyList<SnapshotPeriodResult> calendar = SnapshotPortfolioSelection.Select(year,
            TimeZoneInfo.FindSystemTimeZoneById(timezone), asOf, []);
        return [.. calendar.Select((month, index) => index == 0 ? periods[0] :
            month.Status == "future" ? month : periods.Last(p => p.Id.StartsWith(month.Id + "-", StringComparison.Ordinal) &&
                p.Status != "future") with { Id = month.Id, ExpectedChangeCentihours = null })];
    }

    public static void Validate(SnapshotPortfolioReport report, SnapshotProjectResult project)
    {
        IReadOnlyList<SnapshotPeriodResult> differences = Differences(project.Periods);
        if (!differences.Select(p => p.ExpectedChangeCentihours).SequenceEqual(project.Periods.Select(p => p.ExpectedChangeCentihours)))
            throw new InvalidDataException("Daily differences must be signed exact stock centihours.");
        IReadOnlyList<SnapshotPeriodResult> endpoints = Endpoints(report.Year, report.Timezone, report.AsOf, project.Periods);
        if (project.SelectedSnapshotCount != project.Periods.Count(p => p.CommitObjectId is not null) ||
            project.DistinctSnapshotCount != project.Periods.Where(p => p.CommitObjectId is not null).Select(p => p.CommitObjectId).Distinct().Count())
            throw new InvalidDataException("Daily snapshot scope counts are invalid.");
        foreach (SnapshotPeriodResult period in project.Periods)
        {
            if (period.Status is "baseline-zero" or "assumed-zero" &&
                (period.CommitObjectId is not null || period.TreeObjectId is not null || period.CommitAt is not null))
                throw new InvalidDataException("Daily zero conventions cannot carry selected source provenance.");
            if (period.Status == "future")
            {
                if (period.ActiveCommitDateCount is not null || period.BenchmarkHours is not null)
                    throw new InvalidDataException("Future benchmark operands are unavailable.");
            }
            else if (period.ActiveCommitDateCount is null or < 0 || period.BenchmarkHours != 8L * period.ActiveCommitDateCount)
                throw new InvalidDataException("Daily benchmark requires distinct active dates and eight hours per date.");
            else if (period.ActiveCommitDateCount > (DateTime.IsLeapYear(report.Year) ? 366 : 365) ||
                period.Status is "baseline-zero" or "assumed-zero" && period.ActiveCommitDateCount != 0)
                throw new InvalidDataException("Daily benchmark must stay within the selected year and explicit zero conventions.");
        }
        if (project.MonthlyEndpoints is null || SnapshotMeasurementIdentity.Digest(endpoints) != SnapshotMeasurementIdentity.Digest(project.MonthlyEndpoints))
            throw new InvalidDataException("Daily month endpoints must retain exact selection, receipts, and stock.");
        for (int month = 1; month <= 12; month++)
        {
            SnapshotPeriodResult closing = endpoints[month];
            if (closing.Hours is null) continue;
            if (endpoints[month - 1].Hours is null)
                throw new InvalidDataException("A measured month requires its measured opening stock.");
            long sum = project.Periods.Where(p => p.Id.StartsWith(closing.Id + "-", StringComparison.Ordinal))
                .Sum(p => p.ExpectedChangeCentihours ?? 0);
            if (sum != Centihours(closing.Hours.Expected - endpoints[month - 1].Hours!.Expected))
                throw new InvalidDataException("Daily values do not reconcile to monthly stock changes.");
        }
    }

    public static void ValidateReference(SnapshotPortfolioReport report, SnapshotPortfolioReport reference)
    {
        if (reference.CalendarPolicy is not null || reference.Year != report.Year || reference.Timezone != report.Timezone)
            return;
        foreach (SnapshotProjectResult project in report.Projects)
        {
            SnapshotProjectResult? prior = reference.Projects.FirstOrDefault(p => p.Id == project.Id);
            if (prior is null || prior.HeadObjectId != project.HeadObjectId) continue;
            foreach (SnapshotPeriodResult endpoint in project.MonthlyEndpoints!)
            {
                SnapshotPeriodResult? saved = prior.Periods.FirstOrDefault(p => p.Id == endpoint.Id && p.Cutoff == endpoint.Cutoff);
                if (saved is null || saved.Status == "future") continue;
                if (endpoint.CommitObjectId != saved.CommitObjectId || endpoint.TreeObjectId != saved.TreeObjectId ||
                    endpoint.WholeReceiptId != saved.WholeReceiptId || endpoint.Hours != saved.Hours)
                    throw new InvalidDataException("Daily endpoint disagrees with the saved monthly selection or receipt.");
            }
        }
    }

    private static long Centihours(decimal hours) => hours * 100m == decimal.Truncate(hours * 100m)
        ? checked((long)(hours * 100m)) : throw new InvalidDataException("Stock changes require exact centihours.");
}
