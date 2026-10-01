using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Fact]
    public async Task SnapshotPreflightRejectsSubmoduleWithoutExportOrPrivatePathDisclosure()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        string head = await repository.GitAsync("rev-parse", "HEAD");
        await repository.GitAsync("update-index", "--add", "--cacheinfo", "160000," + head + ",private-component");
        // Commit the index directly: ordinary 'add --all' would remove the synthetic gitlink.
        await repository.GitAsync("-c", "core.hooksPath=", "commit", "--quiet", "-m", "synthetic gitlink");
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        ProcessResult plan = await RunCliAsync("estimate", "portfolio", "--manifest", manifest, "--local", local,
            "--checkpoint", checkpoint, "--preflight", "--no-rate");
        Assert.True(plan.ExitCode == 0, plan.StandardError);
        SnapshotPortfolioReport result = ContractJson.Deserialize<SnapshotPortfolioReport>(plan.StandardOutput);
        Assert.Contains(result.Projects[0].Periods, p => p.PlanningIssue == "unsupported-source-entry");
        Assert.Equal(0, result.Telemetry.Exports);
        Assert.Equal(0, result.Telemetry.EstimatorCalls);
        Assert.DoesNotContain("private-component", plan.StandardOutput, StringComparison.Ordinal);
        Assert.False(Directory.Exists(checkpoint));
    }

    [Fact]
    public async Task SnapshotHistoricalAreasRequireExactApplicableRevisionDefinitions()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        string first = await repository.GitAsync("rev-parse", "HEAD");
        await repository.GitAsync("mv", "src", "current");
        string second = await SnapshotCommitAtAsync(repository, "rename", "2026-02-10T12:00:00Z");
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        SnapshotPortfolioManifest definition = ContractJson.Deserialize<SnapshotPortfolioManifest>(await File.ReadAllTextAsync(manifest, Encoding.UTF8));
        IReadOnlyList<SnapshotAreaDefinition> previous = definition.Projects[0].Areas;
        IReadOnlyList<SnapshotAreaDefinition> current = [previous[0] with { Include = ["current/**"] }, previous[1]];
        SnapshotProjectDefinition project = definition.Projects[0] with
        {
            AreaMeasurementMode = "revision-bound",
            Areas = current,
            AreaRevisions = [new(first, previous), new(second, current)],
        };
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(definition with { Projects = [project] }), Encoding.UTF8);
        ProcessResult measured = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(measured.ExitCode == 0, measured.StandardError);
        SnapshotPortfolioReport report = ContractJson.Deserialize<SnapshotPortfolioReport>(measured.StandardOutput);
        Assert.NotEqual(report.Projects[0].Periods[1].AreaDefinitionDigest, report.Projects[0].Periods[2].AreaDefinitionDigest);
        Assert.Equal(report.Projects[0].AreasDigest, report.Projects[0].Periods[4].AreaDefinitionDigest);
        Assert.All(report.Projects[0].Periods.Where(p => p.WholeReceiptId is not null), p =>
            Assert.Equal(p.Hours!.Expected, p.Areas.Sum(a => a.AllocatedExpectedHours)));
        ProcessResult warm = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(warm.ExitCode == 0, warm.StandardError);
        Assert.Equal(0, ContractJson.Deserialize<SnapshotPortfolioReport>(warm.StandardOutput).Telemetry.EstimatorCalls);
        Assert.Equal(0, ContractJson.Deserialize<SnapshotPortfolioReport>(warm.StandardOutput).Telemetry.Exports);
        project = project with { AreaRevisions = [new(second, current)] };
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(definition with { Projects = [project] }), Encoding.UTF8);
        ProcessResult unavailable = await SnapshotRunAsync(manifest, local, checkpoint, "--preflight");
        Assert.True(unavailable.ExitCode == 0, unavailable.StandardError);
        SnapshotPortfolioReport plan = ContractJson.Deserialize<SnapshotPortfolioReport>(unavailable.StandardOutput);
        Assert.Equal("invalid-area-definition", plan.Projects[0].Periods[1].PlanningIssue);
        ProcessResult failed = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.NotEqual(0, failed.ExitCode);
        Assert.Contains("invalid-area-definition", failed.StandardOutput, StringComparison.Ordinal);
    }
}
