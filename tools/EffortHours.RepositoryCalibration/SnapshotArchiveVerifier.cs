using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace EffortHours.RepositoryCalibration;

internal static class SnapshotArchiveVerifier
{
    public static async Task VerifyAndExtractAsync(
        string archivePath,
        string snapshotPath,
        IReadOnlyList<GitTreeEntry> tree,
        Func<GitTreeEntry, CancellationToken, Task<byte[]>> loadExactBlob,
        CancellationToken cancellationToken)
    {
        Dictionary<string, GitTreeEntry> expected = tree
            .Where(item => string.Equals(item.Type, "blob", StringComparison.Ordinal))
            .ToDictionary(item => item.Path, StringComparer.Ordinal);

        if (Directory.Exists(snapshotPath))
        {
            await VerifyExistingAsync(snapshotPath, expected, cancellationToken).ConfigureAwait(false);
            return;
        }

        string parent = Path.GetDirectoryName(snapshotPath)
            ?? throw new InvalidDataException("Snapshot path requires a parent directory.");
        Directory.CreateDirectory(parent);
        string staging = Path.Combine(parent, $".{Path.GetFileName(snapshotPath)}.{Guid.NewGuid():N}.partial");
        Directory.CreateDirectory(staging);
        try
        {
            await ExtractAsync(
                archivePath,
                staging,
                expected,
                loadExactBlob,
                cancellationToken).ConfigureAwait(false);
            Directory.Move(staging, snapshotPath);
        }
        catch
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }

            throw;
        }
    }

    private static async Task ExtractAsync(
        string archivePath,
        string destination,
        IReadOnlyDictionary<string, GitTreeEntry> expected,
        Func<GitTreeEntry, CancellationToken, Task<byte[]>> loadExactBlob,
        CancellationToken cancellationToken)
    {
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        string root = FindRoot(archive);
        HashSet<string> observed = new(StringComparer.Ordinal);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.FullName.EndsWith('/'))
            {
                continue;
            }

            string relative = StripRoot(entry.FullName, root);
            ValidateRelativePath(relative);
            if (!expected.TryGetValue(relative, out GitTreeEntry? expectedEntry))
            {
                throw new InvalidDataException($"Archive contains unexpected file '{relative}'.");
            }

            if (!observed.Add(relative))
            {
                throw new InvalidDataException($"Archive contains duplicate file '{relative}'.");
            }

            if (expectedEntry.Size is null)
            {
                throw new InvalidDataException($"Git tree has no size for blob '{relative}'.");
            }

            string outputPath = ResolveContainedPath(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            bool archiveMatches = entry.Length == expectedEntry.Size.Value;
            if (archiveMatches)
            {
                await using FileStream output = new(
                    outputPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await using Stream input = entry.Open();
                string actualSha = await CopyAndHashGitBlobAsync(
                    input,
                    output,
                    entry.Length,
                    cancellationToken).ConfigureAwait(false);
                archiveMatches = string.Equals(actualSha, expectedEntry.Sha, StringComparison.Ordinal);
            }

            if (!archiveMatches)
            {
                await WriteExactBlobAsync(
                    outputPath,
                    expectedEntry,
                    loadExactBlob,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (string missing in expected.Keys.Except(observed, StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            GitTreeEntry expectedEntry = expected[missing];
            string outputPath = ResolveContainedPath(destination, missing);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await WriteExactBlobAsync(
                outputPath,
                expectedEntry,
                loadExactBlob,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task VerifyExistingAsync(
        string snapshotPath,
        IReadOnlyDictionary<string, GitTreeEntry> expected,
        CancellationToken cancellationToken)
    {
        HashSet<string> observed = new(StringComparer.Ordinal);
        foreach (string path in EnumerateFilesWithoutLinks(snapshotPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileInfo file = new(path);
            string relative = Path.GetRelativePath(snapshotPath, path).Replace('\\', '/');
            if (!expected.TryGetValue(relative, out GitTreeEntry? expectedEntry) ||
                expectedEntry.Size is null ||
                file.Length != expectedEntry.Size.Value)
            {
                throw new InvalidDataException($"Cached snapshot contains an unexpected file '{relative}'.");
            }

            await using FileStream input = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            string actualSha = await HashGitBlobAsync(input, file.Length, cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(actualSha, expectedEntry.Sha, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Cached snapshot content does not match '{relative}'.");
            }

            observed.Add(relative);
        }

        string? missing = expected.Keys.Except(observed, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
        if (missing is not null)
        {
            throw new InvalidDataException($"Cached snapshot is missing Git blob '{missing}'.");
        }
    }

    private static IEnumerable<string> EnumerateFilesWithoutLinks(string root)
    {
        Stack<string> pending = new();
        pending.Push(root);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            foreach (string path in Directory.EnumerateFileSystemEntries(directory)
                         .Order(StringComparer.Ordinal))
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException(
                        $"Cached snapshot contains a link at '{Path.GetFileName(path)}'.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(path);
                }
                else
                {
                    yield return path;
                }
            }
        }
    }

    private static string FindRoot(ZipArchive archive)
    {
        string[] roots = [.. archive.Entries
            .Select(entry => entry.FullName.Split('/', 2)[0])
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)];
        return roots.Length == 1
            ? roots[0]
            : throw new InvalidDataException("Archive must contain exactly one root directory.");
    }

    private static string StripRoot(string path, string root)
    {
        string prefix = $"{root}/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Archive entry '{path}' is outside its root directory.");
        }

        return path[prefix.Length..];
    }

    private static void ValidateRelativePath(string path)
    {
        if (path.Length == 0 ||
            path.Contains('\\', StringComparison.Ordinal) ||
            path.Split('/').Any(segment => segment is "" or "." or "..") ||
            Path.IsPathRooted(path))
        {
            throw new InvalidDataException($"Archive entry '{path}' is not a safe relative path.");
        }
    }

    private static string ResolveContainedPath(string root, string relative)
    {
        string rootPath = Path.GetFullPath(root);
        string candidate = Path.GetFullPath(
            Path.Combine(rootPath, relative.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, PathComparison))
        {
            throw new InvalidDataException($"Archive entry '{relative}' escapes the snapshot root.");
        }

        return candidate;
    }

    private static async Task<string> CopyAndHashGitBlobAsync(
        Stream input,
        Stream output,
        long length,
        CancellationToken cancellationToken)
    {
        using IncrementalHash hash = CreateGitBlobHash(length);
        byte[] buffer = new byte[64 * 1024];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static async Task WriteExactBlobAsync(
        string outputPath,
        GitTreeEntry expected,
        Func<GitTreeEntry, CancellationToken, Task<byte[]>> loadExactBlob,
        CancellationToken cancellationToken)
    {
        byte[] content = await loadExactBlob(expected, cancellationToken).ConfigureAwait(false);
        if (expected.Size is null || content.LongLength != expected.Size.Value)
        {
            throw new InvalidDataException($"Exact Git blob size mismatch for '{expected.Path}'.");
        }

        using MemoryStream contentStream = new(content, writable: false);
        string actualSha = await HashGitBlobAsync(contentStream, content.LongLength, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(actualSha, expected.Sha, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Exact Git blob content mismatch for '{expected.Path}'.");
        }

        await File.WriteAllBytesAsync(outputPath, content, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> HashGitBlobAsync(
        Stream input,
        long length,
        CancellationToken cancellationToken)
    {
        using IncrementalHash hash = CreateGitBlobHash(length);
        byte[] buffer = new byte[64 * 1024];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            hash.AppendData(buffer, 0, read);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static IncrementalHash CreateGitBlobHash(long length)
    {
        IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        hash.AppendData(Encoding.ASCII.GetBytes($"blob {length}\0"));
        return hash;
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}
