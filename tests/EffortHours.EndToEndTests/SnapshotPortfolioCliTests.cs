using System.Text;
using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Fact]
    public async Task SnapshotPortfolioWarmRunAndPortableReceiptsNeedNoExportOrEstimator()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        string status = await repository.GitAsync("status", "--porcelain=v1");
        ProcessResult cold = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(cold.ExitCode == 0, cold.StandardError);
        SnapshotPortfolioReport first = ContractJson.Deserialize<SnapshotPortfolioReport>(cold.StandardOutput);
        Assert.Equal(3, first.Telemetry.EstimatorCalls);
        Assert.Equal(1, first.Telemetry.Exports);
        ProcessResult warm = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(warm.ExitCode == 0, warm.StandardError);
        SnapshotPortfolioReport second = ContractJson.Deserialize<SnapshotPortfolioReport>(warm.StandardOutput);
        Assert.Equal(0, second.Telemetry.EstimatorCalls);
        Assert.Equal(0, second.Telemetry.Exports);
        Assert.Equal(first.SemanticDigest, second.SemanticDigest);
        Assert.Equal(status, await repository.GitAsync("status", "--porcelain=v1"));
        Assert.DoesNotContain(repository.RootPath, cold.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("source-secret", cold.StandardOutput, StringComparison.Ordinal);

        string exported = Path.Combine(execution.RootPath, "portable.json");
        await File.WriteAllTextAsync(exported, cold.StandardOutput, Encoding.UTF8);
        ProcessResult portable = await SnapshotRunAsync(manifest, local, Path.Combine(execution.RootPath, "fresh"), "--import-receipts", exported);
        Assert.True(portable.ExitCode == 0, portable.StandardError);
        SnapshotPortfolioReport imported = ContractJson.Deserialize<SnapshotPortfolioReport>(portable.StandardOutput);
        Assert.Equal(0, imported.Telemetry.EstimatorCalls);
        Assert.Equal(0, imported.Telemetry.Exports);
        Assert.Equal(first.SemanticDigest, imported.SemanticDigest);
        Assert.Equal(first.Receipts.Select(r => r.ProducerVersion), imported.Receipts.Select(r => r.ProducerVersion));
    }

    [Fact]
    public async Task SnapshotPortfolioChangedAreaReusesOtherAreaAndPersistentArtifacts()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        ProcessResult cold = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(cold.ExitCode == 0, cold.StandardError);
        repository.WriteText("src/Main.cs", "public class Main { public int Add(int a, int b) => a + b + 1; }\n");
        _ = await SnapshotCommitAtAsync(repository, "changed area", "2026-02-15T12:00:00Z");
        ProcessResult changed = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(changed.ExitCode == 0, changed.StandardError);
        SnapshotPortfolioReport report = ContractJson.Deserialize<SnapshotPortfolioReport>(changed.StandardOutput);
        Assert.Equal(2, report.Telemetry.EstimatorCalls);
        Assert.Equal(1, report.Telemetry.Exports);
        Assert.True(report.Telemetry.ArtifactHits > 0);
        ProcessResult fresh = await SnapshotRunAsync(manifest, local, Path.Combine(execution.RootPath, "cold-again"));
        Assert.True(fresh.ExitCode == 0, fresh.StandardError);
        SnapshotPortfolioReport full = ContractJson.Deserialize<SnapshotPortfolioReport>(fresh.StandardOutput);
        Assert.Equal(report.Projects.SelectMany(p => p.Periods).Select(p => p.Hours), full.Projects.SelectMany(p => p.Periods).Select(p => p.Hours));
        Assert.Equal(report.Receipts.Select(r => r.Id), full.Receipts.Select(r => r.Id));
    }

    [Fact]
    public async Task SnapshotPortfolioEquivalentTreeKeepsDistinctProvenanceAndReproductionPins()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        ProcessResult cold = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(cold.ExitCode == 0, cold.StandardError);
        string original = Path.Combine(execution.RootPath, "original.json");
        await File.WriteAllTextAsync(original, cold.StandardOutput, Encoding.UTF8);
        string newHead = await SnapshotCommitAtAsync(repository, "same tree", "2026-02-15T12:00:00Z", allowEmpty: true);
        ProcessResult equivalent = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(equivalent.ExitCode == 0, equivalent.StandardError);
        SnapshotPortfolioReport reused = ContractJson.Deserialize<SnapshotPortfolioReport>(equivalent.StandardOutput);
        Assert.Equal(newHead, reused.Projects[0].Periods[2].CommitObjectId);
        Assert.Equal(0, reused.Telemetry.EstimatorCalls);
        Assert.Equal(0, reused.Telemetry.Exports);
        ProcessResult reproduction = await SnapshotRunAsync(manifest, local, checkpoint, "--reproduce", original);
        Assert.True(reproduction.ExitCode == 0, reproduction.StandardError);
        Assert.Equal(ContractJson.Deserialize<SnapshotPortfolioReport>(cold.StandardOutput).SemanticDigest,
            ContractJson.Deserialize<SnapshotPortfolioReport>(reproduction.StandardOutput).SemanticDigest);
    }

    [Fact]
    public async Task SnapshotPortfolioFailedProjectPreservesCompleteOutputAndSuccessfulReceipts()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture secondRepository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        string output = Path.Combine(execution.RootPath, "result.json");
        ProcessResult initial = await SnapshotRunAsync(manifest, local, checkpoint, "--output", output);
        Assert.True(initial.ExitCode == 0, initial.StandardError);
        string saved = await File.ReadAllTextAsync(output, Encoding.UTF8);
        SnapshotPortfolioManifest definition = ContractJson.Deserialize<SnapshotPortfolioManifest>(await File.ReadAllTextAsync(manifest, Encoding.UTF8));
        SnapshotPortfolioLocalMap map = ContractJson.Deserialize<SnapshotPortfolioLocalMap>(await File.ReadAllTextAsync(local, Encoding.UTF8));
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(definition with
        {
            Projects = [definition.Projects[0], definition.Projects[0] with { Id = "second", Ref = "missing-ref" }],
        }), Encoding.UTF8);
        await File.WriteAllTextAsync(local, ContractJson.SerializeDocument(map with
        {
            Projects = [map.Projects[0], new() { Id = "second", RepositoryPath = secondRepository.RootPath }],
        }), Encoding.UTF8);
        ProcessResult failed = await SnapshotRunAsync(manifest, local, checkpoint, "--output", output);
        Assert.NotEqual(0, failed.ExitCode);
        Assert.Equal(saved, await File.ReadAllTextAsync(output, Encoding.UTF8));
        Assert.Contains("incomplete", await File.ReadAllTextAsync(output + ".failure.json", Encoding.UTF8), StringComparison.Ordinal);
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(definition with
        {
            Projects = [definition.Projects[0], definition.Projects[0] with { Id = "second" }],
        }), Encoding.UTF8);
        ProcessResult resumed = await SnapshotRunAsync(manifest, local, checkpoint, "--output", output);
        Assert.True(resumed.ExitCode == 0, resumed.StandardError);
        SnapshotPortfolioReport result = ContractJson.Deserialize<SnapshotPortfolioReport>(await File.ReadAllTextAsync(output, Encoding.UTF8));
        Assert.Equal(0, result.Telemetry.EstimatorCalls); // Both synthetic projects have identical archived inputs.
    }

    [Fact]
    public async Task SnapshotPortfolioPreflightAndLockFailureDoNotPublishMeasurements()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        ProcessResult plan = await SnapshotRunAsync(manifest, local, checkpoint, "--preflight");
        Assert.True(plan.ExitCode == 0, plan.StandardError);
        Assert.False(Directory.Exists(checkpoint));
        SnapshotPortfolioReport report = ContractJson.Deserialize<SnapshotPortfolioReport>(plan.StandardOutput);
        Assert.Equal("planned", report.Status);
        Assert.Equal(0, report.Telemetry.EstimatorCalls);
        Assert.Equal(0, report.Telemetry.Exports);
        SnapshotPortfolioStore store = new(checkpoint);
        await using FileStream held = await store.AcquireLockAsync(CancellationToken.None);
        ProcessResult blocked = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.NotEqual(0, blocked.ExitCode);
        Assert.Contains("lock", blocked.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalImmutableInputPreservesArchiveAttributesAndStandaloneTotals()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        repository.WriteText(".gitattributes", "omitted.js export-ignore\nsubstitution.txt export-subst\n");
        repository.WriteText("omitted.js", "function excluded() { return 'source-secret'; }\n");
        repository.WriteText("substitution.txt", "$Format:%H$\n");
        string head = await SnapshotCommitAtAsync(repository, "attributes", "2026-02-15T12:00:00Z");
        GitArchiveSnapshot archive = await new GitClient().OpenArchiveAsync(repository.RootPath, head);
        Assert.DoesNotContain("omitted.js", archive.Files.Keys);
        Assert.Equal(head + "\n", Encoding.UTF8.GetString(archive.Files["substitution.txt"]));
        ProcessResult local = await RunCliAsync("estimate", repository.RootPath, "--revision", head, "--no-rate");
        Assert.True(local.ExitCode == 0, local.StandardError);
        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(archive).ScanAsync(archive.RootPath);
        Assert.Equal(new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation).TotalEffort,
            ContractJson.Deserialize<EstimateReport>(local.StandardOutput).TotalEffort);
    }

    private static async Task<GitFixture> SnapshotFixtureAsync()
    {
        GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("App.csproj", ProjectFile);
        repository.WriteText("src/Main.cs", "public class Main { public int Add(int a, int b) => a + b; }\n");
        repository.WriteText("Other.cs", "public class Other { public string Message => \"source-secret\"; }\n");
        repository.WriteText("README.md", "# Synthetic snapshot portfolio fixture\n");
        repository.WriteText(".gitignore", "ignored.cs\n");
        repository.WriteText(".hidden", "hidden file\n");
        _ = await SnapshotCommitAtAsync(repository, "initial snapshot", "2026-01-15T12:00:00Z");
        return repository;
    }

    private static (string Manifest, string Local, string Checkpoint) WriteSnapshotInputs(string execution, string repository)
    {
        SnapshotPortfolioManifest manifest = new()
        {
            Year = 2026,
            Timezone = "UTC",
            Profile = EstimationProfile.Implementation,
            Projects = [new() { Id = "demo", Ref = "main", Areas =
                [new() { Id = "source", Include = ["src/**"] }, new() { Id = "support", Include = ["**"] }] }],
        };
        SnapshotPortfolioLocalMap local = new() { Projects = [new() { Id = "demo", RepositoryPath = repository }] };
        string manifestPath = Path.Combine(execution, "portfolio.json");
        string localPath = Path.Combine(execution, "local.json");
        File.WriteAllText(manifestPath, ContractJson.SerializeDocument(manifest), Encoding.UTF8);
        File.WriteAllText(localPath, ContractJson.SerializeDocument(local), Encoding.UTF8);
        return (manifestPath, localPath, Path.Combine(execution, "checkpoint"));
    }

    private static Task<ProcessResult> SnapshotRunAsync(string manifest, string local, string checkpoint, params string[] options) =>
        RunCliAsync(["estimate", "portfolio", "--manifest", manifest, "--local", local, "--checkpoint", checkpoint,
            "--as-of", "2026-04-15T12:00:00Z", "--no-rate", .. options]);

    private static async Task<string> SnapshotCommitAtAsync(GitFixture repository, string message, string timestamp, bool allowEmpty = false)
    {
        await repository.GitAsync("add", "--all");
        System.Diagnostics.ProcessStartInfo start = StartInfo("git", repository.RootPath);
        start.Environment["GIT_AUTHOR_DATE"] = timestamp;
        start.Environment["GIT_COMMITTER_DATE"] = timestamp;
        foreach (string arg in new[] { "commit", "--quiet", "-m", message }) start.ArgumentList.Add(arg);
        if (allowEmpty) start.ArgumentList.Add("--allow-empty");
        ProcessResult commit = await RunAsync(start);
        Assert.True(commit.ExitCode == 0, commit.StandardError);
        return await repository.GitAsync("rev-parse", "HEAD");
    }
}
