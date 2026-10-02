using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

/// <summary>Presentation denominator only; never feeds repository analysis or effort.</summary>
public static class SnapshotCalendarBenchmark
{
    public static IReadOnlyList<SnapshotPeriodResult> Apply(IReadOnlyList<SnapshotPeriodResult> periods,
        TimeZoneInfo timezone, IEnumerable<DateTimeOffset> reachableCommitTimes)
    {
        // A date becomes active at its earliest commit, even with duplicate or nonmonotonic dates.
        DateTimeOffset[] starts = [.. reachableCommitTimes.GroupBy(t => TimeZoneInfo.ConvertTime(t, timezone).Date)
            .Select(g => g.Min()).Order()];
        return [.. periods.Select(p => p.Status == "future" ? p : p with
        {
            ActiveCommitDateCount = CountBefore(starts, p.Cutoff),
            BenchmarkHours = 8L * CountBefore(starts, p.Cutoff),
        })];
    }

    private static int CountBefore(DateTimeOffset[] times, DateTimeOffset cutoff)
    {
        int low = 0, high = times.Length;
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            if (times[mid] < cutoff) low = mid + 1;
            else high = mid;
        }
        return low;
    }
}

public sealed partial class GitClient
{
    public async Task<IReadOnlyList<DateTimeOffset>> ReadSnapshotBenchmarkHistoryAsync(string repositoryPath,
        string headObjectId, CancellationToken cancellationToken)
    {
        List<DateTimeOffset> times = [];
        await _commands.RunStreamingAsync("git", repositoryPath, ["log", "--format=%ct", headObjectId, "--"],
            async (reader, token) =>
            {
                while (await reader.ReadLineAsync(token).ConfigureAwait(false) is { } line)
                {
                    if (times.Count >= 128 * 1024 * 1024 / 128)
                        throw new InvalidOperationException("Reachable benchmark history exceeds the 128-MiB accounting budget.");
                    if (!long.TryParse(line, NumberStyles.Integer, CultureInfo.InvariantCulture, out long timestamp))
                        throw new InvalidDataException("Git returned malformed benchmark history.");
                    times.Add(DateTimeOffset.FromUnixTimeSeconds(timestamp));
                }
            }, cancellationToken).ConfigureAwait(false);
        return times;
    }
}
