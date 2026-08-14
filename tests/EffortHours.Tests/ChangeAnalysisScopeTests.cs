using EffortHours.Change;

namespace EffortHours.Tests;

public sealed class ChangeAnalysisScopeTests
{
    [Fact]
    public void ScopeKeepsChangedPathsStaticContextAndOneEcosystemRepresentative()
    {
        ChangeSnapshotFile[] before =
        [
            File("Demo.csproj", "project"),
            .. Enumerable.Range(0, ChangeEstimator.FullSnapshotAnalysisFileLimit + 1)
                .Select(index => File($"src/part-{index:D4}/Type.cs", $"blob-{index:D4}")),
        ];
        ChangeSnapshotFile[] after = [.. before.Select(file =>
            file.Path == "src/part-0000/Type.cs"
                ? file with { ObjectId = "changed-blob" }
                : file)];

        ChangeAnalysisScope scope = ChangeAnalysisScope.CreateForFiles(before, after);
        ChangeAnalysisScope reordered = ChangeAnalysisScope.CreateForFiles(
            [.. before.Reverse()],
            [.. after.Reverse()]);

        Assert.Equal(1, scope.ChangedPathCount);
        Assert.Equal(1, scope.ContextPathCount);
        Assert.Equal(before.Length, scope.FullPathCount);
        Assert.Equal(
            ["Demo.csproj", "src/part-0000/Type.cs"],
            [.. scope.Paths.Order(StringComparer.Ordinal)]);
        Assert.Equal(scope.Id, reordered.Id);
        Assert.Equal(
            ChangeAnalysisScope.ComputeInventoryDigest(before),
            ChangeAnalysisScope.ComputeInventoryDigest([.. before.Reverse()]));
        Assert.NotEqual(
            ChangeAnalysisScope.ComputeInventoryDigest(before),
            ChangeAnalysisScope.ComputeInventoryDigest(
                [before[0] with { Mode = "100755" }, .. before[1..]]));
    }

    [Fact]
    public async Task ScopedFileSystemTraversesOnlyScopeButRetainsStaticReferenceLookups()
    {
        InMemoryRepository repository = new();
        repository.WriteText("src/selected.cs", "selected");
        repository.WriteText("src/reference.cs", "reference");
        ScopedRepositoryFileSystem scoped = new(
            repository,
            repository.RootPath,
            new HashSet<string>(["src/selected.cs"], StringComparer.Ordinal));
        string sourceDirectory = Path.Combine(repository.RootPath, "src");
        string selected = Path.Combine(sourceDirectory, "selected.cs");
        string reference = Path.Combine(sourceDirectory, "reference.cs");

        Assert.Equal([sourceDirectory], scoped.GetFileSystemEntries(repository.RootPath));
        Assert.Equal([selected], scoped.GetFileSystemEntries(sourceDirectory));
        Assert.True(scoped.FileExists(reference));
        Assert.Equal(
            "reference",
            System.Text.Encoding.UTF8.GetString(await scoped.ReadAllBytesAsync(reference)));
    }

    [Fact]
    public void ScopeRetainsAHeadRepresentativeWhenTheBaseRepresentativeWasDeleted()
    {
        ChangeSnapshotFile[] before =
        [
            File("Demo.csproj", "project"),
            File("src/alpha.cs", "alpha"),
            File("src/beta.cs", "beta"),
        ];
        ChangeSnapshotFile[] after = [before[0], before[2]];

        ChangeAnalysisScope scope = ChangeAnalysisScope.CreateForFiles(before, after);

        Assert.Equal(1, scope.ChangedPathCount);
        Assert.Equal(
            ["Demo.csproj", "src/alpha.cs", "src/beta.cs"],
            [.. scope.Paths.Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public void IncrementalGitInventoryAppliesAddsModificationsAndDeletesDeterministically()
    {
        ChangeSnapshotFile[] parent =
        [
            File("src/alpha.cs", "alpha-old"),
            File("src/beta.cs", "beta-old"),
        ];
        ChangeSnapshotFile[] changed =
        [
            File("src/gamma.cs", "gamma-new"),
            File("src/alpha.cs", "alpha-new"),
        ];

        IReadOnlyList<ChangeSnapshotFile> actual = GitSnapshotFileSystem.ApplyIncrementalChanges(
            parent,
            ["src/beta.cs", "src/gamma.cs", "src/alpha.cs"],
            changed);

        Assert.Equal(["src/alpha.cs", "src/gamma.cs"], [.. actual.Select(file => file.Path)]);
        Assert.Equal("alpha-new", actual[0].ObjectId);
        Assert.Equal("gamma-new", actual[1].ObjectId);
    }

    private static ChangeSnapshotFile File(string path, string objectId) => new()
    {
        Path = path,
        ObjectId = objectId,
        Length = 1,
        Mode = "100644",
    };
}
