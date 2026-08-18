using EffortHours.Change;

namespace EffortHours.EndToEndTests;

public sealed class GitBatchObjectReaderConcurrencyTests
{
    private static readonly TimeSpan LivenessTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ReadyBlobsProgressWhileTheGitProcessIsBusyOrPriorContentIsConsumed()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        byte[] cachedContent = "cached content\n"u8.ToArray();
        repository.WriteBytes("cached.txt", cachedContent);
        repository.WriteBytes("first.txt", "first produced content\n"u8.ToArray());
        repository.WriteBytes("second.txt", "second produced content\n"u8.ToArray());
        repository.WriteBytes(
            "large.bin",
            GC.AllocateUninitializedArray<byte>(GitBatchObjectReader.MaximumCachedBlobBytes + 1));
        await repository.CommitAsync();

        string cachedObject = await repository.ResolveBlobAsync("cached.txt");
        string firstObject = await repository.ResolveBlobAsync("first.txt");
        string secondObject = await repository.ResolveBlobAsync("second.txt");
        string largeObject = await repository.ResolveBlobAsync("large.bin");

        await using GitBatchObjectReader reader = new(repository.RootPath);
        Assert.Equal(
            cachedContent,
            await reader.ReadBlobAsync(cachedObject, CancellationToken.None));

        await using (Stream heldGitStream = reader.OpenBlob(largeObject))
        {
            Task<Stream> synchronousCacheHit = Task.Run(() => reader.OpenBlob(cachedObject));
            try
            {
                await using Stream cachedStream = await synchronousCacheHit.WaitAsync(LivenessTimeout);
                Assert.Equal(cachedContent.Length, cachedStream.Length);

                byte[] asynchronousCacheHit = await reader
                    .ReadBlobAsync(cachedObject, CancellationToken.None)
                    .AsTask()
                    .WaitAsync(LivenessTimeout);
                Assert.Equal(cachedContent, asynchronousCacheHit);
            }
            finally
            {
                if (!synchronousCacheHit.IsCompleted)
                {
                    await heldGitStream.DisposeAsync();
                    await synchronousCacheHit.WaitAsync(LivenessTimeout);
                }
            }
        }

        await using (Stream firstProduced = reader.OpenBlob(firstObject))
        {
            Task<Stream> nextProduction = Task.Run(() => reader.OpenBlob(secondObject));
            try
            {
                await using Stream secondProduced = await nextProduction.WaitAsync(LivenessTimeout);
                Assert.True(firstProduced.Length > 0);
                Assert.True(secondProduced.Length > 0);
            }
            finally
            {
                if (!nextProduction.IsCompleted)
                {
                    await firstProduced.DisposeAsync();
                    await nextProduction.WaitAsync(LivenessTimeout);
                }
            }
        }

        GitObjectReaderStatistics statistics = reader.GetStatistics();
        Assert.Equal(6, statistics.Requests);
        Assert.Equal(2, statistics.CacheHits);
        Assert.Equal(4, statistics.UniqueObjects);
    }

    [Fact]
    public async Task StandaloneSnapshotOwnsOneReaderUnderConcurrentFirstUse()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        for (int index = 0; index < 32; index++)
        {
            repository.WriteBytes(
                $"file-{index:D2}.txt",
                System.Text.Encoding.UTF8.GetBytes($"content {index}\n"));
        }

        string objectId = await repository.CommitAsync();
        IChangeSnapshot snapshot = await GitSnapshotFileSystem.CreateAsync(
            repository.RootPath,
            objectId,
            CancellationToken.None);
        try
        {
            byte[][] content = await Task.WhenAll(Enumerable.Range(0, 32).Select(index =>
                snapshot.ReadAllBytesAsync(
                    $"file-{index:D2}.txt",
                    CancellationToken.None).AsTask()));

            Assert.All(content, bytes => Assert.NotEmpty(bytes));
        }
        finally
        {
            await snapshot.DisposeAsync();
        }
    }

    private sealed class GitFixture : IDisposable
    {
        private GitFixture(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static async Task<GitFixture> CreateAsync()
        {
            string rootPath = Path.Combine(
                Path.GetTempPath(),
                "efforthours-object-reader-e2e",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);
            GitFixture fixture = new(rootPath);
            await fixture.GitAsync("init", "--quiet", "--initial-branch=main");
            await fixture.GitAsync("config", "user.name", "EffortHours E2E");
            await fixture.GitAsync("config", "user.email", "efforthours-e2e@example.invalid");
            return fixture;
        }

        public void WriteBytes(string relativePath, byte[] content)
        {
            File.WriteAllBytes(Path.Combine(RootPath, relativePath), content);
        }

        public async Task<string> CommitAsync()
        {
            await GitAsync("add", "--all");
            await GitAsync("commit", "--quiet", "-m", "object reader fixture");
            return await GitAsync("rev-parse", "HEAD");
        }

        public Task<string> ResolveBlobAsync(string path) => GitAsync("rev-parse", $"HEAD:{path}");

        public void Dispose()
        {
            foreach (string file in Directory.EnumerateFiles(RootPath, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(RootPath, recursive: true);
        }

        private async Task<string> GitAsync(params string[] arguments)
        {
            ExternalCommandResult result = await ExternalCommand.RunAsync(
                "git",
                RootPath,
                arguments,
                CancellationToken.None);
            return result.StandardOutput.Trim();
        }
    }
}
