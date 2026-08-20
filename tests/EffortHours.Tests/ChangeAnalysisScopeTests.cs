using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ChangeAnalysisScopeTests
{
    [Fact]
    public void SmallGitSnapshotsUseChangedScopeWithoutAFileCountThreshold()
    {
        ChangeSnapshotFile[] before =
        [
            File("Demo.csproj", "project"),
            File("src/Feature.cs", "feature-before"),
            File("notes/unrelated.md", "notes"),
        ];
        ChangeSnapshotFile[] after =
        [
            before[0],
            before[1] with { ObjectId = "feature-after" },
            before[2],
        ];
        GitSnapshotFileSystem baseSnapshot = GitSnapshotFileSystem.Create(
            Environment.CurrentDirectory,
            "base",
            before);
        GitSnapshotFileSystem headSnapshot = GitSnapshotFileSystem.Create(
            Environment.CurrentDirectory,
            "head",
            after);

        ChangeAnalysisScope scope = Assert.IsType<ChangeAnalysisScope>(
            ChangeAnalysisScope.Create(baseSnapshot, headSnapshot));

        Assert.Equal(0, ChangeEstimator.FullSnapshotAnalysisFileLimit);
        Assert.Equal(1, scope.ChangedPathCount);
        Assert.Equal(3, scope.FullPathCount);
        Assert.Equal(
            ["Demo.csproj", "src/Feature.cs"],
            [.. scope.Paths.Order(StringComparer.Ordinal)]);
    }

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
    public void ScopeIdentityReusesTheSameCanonicalPathSet()
    {
        ChangeSnapshotFile[] before =
        [
            File("Demo.csproj", "project-before"),
            File("src/Feature.cs", "feature-before"),
        ];
        ChangeAnalysisScope featureChange = ChangeAnalysisScope.CreateForFiles(
            before,
            [before[0], before[1] with { ObjectId = "feature-after" }]);
        ChangeSnapshotFile[] projectAfter =
        [
            before[0] with { ObjectId = "project-after" },
            before[1],
        ];
        ChangeAnalysisScope projectChange = ChangeAnalysisScope.CreateForFiles(
            before,
            projectAfter);

        Assert.Equal(
            [.. featureChange.Paths.Order(StringComparer.Ordinal)],
            [.. projectChange.Paths.Order(StringComparer.Ordinal)]);
        Assert.NotEqual(featureChange.ContextPathCount, projectChange.ContextPathCount);
        Assert.NotEqual(featureChange.RepresentativePathCount, projectChange.RepresentativePathCount);
        Assert.Equal(featureChange.Id, projectChange.Id);

        RepositoryEvidence cached = new()
        {
            Repository = new RepositoryDescriptor
            {
                Name = "repository",
                Ecosystems = ["dotnet"],
                SourceDigest = "sha256:cached",
            },
            Diagnostics =
            [
                new Diagnostic
                {
                    Code = "FB5205",
                    Severity = DiagnosticSeverity.Information,
                    Message = "Cached scope metadata.",
                },
            ],
        };
        RepositoryEvidence rebound = ChangeEstimator.ApplyAnalysisScope(
            cached,
            new MetadataSnapshot("head", projectAfter),
            projectChange);
        Diagnostic scopeDiagnostic = Assert.Single(
            rebound.Diagnostics,
            diagnostic => diagnostic.Code == "FB5205");

        Assert.DoesNotContain("Cached scope metadata", scopeDiagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("0 relevant unchanged context artifact(s)", scopeDiagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("1 ecosystem representative(s)", scopeDiagnostic.Message, StringComparison.Ordinal);
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
    public void ScopeKeepsOnlyRootAndChangedPathAncestorContext()
    {
        ChangeSnapshotFile[] before =
        [
            File("global.json", "global"),
            File("src/Directory.Build.props", "directory-build"),
            File("src/active/Active.csproj", "active-project"),
            File("src/active/Feature.cs", "feature-before"),
            .. Enumerable.Range(0, 256).Select(index =>
                File($"src/unrelated-{index:D3}/Unrelated.csproj", $"project-{index:D3}")),
        ];
        ChangeSnapshotFile[] after = [.. before.Select(file =>
            file.Path == "src/active/Feature.cs"
                ? file with { ObjectId = "feature-after" }
                : file)];

        ChangeAnalysisScope scope = ChangeAnalysisScope.CreateForFiles(before, after);

        Assert.Equal(1, scope.ChangedPathCount);
        Assert.Equal(3, scope.ContextPathCount);
        Assert.Equal(0, scope.RepresentativePathCount);
        Assert.Equal(259, scope.AvailableContextPathCount);
        Assert.Equal(
            [
                "global.json",
                "src/Directory.Build.props",
                "src/active/Active.csproj",
                "src/active/Feature.cs",
            ],
            [.. scope.Paths.Order(StringComparer.Ordinal)]);
        Assert.DoesNotContain(scope.Paths, path => path.Contains("unrelated", StringComparison.Ordinal));
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

    private sealed class MetadataSnapshot(
        string objectId,
        IReadOnlyList<ChangeSnapshotFile> files) : IChangeSnapshot
    {
        public string ObjectId { get; } = objectId;

        public string RootPath => ".";

        public EffortHours.Analysis.IRepositoryFileSystem FileSystem =>
            throw new NotSupportedException();

        public IReadOnlyList<ChangeSnapshotFile> Files { get; } = files;

        public ValueTask<byte[]> ReadAllBytesAsync(
            string relativePath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
