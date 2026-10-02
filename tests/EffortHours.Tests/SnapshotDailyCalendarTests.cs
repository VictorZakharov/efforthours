using System.Globalization;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class SnapshotDailyCalendarTests
{
    [Fact]
    public void DailySelectionKeepsTraversalOrderStrictCutoffsIdleDaysAndLeapDates()
    {
        SnapshotHistoryCommit[] history =
        [
            new(new string('a', 40), new string('b', 40), Instant("2024-03-02T00:00:00Z")),
            new(new string('c', 40), new string('d', 40), Instant("2024-02-28T12:00:00Z")),
            new(new string('e', 40), new string('f', 40), Instant("2024-03-01T12:00:00Z")),
        ];
        IReadOnlyList<SnapshotPeriodResult> periods = Select(2024, "UTC", "2024-03-02T00:00:00Z", history);
        Assert.Equal(367, periods.Count);
        Assert.Equal("baseline-zero", periods[0].Status);
        Assert.Equal("assumed-zero", Day(periods, "2024-02-27").Status);
        Assert.Equal(history[1].ObjectId, Day(periods, "2024-02-29").CommitObjectId);
        Assert.Equal(history[1].ObjectId, Day(periods, "2024-03-01").CommitObjectId);
        Assert.Equal(history[1].ObjectId, Day(periods, "2024-03-02").CommitObjectId);
        Assert.Equal("partial", Day(periods, "2024-03-02").Status);
        Assert.Equal("future", Day(periods, "2024-03-03").Status);
        Assert.Null(Day(periods, "2024-03-03").Hours);
    }

    [Fact]
    public void DailyAndMonthlySelectionAgreeAtEveryEndpointAcrossDst()
    {
        TimeZoneInfo timezone = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");
        DateTimeOffset asOf = Instant("2026-11-15T12:34:56Z");
        SnapshotHistoryCommit[] history = [.. Enumerable.Range(1, 12).Reverse().Select(m =>
            new SnapshotHistoryCommit(new string((char)('a' + m % 6), 40), new string('e', 40),
                new DateTimeOffset(2026, m, 1, 0, 0, 0, TimeSpan.Zero)))];
        IReadOnlyList<SnapshotPeriodResult> daily = SnapshotPortfolioSelection.Select(2026, timezone, asOf, history, SnapshotPortfolioVersions.Daily);
        IReadOnlyList<SnapshotPeriodResult> monthly = SnapshotPortfolioSelection.Select(2026, timezone, asOf, history);
        IReadOnlyList<SnapshotPeriodResult> endpoints = SnapshotDailyCalendar.Endpoints(2026, timezone.Id, asOf, daily);
        Assert.Equal(monthly, endpoints);
        Assert.Equal(23, (Day(daily, "2026-03-08").Cutoff - Day(daily, "2026-03-07").Cutoff).TotalHours);
        Assert.Equal(25, (Day(daily, "2026-11-01").Cutoff - Day(daily, "2026-10-31").Cutoff).TotalHours);
    }

    [Fact]
    public void SignedChangesTelescopeForEachMonthAndFilteredPortfolioIncludingPartialDay()
    {
        SnapshotPortfolioReport report = Report(12);
        foreach (SnapshotProjectResult project in report.Projects)
        {
            SnapshotDailyCalendar.Validate(report, project);
            Assert.Contains(project.Periods, p => p.ExpectedChangeCentihours < 0);
            Assert.Null(project.Periods[0].ExpectedChangeCentihours);
            Assert.All(project.Periods.Where(p => p.Status == "future"), p => Assert.Null(p.ExpectedChangeCentihours));
        }
        foreach (int[] subset in new[] { new[] { 0 }, [10, 11], [.. Enumerable.Range(0, 12)] })
            for (int month = 1; month <= 10; month++)
            {
                string prefix = $"2026-{month:00}-";
                long daily = subset.Sum(i => report.Projects[i].Periods.Where(p => p.Id.StartsWith(prefix, StringComparison.Ordinal))
                    .Sum(p => p.ExpectedChangeCentihours ?? 0));
                decimal monthly = subset.Sum(i => report.Projects[i].MonthlyEndpoints![month].Hours!.Expected -
                    report.Projects[i].MonthlyEndpoints![month - 1].Hours!.Expected);
                Assert.Equal(monthly * 100, daily);
            }
        SnapshotProjectResult first = report.Projects[0];
        SnapshotPeriodResult[] changed = [.. first.Periods];
        changed[10] = changed[10] with { ExpectedChangeCentihours = changed[10].ExpectedChangeCentihours + 1 };
        Assert.Throws<InvalidDataException>(() => SnapshotDailyCalendar.Validate(report, first with { Periods = changed }));
        Assert.Throws<InvalidDataException>(() => SnapshotDailyCalendar.Validate(report, first with
        {
            MonthlyEndpoints = [.. first.MonthlyEndpoints!.Select((p, i) => i == 1 ? p with { WholeReceiptId = "different" } : p)],
        }));
    }

    [Fact]
    public void BenchmarkCountsDistinctReachableDatesIncludingWeekendsAndNeverRoundsMultipliers()
    {
        IReadOnlyList<SnapshotPeriodResult> periods = SnapshotCalendarBenchmark.Apply(
            Select(2026, "UTC", "2026-01-05T12:00:00Z", [new("snapshot", "tree", Instant("2026-01-03T12:00:00Z"))]),
            2026, TimeZoneInfo.Utc, new Dictionary<string, IReadOnlyList<DateTimeOffset>>
            { ["snapshot"] = [Instant("2026-01-03T18:00:00Z"), Instant("2026-01-03T12:00:00Z"), Instant("2026-01-04T12:00:00Z")] });
        Assert.Equal(0, Day(periods, "2026-01-02").ActiveCommitDateCount);
        Assert.Equal(1, Day(periods, "2026-01-03").ActiveCommitDateCount);
        Assert.Equal(2, Day(periods, "2026-01-04").ActiveCommitDateCount);
        Assert.Equal(16, Day(periods, "2026-01-05").BenchmarkHours);
        Assert.Null(Day(periods, "2026-01-06").BenchmarkHours);
        // Consumers compute stock / benchmark; a zero denominator remains undefined.
        decimal[] stock = [17.53m, 11.02m, 45.67m, 33.44m];
        decimal[] multipliers = [.. stock.Select((h, i) => h / ((i + 1) * 8))];
        decimal sum = Enumerable.Range(1, 3).Sum(i => multipliers[i] - multipliers[i - 1]);
        Assert.InRange(decimal.Abs(sum - (multipliers[^1] - multipliers[0])), 0m, 0.00000000000000000001m);
    }

    [Fact]
    public void DailyPolicyIsExplicitAndLegacyJsonDoesNotGainNewDefaults()
    {
        SnapshotPortfolioManifest manifest = new()
        {
            Year = 2026,
            Timezone = "UTC",
            Profile = EstimationProfile.Implementation,
            Projects = [new() { Id = "demo", Ref = "main", Areas = [new() { Id = "all", Include = ["**"] }] }]
        };
        Assert.DoesNotContain("calendarPolicy", ContractJson.Serialize(manifest), StringComparison.Ordinal);
        manifest = manifest with { CalendarPolicy = SnapshotPortfolioVersions.Daily };
        SnapshotPortfolioValidation.Validate(manifest);
        Assert.True(ContractSchemaValidator.Validate("snapshot-portfolio-manifest.schema.json", ContractJson.Serialize(manifest)).IsValid);
        Assert.Throws<InvalidDataException>(() => SnapshotPortfolioValidation.Validate(manifest with { CalendarPolicy = "daily" }));
    }

    private static SnapshotPortfolioReport Report(int count)
    {
        IReadOnlyList<SnapshotPeriodResult> selected = SnapshotCalendarBenchmark.Apply(
            Select(2026, "UTC", "2026-10-01T12:00:00Z", []), 2026, TimeZoneInfo.Utc,
            new Dictionary<string, IReadOnlyList<DateTimeOffset>>());
        return new()
        {
            Status = "complete",
            AsOf = Instant("2026-10-01T12:00:00Z"),
            Year = 2026,
            Timezone = "UTC",
            ManifestDigest = "",
            MeasurementEpoch = "",
            SemanticDigest = "",
            CalendarPolicy = SnapshotPortfolioVersions.Daily,
            Projects = [.. Enumerable.Range(0, count).Select(i =>
            {
                IReadOnlyList<SnapshotPeriodResult> periods = SnapshotDailyCalendar.Differences([.. selected.Select((p, day) =>
                    p.Hours is null || day == 0 ? p : p with { Hours = new() { Low = day % 5 * (i + 1),
                        Expected = day % 5 * (i + 1), High = day % 5 * (i + 1) } })]);
                return new SnapshotProjectResult { Id = $"project-{i}", AreasDigest = "", ShallowHistory = false, Periods = periods,
                    SelectedSnapshotCount = 0, DistinctSnapshotCount = 0,
                    MonthlyEndpoints = SnapshotDailyCalendar.Endpoints(2026, "UTC", Instant("2026-10-01T12:00:00Z"), periods) };
            })],
        };
    }

    private static IReadOnlyList<SnapshotPeriodResult> Select(int year, string timezone, string asOf,
        IReadOnlyList<SnapshotHistoryCommit> history) => SnapshotPortfolioSelection.Select(year,
            TimeZoneInfo.FindSystemTimeZoneById(timezone), Instant(asOf), history, SnapshotPortfolioVersions.Daily);
    private static SnapshotPeriodResult Day(IReadOnlyList<SnapshotPeriodResult> periods, string id) => periods.Single(p => p.Id == id);
    private static DateTimeOffset Instant(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
}
