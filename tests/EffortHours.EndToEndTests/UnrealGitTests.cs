using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests : ChangeCliTestSupport
{
    [Fact]
    public async Task UnrealLfsPointersAreValuedFromImmutableSnapshotsAndGraphGrowthFeedsChange()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Game/Game.uproject", "{\"FileVersion\":3,\"Description\":\"private-unreal-marker\"}");
        repository.WriteText("Game/Content/BP_Enemy.uasset", UnrealPointer("enemy-v1", 16 * 1024 + 2_560 * 40));
        repository.WriteText("Game/Content/T_Skin.uasset", UnrealPointer("skin", 8_000_000));
        repository.WriteText("Game/Content/Maps/Arena.umap", UnrealPointer("arena", 20_000_000));
        string baseCommit = await repository.CommitAsync("base");
        repository.WriteText("Game/Content/BP_Enemy.uasset", UnrealPointer("enemy-v2", 16 * 1024 + 2_560 * 400));
        string head = await repository.CommitAsync("grow enemy blueprint");
        repository.WriteText("Game/Content/BP_Enemy.uasset", "dirty working tree is ignored");

        ProcessResult estimate = await RunCliAsync("estimate", repository.RootPath, "--revision", head,
            "--snapshot-policy", "git-archive/1.0.0", "--no-rate");
        Assert.Equal(0, estimate.ExitCode);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.EstimateReport, estimate.StandardOutput).IsValid);
        Assert.DoesNotContain("private-unreal-marker", estimate.StandardOutput, StringComparison.Ordinal);
        using (JsonDocument report = JsonDocument.Parse(estimate.StandardOutput))
        {
            Assert.Contains(report.RootElement.GetProperty("workItems").EnumerateArray(), item =>
                item.GetProperty("scope").GetString() == "Game" &&
                item.GetProperty("estimator").GetProperty("id").GetString() == "seed-rule:polyglot-source-backbone");
        }

        await using IChangeSnapshot snapshot = await new GitClient().OpenSnapshotAsync(repository.RootPath, head);
        RepositoryEvidence immutable = await new RepositoryAnalysisPipeline(snapshot.FileSystem).ScanAsync(snapshot.RootPath);
        EvidenceFact structure = Assert.Single(immutable.Facts, fact => fact.Id.StartsWith("unreal:graph:", StringComparison.Ordinal));
        Assert.Equal(400m, structure.Measurements.Single(item => item.Name == "inferred-graph-nodes").Value);

        ProcessResult change = await RunCliAsync("change", repository.RootPath, "--base", baseCommit, "--head", head, "--no-rate");
        Assert.Equal(0, change.ExitCode);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeEstimateReport, change.StandardOutput).IsValid);
        ChangeEstimateReport report2 = ContractJson.Deserialize<ChangeEstimateReport>(change.StandardOutput);
        Assert.Equal("change-seed/0.22.0+seed-rules/0.4.0", report2.EstimatorVersion);
        Assert.True(report2.TotalEffort.Expected > 0m);
        Assert.Contains(report2.WorkItems, item => item.Title.Contains("maintained internal logic", StringComparison.Ordinal));
    }

    private static string UnrealPointer(string seed, long size) =>
        "version https://git-lfs.github.com/spec/v1\noid sha256:" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed))).ToLowerInvariant() +
        $"\nsize {size}\n";
}
