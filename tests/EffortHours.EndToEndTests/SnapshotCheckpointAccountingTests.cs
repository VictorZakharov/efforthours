using EffortHours.Analysis;
using EffortHours.Change;

namespace EffortHours.EndToEndTests;

public sealed partial class SnapshotPortfolioCliTests
{
    [Fact]
    public async Task SnapshotCheckpointAccountsEntriesAndArtifactsWithoutRescanning()
    {
        using GitFixture execution = await GitFixture.CreateAsync();
        string checkpoint = Path.Combine(execution.RootPath, "checkpoint");
        Directory.CreateDirectory(Path.Combine(checkpoint, "artifacts", "demo"));
        await File.WriteAllTextAsync(Path.Combine(checkpoint, "artifacts", "demo", "preexisting.json"), "{}");
        using SnapshotPortfolioStore store = new(checkpoint, 64 * 1024);
        Assert.Null(store.RetainedBytes);
        await store.SaveAsync("receipts", "first", new { Value = new string('a', 512) }, CancellationToken.None);
        await store.SaveAsync("receipts", "first", new { Value = new string('b', 1024) }, CancellationToken.None);
        PhysicalRepositoryAnalysisArtifactStore artifacts = new(Path.Combine(checkpoint, "artifacts", "demo"), "context",
            store.AccountRetainedBytes);
        RepositoryAnalysisContribution contribution = new();
        for (int i = 0; i < 4; i++) artifacts.Save($"dotnet-razor/{i}", contribution);
        Assert.True(artifacts.TryLoad("dotnet-razor/0", out RepositoryAnalysisContribution _));
        await store.SaveAsync("bindings", "second", new { Value = "x" }, CancellationToken.None);
        Assert.Equal(RetainedJsonBytes(checkpoint), store.RetainedBytes);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveAsync("receipts", "large", new { Value = new string('c', 80 * 1024) }, CancellationToken.None));
        Assert.Equal(RetainedJsonBytes(checkpoint), store.RetainedBytes);
        Assert.False(File.Exists(Path.Combine(checkpoint, "receipts", SnapshotMeasurementIdentity.Digest("large")[7..] + ".json")));
    }

    [Fact]
    public void SnapshotArtifactStoreEvictsLeastRecentlyUsedEntries()
    {
        string directory = Directory.CreateTempSubdirectory("efforthours-artifact-lru-").FullName;
        try
        {
            long delta = 0;
            PhysicalRepositoryAnalysisArtifactStore store = new(directory, "context", bytes => delta += bytes);
            RepositoryAnalysisContribution contribution = new();
            int limit = RepositoryAnalysisArtifactCache.DefaultMaximumEntries;
            for (int i = 0; i < limit; i++) store.Save($"dotnet-razor/{i}", contribution);
            Assert.True(store.TryLoad("dotnet-razor/0", out RepositoryAnalysisContribution _));
            store.Save("dotnet-razor/new", contribution);
            Assert.Equal(1, store.Evictions);
            Assert.True(store.TryLoad("dotnet-razor/0", out RepositoryAnalysisContribution _));
            Assert.False(store.TryLoad("dotnet-razor/1", out RepositoryAnalysisContribution _));
            Assert.True(store.TryLoad("dotnet-razor/new", out RepositoryAnalysisContribution _));
            Assert.Equal(Directory.EnumerateFiles(directory, "*.json").Sum(p => new FileInfo(p).Length), delta);
            PhysicalRepositoryAnalysisArtifactStore reopened = new(directory, "context");
            Assert.True(reopened.TryLoad("dotnet-razor/0", out RepositoryAnalysisContribution _));
            Assert.Equal(0, reopened.Evictions);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static long RetainedJsonBytes(string checkpoint) => Directory
        .EnumerateFiles(checkpoint, "*.json", SearchOption.AllDirectories).Sum(p => new FileInfo(p).Length);
}
