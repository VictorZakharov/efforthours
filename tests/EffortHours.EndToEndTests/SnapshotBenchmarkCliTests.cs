using System.Globalization;
using System.Text;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class SnapshotDailyCliTests : ChangeCliTestSupport
{
    [Fact]
    public async Task DailyBenchmarkBindsSelectedAncestryAndYearAcrossPreflightColdWarmResumeAndReproduction()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("App.csproj", ProjectFile);
        repository.WriteText("Main.cs", "public class MainLine { public bool Ready => true; }\n");
        _ = await SnapshotCommitAtAsync(repository, "prior year", "2025-12-30T12:00:00Z");
        repository.WriteText("Main.cs", "public class MainLine { public bool Ready => true; public int Add(int a, int b) => a + b; }\n");
        string main = await SnapshotCommitAtAsync(repository, "new year", "2026-01-01T12:00:00Z");
        await repository.GitAsync("checkout", "-b", "feature");
        repository.WriteText("Branch.cs", "public class Branch { public string Parse(string text) => text.Trim(); }\n");
        _ = await SnapshotCommitAtAsync(repository, "branch", "2026-01-02T12:00:00Z");
        await repository.GitAsync("checkout", "main");
        await repository.GitAsync("merge", "--no-ff", "--no-commit", "feature");
        string merge = await SnapshotCommitAtAsync(repository, "merge", "2026-01-04T12:00:00Z");
        string refs = await repository.GitAsync("show-ref");
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifestPath, string localPath, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        SnapshotPortfolioManifest manifest = ContractJson.Deserialize<SnapshotPortfolioManifest>(await File.ReadAllTextAsync(manifestPath, Encoding.UTF8));
        manifest = manifest with { Projects = [manifest.Projects[0] with { Areas = [new() { Id = "all", Include = ["**"] }] }] };
        await File.WriteAllTextAsync(manifestPath, ContractJson.SerializeDocument(manifest), Encoding.UTF8);
        const string observation = "2026-01-05T12:00:00Z";
        ProcessResult plan = await SnapshotBenchmarkRunAsync(manifestPath, localPath, checkpoint, observation, "--calendar", "daily", "--preflight");
        Assert.True(plan.ExitCode == 0, plan.StandardError);
        SnapshotPortfolioReport planned = ContractJson.Deserialize<SnapshotPortfolioReport>(plan.StandardOutput);
        Assert.Equal([0, 1, 1, 1, 3, 3], planned.Projects[0].Periods.Take(6).Select(p => p.ActiveCommitDateCount));
        Assert.Equal([0, 8, 8, 8, 24, 24], planned.Projects[0].Periods.Take(6).Select(p => p.BenchmarkHours));
        Assert.Equal([main, main, main, merge, merge], planned.Projects[0].Periods.Skip(1).Take(5).Select(p => p.CommitObjectId));
        Assert.All(planned.Projects[0].Periods.Skip(6), p => Assert.Null(p.BenchmarkHours));
        Assert.Equal(0, planned.Telemetry.Exports);
        Assert.Equal(0, planned.Telemetry.EstimatorCalls);
        Assert.False(Directory.Exists(checkpoint));

        string output = Path.Combine(execution.RootPath, "daily.json");
        ProcessResult cold = await SnapshotBenchmarkRunAsync(manifestPath, localPath, checkpoint, observation, "--calendar", "daily", "--output", output);
        Assert.True(cold.ExitCode == 0, cold.StandardError);
        string json = await File.ReadAllTextAsync(output, Encoding.UTF8);
        Assert.True(ContractSchemaValidator.Validate("snapshot-portfolio-report.schema.json", json).IsValid);
        SnapshotPortfolioReport measured = ContractJson.Deserialize<SnapshotPortfolioReport>(json);
        AssertBenchmarkOperands(planned, measured);
        SnapshotPortfolioValidation.Validate(measured);
        ProcessResult warm = await SnapshotBenchmarkRunAsync(manifestPath, localPath, checkpoint, observation, "--calendar", "daily");
        Assert.True(warm.ExitCode == 0, warm.StandardError);
        SnapshotPortfolioReport reused = ContractJson.Deserialize<SnapshotPortfolioReport>(warm.StandardOutput);
        AssertBenchmarkOperands(measured, reused);
        Assert.Equal(measured.SemanticDigest, reused.SemanticDigest);
        Assert.Equal(0, reused.Telemetry.Exports);
        Assert.Equal(0, reused.Telemetry.EstimatorCalls);

        ProcessResult monthly = await SnapshotBenchmarkRunAsync(manifestPath, localPath, Path.Combine(execution.RootPath, "monthly"), observation);
        Assert.True(monthly.ExitCode == 0, monthly.StandardError);
        SnapshotPortfolioReport reference = ContractJson.Deserialize<SnapshotPortfolioReport>(monthly.StandardOutput);
        SnapshotDailyCalendar.ValidateReference(measured, reference);
        Assert.Equal(reference.Projects[0].Periods[1].Hours, measured.Projects[0].MonthlyEndpoints![1].Hours);
        Assert.Equal(24, measured.Projects[0].MonthlyEndpoints![1].BenchmarkHours);

        string resumedCheckpoint = Path.Combine(execution.RootPath, "resume");
        SnapshotPortfolioLocalMap local = ContractJson.Deserialize<SnapshotPortfolioLocalMap>(await File.ReadAllTextAsync(localPath, Encoding.UTF8));
        using (SnapshotPortfolioStore store = new(resumedCheckpoint))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SnapshotPortfolioRunner(store, new CancelSecondSnapshot()).RunAsync(
                manifest with { CalendarPolicy = SnapshotPortfolioVersions.Daily }, local, new()
                { AsOf = DateTimeOffset.Parse(observation, CultureInfo.InvariantCulture), ProducerVersion = measured.Receipts[0].ProducerVersion }, CancellationToken.None));
        ProcessResult resumed = await SnapshotBenchmarkRunAsync(manifestPath, localPath, resumedCheckpoint, observation, "--calendar", "daily");
        Assert.True(resumed.ExitCode == 0, resumed.StandardError);
        SnapshotPortfolioReport completed = ContractJson.Deserialize<SnapshotPortfolioReport>(resumed.StandardOutput);
        AssertBenchmarkOperands(measured, completed);
        Assert.Equal(measured.SemanticDigest, completed.SemanticDigest);
        Assert.Equal(1, completed.Telemetry.EstimatorCalls);
        Assert.Equal(refs, await repository.GitAsync("show-ref"));
        Assert.Equal("", await repository.GitAsync("status", "--porcelain"));
        Assert.False(File.Exists(Path.Combine(repository.RootPath, ".git", "FETCH_HEAD")));

        _ = await SnapshotCommitAtAsync(repository, "later head", "2026-01-06T12:00:00Z", allowEmpty: true);
        string movedHead = await repository.GitAsync("rev-parse", "HEAD");
        ProcessResult reproduction = await SnapshotBenchmarkRunAsync(manifestPath, localPath, checkpoint, observation,
            "--calendar", "daily", "--reproduce", output);
        Assert.True(reproduction.ExitCode == 0, reproduction.StandardError);
        SnapshotPortfolioReport reproduced = ContractJson.Deserialize<SnapshotPortfolioReport>(reproduction.StandardOutput);
        AssertBenchmarkOperands(measured, reproduced);
        Assert.Equal(measured.SemanticDigest, reproduced.SemanticDigest);
        Assert.Equal(0, reproduced.Telemetry.EstimatorCalls);
        Assert.Equal(0, reproduced.Telemetry.Exports);
        Assert.Equal(movedHead, await repository.GitAsync("rev-parse", "HEAD"));
    }

    [Fact]
    public async Task DailyMonthEndBenchmarkExcludesBranchDatesUntilTheirMergeIsSelected()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        string main = await repository.GitAsync("rev-parse", "HEAD");
        await repository.GitAsync("checkout", "-b", "feature");
        repository.WriteText("Branch.cs", "public class Branch { public bool Ready => true; }\n");
        _ = await SnapshotCommitAtAsync(repository, "January branch", "2026-01-31T12:00:00Z");
        await repository.GitAsync("checkout", "main");
        await repository.GitAsync("merge", "--no-ff", "--no-commit", "feature");
        string merge = await SnapshotCommitAtAsync(repository, "February merge", "2026-02-02T12:00:00Z");
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        ProcessResult plan = await SnapshotBenchmarkRunAsync(manifest, local, checkpoint, "2026-02-03T12:00:00Z", "--calendar", "daily", "--preflight");
        Assert.True(plan.ExitCode == 0, plan.StandardError);
        SnapshotPortfolioReport report = ContractJson.Deserialize<SnapshotPortfolioReport>(plan.StandardOutput);
        SnapshotPeriodResult january = report.Projects[0].Periods.Single(p => p.Id == "2026-01-31");
        SnapshotPeriodResult merged = report.Projects[0].Periods.Single(p => p.Id == "2026-02-02");
        Assert.Equal(main, january.CommitObjectId);
        Assert.Equal(1, january.ActiveCommitDateCount);
        Assert.Equal(8, january.BenchmarkHours);
        Assert.Equal(main, report.Projects[0].MonthlyEndpoints![1].CommitObjectId);
        Assert.Equal(8, report.Projects[0].MonthlyEndpoints![1].BenchmarkHours);
        Assert.Equal(merge, merged.CommitObjectId);
        Assert.Equal(3, merged.ActiveCommitDateCount);
        Assert.Equal(24, merged.BenchmarkHours);
        Assert.False(Directory.Exists(checkpoint));
    }

    [Fact]
    public async Task DailyUnavailableSnapshotDoesNotRetainBenchmarkCapacity()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        string tree = await repository.GitAsync("rev-parse", "HEAD^{tree}");
        string objectPath = Path.Combine(repository.RootPath, ".git", "objects", tree[..2], tree[2..]);
        File.SetAttributes(objectPath, FileAttributes.Normal);
        File.Delete(objectPath);
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        ProcessResult plan = await SnapshotBenchmarkRunAsync(manifest, local, checkpoint, "2026-01-16T12:00:00Z", "--calendar", "daily", "--preflight");
        Assert.True(plan.ExitCode == 0, plan.StandardError);
        SnapshotPortfolioReport report = ContractJson.Deserialize<SnapshotPortfolioReport>(plan.StandardOutput);
        Assert.Contains(report.Projects[0].Periods, p => p.Status == "unavailable" && p.CommitObjectId is not null);
        Assert.All(report.Projects[0].Periods.Where(p => p.Status == "unavailable"), p =>
        {
            Assert.Null(p.ActiveCommitDateCount);
            Assert.Null(p.BenchmarkHours);
        });
        Assert.False(Directory.Exists(checkpoint));
    }

    private static Task<ProcessResult> SnapshotBenchmarkRunAsync(string manifest, string local, string checkpoint, string asOf, params string[] options) =>
        RunCliAsync(["estimate", "portfolio", "--manifest", manifest, "--local", local, "--checkpoint", checkpoint,
            "--as-of", asOf, "--no-rate", .. options]);

    private static void AssertBenchmarkOperands(SnapshotPortfolioReport expected, SnapshotPortfolioReport actual) =>
        Assert.Equal(expected.Projects[0].Periods.Select(p => (p.CommitObjectId, p.Cutoff, p.ActiveCommitDateCount, p.BenchmarkHours)),
            actual.Projects[0].Periods.Select(p => (p.CommitObjectId, p.Cutoff, p.ActiveCommitDateCount, p.BenchmarkHours)));
}
