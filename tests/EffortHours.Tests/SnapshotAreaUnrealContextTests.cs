using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class SnapshotAreaUnrealContextTests
{
    private static readonly SnapshotAreaDefinition[] Areas =
    [
        new() { Id = "zombies", Include = ["Content/Zombies/**"] },
        new() { Id = "player", Include = ["Content/Player/**"] },
        new() { Id = "plugin", Include = ["Plugins/**"] },
        new() { Id = "rest", Include = ["**"] },
    ];

    [Fact]
    public async Task AreaPackagesAreOwnedThroughContextOnlyDescriptorsWithoutDuplicatingTheEntryPoint()
    {
        GitArchiveSnapshot snapshot = Snapshot(
            ("Game.uproject", "{\"FileVersion\":3,\"Plugins\":[{\"Name\":\"A\",\"Enabled\":true}]}"),
            ("Config/DefaultGame.ini", "[/Script/EngineSettings.GeneralProjectSettings]\nProjectID=1\n"),
            ("Content/Zombies/BP_Zombie.uasset", Pointer("zombie", 703_946)),
            ("Content/Zombies/BP_ZombieSpawner.uasset", Pointer("spawner", 216_973)),
            ("Content/Player/BP_Player.uasset", Pointer("player", 16 * 1024 + 2_560 * 600)),
            ("Plugins/Tools/Tools.uplugin", "{\"FileVersion\":3}"),
            ("Plugins/Tools/Content/BP_Tool.uasset", Pointer("tool", 16 * 1024 + 2_560 * 50)));
        IReadOnlyList<SnapshotAreaInput> areas = SnapshotAreaPartition.Partition(snapshot, Areas);

        SnapshotAreaInput zombies = areas[0];
        Assert.Equal(2, zombies.OwnedFiles);
        Assert.Equal(1, zombies.ContextFiles);
        Assert.Equal(["Game.uproject"], zombies.Snapshot.ContextOnlyPaths.Order(StringComparer.Ordinal));
        Assert.NotEqual(Select(snapshot, zombies.Snapshot.Files.Keys).InputDigest, zombies.Snapshot.InputDigest);
        Assert.Equal(["Game.uproject"], areas[2].Snapshot.ContextOnlyPaths.Order(StringComparer.Ordinal));
        Assert.Empty(areas[3].Snapshot.ContextOnlyPaths);
        Assert.Equal(0, areas[3].ContextFiles);

        RepositoryEvidence whole = await ScanAsync(snapshot);
        RepositoryEvidence[] scanned = [.. await Task.WhenAll(areas.Select(area => ScanAsync(area.Snapshot)))];
        Assert.Equal(Nodes(whole), scanned.Sum(Nodes));
        Assert.Equal(Count(whole, EvidenceKinds.EntryPoint), scanned.Sum(evidence => Count(evidence, EvidenceKinds.EntryPoint)));
        Assert.Equal(1, Count(scanned[3], EvidenceKinds.EntryPoint));
        Assert.Equal(0, Count(scanned[3], EvidenceKinds.SourceStructure));
        Assert.Contains("scope:context-only", Assert.Single(scanned[0].Facts, fact => fact.Scope == "Game.uproject" && fact.Kind == EvidenceKinds.File).Tags);
        Assert.DoesNotContain(scanned[2].Facts, fact => fact.Id.StartsWith("unreal:", StringComparison.Ordinal) && fact.Scope == ".");

        SeedEstimator estimator = new();
        EstimateReport zombieEstimate = estimator.Estimate(scanned[0], EstimationProfile.Implementation, rateCard: null);
        Assert.Contains(zombieEstimate.Categories, category =>
            category.Category == EffortCategory.ProductionImplementation && category.Hours.Expected > 0m);
        Assert.DoesNotContain(zombieEstimate.WorkItems, item => item.Estimator.Id == "seed-rule:application-entry-point");
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.EstimateReport, ContractJson.Serialize(zombieEstimate)).IsValid);
    }

    [Fact]
    public void NonUnrealAreasKeepTheirPreviousInputsAndDigests()
    {
        GitArchiveSnapshot snapshot = Snapshot(("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"/>"),
            ("src/Main.cs", "public class Main {}"), (".gitignore", "bin/\n"), ("Content/Readme.uasset.txt", "not a package"));
        IReadOnlyList<SnapshotAreaInput> areas = SnapshotAreaPartition.Partition(snapshot,
            [new() { Id = "source", Include = ["src/**"] }, new() { Id = "rest", Include = ["**"] }]);
        Assert.All(areas, area =>
        {
            Assert.Empty(area.Snapshot.ContextOnlyPaths);
            Assert.Equal(Select(snapshot, area.Snapshot.Files.Keys).InputDigest, area.Snapshot.InputDigest);
        });
        Assert.Equal(1, areas[0].ContextFiles);
    }

    [Fact]
    public async Task ContextOnlyPathsMustBeSnapshotFilesAndScanOptionsRejectUnknownPaths()
    {
        GitArchiveSnapshot snapshot = Snapshot(("README.md", "# Readme"));
        Assert.Throws<InvalidDataException>(() => snapshot.Select(["README.md"], ["missing.uproject"]));
        await Assert.ThrowsAsync<InvalidDataException>(() => new RepositoryAnalysisPipeline(snapshot).ScanAsync(snapshot.RootPath,
            new RepositoryScanOptions { ContextOnlyPaths = new HashSet<string>(["missing.uproject"], StringComparer.Ordinal) }));
    }

    private static Task<RepositoryEvidence> ScanAsync(GitArchiveSnapshot snapshot) =>
        new RepositoryAnalysisPipeline(snapshot).ScanAsync(snapshot.RootPath,
            new RepositoryScanOptions { ContextOnlyPaths = snapshot.ContextOnlyPaths });

    private static GitArchiveSnapshot Select(GitArchiveSnapshot snapshot, IEnumerable<string> paths) =>
        new(paths.ToDictionary(path => path, path => snapshot.Files[path], StringComparer.Ordinal));

    private static decimal Nodes(RepositoryEvidence evidence) => evidence.Facts
        .Where(fact => fact.Id.StartsWith("unreal:graph:", StringComparison.Ordinal))
        .Sum(fact => fact.Measurements.Single(item => item.Name == "inferred-graph-nodes").Value);

    private static int Count(RepositoryEvidence evidence, string kind) => evidence.Facts.Count(fact =>
        fact.Kind == kind && fact.Id.StartsWith("unreal:", StringComparison.Ordinal));

    private static GitArchiveSnapshot Snapshot(params (string Path, string Text)[] files) =>
        new(files.ToDictionary(f => f.Path, f => Encoding.UTF8.GetBytes(f.Text), StringComparer.Ordinal));

    private static string Pointer(string seed, long size) =>
        "version https://git-lfs.github.com/spec/v1\noid sha256:" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed))).ToLowerInvariant() +
        $"\nsize {size}\n";
}
