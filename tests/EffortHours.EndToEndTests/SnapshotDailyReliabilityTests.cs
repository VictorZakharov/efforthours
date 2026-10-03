using System.Text;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.EndToEndTests;

public sealed partial class SnapshotDailyCliTests : ChangeCliTestSupport
{
    [Fact]
    public async Task DailyCancellationRetainsCompletedMeasurementsAndResumeFinishesRemainingWork()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        repository.WriteText("Feature.cs", "public class Feature { public bool Ready => true; }\n");
        _ = await SnapshotCommitAtAsync(repository, "second stock", "2026-02-15T12:00:00Z");
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifestPath, string localPath, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        SnapshotPortfolioManifest manifest = ContractJson.Deserialize<SnapshotPortfolioManifest>(await File.ReadAllTextAsync(manifestPath, Encoding.UTF8))
            with
        { CalendarPolicy = SnapshotPortfolioVersions.Daily };
        SnapshotPortfolioLocalMap local = ContractJson.Deserialize<SnapshotPortfolioLocalMap>(await File.ReadAllTextAsync(localPath, Encoding.UTF8));
        using (SnapshotPortfolioStore store = new(checkpoint))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SnapshotPortfolioRunner(store, new CancelSecondSnapshot())
                .RunAsync(manifest, local, new()
                {
                    AsOf = DateTimeOffset.Parse("2026-04-15T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                    ProducerVersion = "synthetic-cancellation-producer"
                }, CancellationToken.None));
        }
        ProcessResult resumed = await SnapshotRunAsync(manifestPath, localPath, checkpoint, "--calendar", "daily");
        Assert.True(resumed.ExitCode == 0, resumed.StandardError);
        SnapshotPortfolioReport report = ContractJson.Deserialize<SnapshotPortfolioReport>(resumed.StandardOutput);
        SnapshotPortfolioValidation.Validate(report);
        Assert.Equal(1, report.Telemetry.EstimatorCalls);
        Assert.Equal(1, report.Telemetry.Exports);
        Assert.Contains(report.Receipts, r => r.ProducerVersion == "synthetic-cancellation-producer");
        ProcessResult warm = await SnapshotRunAsync(manifestPath, localPath, checkpoint, "--calendar", "daily");
        Assert.True(warm.ExitCode == 0, warm.StandardError);
        SnapshotPortfolioReport reused = ContractJson.Deserialize<SnapshotPortfolioReport>(warm.StandardOutput);
        Assert.Equal(report.SemanticDigest, reused.SemanticDigest);
        Assert.Equal(0, reused.Telemetry.EstimatorCalls);
        Assert.Equal(0, reused.Telemetry.Exports);
    }

    [Fact]
    public async Task DailyIncompatibleImportAndMonthlyAdapterFailExplicitlyPreservingOutput()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        SnapshotPortfolioManifest original = ContractJson.Deserialize<SnapshotPortfolioManifest>(await File.ReadAllTextAsync(manifest, Encoding.UTF8));
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(original with { Profile = EstimationProfile.Recreation }), Encoding.UTF8);
        string monthly = Path.Combine(execution.RootPath, "recreation.json");
        ProcessResult measured = await SnapshotRunAsync(manifest, local, checkpoint, "--output", monthly);
        Assert.True(measured.ExitCode == 0, measured.StandardError);
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(original), Encoding.UTF8);
        string dailyCheckpoint = Path.Combine(execution.RootPath, "daily-checkpoint");
        ProcessResult incompatible = await SnapshotRunAsync(manifest, local, dailyCheckpoint, "--calendar", "daily", "--import-receipts", monthly);
        Assert.NotEqual(0, incompatible.ExitCode);
        Assert.Contains("incompatible-measurement-identity", incompatible.StandardOutput, StringComparison.Ordinal);
        ProcessResult plan = await SnapshotRunAsync(manifest, local, Path.Combine(execution.RootPath, "plan-checkpoint"),
            "--calendar", "daily", "--import-receipts", monthly, "--preflight");
        Assert.NotEqual(0, plan.ExitCode);
        Assert.Contains("incompatible-measurement-identity", plan.StandardOutput, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(execution.RootPath, "plan-checkpoint")));
        string output = Path.Combine(execution.RootPath, "daily.json");
        ProcessResult daily = await SnapshotRunAsync(manifest, local, dailyCheckpoint, "--calendar", "daily", "--output", output);
        Assert.True(daily.ExitCode == 0, daily.StandardError);
        string saved = await File.ReadAllTextAsync(output, Encoding.UTF8);
        ProcessResult failed = await SnapshotRunAsync(manifest, local, dailyCheckpoint, "--calendar", "daily",
            "--import-receipts", monthly, "--output", output);
        Assert.NotEqual(0, failed.ExitCode);
        Assert.Equal(saved, await File.ReadAllTextAsync(output, Encoding.UTF8));
        string asset = Path.Combine(execution.RootPath, "asset.json");
        await File.WriteAllTextAsync(asset, "preserved-publication\n", Encoding.UTF8);
        ProcessResult adapter = await RunCliAsync("portfolio-adapter", "--input", output, "--studies", "not-read.json", "--output", asset);
        Assert.Equal(3, adapter.ExitCode);
        Assert.Contains("requires a monthly portfolio", adapter.StandardError, StringComparison.Ordinal);
        Assert.Equal("preserved-publication\n", await File.ReadAllTextAsync(asset, Encoding.UTF8));
    }

    private sealed class CancelSecondSnapshot : IEstimator
    {
        private int _calls;
        public EstimateReport Estimate(RepositoryEvidence evidence, EstimationProfile profile, RateCard? rateCard = null)
        {
            if (++_calls == 2) throw new OperationCanceledException("Synthetic cancellation after a durable first receipt.");
            return new SeedEstimator().Estimate(evidence, profile, rateCard);
        }
    }
}
