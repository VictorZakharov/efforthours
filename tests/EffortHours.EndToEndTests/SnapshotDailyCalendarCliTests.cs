using System.Text;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class SnapshotDailyCliTests : ChangeCliTestSupport
{
    [Fact]
    public async Task DailyImportsMonthlyReceiptsReconcilesAndReproducesAfterHeadMoves()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        repository.WriteText("src/Feature.cs", "public class Feature { public int Parse(string value) => int.Parse(value); }\n");
        _ = await SnapshotCommitAtAsync(repository, "weekend feature", "2026-02-15T12:00:00Z");
        await repository.GitAsync("rm", "src/Feature.cs");
        _ = await SnapshotCommitAtAsync(repository, "remove feature", "2026-03-14T12:00:00Z");
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        string monthlyPath = Path.Combine(execution.RootPath, "monthly.json");
        ProcessResult monthlyRun = await SnapshotRunAsync(manifest, local, checkpoint, "--output", monthlyPath);
        Assert.True(monthlyRun.ExitCode == 0, monthlyRun.StandardError);
        SnapshotPortfolioReport monthly = ContractJson.Deserialize<SnapshotPortfolioReport>(await File.ReadAllTextAsync(monthlyPath, Encoding.UTF8));
        string dailyCheckpoint = Path.Combine(execution.RootPath, "daily-checkpoint");
        string output = Path.Combine(execution.RootPath, "daily.json");
        ProcessResult plan = await SnapshotRunAsync(manifest, local, dailyCheckpoint, "--calendar", "daily",
            "--import-receipts", monthlyPath, "--reproduce", monthlyPath, "--preflight");
        Assert.True(plan.ExitCode == 0, plan.StandardError);
        Assert.False(Directory.Exists(dailyCheckpoint));
        SnapshotPortfolioReport preview = ContractJson.Deserialize<SnapshotPortfolioReport>(plan.StandardOutput);
        Assert.DoesNotContain(preview.Projects[0].Periods, p => p.CacheDisposition == "measurement-required");
        Assert.Equal(0, preview.Telemetry.EstimatorCalls);
        Assert.Equal(0, preview.Telemetry.Exports);
        ProcessResult dailyRun = await SnapshotRunAsync(manifest, local, dailyCheckpoint, "--calendar", "daily",
            "--import-receipts", monthlyPath, "--reproduce", monthlyPath, "--output", output);
        Assert.True(dailyRun.ExitCode == 0, dailyRun.StandardError);
        string json = await File.ReadAllTextAsync(output, Encoding.UTF8);
        SnapshotPortfolioReport daily = ContractJson.Deserialize<SnapshotPortfolioReport>(json);
        SnapshotPortfolioValidation.Validate(daily);
        SnapshotPortfolioReport tampered = daily with
        {
            Projects = [daily.Projects[0] with
        {
            Periods = [.. daily.Projects[0].Periods.Select((p, i) => i == 20 ? p with { ExpectedChangeCentihours = p.ExpectedChangeCentihours + 1 } : p)],
        }]
        };
        tampered = tampered with { SemanticDigest = SnapshotPortfolioValidation.ReportDigest(tampered) };
        Assert.Throws<InvalidDataException>(() => SnapshotPortfolioValidation.Validate(tampered));
        SnapshotPortfolioReport wrongMonthly = monthly with
        {
            Projects = [monthly.Projects[0] with
        {
            Periods = [.. monthly.Projects[0].Periods.Select((p, i) => i == 1 ? p with { WholeReceiptId = "different" } : p)],
        }]
        };
        Assert.Throws<InvalidDataException>(() => SnapshotDailyCalendar.ValidateReference(daily, wrongMonthly));
        Assert.True(ContractSchemaValidator.Validate("snapshot-portfolio-report.schema.json", json).IsValid);
        Assert.Equal(0, daily.Telemetry.EstimatorCalls);
        Assert.Equal(0, daily.Telemetry.Exports);
        Assert.Equal(SnapshotPortfolioVersions.Daily, daily.CalendarPolicy);
        Assert.Equal(366, daily.Projects[0].Periods.Count);
        Assert.Equal(3, daily.Projects[0].DistinctSnapshotCount);
        Assert.All(daily.Projects[0].Periods, p => Assert.Empty(p.Areas));
        Assert.Contains(daily.Projects[0].Periods, p => p.ExpectedChangeCentihours < 0);
        Assert.Equal(3, daily.Projects[0].Periods.Single(p => p.Id == "2026-04-15").ActiveCommitDateCount);
        for (int month = 0; month <= 12; month++)
        {
            SnapshotPeriodResult endpoint = daily.Projects[0].MonthlyEndpoints![month];
            Assert.Equal(monthly.Projects[0].Periods[month].CommitObjectId, endpoint.CommitObjectId);
            Assert.Equal(monthly.Projects[0].Periods[month].WholeReceiptId, endpoint.WholeReceiptId);
            Assert.Equal(monthly.Projects[0].Periods[month].Hours, endpoint.Hours);
        }
        Assert.Equal(monthly.Receipts.Where(r => daily.Receipts.Any(d => d.Id == r.Id)).Select(ContractJson.Serialize),
            daily.Receipts.Select(ContractJson.Serialize));
        Assert.DoesNotContain(repository.RootPath, json, StringComparison.Ordinal);
        Assert.DoesNotContain("source-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("weekend feature", json, StringComparison.Ordinal);
        repository.WriteText("src/Later.cs", "public class Later { public bool Active => true; }\n");
        _ = await SnapshotCommitAtAsync(repository, "newer work", "2026-04-16T12:00:00Z");
        ProcessResult reproduced = await SnapshotRunAsync(manifest, local, dailyCheckpoint, "--calendar", "daily",
            "--reproduce", output);
        Assert.True(reproduced.ExitCode == 0, reproduced.StandardError);
        SnapshotPortfolioReport warm = ContractJson.Deserialize<SnapshotPortfolioReport>(reproduced.StandardOutput);
        Assert.Equal(daily.SemanticDigest, warm.SemanticDigest);
        Assert.Equal(0, warm.Telemetry.EstimatorCalls);
        Assert.Equal(0, warm.Telemetry.Exports);
        Assert.Equal(daily.Projects[0].HeadObjectId, warm.Projects[0].HeadObjectId);
        ProcessResult priced = await RunCliAsync("estimate", "portfolio", "--manifest", manifest, "--local", local,
            "--checkpoint", dailyCheckpoint, "--calendar", "daily", "--reproduce", output, "--hourly-rate", "80");
        Assert.True(priced.ExitCode == 0, priced.StandardError);
        SnapshotPortfolioReport money = ContractJson.Deserialize<SnapshotPortfolioReport>(priced.StandardOutput);
        Assert.Equal(0, money.Telemetry.EstimatorCalls);
        Assert.Equal(0, money.Telemetry.Exports);
        Assert.Equal(daily.Receipts.Select(r => r.Id), money.Receipts.Select(r => r.Id));
        Assert.Equal(daily.Projects[0].Periods.Select(p => p.ExpectedChangeCentihours),
            money.Projects[0].Periods.Select(p => p.ExpectedChangeCentihours));
        Assert.Equal("", await repository.GitAsync("status", "--porcelain"));
        Assert.False(File.Exists(Path.Combine(repository.RootPath, ".git", "FETCH_HEAD")));
    }

    [Fact]
    public async Task DailyPreflightSkipsAreasAndFailureRetainsSuccessfulReceiptsForResume()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        SnapshotPortfolioManifest definition = ContractJson.Deserialize<SnapshotPortfolioManifest>(await File.ReadAllTextAsync(manifest, Encoding.UTF8));
        // An area that never existed cannot alter whole-only daily analysis context.
        definition = definition with
        {
            Projects = [definition.Projects[0] with
        {
            Areas = [new() { Id = "absent", Include = ["absent/**"] }, new() { Id = "rest", Include = ["**"] }],
        }]
        };
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(definition), Encoding.UTF8);
        ProcessResult plan = await SnapshotRunAsync(manifest, local, checkpoint, "--calendar", "daily", "--preflight");
        Assert.True(plan.ExitCode == 0, plan.StandardError);
        SnapshotPortfolioReport planned = ContractJson.Deserialize<SnapshotPortfolioReport>(plan.StandardOutput);
        Assert.Equal(1, planned.Projects[0].DistinctSnapshotCount);
        Assert.Equal(0, planned.Telemetry.Exports);
        Assert.Equal(0, planned.Telemetry.EstimatorCalls);
        Assert.Equal(0, planned.Telemetry.SelectorCompilations);
        Assert.False(Directory.Exists(checkpoint));
        definition = definition with { Projects = [definition.Projects[0], definition.Projects[0] with { Id = "second", Ref = new string('a', 40) }] };
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(definition), Encoding.UTF8);
        await File.WriteAllTextAsync(local, ContractJson.SerializeDocument(new SnapshotPortfolioLocalMap
        {
            Projects = [new() { Id = "demo", RepositoryPath = repository.RootPath }, new() { Id = "second", RepositoryPath = repository.RootPath }],
        }), Encoding.UTF8);
        string output = Path.Combine(execution.RootPath, "daily.json");
        ProcessResult failed = await SnapshotRunAsync(manifest, local, checkpoint, "--calendar", "daily", "--output", output);
        Assert.NotEqual(0, failed.ExitCode);
        Assert.False(File.Exists(output));
        Assert.True(File.Exists(output + ".failure.json"));
        definition = definition with { Projects = [definition.Projects[0], definition.Projects[1] with { Ref = "main" }] };
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(definition), Encoding.UTF8);
        ProcessResult resumed = await SnapshotRunAsync(manifest, local, checkpoint, "--calendar", "daily", "--output", output);
        Assert.True(resumed.ExitCode == 0, resumed.StandardError);
        SnapshotPortfolioReport report = ContractJson.Deserialize<SnapshotPortfolioReport>(await File.ReadAllTextAsync(output, Encoding.UTF8));
        Assert.Equal(0, report.Telemetry.EstimatorCalls);
        Assert.Equal(0, report.Telemetry.Exports);
        SnapshotPortfolioValidation.Validate(report);
    }

    [Fact]
    public async Task DailyBenchmarkIncludesMergedBranchDatesWhileSelectionRemainsFirstParent()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        string initial = await repository.GitAsync("rev-parse", "HEAD");
        await repository.GitAsync("checkout", "-b", "feature");
        repository.WriteText("Branch.cs", "public class Branch { public bool Ready => true; }\n");
        _ = await SnapshotCommitAtAsync(repository, "branch weekend", "2026-02-07T12:00:00Z");
        _ = await SnapshotCommitAtAsync(repository, "duplicate active date", "2026-02-07T15:00:00Z", allowEmpty: true);
        await repository.GitAsync("checkout", "main");
        repository.WriteText("Main.cs", "public class MainLine { public bool Ready => true; }\n");
        _ = await SnapshotCommitAtAsync(repository, "mainline work", "2026-02-10T12:00:00Z");
        await repository.GitAsync("merge", "--no-ff", "--no-commit", "feature");
        string merge = await SnapshotCommitAtAsync(repository, "merge branch", "2026-02-15T12:00:00Z");
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        ProcessResult result = await SnapshotRunAsync(manifest, local, checkpoint, "--calendar", "daily");
        Assert.True(result.ExitCode == 0, result.StandardError);
        SnapshotPortfolioReport report = ContractJson.Deserialize<SnapshotPortfolioReport>(result.StandardOutput);
        SnapshotPeriodResult weekend = report.Projects[0].Periods.Single(p => p.Id == "2026-02-07");
        Assert.Equal(initial, weekend.CommitObjectId);
        Assert.Equal(0, weekend.ExpectedChangeCentihours);
        Assert.Equal(1, weekend.ActiveCommitDateCount);
        SnapshotPeriodResult merged = report.Projects[0].Periods.Single(p => p.Id == "2026-02-15");
        Assert.Equal(merge, merged.CommitObjectId);
        Assert.Equal(4, merged.ActiveCommitDateCount);
        Assert.Equal(32, merged.BenchmarkHours);
    }

    [Fact]
    public async Task DailyShallowHistoryAndMissingHeadsStayUnavailableWithoutPublishing()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        SnapshotPortfolioManifest original = ContractJson.Deserialize<SnapshotPortfolioManifest>(await File.ReadAllTextAsync(manifest, Encoding.UTF8));
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(original with
        {
            Projects = [original.Projects[0] with { Ref = "missing-ref" }],
        }), Encoding.UTF8);
        ProcessResult missing = await SnapshotRunAsync(manifest, local, checkpoint, "--calendar", "daily", "--preflight");
        Assert.True(missing.ExitCode == 0, missing.StandardError);
        SnapshotPortfolioReport missingPlan = ContractJson.Deserialize<SnapshotPortfolioReport>(missing.StandardOutput);
        Assert.Equal("unavailable", missingPlan.Projects[0].Periods[1].Status);
        Assert.Null(missingPlan.Projects[0].Periods[1].Hours);
        Assert.Equal("missing-object-or-ref", missingPlan.Projects[0].PlanningIssue);
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(original), Encoding.UTF8);
        string shallow = Path.Combine(execution.RootPath, "shallow");
        System.Diagnostics.ProcessStartInfo start = StartInfo("git", execution.RootPath);
        foreach (string arg in new[] { "clone", "--quiet", "--depth", "1", new Uri(repository.RootPath + Path.DirectorySeparatorChar).AbsoluteUri, shallow })
            start.ArgumentList.Add(arg);
        ProcessResult clone = await RunAsync(start);
        Assert.True(clone.ExitCode == 0, clone.StandardError);
        await File.WriteAllTextAsync(local, ContractJson.SerializeDocument(new SnapshotPortfolioLocalMap
        {
            Projects = [new() { Id = "demo", RepositoryPath = shallow }],
        }), Encoding.UTF8);
        ProcessResult plan = await SnapshotRunAsync(manifest, local, checkpoint, "--calendar", "daily", "--preflight");
        Assert.True(plan.ExitCode == 0, plan.StandardError);
        SnapshotPortfolioReport shallowPlan = ContractJson.Deserialize<SnapshotPortfolioReport>(plan.StandardOutput);
        Assert.True(shallowPlan.Projects[0].ShallowHistory);
        Assert.Equal("shallow-history", shallowPlan.Projects[0].PlanningIssue);
        Assert.Null(shallowPlan.Projects[0].Periods[1].Hours);
        Assert.False(Directory.Exists(checkpoint));
        string output = Path.Combine(execution.RootPath, "daily.json");
        ProcessResult failed = await SnapshotRunAsync(manifest, local, checkpoint, "--calendar", "daily", "--output", output);
        Assert.NotEqual(0, failed.ExitCode);
        Assert.False(File.Exists(output));
    }
}
