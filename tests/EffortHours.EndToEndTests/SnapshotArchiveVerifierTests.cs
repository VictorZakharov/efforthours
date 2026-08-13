using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using EffortHours.RepositoryCalibration;

namespace EffortHours.EndToEndTests;

public sealed class SnapshotArchiveVerifierTests
{
    [Fact]
    public async Task RestoresTransformedAndMissingFilesFromPinnedGitBlobs()
    {
        using TestDirectory directory = new();
        byte[] exactScript = Encoding.UTF8.GetBytes("echo exact\r\n");
        byte[] archiveScript = Encoding.UTF8.GetBytes("echo exact\n");
        byte[] missing = Encoding.UTF8.GetBytes("export-subst omitted this file\n");
        byte[] unchanged = Encoding.UTF8.GetBytes("unchanged\n");
        string archivePath = directory.PathFor("snapshot.zip");
        CreateArchive(
            archivePath,
            ("root/gradlew.bat", archiveScript),
            ("root/unchanged.txt", unchanged));
        GitTreeEntry[] tree =
        [
            Blob("gradlew.bat", exactScript),
            Blob("omitted.txt", missing),
            Blob("unchanged.txt", unchanged),
        ];
        Dictionary<string, byte[]> exact = new(StringComparer.Ordinal)
        {
            ["gradlew.bat"] = exactScript,
            ["omitted.txt"] = missing,
        };
        List<string> requested = [];
        string snapshotPath = directory.PathFor("snapshot");

        await SnapshotArchiveVerifier.VerifyAndExtractAsync(
            archivePath,
            snapshotPath,
            tree,
            (entry, _) =>
            {
                requested.Add(entry.Path);
                return Task.FromResult(exact[entry.Path]);
            },
            CancellationToken.None);

        Assert.Equal(exactScript, await File.ReadAllBytesAsync(Path.Combine(snapshotPath, "gradlew.bat")));
        Assert.Equal(missing, await File.ReadAllBytesAsync(Path.Combine(snapshotPath, "omitted.txt")));
        Assert.Equal(unchanged, await File.ReadAllBytesAsync(Path.Combine(snapshotPath, "unchanged.txt")));
        Assert.Equal(["gradlew.bat", "omitted.txt"], requested.Order(StringComparer.Ordinal));

        await SnapshotArchiveVerifier.VerifyAndExtractAsync(
            archivePath,
            snapshotPath,
            tree,
            (_, _) => throw new InvalidOperationException("A verified cache must not reload blobs."),
            CancellationToken.None);
    }

    [Fact]
    public async Task RejectsArchiveTraversalWithoutWritingOutsideSnapshot()
    {
        using TestDirectory directory = new();
        string archivePath = directory.PathFor("unsafe.zip");
        CreateArchive(
            archivePath,
            ("root/../outside.txt", Encoding.UTF8.GetBytes("unsafe")));
        string snapshotPath = directory.PathFor("snapshot");

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            SnapshotArchiveVerifier.VerifyAndExtractAsync(
                archivePath,
                snapshotPath,
                [Blob("safe.txt", Encoding.UTF8.GetBytes("safe"))],
                (_, _) => throw new InvalidOperationException("No blob should be loaded."),
                CancellationToken.None));

        Assert.Contains("safe relative path", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(directory.PathFor("outside.txt")));
        Assert.False(Directory.Exists(snapshotPath));
    }

    [Fact]
    public async Task RejectsTamperingInAnExistingSnapshotCache()
    {
        using TestDirectory directory = new();
        byte[] expected = Encoding.UTF8.GetBytes("good");
        string archivePath = directory.PathFor("snapshot.zip");
        CreateArchive(archivePath, ("root/file.txt", expected));
        GitTreeEntry[] tree = [Blob("file.txt", expected)];
        string snapshotPath = directory.PathFor("snapshot");
        await SnapshotArchiveVerifier.VerifyAndExtractAsync(
            archivePath,
            snapshotPath,
            tree,
            (_, _) => throw new InvalidOperationException("No blob should be loaded."),
            CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(snapshotPath, "file.txt"), "evil");

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            SnapshotArchiveVerifier.VerifyAndExtractAsync(
                archivePath,
                snapshotPath,
                tree,
                (_, _) => throw new InvalidOperationException("No blob should be loaded."),
                CancellationToken.None));

        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
    }

    private static GitTreeEntry Blob(string path, byte[] content) => new()
    {
        Path = path,
        Mode = "100644",
        Type = "blob",
        Sha = GitBlobSha1(content),
        Size = content.LongLength,
    };

    private static string GitBlobSha1(byte[] content)
    {
        byte[] header = Encoding.ASCII.GetBytes($"blob {content.LongLength}\0");
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        hash.AppendData(header);
        hash.AppendData(content);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void CreateArchive(
        string path,
        params (string Path, byte[] Content)[] entries)
    {
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach ((string entryPath, byte[] content) in entries)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryPath, CompressionLevel.NoCompression);
            using Stream output = entry.Open();
            output.Write(content);
        }
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Root = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "efforthours-repository-calibration-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        private string Root { get; }

        public string PathFor(string relative) => System.IO.Path.Combine(Root, relative);

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
