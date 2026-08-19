namespace EffortHours.Change;

internal readonly record struct GitSnapshotTreeEntry(
    string Mode,
    string Type,
    string ObjectId,
    string Path);

internal static class GitSnapshotTreeReader
{
    internal const int MaximumParallelReads = 12;
    internal const int MinimumParallelTreePaths = 256;
    private const int MaximumExpansionLevels = 2;
    internal const int MaximumShardPathCharacters = 16_000;

    public static async Task<IReadOnlyList<ChangeSnapshotFile>> ReadAsync(
        string repositoryPath,
        string objectId,
        int requestedParallelism,
        CancellationToken cancellationToken,
        int minimumParallelTreePaths = MinimumParallelTreePaths)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumParallelTreePaths, 1);
        int parallelism = Math.Clamp(requestedParallelism, 1, MaximumParallelReads);
        if (parallelism == 1)
        {
            return await ReadLeavesAsync(
                repositoryPath,
                objectId,
                pathspecs: null,
                cancellationToken).ConfigureAwait(false);
        }

        List<ChangeSnapshotFile> files = [];
        List<string> frontier = [];
        AddEntries(
            await ReadEntriesAsync(
                repositoryPath,
                objectId,
                pathspecs: null,
                cancellationToken).ConfigureAwait(false),
            files,
            frontier);

        for (int level = 0;
             level < MaximumExpansionLevels && frontier.Count < parallelism * 2;
             level++)
        {
            if (frontier.Count == 0)
            {
                return files;
            }

            string[] expanded = [.. frontier.Order(StringComparer.Ordinal)];
            frontier.Clear();
            AddEntries(
                await ReadEntriesAsync(
                    repositoryPath,
                    objectId,
                    expanded,
                    cancellationToken).ConfigureAwait(false),
                files,
                frontier);
        }

        if (frontier.Count == 0)
        {
            return files;
        }

        if (frontier.Count < minimumParallelTreePaths)
        {
            if (frontier.Sum(path => path.Length + 1) > MaximumShardPathCharacters)
            {
                return await ReadLeavesAsync(
                    repositoryPath,
                    objectId,
                    pathspecs: null,
                    cancellationToken).ConfigureAwait(false);
            }

            files.AddRange(await ReadLeavesAsync(
                repositoryPath,
                objectId,
                frontier,
                cancellationToken).ConfigureAwait(false));
            return files;
        }

        string[][] shards = Partition(frontier, Math.Min(parallelism, frontier.Count));
        if (!FitsCommandLine(shards))
        {
            return await ReadLeavesAsync(
                repositoryPath,
                objectId,
                pathspecs: null,
                cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<ChangeSnapshotFile>[] descendants = await Task.WhenAll(
            shards.Select(paths => ReadLeavesAsync(
                repositoryPath,
                objectId,
                paths,
                cancellationToken))).ConfigureAwait(false);
        foreach (IReadOnlyList<ChangeSnapshotFile> shard in descendants)
        {
            files.AddRange(shard);
        }

        return files;
    }

    internal static string[][] Partition(IReadOnlyList<string> paths, int shardCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(shardCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(shardCount, paths.Count);
        List<string>[] shards = [.. Enumerable.Range(0, shardCount)
            .Select(_ => new List<string>())];
        int index = 0;
        foreach (string path in paths.Order(StringComparer.Ordinal))
        {
            shards[index++ % shardCount].Add(path);
        }

        return [.. shards.Select(shard => shard.ToArray())];
    }

    internal static bool FitsCommandLine(IEnumerable<IReadOnlyList<string>> shards) =>
        shards.All(shard => shard.Sum(path => path.Length + 1) <= MaximumShardPathCharacters);

    private static void AddEntries(
        IEnumerable<GitSnapshotTreeEntry> entries,
        List<ChangeSnapshotFile> files,
        List<string> frontier)
    {
        foreach (GitSnapshotTreeEntry entry in entries)
        {
            if (entry.Type == "tree")
            {
                frontier.Add(entry.Path);
                continue;
            }

            files.Add(new ChangeSnapshotFile
            {
                Mode = entry.Mode,
                ObjectId = entry.ObjectId,
                Length = entry.Type == "commit" ? 0L : -1L,
                Path = entry.Path,
            });
        }
    }

    private static async Task<IReadOnlyList<ChangeSnapshotFile>> ReadLeavesAsync(
        string repositoryPath,
        string objectId,
        IReadOnlyList<string>? pathspecs,
        CancellationToken cancellationToken)
    {
        byte[] output = await RunGitAsync(
            repositoryPath,
            objectId,
            recursive: true,
            pathspecs,
            cancellationToken).ConfigureAwait(false);
        return GitSnapshotFileSystem.ParseTreeWithoutLengths(output);
    }

    private static async Task<IReadOnlyList<GitSnapshotTreeEntry>> ReadEntriesAsync(
        string repositoryPath,
        string objectId,
        IReadOnlyList<string>? pathspecs,
        CancellationToken cancellationToken)
    {
        byte[] output = await RunGitAsync(
            repositoryPath,
            objectId,
            recursive: false,
            pathspecs,
            cancellationToken).ConfigureAwait(false);
        return GitSnapshotFileSystem.ParseTreeEntries(output);
    }

    private static async Task<byte[]> RunGitAsync(
        string repositoryPath,
        string objectId,
        bool recursive,
        IReadOnlyList<string>? pathspecs,
        CancellationToken cancellationToken)
    {
        List<string> arguments = ["--literal-pathspecs", "ls-tree"];
        if (recursive)
        {
            arguments.Add("-r");
        }

        arguments.Add("-z");
        arguments.Add("--full-tree");
        arguments.Add(objectId);
        if (pathspecs is { Count: > 0 })
        {
            arguments.Add("--");
            arguments.AddRange(pathspecs.Select(path => $"{path}/"));
        }

        return await ExternalCommand.RunBinaryAsync(
            "git",
            repositoryPath,
            arguments,
            cancellationToken).ConfigureAwait(false);
    }
}
