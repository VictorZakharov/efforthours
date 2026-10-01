using System.Text;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Fact]
    public async Task SnapshotLatestOnlyPreservesHistoricalWholeMeasurementsAndReusesWholeAcrossBoundaryChanges()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        await repository.GitAsync("mv", "src", "earlier");
        _ = await SnapshotCommitAtAsync(repository, "rename first area", "2026-02-10T12:00:00Z");
        await repository.GitAsync("mv", "earlier", "current");
        repository.WriteText("current/New.cs", "public class NewFeature { public bool Ready => true; }\n");
        _ = await SnapshotCommitAtAsync(repository, "introduce latest area", "2026-03-10T12:00:00Z");
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        SnapshotPortfolioManifest definition = ContractJson.Deserialize<SnapshotPortfolioManifest>(await File.ReadAllTextAsync(manifest, Encoding.UTF8));
        SnapshotProjectDefinition project = definition.Projects[0] with
        {
            AreaMeasurementMode = "latest-only",
            Areas = [new() { Id = "source", Include = ["current/**"] }, new() { Id = "support", Include = ["**"] }],
        };
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(definition with { Projects = [project] }), Encoding.UTF8);
        ProcessResult cold = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(cold.ExitCode == 0, cold.StandardError);
        SnapshotPortfolioReport first = ContractJson.Deserialize<SnapshotPortfolioReport>(cold.StandardOutput);
        SnapshotPortfolioValidation.Validate(first);
        Assert.True(ContractSchemaValidator.Validate("snapshot-portfolio-report.schema.json", cold.StandardOutput).IsValid);
        Assert.All(first.Projects[0].Periods.Skip(1).Take(3), p =>
        {
            Assert.NotNull(p.WholeReceiptId);
            Assert.NotNull(p.Hours);
            Assert.Empty(p.Areas);
            Assert.Equal("not-requested", p.AreaDisposition);
        });
        SnapshotPeriodResult latest = first.Projects[0].Periods[4];
        Assert.Equal("measured", latest.AreaDisposition);
        Assert.Equal(latest.Hours!.Expected, latest.Areas.Sum(a => a.AllocatedExpectedHours));
        ProcessResult warm = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(warm.ExitCode == 0, warm.StandardError);
        SnapshotPortfolioReport second = ContractJson.Deserialize<SnapshotPortfolioReport>(warm.StandardOutput);
        Assert.Equal(first.SemanticDigest, second.SemanticDigest);
        Assert.Equal(0, second.Telemetry.EstimatorCalls);
        Assert.Equal(0, second.Telemetry.Exports);
        project = project with { Areas = [project.Areas[0] with { Include = ["current/**", "README.md"] }, project.Areas[1]] };
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(definition with { Projects = [project] }), Encoding.UTF8);
        ProcessResult revised = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(revised.ExitCode == 0, revised.StandardError);
        SnapshotPortfolioReport changed = ContractJson.Deserialize<SnapshotPortfolioReport>(revised.StandardOutput);
        Assert.Equal(first.Projects[0].Periods.Select(p => p.WholeReceiptId), changed.Projects[0].Periods.Select(p => p.WholeReceiptId));
        Assert.Equal(2, changed.Telemetry.EstimatorCalls);
        Assert.Equal(1, changed.Telemetry.Exports);
        Assert.Equal(changed.Projects[0].Periods[4].Hours!.Expected, changed.Projects[0].Periods[4].Areas.Sum(a => a.AllocatedExpectedHours));
    }

    [Fact]
    public async Task SnapshotPreflightDistinguishesUnmatchedHistoricalSelectorsAndReusesImmutablePlanning()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        await repository.GitAsync("mv", "src", "current");
        _ = await SnapshotCommitAtAsync(repository, "rename", "2026-02-10T12:00:00Z");
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        SnapshotPortfolioManifest definition = ContractJson.Deserialize<SnapshotPortfolioManifest>(await File.ReadAllTextAsync(manifest, Encoding.UTF8));
        SnapshotProjectDefinition project = definition.Projects[0] with
        {
            Areas = [new() { Id = "source", Include = ["current/**"] }, new() { Id = "support", Include = ["**"] }],
        };
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(definition with { Projects = [project] }), Encoding.UTF8);
        ProcessResult planned = await SnapshotRunAsync(manifest, local, checkpoint, "--preflight");
        Assert.True(planned.ExitCode == 0, planned.StandardError);
        SnapshotPortfolioReport plan = ContractJson.Deserialize<SnapshotPortfolioReport>(planned.StandardOutput);
        Assert.Equal("unmatched-selector", plan.Projects[0].Periods[1].PlanningIssue);
        Assert.Equal("source", plan.Projects[0].Periods[1].PlanningAreaId);
        Assert.Equal("unavailable", plan.Projects[0].Periods[1].Status);
        Assert.Null(plan.Projects[0].Periods[1].Hours);
        Assert.NotNull(plan.Projects[0].Periods[1].CommitObjectId);
        Assert.Null(plan.Projects[0].PlanningIssue);
        Assert.Equal(2, plan.Telemetry.InventoryReads);
        Assert.Equal(2, plan.Telemetry.AreaPlanningCalls);
        Assert.Equal(2, plan.Telemetry.SelectorCompilations);
        Assert.True(plan.Telemetry.PlanningReuseHits >= 2);
        Assert.Equal(0, plan.Telemetry.Exports);
        Assert.Equal(0, plan.Telemetry.EstimatorCalls);
        Assert.False(Directory.Exists(checkpoint));
        Assert.Contains("planning: demo", planned.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain(repository.RootPath, planned.StandardOutput, StringComparison.Ordinal);
        ProcessResult failed = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.NotEqual(0, failed.ExitCode);
        Assert.Contains("unmatched-selector", failed.StandardOutput, StringComparison.Ordinal);
        Assert.True(ContractSchemaValidator.Validate("snapshot-portfolio-failure.schema.json", failed.StandardOutput).IsValid);
        project = project with { AreaMeasurementMode = "latest-only" };
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(definition with { Projects = [project] }), Encoding.UTF8);
        ProcessResult latestPlan = await SnapshotRunAsync(manifest, local, Path.Combine(execution.RootPath, "fresh-plan"), "--preflight");
        Assert.True(latestPlan.ExitCode == 0, latestPlan.StandardError);
        SnapshotPortfolioReport latest = ContractJson.Deserialize<SnapshotPortfolioReport>(latestPlan.StandardOutput);
        Assert.All(latest.Projects[0].Periods, p => Assert.Null(p.PlanningIssue));
        Assert.Equal(1, latest.Telemetry.AreaPlanningCalls);
        project = project with { Areas = [project.Areas[0] with { Include = ["absent/**"] }, project.Areas[1]] };
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(definition with { Projects = [project] }), Encoding.UTF8);
        ProcessResult invalidLatest = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.NotEqual(0, invalidLatest.ExitCode);
        Assert.Contains("unmatched-selector", invalidLatest.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SnapshotMixedVisibilityDashboardOmitsPrivateLocatorsAndRejectsStaleReviewsAtomically()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        SnapshotPortfolioManifest definition = ContractJson.Deserialize<SnapshotPortfolioManifest>(await File.ReadAllTextAsync(manifest, Encoding.UTF8));
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(definition with
        {
            Projects = [definition.Projects[0], definition.Projects[0] with { Id = "private" }],
        }), Encoding.UTF8);
        await File.WriteAllTextAsync(local, ContractJson.SerializeDocument(new SnapshotPortfolioLocalMap
        {
            Projects = [new() { Id = "demo", RepositoryPath = repository.RootPath }, new() { Id = "private", RepositoryPath = repository.RootPath }],
        }), Encoding.UTF8);
        ProcessResult measured = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(measured.ExitCode == 0, measured.StandardError);
        SnapshotPortfolioReport report = ContractJson.Deserialize<SnapshotPortfolioReport>(measured.StandardOutput);
        string input = Path.Combine(execution.RootPath, "result.json");
        string authored = Path.Combine(execution.RootPath, "studies.json");
        string asset = Path.Combine(execution.RootPath, "asset.json");
        await File.WriteAllTextAsync(input, measured.StandardOutput, Encoding.UTF8);
        SnapshotDashboardStudy publicStudy = new()
        {
            Id = "demo",
            SourceVisibility = "public",
            PublicRepositoryUrl = "https://github.com/example/synthetic",
            AreasDigest = report.Projects[0].AreasDigest,
            Areas = [new() { Id = "source", Folder = "src", ReviewedCommit = report.Projects[0].HeadObjectId! },
                new() { Id = "support", Folder = ".", ReviewedCommit = report.Projects[0].HeadObjectId! }],
        };
        SnapshotDashboardStudy privateStudy = publicStudy with { Id = "private", SourceVisibility = "closed-source", PublicRepositoryUrl = null };
        SnapshotDashboardStudies studies = new() { Projects = [publicStudy, privateStudy] };
        await File.WriteAllTextAsync(authored, ContractJson.SerializeDocument(studies), Encoding.UTF8);
        ProcessResult adapter = await RunCliAsync("portfolio-adapter", "--input", input, "--studies", authored, "--output", asset);
        Assert.True(adapter.ExitCode == 0, adapter.StandardError);
        string saved = await File.ReadAllTextAsync(asset, Encoding.UTF8);
        Assert.True(ContractSchemaValidator.Validate("snapshot-dashboard-asset.schema.json", saved).IsValid);
        SnapshotDashboardAsset result = ContractJson.Deserialize<SnapshotDashboardAsset>(saved);
        Assert.All(result.Projects[0].Periods.SelectMany(p => p.Areas), a => Assert.Contains("/tree/", a.FolderLink!, StringComparison.Ordinal));
        Assert.All(result.Projects[1].Periods.SelectMany(p => p.Areas), a => Assert.Null(a.FolderLink));
        string privateJson = ContractJson.Serialize(result.Projects[1]);
        Assert.DoesNotContain("folder", privateJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github", privateJson, StringComparison.OrdinalIgnoreCase);
        foreach (SnapshotDashboardStudies invalid in new[]
        {
            studies with { Projects = [publicStudy with { PublicRepositoryUrl = "https://github.com/example/synthetic?secret=value" }, privateStudy] },
            studies with { Projects = [publicStudy, privateStudy with { Areas = [privateStudy.Areas[0] with { ReviewedCommit = new string('a', 40) }, privateStudy.Areas[1]] }] },
            studies with { Projects = [publicStudy, privateStudy with { PublicRepositoryUrl = "https://private.invalid/hidden" }] },
        })
        {
            await File.WriteAllTextAsync(authored, ContractJson.SerializeDocument(invalid), Encoding.UTF8);
            ProcessResult rejected = await RunCliAsync("portfolio-adapter", "--input", input, "--studies", authored, "--output", asset);
            Assert.Equal(3, rejected.ExitCode);
            Assert.DoesNotContain("internal error", rejected.StandardError, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(saved, await File.ReadAllTextAsync(asset, Encoding.UTF8));
        }
        // Private folders are optional, but a supplied folder remains hash-verified private input.
        studies = studies with { Projects = [publicStudy, privateStudy with { Areas = [.. privateStudy.Areas.Select(a => a with { Folder = null })] }] };
        await File.WriteAllTextAsync(authored, ContractJson.SerializeDocument(studies), Encoding.UTF8);
        ProcessResult noFolders = await RunCliAsync("portfolio-adapter", "--input", input, "--studies", authored, "--output", asset);
        Assert.True(noFolders.ExitCode == 0, noFolders.StandardError);
        Assert.Equal(saved, await File.ReadAllTextAsync(asset, Encoding.UTF8));
    }
}
