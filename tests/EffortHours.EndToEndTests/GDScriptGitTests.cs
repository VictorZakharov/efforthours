using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests : ChangeCliTestSupport
{
    [Fact]
    public async Task GDScriptGitScopeRetainsProjectOwnershipAndImmutableFormattingComparison()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("game/project.godot", "config_version=5\n");
        repository.WriteText("game/player.gd", "extends Node\nfunc value():\n    return 1\n");
        repository.WriteText("unrelated/project.godot", "config_version=5\n");
        repository.WriteText("unrelated/other.gd", "func other():\n    pass\n");
        _ = await repository.CommitAsync("base");
        repository.WriteText("game/player.gd", "extends Node\nfunc value( ):\n  # layout\n  return 1\n");
        string head = await repository.CommitAsync("format");
        repository.WriteText("game/player.gd", "func dirty_worktree():\n    pass\n");
        string status = await repository.GitAsync("status", "--porcelain=v1");
        ProcessResult result = await RunCliAsync("change", repository.RootPath, "--commit", head, "--no-rate");
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        ChangeEstimateReport report = ContractJson.Deserialize<ChangeEstimateReport>(result.StandardOutput);
        Assert.Equal(0m, report.TotalEffort.Expected);
        Assert.Equal(ChangePathClassification.FormattingOnly, Assert.Single(report.Evidence.Paths).Classification);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeEstimateReport, result.StandardOutput).IsValid);
        Assert.Equal(status, await repository.GitAsync("status", "--porcelain=v1"));

        await using IChangeSnapshot snapshot = await new GitClient().OpenSnapshotAsync(repository.RootPath, head);
        RepositoryEvidence immutable = await new RepositoryAnalysisPipeline(snapshot.FileSystem).ScanAsync(snapshot.RootPath);
        Assert.Equal(["game", "unrelated"], immutable.Facts.Where(fact =>
            fact.Kind == EvidenceKinds.EcosystemPackage).Select(fact => fact.Scope).Order(StringComparer.Ordinal));
        Assert.DoesNotContain("dirty_worktree", ContractJson.Serialize(immutable), StringComparison.Ordinal);
    }
}
