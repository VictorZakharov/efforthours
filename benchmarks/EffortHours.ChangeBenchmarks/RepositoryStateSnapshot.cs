using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EffortHours.ChangeBenchmarks;

internal sealed record RepositoryStateSnapshot(
    string Digest,
    int FileCount,
    long TotalBytes)
{
    public static RepositoryStateSnapshot Capture(string rootPath, string? excludedRootName = null)
    {
        string root = Path.GetFullPath(rootPath);
        string[] files = [.. EnumerateFiles(root, excludedRootName).Order(StringComparer.Ordinal)];
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long totalBytes = 0;
        foreach (string path in files)
        {
            FileInfo file = new(path);
            string relativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
            Append(hash, relativePath);
            Append(hash, file.Length.ToString(CultureInfo.InvariantCulture));
            Append(hash, file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture));
            using FileStream content = file.OpenRead();
            byte[] buffer = new byte[64 * 1024];
            int read;
            while ((read = content.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.AppendData(buffer, 0, read);
            }

            totalBytes += file.Length;
        }

        return new RepositoryStateSnapshot(
            "sha256:" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            files.Length,
            totalBytes);
    }

    public static RepositoryStateSnapshot Combine(
        IEnumerable<(string Id, RepositoryStateSnapshot Snapshot)> snapshots)
    {
        (string Id, RepositoryStateSnapshot Snapshot)[] ordered = [.. snapshots
            .OrderBy(value => value.Id, StringComparer.Ordinal)];
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach ((string id, RepositoryStateSnapshot snapshot) in ordered)
        {
            Append(hash, id);
            Append(hash, snapshot.Digest);
            Append(hash, snapshot.FileCount.ToString(CultureInfo.InvariantCulture));
            Append(hash, snapshot.TotalBytes.ToString(CultureInfo.InvariantCulture));
        }

        return new RepositoryStateSnapshot(
            "sha256:" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            ordered.Sum(value => value.Snapshot.FileCount),
            ordered.Sum(value => value.Snapshot.TotalBytes));
    }

    private static bool IsUnderExcludedRoot(string root, string path, string excludedRootName)
    {
        string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        return relative.Equals(excludedRootName, StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith(excludedRootName + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateFiles(
        string root,
        string? excludedRootName)
    {
        Stack<string> pending = new();
        pending.Push(root);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            foreach (string path in Directory.EnumerateFiles(directory))
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 &&
                    (excludedRootName is null ||
                        !IsUnderExcludedRoot(root, path, excludedRootName)))
                {
                    yield return path;
                }
            }

            foreach (string path in Directory.EnumerateDirectories(directory))
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 &&
                    (excludedRootName is null ||
                        !IsUnderExcludedRoot(root, path, excludedRootName)))
                {
                    pending.Push(path);
                }
            }
        }
    }

    private static void Append(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
