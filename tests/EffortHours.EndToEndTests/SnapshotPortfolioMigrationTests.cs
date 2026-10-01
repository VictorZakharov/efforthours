using System.Text;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Fact]
    public async Task SnapshotCheckpointSerializesIdenticalConcurrentEntries()
    {
        using GitFixture execution = await GitFixture.CreateAsync();
        using SnapshotPortfolioStore store = new(Path.Combine(execution.RootPath, "checkpoint"));
        string receiptId = "sha256:" + new string('a', 64);
        await Task.WhenAll(Enumerable.Range(0, 64).Select(async _ =>
        {
            await store.SaveAsync("measurements", "same-input", new SnapshotReceiptReference(receiptId), CancellationToken.None);
            SnapshotReceiptReference? loaded = await store.LoadAsync<SnapshotReceiptReference>("measurements", "same-input", CancellationToken.None);
            Assert.Equal(receiptId, loaded?.ReceiptId);
        }));
        Assert.Equal(0, store.Invalidations);
    }

    [Fact]
    public async Task SnapshotPortfolioRejectsPublicationThroughDirectoryAlias()
    {
        if (OperatingSystem.IsWindows()) return; // Directory symlink creation requires host privileges on Windows.
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        string alias = Path.Combine(execution.RootPath, "source-alias");
        string parentAlias = Path.Combine(execution.RootPath, "parent-alias");
        Directory.CreateSymbolicLink(parentAlias, Path.GetDirectoryName(repository.RootPath)!);
        Directory.CreateSymbolicLink(alias, Path.Combine(parentAlias, Path.GetFileName(repository.RootPath)));
        string output = Path.Combine(alias, "result.json");
        Assert.True(SnapshotPortfolioPaths.IsWithin(repository.RootPath, output));
        ProcessResult rejected = await SnapshotRunAsync(manifest, local, checkpoint, "--output", output);
        Assert.NotEqual(0, rejected.ExitCode);
        Assert.False(File.Exists(Path.Combine(repository.RootPath, "result.json")));
        Assert.False(File.Exists(Path.Combine(repository.RootPath, "result.json.failure.json")));
    }

    [Fact]
    public async Task SnapshotPortfolioConcurrentProjectsMatchSerialAndCancelledWritesPreservePublication()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        SnapshotPortfolioManifest input = ContractJson.Deserialize<SnapshotPortfolioManifest>(await File.ReadAllTextAsync(manifest, Encoding.UTF8));
        input = input with { Projects = [input.Projects[0], input.Projects[0] with { Id = "second" }] };
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(input), Encoding.UTF8);
        await File.WriteAllTextAsync(local, ContractJson.SerializeDocument(new SnapshotPortfolioLocalMap
        {
            Projects = [new() { Id = "demo", RepositoryPath = repository.RootPath }, new() { Id = "second", RepositoryPath = repository.RootPath }],
        }), Encoding.UTF8);
        ProcessResult serial = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(serial.ExitCode == 0, serial.StandardError);
        ProcessResult concurrent = await SnapshotRunAsync(manifest, local, Path.Combine(execution.RootPath, "parallel"), "--concurrency", "2");
        Assert.True(concurrent.ExitCode == 0, concurrent.StandardError);
        Assert.Equal(ContractJson.Deserialize<SnapshotPortfolioReport>(serial.StandardOutput).SemanticDigest,
            ContractJson.Deserialize<SnapshotPortfolioReport>(concurrent.StandardOutput).SemanticDigest);
        string output = Path.Combine(execution.RootPath, "complete.json");
        await File.WriteAllTextAsync(output, serial.StandardOutput, Encoding.UTF8);
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SnapshotPortfolioStore.AtomicWriteAsync(output, "partial", cancelled.Token));
        Assert.Equal(serial.StandardOutput, await File.ReadAllTextAsync(output, Encoding.UTF8));
        Assert.Empty(Directory.EnumerateFiles(execution.RootPath, "*.tmp"));
    }

    [Theory]
    [InlineData("rebuild")]
    [InlineData("new-epoch")]
    public async Task SnapshotPortfolioRequiresExplicitMigrationAndArchivesOriginalEpoch(string migration)
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        ProcessResult first = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(first.ExitCode == 0, first.StandardError);
        SnapshotPortfolioReport original = ContractJson.Deserialize<SnapshotPortfolioReport>(first.StandardOutput);
        MeasurementIdentity obsolete = original.Receipts[0].Measurement with { ImplementationDigest = SnapshotMeasurementIdentity.Hash("old implementation"u8) };
        Dictionary<string, SnapshotMeasurementReceipt> changed = original.Receipts.ToDictionary(r => r.Id, r =>
        {
            SnapshotMeasurementReceipt receipt = r with { Measurement = obsolete, ProducerVersion = "historical-producer", Id = "" };
            return receipt with { Id = SnapshotPortfolioValidation.ReceiptId(receipt) };
        });
        SnapshotPortfolioReport historical = original with
        {
            MeasurementEpoch = SnapshotMeasurementIdentity.Digest(obsolete),
            Receipts = [.. changed.Values.OrderBy(r => r.Id, StringComparer.Ordinal)],
            Projects = [.. original.Projects.Select(p => p with
            {
                Periods = [.. p.Periods.Select(period => period.WholeReceiptId is null ? period : period with
                {
                    WholeReceiptId = changed[period.WholeReceiptId].Id,
                    Areas = [.. period.Areas.Select(a => a with { ReceiptId = changed[a.ReceiptId].Id })],
                })],
            })],
        };
        historical = historical with { SemanticDigest = SnapshotPortfolioValidation.ReportDigest(historical) };
        SnapshotPortfolioValidation.Validate(historical);
        string output = Path.Combine(execution.RootPath, "published.json");
        string saved = ContractJson.SerializeDocument(historical);
        await File.WriteAllTextAsync(output, saved, Encoding.UTF8);
        ProcessResult blocked = await SnapshotRunAsync(manifest, local, checkpoint, "--output", output);
        Assert.NotEqual(0, blocked.ExitCode);
        Assert.Equal(saved, await File.ReadAllTextAsync(output, Encoding.UTF8));
        ProcessResult upgraded = await SnapshotRunAsync(manifest, local, checkpoint, "--output", output, "--upgrade", migration);
        Assert.True(upgraded.ExitCode == 0, upgraded.StandardError);
        SnapshotPortfolioReport current = ContractJson.Deserialize<SnapshotPortfolioReport>(await File.ReadAllTextAsync(output, Encoding.UTF8));
        Assert.Equal(original.MeasurementEpoch, current.MeasurementEpoch);
        Assert.Equal(historical.SemanticDigest, current.PreviousEpochDigest);
        Assert.Equal(0, current.Telemetry.EstimatorCalls);
        Assert.Equal(saved, await File.ReadAllTextAsync(output + ".epoch-" + historical.SemanticDigest[7..] + ".json", Encoding.UTF8));
        Assert.All(current.Receipts, r => Assert.Equal(original.Receipts[0].ProducerVersion, r.ProducerVersion));
    }

    [Fact]
    public async Task SnapshotPortfolioPreflightExplainsChangedAreasAndNeverWritesIntoSource()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        ProcessResult first = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(first.ExitCode == 0, first.StandardError);
        string previous = Path.Combine(execution.RootPath, "previous.json");
        await File.WriteAllTextAsync(previous, first.StandardOutput, Encoding.UTF8);
        repository.WriteText("src/Main.cs", "public class Main { public int Add(int a, int b) => a + b + 2; }\n");
        _ = await SnapshotCommitAtAsync(repository, "changed source", "2026-02-10T12:00:00Z");
        string[] files = [.. Directory.EnumerateFiles(checkpoint, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];
        ProcessResult plan = await SnapshotRunAsync(manifest, local, checkpoint, "--preflight", "--previous-result", previous);
        Assert.True(plan.ExitCode == 0, plan.StandardError);
        SnapshotPortfolioReport report = ContractJson.Deserialize<SnapshotPortfolioReport>(plan.StandardOutput);
        Assert.Equal(0, report.Telemetry.Exports);
        Assert.Equal(0, report.Telemetry.EstimatorCalls);
        Assert.Contains(report.Projects[0].Periods[2].AreaPlans, a => a.Id == "source" && a.Disposition == "changed-or-unmeasured-input");
        Assert.Contains(report.Projects[0].Periods[2].AreaPlans, a => a.Id == "support" && a.Disposition == "unchanged-input-receipt-expected");
        Assert.Equal(files, Directory.EnumerateFiles(checkpoint, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        string forbidden = Path.Combine(repository.RootPath, "result.json");
        ProcessResult unsafeOutput = await SnapshotRunAsync(manifest, local, checkpoint, "--output", forbidden);
        Assert.NotEqual(0, unsafeOutput.ExitCode);
        Assert.False(File.Exists(forbidden));
        Assert.False(File.Exists(forbidden + ".failure.json"));
    }
}
