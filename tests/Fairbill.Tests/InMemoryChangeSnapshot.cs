using System.Security.Cryptography;
using System.Text;
using Fairbill.Analysis;
using Fairbill.Change;

namespace Fairbill.Tests;

internal sealed class InMemoryChangeSnapshot : IChangeSnapshot
{
    private readonly InMemoryRepository _repository = new();

    public InMemoryChangeSnapshot(params (string Path, string Content)[] files)
    {
        ArgumentNullException.ThrowIfNull(files);
        foreach ((string path, string content) in files.OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            _repository.WriteText(path, content);
        }

        Files =
        [
            .. files
                .OrderBy(file => file.Path, StringComparer.Ordinal)
                .Select(file =>
                {
                    byte[] content = Encoding.UTF8.GetBytes(file.Content);
                    return new ChangeSnapshotFile
                    {
                        Path = NormalizePath(file.Path),
                        ObjectId = Digest(content),
                        Length = content.LongLength,
                        Mode = "100644",
                    };
                }),
        ];
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

    public static Func<CancellationToken, Task<IChangeSnapshot>> Factory(
        params (string Path, string Content)[] files)
    {
        (string Path, string Content)[] captured = [.. files];
        return cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IChangeSnapshot>(new InMemoryChangeSnapshot(captured));
        };
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static string Digest(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
