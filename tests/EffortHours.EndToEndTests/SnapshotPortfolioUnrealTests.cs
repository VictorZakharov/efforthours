using System.Security.Cryptography;
using System.Text;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class SnapshotPortfolioCliTests
{
    [Fact]
    public async Task UnrealAreasValuePackagesThroughTheContextOnlyProjectDescriptor()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Game.uproject", "{\"FileVersion\":3}");
        repository.WriteText("Config/DefaultGame.ini", "[/Script/EngineSettings.GeneralProjectSettings]\nProjectID=1\n");
        repository.WriteText("Content/Zombies/BP_Zombie.uasset", Pointer("zombie", 703_946));
        repository.WriteText("Content/Zombies/BP_ZombieSpawner.uasset", Pointer("spawner", 216_973));
        repository.WriteText("Content/Player/BP_Player.uasset", Pointer("player", 16 * 1024 + 2_560 * 600));
        _ = await SnapshotCommitAtAsync(repository, "unreal snapshot", "2026-03-10T12:00:00Z");
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        SnapshotPortfolioManifest definition = ContractJson.Deserialize<SnapshotPortfolioManifest>(await File.ReadAllTextAsync(manifest, Encoding.UTF8));
        SnapshotProjectDefinition project = definition.Projects[0] with
        {
            Areas = [new() { Id = "zombies", Include = ["Content/Zombies/**"] }, new() { Id = "player", Include = ["Content/Player/**"] },
                new() { Id = "rest", Include = ["**"] }],
        };
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(definition with { Projects = [project] }), Encoding.UTF8);

        ProcessResult run = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(run.ExitCode == 0, run.StandardError);
        SnapshotPortfolioReport report = ContractJson.Deserialize<SnapshotPortfolioReport>(run.StandardOutput);
        SnapshotPortfolioValidation.Validate(report);
        SnapshotPeriodResult latest = report.Projects[0].Periods[4];
        Assert.Equal(latest.Hours!.Expected, latest.Areas.Sum(a => a.AllocatedExpectedHours));
        foreach (SnapshotAreaResult area in latest.Areas.Take(2))
        {
            Assert.Equal(1, area.ContextFileCount);
            SnapshotMeasurementReceipt receipt = report.Receipts.Single(r => r.Id == area.ReceiptId);
            Assert.Contains(receipt.Categories, c => c.Category == EffortCategory.ProductionImplementation && c.Hours.Expected > 0m);
        }

        Assert.Equal(0, latest.Areas[2].ContextFileCount);
        ProcessResult warm = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(warm.ExitCode == 0, warm.StandardError);
        SnapshotPortfolioReport second = ContractJson.Deserialize<SnapshotPortfolioReport>(warm.StandardOutput);
        Assert.Equal(report.SemanticDigest, second.SemanticDigest);
        Assert.Equal(0, second.Telemetry.EstimatorCalls);
    }

    private static string Pointer(string seed, long size) =>
        "version https://git-lfs.github.com/spec/v1\noid sha256:" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed))).ToLowerInvariant() +
        $"\nsize {size}\n";
}
