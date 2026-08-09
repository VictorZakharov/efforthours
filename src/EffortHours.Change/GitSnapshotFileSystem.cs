using System.Globalization;
using System.Text;
using EffortHours.Analysis;

namespace EffortHours.Change;

internal sealed class GitSnapshotFileSystem : IRepositoryFileSystem, IChangeSnapshot
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly Dictionary<string, ChangeSnapshotFile> _files;
    private readonly HashSet<string> _directories;
    private readonly GitBatchObjectReader _objectReader;

    private GitSnapshotFileSystem(
        string repositoryPath,
        string objectId,
        IReadOnlyList<ChangeSnapshotFile> files)
    {
        ObjectId = objectId;
        RootPath = Path.GetFullPath(Path.Combine(
            repositoryPath,
            ".efforthours-virtual-snapshot",
            objectId));
        _files = new Dictionary<string, ChangeSnapshotFile>(StringComparer.Ordinal);
        _directories = new HashSet<string>(StringComparer.Ordinal) { RootPath };
        foreach (ChangeSnapshotFile file in files)
        {
            string fullPath = ResolveRelativePath(file.Path);
            _files.Add(fullPath, file);
            string? directory = Path.GetDirectoryName(fullPath);
            while (directory is not null && IsWithinRoot(directory))
            {
                _directories.Add(directory);
                if (string.Equals(directory, RootPath, StringComparison.Ordinal))
                {
                    break;
                }

                directory = Path.GetDirectoryName(directory);
            }
        }

        Files = [.. files.OrderBy(file => file.Path, StringComparer.Ordinal)];
        _objectReader = new GitBatchObjectReader(repositoryPath);
    }

    public string ObjectId { get; }

    public string RootPath { get; }

    public IRepositoryFileSystem FileSystem => this;

    public IReadOnlyList<ChangeSnapshotFile> Files { get; }

    public static async Task<GitSnapshotFileSystem> CreateAsync(
        string repositoryPath,
        string objectId,
        CancellationToken cancellationToken)
    {
        byte[] output = await ExternalCommand.RunBinaryAsync(
            "git",
            repositoryPath,
            ["ls-tree", "-r", "-z", "--full-tree", "--long", objectId],
            cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ChangeSnapshotFile> files = ParseTree(output);
        return new GitSnapshotFileSystem(repositoryPath, objectId, files);
    }

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public bool DirectoryExists(string path) => _directories.Contains(Path.GetFullPath(path));

    public bool FileExists(string path) => _files.ContainsKey(Path.GetFullPath(path));

    public FileAttributes GetAttributes(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (_directories.Contains(fullPath))
        {
            return FileAttributes.Directory;
        }

        ChangeSnapshotFile file = GetFile(fullPath);
        return file.IsLink || file.IsSubmodule
            ? FileAttributes.ReparsePoint
            : FileAttributes.Normal;
    }

    public string[] GetFileSystemEntries(string directoryPath)
    {
        string fullDirectory = Path.GetFullPath(directoryPath);
        if (!_directories.Contains(fullDirectory))
        {
            throw new DirectoryNotFoundException($"Git snapshot directory was not found: {directoryPath}");
        }

        return
        [
            .. _directories
                .Where(path => !string.Equals(path, fullDirectory, StringComparison.Ordinal) &&
                    string.Equals(Path.GetDirectoryName(path), fullDirectory, StringComparison.Ordinal))
                .Concat(_files.Keys.Where(path =>
                    string.Equals(Path.GetDirectoryName(path), fullDirectory, StringComparison.Ordinal))),
        ];
    }

    public RepositoryFileMetadata GetFileMetadata(string path)
    {
        ChangeSnapshotFile file = GetFile(Path.GetFullPath(path));
        return new RepositoryFileMetadata(file.Length, 0L, true);
    }

    public Stream OpenRead(string path, int bufferSize)
    {
        _ = bufferSize;
        ChangeSnapshotFile file = GetFile(Path.GetFullPath(path));
        return _objectReader.OpenBlob(file.ObjectId);
    }

    public async ValueTask<byte[]> ReadAllBytesAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ChangeSnapshotFile file = GetFile(Path.GetFullPath(path));
        byte[] content = await _objectReader.ReadBlobAsync(
            file.ObjectId,
            cancellationToken).ConfigureAwait(false);
        return [.. content];
    }

    public async ValueTask<string[]> ReadAllLinesAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        byte[] content = await ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        using MemoryStream stream = new(content, writable: false);
        using StreamReader reader = new(stream, detectEncodingFromByteOrderMarks: true);
        List<string> lines = [];
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lines.Add(line);
        }

        return [.. lines];
    }

    ValueTask<byte[]> IChangeSnapshot.ReadAllBytesAsync(
        string relativePath,
        CancellationToken cancellationToken) =>
        ReadAllBytesAsync(ResolveRelativePath(relativePath), cancellationToken);

    public async ValueTask DisposeAsync() => await _objectReader.DisposeAsync().ConfigureAwait(false);

    private static List<ChangeSnapshotFile> ParseTree(byte[] output)
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

    private static void ValidateGitPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            Path.IsPathRooted(path) ||
            path.Split('/').Any(segment => segment is "" or "." or "..") ||
            (OperatingSystem.IsWindows() && path.Contains('\\')))
        {
            throw new InvalidOperationException($"Git returned an unsafe snapshot path: '{path}'.");
        }
    }

    private ChangeSnapshotFile GetFile(string fullPath) =>
        _files.TryGetValue(fullPath, out ChangeSnapshotFile? file)
            ? file
            : throw new FileNotFoundException("Git snapshot file was not found.", fullPath);

    private string ResolveRelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string fullPath = Path.GetFullPath(Path.Combine(
            RootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinRoot(fullPath))
        {
            throw new ArgumentOutOfRangeException(nameof(relativePath), "Snapshot path escapes its root.");
        }

        return fullPath;
    }

    private bool IsWithinRoot(string path)
    {
        string relative = Path.GetRelativePath(RootPath, path);
        return relative != ".." &&
            !Path.IsPathRooted(relative) &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
