using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Change;

namespace EffortHours.ChangeCalibration;

internal sealed class MemoryChangeSnapshot : IChangeSnapshot
{
    private readonly MemoryRepositoryFileSystem _repository;

    public MemoryChangeSnapshot(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _repository = new MemoryRepositoryFileSystem(files);
        Files = [.. files
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair =>
            {
                byte[] content = Encoding.UTF8.GetBytes(pair.Value);
                return new ChangeSnapshotFile
                {
                    Path = NormalizePath(pair.Key),
                    ObjectId = Digest(content),
                    Length = content.LongLength,
                    Mode = "100644",
                };
            })];
        ObjectId = Digest(Encoding.UTF8.GetBytes(string.Join(
            '\n',
            Files.Select(file => $"{file.Mode} {file.ObjectId} {file.Path}"))));
    }

    public string ObjectId { get; }

    public string RootPath => _repository.RootPath;

    public IRepositoryFileSystem FileSystem => _repository;

    public IReadOnlyList<ChangeSnapshotFile> Files { get; }

    public ValueTask<byte[]> ReadAllBytesAsync(
        string relativePath,
        CancellationToken cancellationToken = default) =>
        _repository.ReadAllBytesAsync(
            Path.Combine(
                RootPath,
                NormalizePath(relativePath).Replace('/', Path.DirectorySeparatorChar)),
            cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static string Digest(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
