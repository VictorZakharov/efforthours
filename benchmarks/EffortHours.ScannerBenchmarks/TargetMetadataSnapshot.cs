using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace EffortHours.ScannerBenchmarks;

internal sealed record TargetMetadataSnapshot(
    string Digest,
    int FileCount,
    int DirectoryCount,
    long TotalBytes)
{
    public static TargetMetadataSnapshot Capture(string rootPath)
    {
        string root = Path.GetFullPath(rootPath);
        List<MetadataEntry> entries = [];
        Stack<string> pending = new();
        pending.Push(root);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            foreach (string path in Directory.GetFileSystemEntries(directory))
            {
                FileAttributes attributes = File.GetAttributes(path);
                string relativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
                bool isDirectory = attributes.HasFlag(FileAttributes.Directory);
                bool isLink = attributes.HasFlag(FileAttributes.ReparsePoint);
                long length = !isDirectory && !isLink ? new FileInfo(path).Length : 0;
                long writeTicks = File.GetLastWriteTimeUtc(path).Ticks;
                entries.Add(new MetadataEntry(relativePath, attributes, length, writeTicks));
                if (isDirectory && !isLink)
                {
                    pending.Push(path);
                }
            }
        }

        entries.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (MetadataEntry entry in entries)
        {
            Append(hash, entry.Path);
            Append(hash, ((int)entry.Attributes).ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(hash, entry.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(hash, entry.LastWriteTimeUtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        string digest = "sha256:" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        return new TargetMetadataSnapshot(
            digest,
            entries.Count(entry => !entry.Attributes.HasFlag(FileAttributes.Directory)),
            entries.Count(entry => entry.Attributes.HasFlag(FileAttributes.Directory)),
            entries.Sum(entry => entry.Length));
    }

    private static void Append(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private sealed record MetadataEntry(
        string Path,
        FileAttributes Attributes,
        long Length,
        long LastWriteTimeUtcTicks);
}
