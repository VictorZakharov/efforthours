using System.Globalization;
using System.Text;
using EffortHours.Analysis;

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
        IReadOnlyList<string>? pathspecs = null,
        GitObjectStorageLayout? storageLayout = null)
    {
        List<string> arguments =
        [
            "--literal-pathspecs",
            "ls-tree",
            "-r",
            "-z",
            "--full-tree",
        ];
        bool readLengthsInline = pathspecs is { Count: > 0 };
        if (!readLengthsInline)
        {
            storageLayout ??= await GitObjectStorageLayout.ReadAsync(
                repositoryPath,
                cancellationToken).ConfigureAwait(false);
            int parallelism = storageLayout.Value.SelectTreeReadParallelism(
                RepositoryAnalysisConcurrency.MaximumGitTreeReads);
            return await GitSnapshotTreeReader.ReadAsync(
                repositoryPath,
                objectId,
                parallelism,
                cancellationToken).ConfigureAwait(false);
        }

        if (readLengthsInline)
        {
            arguments.Add("--long");
        }

        arguments.Add(objectId);
        if (pathspecs is { Count: > 0 })
        {
            arguments.Add("--");
            arguments.AddRange(pathspecs);
        }

        using IDisposable lease = await RepositoryAnalysisConcurrency.AcquireGitTreeReadAsync(
            cancellationToken).ConfigureAwait(false);
        ExternalBinaryCommandResult result = await ExternalCommand.RunBinaryMeasuredAsync(
            "git",
            repositoryPath,
            arguments,
            cancellationToken).ConfigureAwait(false);
        RepositoryAnalysisConcurrency.RecordExternalProcessMetrics(
            RepositoryAnalysisWorkKind.GitTreeRead,
            result.ProcessCpuTime,
            result.StandardOutput.LongLength);
        return ParseTree(result.StandardOutput);
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

    internal static List<ChangeSnapshotFile> ParseTreeWithoutLengths(byte[] output)
    {
        List<ChangeSnapshotFile> files = [];
        foreach (GitSnapshotTreeEntry entry in ParseTreeEntries(output))
        {
            if (entry.Type == "tree")
            {
                throw new InvalidOperationException(
                    "Git returned a tree where a recursive inventory expected only leaves.");
            }

            files.Add(new ChangeSnapshotFile
            {
                Mode = entry.Mode,
                ObjectId = entry.ObjectId,
                Length = entry.Type == "commit" ? 0L : -1L,
                Path = entry.Path,
            });
        }

        return files;
    }

    internal static List<GitSnapshotTreeEntry> ParseTreeEntries(byte[] output)
    {
        List<GitSnapshotTreeEntry> entries = [];
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
            if (fields.Length != 3 || fields[1] is not ("blob" or "commit" or "tree"))
            {
                throw new InvalidOperationException("Git returned malformed tree identity metadata.");
            }

            entries.Add(new GitSnapshotTreeEntry(
                fields[0],
                fields[1],
                fields[2].ToLowerInvariant(),
                path));
            start = end + 1;
        }

        return entries;
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
