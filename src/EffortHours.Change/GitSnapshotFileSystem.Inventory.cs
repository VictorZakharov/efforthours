using System.Globalization;
using System.Text;

namespace EffortHours.Change;

internal sealed partial class GitSnapshotFileSystem
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal static async Task<IReadOnlyList<ChangeSnapshotFile>> ReadFilesAsync(
        string repositoryPath,
        string objectId,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? pathspecs = null)
    {
        List<string> arguments =
        [
            "--literal-pathspecs",
            "ls-tree",
            "-r",
            "-z",
            "--full-tree",
            "--long",
            objectId,
        ];
        if (pathspecs is { Count: > 0 })
        {
            arguments.Add("--");
            arguments.AddRange(pathspecs);
        }

        byte[] output = await ExternalCommand.RunBinaryAsync(
            "git",
            repositoryPath,
            arguments,
            cancellationToken).ConfigureAwait(false);
        return ParseTree(output);
    }

    internal static async Task<IReadOnlyList<string>> ReadChangedPathsAsync(
        string repositoryPath,
        string parentObjectId,
        string objectId,
        CancellationToken cancellationToken)
    {
        byte[] output = await ExternalCommand.RunBinaryAsync(
            "git",
            repositoryPath,
            [
                "diff-tree",
                "--no-commit-id",
                "-r",
                "-z",
                "--name-only",
                "--no-renames",
                parentObjectId,
                objectId,
            ],
            cancellationToken).ConfigureAwait(false);
        List<string> paths = [];
        int start = 0;
        while (start < output.Length)
        {
            int end = Array.IndexOf(output, (byte)0, start);
            if (end < 0)
            {
                throw new InvalidOperationException("Git changed-path output was not NUL terminated.");
            }

            string path;
            try
            {
                path = StrictUtf8.GetString(output.AsSpan(start, end - start));
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidOperationException(
                    "The Git diff contains a path that is not valid UTF-8 and cannot be analyzed safely.",
                    exception);
            }

            ValidateGitPath(path);
            paths.Add(path);
            start = end + 1;
        }

        return [.. paths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    internal static IReadOnlyList<ChangeSnapshotFile> ApplyIncrementalChanges(
        IReadOnlyList<ChangeSnapshotFile> parentFiles,
        IReadOnlyList<string> changedPaths,
        IReadOnlyList<ChangeSnapshotFile> changedFiles)
    {
        Dictionary<string, ChangeSnapshotFile> files = parentFiles.ToDictionary(
            file => file.Path,
            StringComparer.Ordinal);
        foreach (string path in changedPaths)
        {
            files.Remove(path);
        }

        foreach (ChangeSnapshotFile file in changedFiles)
        {
            files[file.Path] = file;
        }

        return [.. files.Values.OrderBy(file => file.Path, StringComparer.Ordinal)];
    }

    internal static List<ChangeSnapshotFile> ParseTree(byte[] output)
    {
        List<ChangeSnapshotFile> files = [];
        int start = 0;
        while (start < output.Length)
        {
            int end = Array.IndexOf(output, (byte)0, start);
            if (end < 0)
            {
                throw new InvalidOperationException("Git tree output was not NUL terminated.");
            }

            ReadOnlySpan<byte> entry = output.AsSpan(start, end - start);
            int tab = entry.IndexOf((byte)'\t');
            if (tab <= 0 || tab == entry.Length - 1)
            {
                throw new InvalidOperationException("Git returned a malformed tree entry.");
            }

            string header;
            string path;
            try
            {
                header = StrictUtf8.GetString(entry[..tab]);
                path = StrictUtf8.GetString(entry[(tab + 1)..]);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidOperationException(
                    "The Git tree contains a path that is not valid UTF-8 and cannot be analyzed safely.",
                    exception);
            }

            ValidateGitPath(path);
            string[] fields = header.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length != 4)
            {
                throw new InvalidOperationException("Git returned a malformed long tree header.");
            }

            long length = fields[3] == "-"
                ? 0L
                : long.Parse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture);
            files.Add(new ChangeSnapshotFile
            {
                Mode = fields[0],
                ObjectId = fields[2].ToLowerInvariant(),
                Length = length,
                Path = path,
            });
            start = end + 1;
        }

        return files;
    }

    internal static void ValidateGitPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            Path.IsPathRooted(path) ||
            path.Split('/').Any(segment => segment is "" or "." or "..") ||
            (OperatingSystem.IsWindows() && path.Contains('\\')))
        {
            throw new InvalidOperationException($"Git returned an unsafe snapshot path: '{path}'.");
        }
    }
}
