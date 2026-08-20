using System.Globalization;

namespace EffortHours.Change;

internal readonly record struct GitObjectStorageLayout(
    long LooseObjectCount,
    long PackedObjectCount)
{
    public static async Task<GitObjectStorageLayout> ReadAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        ExternalCommandResult result = await ExternalCommand.RunAsync(
            "git",
            repositoryPath,
            ["count-objects", "-v"],
            cancellationToken).ConfigureAwait(false);
        return Parse(result.StandardOutput);
    }

    internal int SelectTreeReadParallelism(int maximumParallelism)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumParallelism, 1);
        return LooseObjectCount >= GitSnapshotTreeReader.MinimumLooseObjectsForParallelRead
            ? Math.Min(maximumParallelism, GitSnapshotTreeReader.MaximumParallelReads)
            : 1;
    }

    internal static GitObjectStorageLayout Parse(string output)
    {
        Dictionary<string, long> values = new(StringComparer.Ordinal);
        foreach (string line in output.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = line.IndexOf(':');
            if (separator <= 0 || separator == line.Length - 1 ||
                !long.TryParse(
                    line.AsSpan(separator + 1).Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long value) ||
                value < 0)
            {
                continue;
            }

            values[line[..separator]] = value;
        }

        if (!values.TryGetValue("count", out long looseObjects) ||
            !values.TryGetValue("in-pack", out long packedObjects))
        {
            throw new InvalidOperationException(
                "Git did not report its object-storage layout in the expected format.");
        }

        return new GitObjectStorageLayout(looseObjects, packedObjects);
    }
}
