using System.Globalization;
using System.Text;

namespace EffortHours.Change;

internal sealed record GitSnapshotDelta(
    IReadOnlyList<string> ChangedPaths,
    IReadOnlyList<ChangeSnapshotFile> ChangedFiles);

internal static class GitSnapshotDeltaBatch
{
    internal const int MaximumBatchOutputBytes = 64 * 1024 * 1024;
    private const string CommitPrefix = "COMMIT:";
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static async Task<IReadOnlyDictionary<string, GitSnapshotDelta>> ReadAsync(
        string repositoryPath,
        IReadOnlyList<string> commitObjectIds,
        CancellationToken cancellationToken)
    {
        string[] commits = [.. commitObjectIds
            .Select(objectId => objectId.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        if (commits.Length == 0)
        {
            return new Dictionary<string, GitSnapshotDelta>(StringComparer.Ordinal);
        }

        byte[] diffOutput = await ExternalCommand.RunBinaryAsync(
            "git",
            repositoryPath,
            [
                "diff-tree",
                "--stdin",
                "-r",
                "-z",
                "--raw",
                "--no-renames",
                "--full-index",
                "--format=format:COMMIT:%H%x00",
            ],
            commits,
            MaximumBatchOutputBytes,
            cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, IReadOnlyList<RawChangedFile>> raw = ParseDiff(diffOutput);
        string[] blobObjectIds = [.. raw.Values
            .SelectMany(files => files)
            .Where(file => file.NewMode != "000000" && file.NewMode != "160000")
            .Select(file => file.NewObjectId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        IReadOnlyDictionary<string, long> lengths = await ReadBlobLengthsAsync(
            repositoryPath,
            blobObjectIds,
            cancellationToken).ConfigureAwait(false);

        Dictionary<string, GitSnapshotDelta> result = new(StringComparer.Ordinal);
        foreach (string commit in commits)
        {
            if (!raw.TryGetValue(commit, out IReadOnlyList<RawChangedFile>? files))
            {
                throw new InvalidOperationException(
                    $"Git batch diff omitted requested commit '{commit}'.");
            }

            string[] paths = [.. files
                .Select(file => file.Path)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];
            ChangeSnapshotFile[] changedFiles = [.. files
                .Where(file => file.NewMode != "000000")
                .Select(file => new ChangeSnapshotFile
                {
                    Mode = file.NewMode,
                    ObjectId = file.NewObjectId,
                    Length = file.NewMode == "160000" ? 0L : lengths[file.NewObjectId],
                    Path = file.Path,
                })
                .OrderBy(file => file.Path, StringComparer.Ordinal)];
            result.Add(commit, new GitSnapshotDelta(paths, changedFiles));
        }

        return result;
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<RawChangedFile>> ParseDiff(
        byte[] output)
    {
        Dictionary<string, List<RawChangedFile>> result = new(StringComparer.Ordinal);
        string? currentCommit = null;
        int offset = 0;
        while (offset < output.Length)
        {
            ReadOnlySpan<byte> token = ReadToken(output, ref offset);
            if (token.Length == 0)
            {
                continue;
            }

            string value = Decode(token, "Git batch diff metadata is not valid UTF-8.");
            value = value.TrimStart('\r', '\n');
            if (value.StartsWith(CommitPrefix, StringComparison.Ordinal))
            {
                currentCommit = value[CommitPrefix.Length..].ToLowerInvariant();
                ValidateObjectId(currentCommit);
                if (!result.TryAdd(currentCommit, []))
                {
                    throw new InvalidOperationException(
                        $"Git batch diff repeated commit '{currentCommit}'.");
                }

                continue;
            }

            if (currentCommit is null || value[0] != ':')
            {
                throw new InvalidOperationException("Git returned malformed batch diff metadata.");
            }

            string[] fields = value[1..].Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length != 5 || fields[4].Length == 0)
            {
                throw new InvalidOperationException("Git returned a malformed raw diff entry.");
            }

            ReadOnlySpan<byte> pathToken = ReadToken(output, ref offset);
            string path = Decode(pathToken, "The Git diff contains a path that is not valid UTF-8.");
            GitSnapshotFileSystem.ValidateGitPath(path);
            ValidateMode(fields[1]);
            ValidateObjectId(fields[3], allowZero: fields[1] == "000000");
            result[currentCommit].Add(new RawChangedFile(
                path,
                fields[1],
                fields[3].ToLowerInvariant()));
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<RawChangedFile>)[.. pair.Value
                .OrderBy(file => file.Path, StringComparer.Ordinal)],
            StringComparer.Ordinal);
    }

    internal static IReadOnlyDictionary<string, long> ParseBlobLengths(byte[] output)
    {
        string text = Encoding.ASCII.GetString(output);
        Dictionary<string, long> result = new(StringComparer.Ordinal);
        foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = line.TrimEnd('\r').Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length != 3 || fields[1] != "blob" ||
                !long.TryParse(
                    fields[2],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long length) ||
                length < 0)
            {
                throw new InvalidOperationException("Git returned malformed batch object metadata.");
            }

            string objectId = fields[0].ToLowerInvariant();
            ValidateObjectId(objectId);
            if (!result.TryAdd(objectId, length))
            {
                throw new InvalidOperationException(
                    $"Git batch object metadata repeated object '{objectId}'.");
            }
        }

        return result;
    }

    private static async Task<IReadOnlyDictionary<string, long>> ReadBlobLengthsAsync(
        string repositoryPath,
        string[] objectIds,
        CancellationToken cancellationToken)
    {
        if (objectIds.Length == 0)
        {
            return new Dictionary<string, long>(StringComparer.Ordinal);
        }

        byte[] output = await ExternalCommand.RunBinaryAsync(
            "git",
            repositoryPath,
            ["cat-file", "--batch-check"],
            objectIds,
            MaximumBatchOutputBytes,
            cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, long> lengths = ParseBlobLengths(output);
        if (objectIds.Any(objectId => !lengths.ContainsKey(objectId)))
        {
            throw new InvalidOperationException("Git batch object metadata omitted a requested blob.");
        }

        return lengths;
    }

    private static ReadOnlySpan<byte> ReadToken(byte[] output, ref int offset)
    {
        int end = Array.IndexOf(output, (byte)0, offset);
        if (end < 0)
        {
            throw new InvalidOperationException("Git batch diff output was not NUL terminated.");
        }

        ReadOnlySpan<byte> token = output.AsSpan(offset, end - offset);
        offset = end + 1;
        return token;
    }

    private static string Decode(ReadOnlySpan<byte> value, string message)
    {
        try
        {
            return StrictUtf8.GetString(value);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidOperationException(message, exception);
        }
    }

    private static void ValidateMode(string mode)
    {
        if (mode.Length != 6 || mode.Any(character => character is < '0' or > '7'))
        {
            throw new InvalidOperationException("Git returned an invalid raw diff mode.");
        }
    }

    private static void ValidateObjectId(string objectId, bool allowZero = false)
    {
        bool validLength = objectId.Length is 40 or 64;
        bool validCharacters = objectId.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
        bool zero = objectId.All(character => character == '0');
        if (!validLength || !validCharacters || zero && !allowZero)
        {
            throw new InvalidOperationException("Git returned an invalid object identity.");
        }
    }

    internal sealed record RawChangedFile(
        string Path,
        string NewMode,
        string NewObjectId);
}
