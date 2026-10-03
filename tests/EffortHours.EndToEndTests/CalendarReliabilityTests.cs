using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Fact]
    public async Task CalendarMultipleProjectsKeepOneDenominatorAndDstCalendarDays()
    {
        using GitFixture first = await CalendarFixtureAsync();
        using GitFixture second = await CalendarFixtureAsync();
        ProcessResult pair = await RunCliAsync("calendar", "--project", "first=" + first.RootPath,
            "--project", "second=" + second.RootPath, "--from", "2026-02-01", "--to", "2026-02-28", "--timezone", "UTC", "--format", "json");
        Assert.True(pair.ExitCode == 0, pair.StandardError);
        ChangePortfolioComparisonReport report = ContractJson.Deserialize<ChangePortfolioComparisonReport>(pair.StandardOutput);
        ChangePortfolioComparisonSeries total = report.Series.Single(s => s.Kind == ChangePortfolioSeriesKind.Portfolio);
        Assert.Equal(224m, total.TotalCapacityHours);
        Assert.Equal(report.SourcePortfolio!.Aggregation!.Repositories.Sum(r => r.NormalizedEffort.Expected), total.TotalEffort.Expected);
        Assert.Equal(2, report.SourcePortfolio.Aggregation.Repositories.Count);
        ProcessResult dst = await RunCliAsync("calendar", first.RootPath, "--from", "2026-03-07", "--to", "2026-03-09",
            "--timezone", "America/Toronto", "--format", "json");
        Assert.True(dst.ExitCode == 0, dst.StandardError);
        ChangePortfolioComparisonReport shifted = ContractJson.Deserialize<ChangePortfolioComparisonReport>(dst.StandardOutput);
        Assert.Equal(3, shifted.Buckets.Count);
        Assert.Equal(TimeSpan.FromHours(23), shifted.Buckets[1].UntilExclusive - shifted.Buckets[1].SinceInclusive);
        Assert.Equal(24m, shifted.Series.Single(s => s.Kind == ChangePortfolioSeriesKind.Portfolio).TotalCapacityHours);
    }

    [Fact]
    public async Task CalendarFailedProjectPublishesNoGraphAndCanResumeSuccessfulCheckpoint()
    {
        using GitFixture good = await CalendarFixtureAsync();
        using GitFixture bad = await CalendarFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        // Remove the selected parent's object to fail after pinning, inside the repository shard.
        string parent = await bad.GitAsync("rev-parse", "HEAD^");
        string objectPath = Path.Combine(bad.RootPath, ".git", "objects", parent[..2], parent[2..]);
        File.SetAttributes(objectPath, FileAttributes.Normal);
        File.Delete(objectPath);
        string output = Path.Combine(execution.RootPath, "report.html");
        string[] args = ["calendar", "--project", "good=" + good.RootPath, "--project", "bad=" + bad.RootPath,
            "--from", "2026-02-01", "--to", "2026-02-28", "--output", output, "--timezone", "UTC"];
        ProcessResult failure = await RunCliAsync(args);
        Assert.NotEqual(0, failure.ExitCode);
        string html = await File.ReadAllTextAsync(output, Encoding.UTF8);
        Assert.Contains("Incomplete EHE calendar", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"total\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(good.RootPath, html, StringComparison.Ordinal);
        Assert.DoesNotContain(bad.RootPath, html, StringComparison.Ordinal);
        Assert.Contains("Project bad", html, StringComparison.Ordinal);
        string cache = output + ".eh-checkpoint";
        Assert.True(Directory.Exists(cache));
        ProcessResult warm = await RunCliAsync("calendar", "--project", "good=" + good.RootPath,
            "--from", "2026-02-01", "--to", "2026-02-28", "--format", "json", "--timezone", "UTC", "--checkpoint", cache);
        Assert.True(warm.ExitCode == 0, warm.StandardError);
        Assert.Equal(1, ContractJson.Deserialize<ChangePortfolioComparisonReport>(warm.StandardOutput).Execution.Checkpoint.HitCount);
    }

    [Fact]
    public async Task PlainCalendarPromptsWithDefaultsAndAllowsOverridesWithoutAgentHelp()
    {
        using GitFixture repo = await CalendarFixtureAsync();
        ProcessResult result = await CalendarProcessAsync(repo.RootPath,
            "UTC\n\n2026-02-01\n2026-02-28\n\ntext\n-\n\n\n\n", ["calendar"]);
        Assert.True(result.ExitCode == 0, result.StandardError);
        DateTime now = DateTime.UtcNow;
        DateTime end = new(now.Year, now.Month, 1);
        Assert.Contains("First date (inclusive) [" + end.AddMonths(-1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + "]", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("Last date (inclusive) [" + end.AddDays(-1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + "]", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("# Daily Change EHE calendar", result.StandardOutput, StringComparison.Ordinal);
        ProcessResult ended = await CalendarProcessAsync(repo.RootPath, null, ["calendar"]);
        Assert.NotEqual(0, ended.ExitCode);
        Assert.Contains("Interactive input ended", ended.StandardError, StringComparison.Ordinal);
    }
}
