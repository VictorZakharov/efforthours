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

    private static ChangeSnapshotFile File(string path, string objectId, long length) => new()
    {
        Path = path,
        ObjectId = objectId,
        Length = length,
        Mode = "100644",
    };
}
