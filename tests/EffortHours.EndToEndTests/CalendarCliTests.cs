using System.Diagnostics;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class CalendarCliTests : ChangeCliTestSupport
{
    [Fact]
    public async Task CalendarSingleShotKeepsExactDailyTotalsAndReusesEvidenceForHtmlAndText()
    {
        using GitFixture repo = await CalendarFixtureAsync();
        using GitFixture output = await GitFixture.CreateAsync();
        string checkpoint = Path.Combine(output.RootPath, "cache");
        string jsonPath = Path.Combine(output.RootPath, "calendar.json");
        string[] args = ["calendar", repo.RootPath, "--from", "2026-02-01", "--to", "2026-02-28",
            "--timezone", "America/Toronto", "--checkpoint", checkpoint];
        ProcessResult json = await RunCliAsync([.. args, "--format", "json", "--output", jsonPath]);
        Assert.True(json.ExitCode == 0, json.StandardError);
        string saved = await File.ReadAllTextAsync(jsonPath, Encoding.UTF8);
        Assert.True(ContractSchemaValidator.Validate("change-portfolio-comparison-report.schema.json", saved).IsValid);
        ChangePortfolioComparisonReport report = ContractJson.Deserialize<ChangePortfolioComparisonReport>(saved);
        Assert.Equal(28, report.Buckets.Count);
        Assert.Single(report.SourcePortfolio!.Items);
        ChangePortfolioComparisonSeries total = report.Series.Single(s => s.Kind == ChangePortfolioSeriesKind.Portfolio);
        Assert.Equal(224m, total.TotalCapacityHours);
        Assert.Equal(8m, total.Points[0].CapacityHours);
        Assert.Equal(0m, total.Points[0].Effort.Expected);
        Assert.True(total.TotalEffort.Expected > 0);
        ProcessResult html = await RunCliAsync([.. args, "--format", "html"]);
        Assert.True(html.ExitCode == 0, html.StandardError);
        Assert.Contains("<!doctype html>", html.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Show on calendar", html.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("reference hours per calendar day", html.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(repo.RootPath, html.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("calendar-person@example.test", html.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("source-secret", html.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("selection/analysis/reconciliation started", html.StandardError, StringComparison.Ordinal);
        Assert.Contains("checkpoint", html.StandardError, StringComparison.OrdinalIgnoreCase);
        ProcessResult text = await RunCliAsync([.. args, "--format", "text"]);
        Assert.True(text.ExitCode == 0, text.StandardError);
        Assert.Contains("| 2026-02-14 |", text.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(report.Verification.SemanticDigest, text.StandardOutput, StringComparison.Ordinal);
        Assert.Equal("", await repo.GitAsync("status", "--porcelain"));
        Assert.False(File.Exists(Path.Combine(repo.RootPath, ".git", "FETCH_HEAD")));
    }

    [Fact]
    public async Task CalendarDefaultsToLastCompleteMonthAndEmptySelectionIsCompleteZero()
    {
        using GitFixture repo = await CalendarFixtureAsync();
        ProcessResult result = await RunCliAsync("calendar", repo.RootPath, "--author", "nobody@example.test", "--format", "json", "--timezone", "UTC");
        Assert.True(result.ExitCode == 0, result.StandardError);
        ChangePortfolioComparisonReport report = ContractJson.Deserialize<ChangePortfolioComparisonReport>(result.StandardOutput);
        DateTime now = DateTime.UtcNow;
        DateTime end = new(now.Year, now.Month, 1);
        Assert.Equal(new DateTimeOffset(end.AddMonths(-1), TimeSpan.Zero), report.Buckets[0].SinceInclusive);
        Assert.Equal(new DateTimeOffset(end, TimeSpan.Zero), report.Buckets[^1].UntilExclusive);
        Assert.Equal(ChangePortfolioComparisonStatus.Complete, report.Status);
        Assert.Equal(0, report.SourcePortfolio!.TotalEffort.Expected);
    }

    [Fact]
    public async Task CalendarRejectsBadRangesAndSourceOutputBeforeAnalysis()
    {
        using GitFixture repo = await CalendarFixtureAsync();
        foreach (string[] options in new[]
        {
            new[] { "--from", "2026-02-30" }, ["--from", "2026-02-28", "--to", "2026-02-01"],
            ["--capacity-hours-per-day", "0"], ["--from", "2020-01-01", "--to", "2026-01-01"],
            ["--format", "html", "--output", Path.Combine(repo.RootPath, "report.html")],
            ["--workspace", repo.RootPath],
            ["--timeout-seconds", "999999999999999999999"],
        })
        {
            ProcessResult result = await RunCliAsync(["calendar", repo.RootPath, .. options]);
            Assert.NotEqual(0, result.ExitCode);
            Assert.DoesNotContain("static-analysis", result.StandardError, StringComparison.Ordinal);
        }
        Assert.Equal("", await repo.GitAsync("status", "--porcelain"));
    }

    [Fact]
    public async Task CalendarInteractiveAndWorkspaceModesShareTheFlagDrivenCalculation()
    {
        using GitFixture repo = await CalendarFixtureAsync();
        string[] options = ["--from", "2026-02-01", "--to", "2026-02-28", "--timezone", "UTC", "--format", "json", "--output", "-"];
        ProcessResult prompted = await CalendarProcessAsync(repo.RootPath, "\n\n\n\n\n",
            ["calendar", "--interactive", .. options]);
        Assert.True(prompted.ExitCode == 0, prompted.StandardError);
        Assert.Contains("Reference hours per calendar day [8]", prompted.StandardError, StringComparison.Ordinal);
        Assert.Contains("Repositories (checkout or all) [checkout]", prompted.StandardError, StringComparison.Ordinal);
        Assert.Contains("Git identity aliases", prompted.StandardError, StringComparison.Ordinal);
        using GitFixture workspace = await GitFixture.CreateAsync();
        string checkout = Path.Combine(workspace.RootPath, "child");
        ProcessStartInfo clone = StartInfo("git", workspace.RootPath);
        foreach (string arg in new[] { "clone", "--quiet", repo.RootPath, checkout }) clone.ArgumentList.Add(arg);
        Assert.Equal(0, (await RunAsync(clone)).ExitCode);
        // Discovery stops at repository roots; use an external non-repository parent for this test.
        string folder = Path.Combine(workspace.RootPath, "scope");
        Directory.CreateDirectory(folder);
        Directory.Move(checkout, Path.Combine(folder, "child"));
        foreach (string file in Directory.EnumerateFiles(Path.Combine(workspace.RootPath, ".git"), "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(Path.Combine(workspace.RootPath, ".git"), recursive: true);
        ProcessResult discovered = await CalendarProcessAsync(folder, null,
            ["calendar", "--author", "calendar-person@example.test", .. options]);
        Assert.True(discovered.ExitCode == 0, discovered.StandardError);
        Assert.Single(ContractJson.Deserialize<ChangePortfolioComparisonReport>(discovered.StandardOutput).Selection.AuthorPeriodManifest!.Repositories);
        ProcessResult allInside = await CalendarProcessAsync(Path.Combine(folder, "child"), null,
            ["calendar", "--all-repos", "--author", "calendar-person@example.test", .. options]);
        Assert.True(allInside.ExitCode == 0, allInside.StandardError);
        Assert.Single(ContractJson.Deserialize<ChangePortfolioComparisonReport>(allInside.StandardOutput).Selection.AuthorPeriodManifest!.Repositories);
    }

    private static async Task<GitFixture> CalendarFixtureAsync()
    {
        GitFixture repo = await GitFixture.CreateAsync();
        await repo.GitAsync("config", "user.email", "calendar-person@example.test");
        repo.WriteText("Demo.csproj", ProjectFile);
        _ = await SnapshotCommitAtAsync(repo, "initial", "2026-01-10T12:00:00Z");
        repo.WriteText("Feature.cs", "public class Feature { public string Parse(string input) => input.Trim(); } // source-secret\n");
        _ = await SnapshotCommitAtAsync(repo, "selected", "2026-02-14T12:00:00Z");
        repo.WriteText("Later.cs", "public class Later { public bool Ready => true; }\n");
        _ = await SnapshotCommitAtAsync(repo, "outside interval", "2026-03-02T12:00:00Z");
        return repo;
    }

    private static async Task<ProcessResult> CalendarProcessAsync(string cwd, string? input, string[] args)
    {
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        ProcessStartInfo start = StartInfo("dotnet", cwd);
        start.RedirectStandardInput = true;
        start.ArgumentList.Add(Path.Combine(FindRepositoryRoot(), "src", "EffortHours.Cli", "bin", configuration, "net10.0", "efforthours.dll"));
        foreach (string arg in args) start.ArgumentList.Add(arg);
        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (input is not null) await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(deadline.Token);
        return new(process.ExitCode, await stdout, await stderr);
    }
}
