using System.Security.Cryptography;
using System.Text;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    private static readonly string[] MeasurementBuildInputs = ["Directory.Packages.props", "Directory.Build.props", "global.json"];
    [Fact]
    public async Task SnapshotPortfolioCorruptReceiptIsAMissAndPricingRequiresNoAnalysis()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        ProcessResult cold = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(cold.ExitCode == 0, cold.StandardError);
        SnapshotPortfolioReport first = ContractJson.Deserialize<SnapshotPortfolioReport>(cold.StandardOutput);
        string receipt = first.Projects[0].Periods[1].WholeReceiptId!;
        string receiptPath = Path.Combine(checkpoint, "receipts", SnapshotMeasurementIdentity.Digest(receipt)[7..] + ".json");
        await File.WriteAllTextAsync(receiptPath, "{}", Encoding.UTF8);
        ProcessResult recovered = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(recovered.ExitCode == 0, recovered.StandardError);
        SnapshotPortfolioReport repaired = ContractJson.Deserialize<SnapshotPortfolioReport>(recovered.StandardOutput);
        Assert.Equal(1, repaired.Telemetry.EstimatorCalls);
        Assert.Equal(first.SemanticDigest, repaired.SemanticDigest);
        string referenceKey = SnapshotPortfolioStore.MeasurementKey(first.Receipts.Single(r => r.Id == receipt).InputDigest,
            first.Receipts.Single(r => r.Id == receipt).Measurement, first.Receipts.Single(r => r.Id == receipt).SelectedFileCount, 0);
        await File.WriteAllTextAsync(Path.Combine(checkpoint, "measurements", SnapshotMeasurementIdentity.Digest(referenceKey)[7..] + ".json"), "{}", Encoding.UTF8);
        await File.WriteAllTextAsync(receiptPath, "{}", Encoding.UTF8);
        ProcessResult missingReference = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(missingReference.ExitCode == 0, missingReference.StandardError);
        Assert.True(ContractJson.Deserialize<SnapshotPortfolioReport>(missingReference.StandardOutput).Telemetry.ReceiptInvalidations > 0);
        ProcessResult priced = await RunCliAsync("estimate", "portfolio", "--manifest", manifest, "--local", local,
            "--checkpoint", checkpoint, "--as-of", "2026-04-15T12:00:00Z", "--hourly-rate", "175", "--currency", "CAD");
        Assert.True(priced.ExitCode == 0, priced.StandardError);
        SnapshotPortfolioReport money = ContractJson.Deserialize<SnapshotPortfolioReport>(priced.StandardOutput);
        Assert.Equal(0, money.Telemetry.EstimatorCalls);
        Assert.Equal(0, money.Telemetry.Exports);
        Assert.Equal(first.Receipts.Select(r => r.Id), money.Receipts.Select(r => r.Id));
        Assert.Equal("CAD", money.RateCard!.Currency);
        Assert.Equal(money.Projects[0].Periods[1].Hours!.Expected * 175, money.Projects[0].Periods[1].TotalCost!.Expected);
    }

    [Fact]
    public async Task SnapshotPortfolioDependencyContextAndJavaScriptPersistenceMatchColdAnalysis()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        repository.WriteText("package.json", "{\"name\":\"demo\",\"dependencies\":{\"express\":\"5.0.0\"}}\n");
        repository.WriteText("web/main.js", "import express from 'express'; const app = express(); app.get('/hello', (req, res) => res.send('hello'));\n");
        _ = await SnapshotCommitAtAsync(repository, "javascript", "2026-01-20T12:00:00Z");
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        ProcessResult before = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(before.ExitCode == 0, before.StandardError);
        repository.WriteText("src/Main.cs", "public class Main { public int Add(int a, int b) => a + b + 2; }\n");
        _ = await SnapshotCommitAtAsync(repository, "changed csharp", "2026-02-10T12:00:00Z");
        ProcessResult reused = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(reused.ExitCode == 0, reused.StandardError);
        ProcessResult cold = await SnapshotRunAsync(manifest, local, Path.Combine(execution.RootPath, "cold"));
        Assert.True(cold.ExitCode == 0, cold.StandardError);
        Assert.Equal(ContractJson.Deserialize<SnapshotPortfolioReport>(cold.StandardOutput).Receipts.Select(r => r.Id),
            ContractJson.Deserialize<SnapshotPortfolioReport>(reused.StandardOutput).Receipts.Select(r => r.Id));
        repository.WriteText("package.json", "{\"name\":\"demo\",\"dependencies\":{\"react\":\"19.0.0\"}}\n");
        repository.WriteText("App.csproj", ProjectFile.Replace("</Project>", "<ItemGroup><PackageReference Include=\"xunit\" Version=\"2.9.0\"/></ItemGroup></Project>", StringComparison.Ordinal));
        _ = await SnapshotCommitAtAsync(repository, "dependency context", "2026-03-10T12:00:00Z");
        ProcessResult contextual = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(contextual.ExitCode == 0, contextual.StandardError);
        ProcessResult full = await SnapshotRunAsync(manifest, local, Path.Combine(execution.RootPath, "context-cold"));
        Assert.True(full.ExitCode == 0, full.StandardError);
        Assert.Equal(ContractJson.Deserialize<SnapshotPortfolioReport>(full.StandardOutput).Receipts.Select(r => r.Id),
            ContractJson.Deserialize<SnapshotPortfolioReport>(contextual.StandardOutput).Receipts.Select(r => r.Id));
    }

    [Fact]
    public async Task SnapshotDashboardAdapterValidatesStudiesFoldersAndPreservesExistingAsset()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        ProcessResult measured = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(measured.ExitCode == 0, measured.StandardError);
        SnapshotPortfolioReport report = ContractJson.Deserialize<SnapshotPortfolioReport>(measured.StandardOutput);
        string input = Path.Combine(execution.RootPath, "result.json");
        string studiesPath = Path.Combine(execution.RootPath, "studies.json");
        string asset = Path.Combine(execution.RootPath, "asset.json");
        await File.WriteAllTextAsync(input, measured.StandardOutput, Encoding.UTF8);
        SnapshotDashboardStudies studies = new()
        {
            Projects = [new()
            {
                Id = "demo", PublicRepositoryUrl = "https://github.com/example/synthetic", AreasDigest = report.Projects[0].AreasDigest,
                Areas = [new() { Id = "source", Folder = "src", ReviewedCommit = report.Projects[0].HeadObjectId! },
                    new() { Id = "support", Folder = ".", ReviewedCommit = report.Projects[0].HeadObjectId! }],
            }],
        };
        await File.WriteAllTextAsync(studiesPath, ContractJson.SerializeDocument(studies), Encoding.UTF8);
        ProcessResult adapter = await RunCliAsync("portfolio-adapter", "--input", input, "--studies", studiesPath, "--output", asset);
        Assert.True(adapter.ExitCode == 0, adapter.StandardError);
        string saved = await File.ReadAllTextAsync(asset, Encoding.UTF8);
        Assert.Contains("/tree/" + report.Projects[0].HeadObjectId + "/src", saved, StringComparison.Ordinal);
        Assert.Contains("Expected EHE", saved, StringComparison.Ordinal);
        Assert.True(ContractSchemaValidator.Validate("snapshot-dashboard-asset.schema.json", saved).IsValid);
        studies = studies with
        {
            Projects = [studies.Projects[0] with
        {
            Areas = [studies.Projects[0].Areas[0] with { Folder = "missing" }, studies.Projects[0].Areas[1]],
        }]
        };
        await File.WriteAllTextAsync(studiesPath, ContractJson.SerializeDocument(studies), Encoding.UTF8);
        ProcessResult invalid = await RunCliAsync("portfolio-adapter", "--input", input, "--studies", studiesPath, "--output", asset);
        Assert.NotEqual(0, invalid.ExitCode);
        Assert.Equal(saved, await File.ReadAllTextAsync(asset, Encoding.UTF8));
    }

    [Fact]
    public async Task SnapshotPortfolioPlansShallowHistoryAndMissingRefsWithoutAssumedZero()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        SnapshotPortfolioManifest original = ContractJson.Deserialize<SnapshotPortfolioManifest>(await File.ReadAllTextAsync(manifest, Encoding.UTF8));
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(original with
        {
            Projects = [original.Projects[0] with { Ref = "missing-ref" }],
        }), Encoding.UTF8);
        ProcessResult plan = await SnapshotRunAsync(manifest, local, checkpoint, "--preflight");
        Assert.True(plan.ExitCode == 0, plan.StandardError);
        SnapshotPortfolioReport result = ContractJson.Deserialize<SnapshotPortfolioReport>(plan.StandardOutput);
        Assert.Equal("missing-object-or-ref", result.Projects[0].PlanningIssue);
        Assert.Equal("unavailable", result.Projects[0].Periods[1].Status);
        Assert.Null(result.Projects[0].Periods[1].Hours);
        Assert.False(Directory.Exists(checkpoint));
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(original), Encoding.UTF8);
        string shallow = Path.Combine(execution.RootPath, "shallow");
        System.Diagnostics.ProcessStartInfo start = StartInfo("git", execution.RootPath);
        foreach (string arg in new[] { "clone", "--quiet", "--depth", "1", new Uri(repository.RootPath + Path.DirectorySeparatorChar).AbsoluteUri, shallow })
            start.ArgumentList.Add(arg);
        ProcessResult cloned = await RunAsync(start);
        Assert.True(cloned.ExitCode == 0, cloned.StandardError);
        await File.WriteAllTextAsync(local, ContractJson.SerializeDocument(new SnapshotPortfolioLocalMap
        {
            Projects = [new() { Id = "demo", RepositoryPath = shallow }],
        }), Encoding.UTF8);
        ProcessResult shallowPlan = await SnapshotRunAsync(manifest, local, checkpoint, "--preflight");
        Assert.True(shallowPlan.ExitCode == 0, shallowPlan.StandardError);
        Assert.True(ContractJson.Deserialize<SnapshotPortfolioReport>(shallowPlan.StandardOutput).Projects[0].ShallowHistory);
        ProcessResult failed = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.NotEqual(0, failed.ExitCode);
        Assert.Contains("Shallow", failed.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SnapshotArchiveNeverExecutesConfiguredFiltersAndRejectsMutableAttributeOverrides()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        repository.WriteText(".gitattributes", "*.txt filter=unsafe\n");
        repository.WriteText("body.txt", "exact body\n");
        string head = await SnapshotCommitAtAsync(repository, "filter attribute", "2026-02-15T12:00:00Z");
        await repository.GitAsync("config", "filter.unsafe.smudge", "touch target-filter-executed");
        await repository.GitAsync("config", "filter.unsafe.required", "true");
        GitArchiveSnapshot archive = await new GitClient().OpenArchiveAsync(repository.RootPath, head);
        Assert.Equal("exact body\n", Encoding.UTF8.GetString(archive.Files["body.txt"]));
        Assert.False(File.Exists(Path.Combine(repository.RootPath, "target-filter-executed")));
        string info = Path.Combine(repository.RootPath, ".git", "info", "attributes");
        await File.WriteAllTextAsync(info, "*.txt export-ignore\n", Encoding.UTF8);
        await Assert.ThrowsAsync<InvalidDataException>(() => new GitClient().OpenArchiveAsync(repository.RootPath, head));
    }

    [Fact]
    public void MeasurementFingerprintCoversSourceAndDependencyInputsWithoutProducerVersion()
    {
        string root = FindRepositoryRoot();
        string[] prefixes = ["EffortHours.Analysis", "EffortHours.Analyzers.", "EffortHours.Contracts", "EffortHours.Core", "EffortHours.Estimation", "EffortHours.Change"];
        List<string> files = [];
        foreach (string project in Directory.EnumerateDirectories(Path.Combine(root, "src")))
        {
            if (!prefixes.Any(prefix => Path.GetFileName(project).StartsWith(prefix, StringComparison.Ordinal))) continue;
            files.AddRange(Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
                .Where(p => !Path.GetRelativePath(project, p).Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj")));
            files.AddRange(Directory.EnumerateFiles(project, "*.csproj"));
            files.Add(Path.Combine(project, "packages.lock.json"));
        }
        files.AddRange(MeasurementBuildInputs.Select(p => Path.Combine(root, p)));
        string[] hashes = [.. files.Select(p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))).Order(StringComparer.Ordinal)];
        Assert.Equal(SnapshotMeasurementIdentity.Hash(Encoding.UTF8.GetBytes(string.Join('\n', hashes))), SnapshotMeasurementIdentity.ImplementationDigest);
    }
}
