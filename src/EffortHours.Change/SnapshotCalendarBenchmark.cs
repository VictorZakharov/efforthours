using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

/// <summary>Selected-snapshot presentation denominator; never feeds analysis or effort.</summary>
public static class SnapshotCalendarBenchmark
{
    public static async Task<IReadOnlyList<SnapshotPeriodResult>> ApplyAsync(
        IReadOnlyList<SnapshotPeriodResult> periods, int year, TimeZoneInfo timezone,
        Func<string, CancellationToken, Task<IReadOnlyList<DateTimeOffset>>> readHistory,
        CancellationToken token)
    {
        RequireCalendar(periods, year);
        // Retain at most 366 compact inventories, each with at most 366 date starts.
        Dictionary<string, IReadOnlyList<DateTimeOffset>> inventories = new(StringComparer.Ordinal);
        foreach (SnapshotPeriodResult period in periods)
        {
            token.ThrowIfCancellationRequested();
            if (period.Status is "future" or "unavailable" or "baseline-zero" or "assumed-zero" ||
                period.CommitObjectId is null || inventories.ContainsKey(period.CommitObjectId)) continue;
            SnapshotBenchmarkInventory inventory = new(year, timezone);
            foreach (DateTimeOffset time in await readHistory(period.CommitObjectId, token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();
                inventory.Add(time);
            }
            inventories.Add(period.CommitObjectId, inventory.Starts());
        }
        return ApplyInventories(periods, inventories);
    }

    public static IReadOnlyList<SnapshotPeriodResult> Apply(IReadOnlyList<SnapshotPeriodResult> periods,
        int year, TimeZoneInfo timezone, IReadOnlyDictionary<string, IReadOnlyList<DateTimeOffset>> reachableCommitTimes)
    {
        RequireCalendar(periods, year);
        Dictionary<string, IReadOnlyList<DateTimeOffset>> inventories = new(StringComparer.Ordinal);
        foreach (SnapshotPeriodResult period in periods)
        {
            if (period.Status is "future" or "unavailable" or "baseline-zero" or "assumed-zero" ||
                period.CommitObjectId is null || inventories.ContainsKey(period.CommitObjectId)) continue;
            if (!reachableCommitTimes.TryGetValue(period.CommitObjectId, out IReadOnlyList<DateTimeOffset>? times))
                throw new InvalidDataException("Selected snapshot benchmark history is unavailable.");
            SnapshotBenchmarkInventory dates = new(year, timezone);
            foreach (DateTimeOffset time in times) dates.Add(time);
            inventories.Add(period.CommitObjectId, dates.Starts());
        }
        return ApplyInventories(periods, inventories);
    }

    private static IReadOnlyList<SnapshotPeriodResult> ApplyInventories(IReadOnlyList<SnapshotPeriodResult> periods,
        Dictionary<string, IReadOnlyList<DateTimeOffset>> inventories)
    {
        return [.. periods.Select(period =>
        {
            if (period.Status is "future" or "unavailable")
                return period with { ActiveCommitDateCount = null, BenchmarkHours = null };
            if (period.Status is "baseline-zero" or "assumed-zero")
                return period with { ActiveCommitDateCount = 0, BenchmarkHours = 0 };
            if (period.CommitObjectId is null) throw new InvalidDataException("A benchmark requires a selected immutable snapshot.");
            int count = CountBefore(inventories[period.CommitObjectId], period.Cutoff);
            return period with { ActiveCommitDateCount = count, BenchmarkHours = 8L * count };
        })];
    }

    private static void RequireCalendar(IReadOnlyList<SnapshotPeriodResult> periods, int year)
    {
        if (year is < 1970 or > 9998 || periods.Count > (DateTime.IsLeapYear(year) ? 367 : 366))
            throw new InvalidDataException("Benchmark inventories require one bounded calendar year.");
    }

    private static int CountBefore(IReadOnlyList<DateTimeOffset> times, DateTimeOffset cutoff)
    {
        int low = 0, high = times.Count;
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            if (times[mid] < cutoff) low = mid + 1;
            else high = mid;
        }
        return low;
    }
}

internal sealed class SnapshotBenchmarkInventory(int year, TimeZoneInfo timezone)
{
    private readonly DateTimeOffset?[] _dates = new DateTimeOffset?[366];

    public void Add(DateTimeOffset time)
    {
        DateTime local = TimeZoneInfo.ConvertTime(time, timezone).Date;
        if (local.Year != year) return;
        int index = local.DayOfYear - 1;
        if (_dates[index] is null || time < _dates[index]) _dates[index] = time;
    }

    public DateTimeOffset[] Starts() => [.. _dates.OfType<DateTimeOffset>().Order()];
}

public sealed partial class GitClient
{
    public async Task<IReadOnlyList<DateTimeOffset>> ReadSnapshotBenchmarkHistoryAsync(string repositoryPath,
        string selectedObjectId, int year, TimeZoneInfo timezone, CancellationToken cancellationToken)
    {
        SnapshotBenchmarkInventory inventory = new(year, timezone);
        long charged = 0;
        await _commands.RunStreamingAsync("git", repositoryPath, ["log", "--format=%ct", selectedObjectId, "--"],
            async (reader, token) =>
            {
                while (await reader.ReadLineAsync(token).ConfigureAwait(false) is { } line)
                {
                    charged += line.Length * 2L + 128;
                    if (charged > 128L * 1024 * 1024)
                        throw new InvalidOperationException("Reachable benchmark history exceeds the 128-MiB accounting budget.");
                    if (!long.TryParse(line, NumberStyles.Integer, CultureInfo.InvariantCulture, out long timestamp))
                        throw new InvalidDataException("Git returned malformed benchmark history.");
                    inventory.Add(DateTimeOffset.FromUnixTimeSeconds(timestamp));
                }
            }, cancellationToken).ConfigureAwait(false);
        return inventory.Starts();
    }
}
