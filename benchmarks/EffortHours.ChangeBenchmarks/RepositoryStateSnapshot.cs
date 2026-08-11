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
        string[] files = [.. Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => excludedRootName is null || !IsUnderExcludedRoot(root, path, excludedRootName))
            .Order(StringComparer.Ordinal)];
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

    private static bool IsUnderExcludedRoot(string root, string path, string excludedRootName)
    {
        string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        return relative.Equals(excludedRootName, StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith(excludedRootName + "/", StringComparison.OrdinalIgnoreCase);
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
