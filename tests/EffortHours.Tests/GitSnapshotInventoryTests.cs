using System.Text;
using EffortHours.Change;

namespace EffortHours.Tests;

public sealed class GitSnapshotInventoryTests
{
    [Fact]
    public void IncrementalInventoryMatchesCanonicalFullInventoryAcrossDeltaOrders()
    {
        GitSnapshotInventory parent = new(
            "parent",
            [
                File("src/alpha.cs", "alpha", 10),
                File("src/beta.cs", "beta", 20),
                File("src/context.csproj", "context", 30),
            ]);
        ChangeSnapshotFile betaAfter = File("src/beta.cs", "beta-after", 40);
        ChangeSnapshotFile gamma = File("src/gamma.cs", "gamma", 50);
        GitSnapshotInventory direct = GitSnapshotInventory.CreateIncremental(
            "direct",
            "parent",
            parent,
            ["src/alpha.cs", "src/beta.cs", "src/gamma.cs"],
            [gamma, betaAfter]);
        GitSnapshotInventory firstStep = GitSnapshotInventory.CreateIncremental(
            "first-step",
            "parent",
            parent,
            ["src/gamma.cs"],
            [gamma with { Length = -1 }]);
        GitSnapshotInventory secondStep = GitSnapshotInventory.CreateIncremental(
            "second-step",
            "first-step",
            firstStep,
            ["src/beta.cs", "src/alpha.cs"],
            [betaAfter with { Length = -1 }]);
        GitSnapshotInventory reconstructed = new(
            "reconstructed",
            [
                betaAfter with { Length = 999 },
                File("src/context.csproj", "context", 999),
                gamma with { Length = 999 },
            ]);

        Assert.Equal(reconstructed.SourceDigest, direct.SourceDigest);
        Assert.Equal(reconstructed.SourceDigest, secondStep.SourceDigest);
        Assert.Equal(
            ["src/beta.cs", "src/context.csproj", "src/gamma.cs"],
            [.. direct.Files.Select(file => file.Path)]);
        Assert.Equal(
            reconstructed.ContentObjectCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            direct.ContentObjectCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.Equal(
            reconstructed.AnalysisIndex.ContextPaths,
            direct.AnalysisIndex.ContextPaths);
        Assert.Equal(
            reconstructed.AnalysisIndex.RepresentativePaths,
            direct.AnalysisIndex.RepresentativePaths);

        Assert.Equal(3, parent.FileCount);
        Assert.Contains(parent.Files, file => file.Path == "src/alpha.cs");
        Assert.Equal("parent", direct.RootObjectId);
        Assert.Equal("parent", direct.FirstParentObjectId);
    }

    [Fact]
    public void IdentityOnlyTreeParserDefersBlobLengthsButKeepsSubmodulesBounded()
    {
        string blob = new('a', 40);
        string link = new('b', 40);
        string commit = new('c', 40);
        byte[] output = Encoding.UTF8.GetBytes(
            $"100644 blob {blob}\tsrc/Feature.cs\0" +
            $"120000 blob {link}\tsrc/link\0" +
            $"160000 commit {commit}\tsrc/submodule\0");

        List<ChangeSnapshotFile> files =
            GitSnapshotFileSystem.ParseTreeWithoutLengths(output);

        Assert.Equal(3, files.Count);
        Assert.Equal(-1, files[0].Length);
        Assert.Equal(-1, files[1].Length);
        Assert.Equal(0, files[2].Length);
        Assert.True(files[1].IsLink);
        Assert.True(files[2].IsSubmodule);
    }

    [Fact]
    public void ShallowTreeParserAndPartitioningKeepDeterministicDisjointWork()
    {
        string tree = new('a', 40);
        string blob = new('b', 40);
        List<GitSnapshotTreeEntry> entries = GitSnapshotFileSystem.ParseTreeEntries(
            Encoding.UTF8.GetBytes(
                $"040000 tree {tree}\tsrc\0" +
                $"100644 blob {blob}\tREADME.md\0"));

        Assert.Equal("tree", entries[0].Type);
        Assert.Equal("src", entries[0].Path);
        Assert.Equal("blob", entries[1].Type);
        string[][] shards = GitSnapshotTreeReader.Partition(
            ["zeta", "alpha", "gamma", "beta", "epsilon"],
            shardCount: 3);
        Assert.Equal(["alpha", "gamma"], shards[0]);
        Assert.Equal(["beta", "zeta"], shards[1]);
        Assert.Equal(["epsilon"], shards[2]);
        Assert.Equal(5, shards.SelectMany(shard => shard).Distinct(StringComparer.Ordinal).Count());
        Assert.True(GitSnapshotTreeReader.FitsCommandLine(shards));
        Assert.False(GitSnapshotTreeReader.FitsCommandLine(
            [[new string('x', GitSnapshotTreeReader.MaximumShardPathCharacters)]]));
        Assert.Equal(128, GitSnapshotTreeReader.MinimumParallelTreePaths);
        Assert.Equal(4, GitSnapshotTreeReader.MaximumParallelReads);
    }

    [Fact]
    public void ObjectStorageLayoutSelectsParallelReadsOnlyForLargeLooseStores()
    {
        GitObjectStorageLayout packed = GitObjectStorageLayout.Parse(
            "count: 0\nin-pack: 20000\npacks: 1\n");
        GitObjectStorageLayout smallLoose = GitObjectStorageLayout.Parse(
            "count: 1023\nin-pack: 0\npacks: 0\n");
        GitObjectStorageLayout largeLoose = GitObjectStorageLayout.Parse(
            "count: 1024\nin-pack: 0\npacks: 0\n");

        Assert.Equal(1, packed.SelectTreeReadParallelism(12));
        Assert.Equal(1, smallLoose.SelectTreeReadParallelism(12));
        Assert.Equal(4, largeLoose.SelectTreeReadParallelism(12));
        Assert.Equal(20000, packed.PackedObjectCount);
    }

    [Fact]
    public void IncrementalInventoryRetainsDuplicateObjectCountsAcrossRemovals()
    {
        GitSnapshotInventory parent = new(
            "parent",
            [
                File("src/alpha.cs", "shared", 10),
                File("src/beta.cs", "shared", 10),
            ]);
        GitSnapshotInventory oneRemaining = GitSnapshotInventory.CreateIncremental(
            "one-remaining",
            "parent",
            parent,
            ["src/alpha.cs"],
            []);
        GitSnapshotInventory noneRemaining = GitSnapshotInventory.CreateIncremental(
            "none-remaining",
            "one-remaining",
            oneRemaining,
            ["src/beta.cs"],
            []);

        Assert.Equal(2, parent.ContentObjectCounts["shared"]);
        Assert.Equal(1, oneRemaining.ContentObjectCounts["shared"]);
        Assert.DoesNotContain("shared", noneRemaining.ContentObjectCounts.Keys);
        Assert.NotEqual(parent.SourceDigest, oneRemaining.SourceDigest);
        Assert.NotEqual(oneRemaining.SourceDigest, noneRemaining.SourceDigest);
    }

    [Fact]
    public void PathSetIdentityChangesOnlyWhenPathsChange()
    {
        GitSnapshotInventory parent = new(
            "parent",
            [
                File("src/App.csproj", "project", 10),
                File("src/Program.cs", "before", 20),
            ]);
        GitSnapshotInventory contentEdit = GitSnapshotInventory.CreateIncremental(
            "content-edit",
            "parent",
            parent,
            ["src/Program.cs"],
            [File("src/Program.cs", "after", 30)]);
        GitSnapshotInventory reconstructed = new(
            "reconstructed",
            [
                File("src/App.csproj", "different-project-content", 40),
                File("src/Program.cs", "different-source-content", 50),
            ]);
        GitSnapshotInventory added = GitSnapshotInventory.CreateIncremental(
            "added",
            "content-edit",
            contentEdit,
            ["src/Extra.cs"],
            [File("src/Extra.cs", "extra", 60)]);
        GitSnapshotInventory embeddedNewline = new(
            "embedded-newline",
            [File("src/alpha\nbeta.cs", "joined", 70)]);
        GitSnapshotInventory separatePaths = new(
            "separate-paths",
            [
                File("src/alpha", "alpha", 80),
                File("beta.cs", "beta", 90),
            ]);

        Assert.Equal(parent.PathSetIdentity, contentEdit.PathSetIdentity);
        Assert.Equal(parent.PathSetIdentity, reconstructed.PathSetIdentity);
        Assert.NotEqual(parent.PathSetIdentity, added.PathSetIdentity);
        Assert.NotEqual(embeddedNewline.PathSetIdentity, separatePaths.PathSetIdentity);
        Assert.NotEqual(parent.SourceDigest, contentEdit.SourceDigest);
    }

    private static ChangeSnapshotFile File(string path, string objectId, long length) => new()
    {
        Path = path,
        ObjectId = objectId,
        Length = length,
        Mode = "100644",
    };
}
