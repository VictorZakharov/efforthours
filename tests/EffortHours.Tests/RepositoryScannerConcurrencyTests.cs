using System.Collections.Concurrent;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class RepositoryScannerConcurrencyTests
{
    [Fact]
    public async Task ProducesTheNextFileWhileProcessingPriorBufferedContent()
    {
        InMemoryRepository repository = new();
        repository.WriteText("first.txt", "first buffered file\n");
        repository.WriteText("second.txt", "second buffered file\n");
        using CoordinatedFileSystem fileSystem = new(repository);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

        RepositoryEvidence evidence = await new RepositoryScanner(fileSystem)
            .ScanAsync(repository.RootPath, cancellationToken: timeout.Token);

        Assert.True(fileSystem.FirstProcessingOverlappedSecondProduction);
        Assert.Contains(evidence.Facts, fact => fact.Id == "file:first.txt");
        Assert.Contains(evidence.Facts, fact => fact.Id == "file:second.txt");
    }

    private sealed class CoordinatedFileSystem(
        InMemoryRepository inner) : IRepositoryFileSystem, IDisposable
    {
        private readonly ConcurrentDictionary<string, int> _metadataRequests =
            new(StringComparer.Ordinal);
        private readonly ManualResetEventSlim _firstProcessingReached = new();
        private readonly ManualResetEventSlim _secondProductionReached = new();

        public bool FirstProcessingOverlappedSecondProduction =>
            _firstProcessingReached.IsSet && _secondProductionReached.IsSet;

        public string GetFullPath(string path) => inner.GetFullPath(path);

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);

        public bool FileExists(string path) => inner.FileExists(path);

        public FileAttributes GetAttributes(string path) => inner.GetAttributes(path);

        public string[] GetFileSystemEntries(string directoryPath) =>
            inner.GetFileSystemEntries(directoryPath);

        public RepositoryFileMetadata GetFileMetadata(string path)
        {
            string fileName = Path.GetFileName(path);
            int request = _metadataRequests.AddOrUpdate(fileName, 1, (_, count) => count + 1);
            if (fileName == "first.txt" && request == 2)
            {
                _firstProcessingReached.Set();
                if (!_secondProductionReached.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new InvalidOperationException(
                        "The next file was not produced while the prior file was processed.");
                }
            }

            return inner.GetFileMetadata(path);
        }

        public Stream OpenRead(string path, int bufferSize) => inner.OpenRead(path, bufferSize);

        public async ValueTask<byte[]> ReadAllBytesAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            if (Path.GetFileName(path) == "second.txt")
            {
                _secondProductionReached.Set();
                if (!_firstProcessingReached.Wait(TimeSpan.FromSeconds(5), cancellationToken))
                {
                    throw new InvalidOperationException(
                        "The prior file was not processed while the next file was produced.");
                }
            }

            return await inner.ReadAllBytesAsync(path, cancellationToken);
        }

        public ValueTask<string[]> ReadAllLinesAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            inner.ReadAllLinesAsync(path, cancellationToken);

        public void Dispose()
        {
            _firstProcessingReached.Dispose();
            _secondProductionReached.Dispose();
        }
    }
}
