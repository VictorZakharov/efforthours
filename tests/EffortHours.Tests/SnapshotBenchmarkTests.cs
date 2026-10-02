using System.Globalization;
using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class SnapshotBenchmarkTests
{
    [Fact]
    public async Task SelectedCommitInventoriesAreReusedAcrossIdleDaysAndMonthlyOperandsAgree()
    {
        SnapshotHistoryCommit[] history = [new("merged", "tree-2", At("2026-01-04T12:00:00Z")),
            new("main", "tree-1", At("2026-01-01T12:00:00Z"))];
        Dictionary<string, IReadOnlyList<DateTimeOffset>> ancestry = new(StringComparer.Ordinal)
        {
            ["main"] = [At("2025-12-30T12:00:00Z"), At("2026-01-01T12:00:00Z")],
            ["merged"] = [At("2025-12-30T12:00:00Z"), At("2026-01-01T12:00:00Z"),
                At("2026-01-02T12:00:00Z"), At("2026-01-02T18:00:00Z"), At("2026-01-04T12:00:00Z")],
        };
        List<string> reads = [];
        DateTimeOffset asOf = At("2026-01-05T12:00:00Z");
        IReadOnlyList<SnapshotPeriodResult> periods = await SnapshotCalendarBenchmark.ApplyAsync(
            SnapshotPortfolioSelection.Select(2026, TimeZoneInfo.Utc, asOf, history, SnapshotPortfolioVersions.Daily),
            2026, TimeZoneInfo.Utc, (commit, _) => { reads.Add(commit); return Task.FromResult(ancestry[commit]); }, CancellationToken.None);
        Assert.Equal(["main", "merged"], reads);
        Assert.Equal([0, 1, 1, 1, 3, 3], periods.Take(6).Select(p => p.ActiveCommitDateCount));
        Assert.All(periods.Skip(6), p => Assert.Null(p.BenchmarkHours));
        IReadOnlyList<SnapshotPeriodResult> monthly = SnapshotCalendarBenchmark.Apply(
            SnapshotPortfolioSelection.Select(2026, TimeZoneInfo.Utc, asOf, history), 2026, TimeZoneInfo.Utc, ancestry);
        SnapshotPeriodResult closing = periods[5];
        Assert.Equal(monthly[1].CommitObjectId, closing.CommitObjectId);
        Assert.Equal(monthly[1].BenchmarkHours, closing.BenchmarkHours);
        decimal[] ratios = [.. periods.Skip(1).Take(5).Select(p => (p.CommitObjectId == "main" ? 17.53m : 45.67m) / p.BenchmarkHours!.Value)];
        decimal changes = Enumerable.Range(1, ratios.Length - 1).Sum(i => ratios[i] - ratios[i - 1]);
        decimal monthlyRatio = 45.67m / monthly[1].BenchmarkHours!.Value;
        Assert.InRange(decimal.Abs(changes - (monthlyRatio - ratios[0])), 0, 0.00000000000000000001m);
    }

    [Fact]
    public void LocalYearFilteringPrecedesExclusiveCutoffAndHandlesNonmonotonicAncestry()
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");
        SnapshotHistoryCommit[] history = [new("selected", "tree", At("2026-01-01T06:00:00Z"))];
        Dictionary<string, IReadOnlyList<DateTimeOffset>> ancestry = new(StringComparer.Ordinal)
        {
            ["selected"] = [At("2026-01-01T03:00:00Z"), // UTC year 2026, local year 2025.
                At("2026-01-01T05:00:00Z"), At("2026-01-01T06:00:00Z"), // Same local active date.
                At("2026-01-03T05:00:00Z"), // A future-dated ancestor, exactly January 2's cutoff.
                At("2027-01-01T05:00:00Z")],
        };
        IReadOnlyList<SnapshotPeriodResult> periods = SnapshotCalendarBenchmark.Apply(
            SnapshotPortfolioSelection.Select(2026, zone, At("2026-01-04T12:00:00Z"), history, SnapshotPortfolioVersions.Daily),
            2026, zone, ancestry);
        Assert.Equal(0, periods[0].ActiveCommitDateCount);
        Assert.Equal(1, periods[1].ActiveCommitDateCount);
        Assert.Equal(1, periods[2].ActiveCommitDateCount);
        Assert.Equal(2, periods[3].ActiveCommitDateCount);
        Assert.Equal(2, periods[4].ActiveCommitDateCount);
        Assert.Equal(history[0].ObjectId, periods[3].CommitObjectId);
    }

    [Fact]
    public async Task UnselectedAndFuturePeriodsNeverReadLaterHeadAncestryAndCancellationIsPreserved()
    {
        IReadOnlyList<SnapshotPeriodResult> periods = SnapshotPortfolioSelection.Select(2026, TimeZoneInfo.Utc,
            At("2026-01-02T12:00:00Z"), [], SnapshotPortfolioVersions.Daily);
        IReadOnlyList<SnapshotPeriodResult> result = await SnapshotCalendarBenchmark.ApplyAsync(periods, 2026,
            TimeZoneInfo.Utc, (_, _) => throw new InvalidOperationException("No selected commit to query."), CancellationToken.None);
        Assert.All(result.Take(3), p => Assert.Equal(0, p.BenchmarkHours));
        Assert.All(result.Skip(3), p => Assert.Null(p.BenchmarkHours));
        SnapshotPeriodResult unavailable = result[1] with { Status = "unavailable" };
        Assert.Null(SnapshotCalendarBenchmark.Apply([unavailable], 2026, TimeZoneInfo.Utc,
            new Dictionary<string, IReadOnlyList<DateTimeOffset>>())[0].BenchmarkHours);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SnapshotCalendarBenchmark.ApplyAsync(periods,
            2026, TimeZoneInfo.Utc, (_, _) => throw new InvalidOperationException(), cancellation.Token));
        Assert.Throws<InvalidDataException>(() => SnapshotCalendarBenchmark.Apply([.. Enumerable.Repeat(result[0], 368)],
            2026, TimeZoneInfo.Utc, new Dictionary<string, IReadOnlyList<DateTimeOffset>>()));
    }

    [Fact]
    public void DailyValidationRejectsCapacityOnDeclaredZeroAndOutOfYearDateCounts()
    {
        IReadOnlyList<SnapshotPeriodResult> periods = SnapshotCalendarBenchmark.Apply(
            SnapshotPortfolioSelection.Select(2026, TimeZoneInfo.Utc, At("2026-01-02T12:00:00Z"), [], SnapshotPortfolioVersions.Daily),
            2026, TimeZoneInfo.Utc, new Dictionary<string, IReadOnlyList<DateTimeOffset>>());
        SnapshotPortfolioReport report = new()
        {
            Status = "complete",
            Projects = [],
            Year = 2026,
            Timezone = "UTC",
            AsOf = At("2026-01-02T12:00:00Z"),
            ManifestDigest = "",
            MeasurementEpoch = "",
            SemanticDigest = ""
        };
        SnapshotProjectResult project = new()
        {
            Id = "demo",
            AreasDigest = "",
            ShallowHistory = false,
            SelectedSnapshotCount = 0,
            DistinctSnapshotCount = 0,
            Periods = SnapshotDailyCalendar.Differences(periods)
        };
        foreach (int count in new[] { 1, 366 })
            Assert.Throws<InvalidDataException>(() => SnapshotDailyCalendar.Validate(report, project with
            { Periods = [project.Periods[0] with { ActiveCommitDateCount = count, BenchmarkHours = count * 8 }, .. project.Periods.Skip(1)] }));
        SnapshotPeriodResult outOfYear = project.Periods[1] with
        {
            Status = "complete",
            CommitObjectId = "selected",
            ActiveCommitDateCount = 366,
            BenchmarkHours = 366 * 8
        };
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => SnapshotDailyCalendar.Validate(report, project with
        { SelectedSnapshotCount = 1, DistinctSnapshotCount = 1, Periods = [project.Periods[0], outOfYear, .. project.Periods.Skip(2)] }));
        Assert.Contains("selected year", error.Message, StringComparison.Ordinal);
    }

    private static DateTimeOffset At(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
