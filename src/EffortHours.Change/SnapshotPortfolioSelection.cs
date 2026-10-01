using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record SnapshotHistoryCommit(string ObjectId, string TreeObjectId, DateTimeOffset CommittedAt);

public static class SnapshotPortfolioSelection
{
    public static IReadOnlyList<SnapshotPeriodResult> Select(
        int year, TimeZoneInfo timezone, DateTimeOffset asOf, IReadOnlyList<SnapshotHistoryCommit> history)
    {
        List<SnapshotPeriodResult> result = [];
        DateTimeOffset start = Boundary(new DateTime(year, 1, 1), timezone);
        result.Add(new SnapshotPeriodResult
        {
            Id = $"{year}-baseline",
            Status = start > asOf ? "future" : "baseline-zero",
            Cutoff = start,
            Hours = start > asOf ? null : Zero,
        });
        for (int month = 1; month <= 12; month++)
        {
            DateTimeOffset monthStart = Boundary(new DateTime(year, month, 1), timezone);
            DateTimeOffset monthEnd = Boundary(new DateTime(year, month, 1).AddMonths(1), timezone);
            bool future = monthStart > asOf;
            bool partial = !future && asOf < monthEnd;
            DateTimeOffset cutoff = partial ? asOf : monthEnd;
            // History is in first-parent traversal order, not timestamp-sorted order.
            SnapshotHistoryCommit? selected = future ? null : history.FirstOrDefault(c => c.CommittedAt < cutoff);
            result.Add(new SnapshotPeriodResult
            {
                Id = $"{year}-{month:00}",
                Status = future ? "future" : selected is null
                    ? "assumed-zero" : partial ? "partial" : "complete",
                Cutoff = cutoff,
                CommitObjectId = selected?.ObjectId,
                TreeObjectId = selected?.TreeObjectId,
                CommitAt = selected?.CommittedAt,
                Hours = !future && selected is null ? Zero : null,
            });
        }
        return result;
    }

    public static EffortRange Zero { get; } = new() { Low = 0m, Expected = 0m, High = 0m };

    private static DateTimeOffset Boundary(DateTime value, TimeZoneInfo timezone)
    {
        if (timezone.IsInvalidTime(value))
        {
            // Calendar transitions can skip midnight. The boundary is the first valid local instant.
            do { value = value.AddMinutes(1); } while (timezone.IsInvalidTime(value));
        }
        TimeSpan offset = timezone.IsAmbiguousTime(value)
            ? timezone.GetAmbiguousTimeOffsets(value).Max() : timezone.GetUtcOffset(value);
        return new DateTimeOffset(value, offset).ToUniversalTime();
    }
}

public sealed partial class GitClient
{
    public async Task<(IReadOnlyList<SnapshotHistoryCommit> Commits, bool Shallow)> ReadSnapshotHistoryAsync(
        string repositoryPath, string headObjectId, CancellationToken cancellationToken = default)
    {
        List<SnapshotHistoryCommit> commits = [];
        long charged = 0;
        await _commands.RunStreamingAsync("git", repositoryPath,
            ["log", "--first-parent", "--format=%H %T %ct", headObjectId, "--"], async (reader, token) =>
            {
                while (await reader.ReadLineAsync(token).ConfigureAwait(false) is { } line)
                {
                    charged += line.Length * 2L + 128;
                    if (charged > 128L * 1024 * 1024)
                        throw new InvalidOperationException("First-parent history exceeds the 128-MiB selection budget.");
                    string[] values = line.Split(' ');
                    if (values.Length != 3 || !long.TryParse(values[2], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out long timestamp))
                        throw new InvalidDataException("Git returned malformed first-parent history.");
                    commits.Add(new SnapshotHistoryCommit(RequireObjectId(values[0], "history"),
                        RequireObjectId(values[1], "tree"), DateTimeOffset.FromUnixTimeSeconds(timestamp)));
                }
            }, cancellationToken).ConfigureAwait(false);
        if (commits.Count == 0) throw new InvalidDataException("Pinned head has no first-parent history.");
        ExternalCommandResult shallow = await _commands.RunAsync("git", repositoryPath,
            ["rev-parse", "--is-shallow-repository"], cancellationToken).ConfigureAwait(false);
        return (commits, shallow.StandardOutput.Trim() == "true");
    }
}
