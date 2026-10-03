using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Fact]
    public async Task CalendarDaysStayInvariantAcrossWindowsAndDateFieldsAreExplicit()
    {
        using GitFixture repo = await GitFixture.CreateAsync();
        using GitFixture external = await GitFixture.CreateAsync();
        await repo.GitAsync("config", "user.email", "calendar-person@example.test");
        repo.WriteText("Demo.csproj", ProjectFile);
        repo.WriteText("Feature.cs", "public class Feature { public int Value => 0; }");
        _ = await SnapshotCommitAtAsync(repo, "base", "2026-01-10T12:00:00Z");
        repo.WriteText("Feature.cs", "public class Feature { public int Value => 1; }");
        _ = await SnapshotCommitAtAsync(repo, "earlier", "2026-01-19T12:00:00Z");
        repo.WriteText("Feature.cs", "public class Feature { public int Value => 2; }");
        _ = await SnapshotCommitAtAsync(repo, "target", "2026-01-20T12:00:00Z");
        repo.WriteText("Feature.cs", "public class Feature { public int Value => 3; }");
        await repo.GitAsync("add", "--all");
        var commit = StartInfo("git", repo.RootPath);
        commit.Environment["GIT_AUTHOR_DATE"] = "2026-01-19T15:00:00Z";
        commit.Environment["GIT_COMMITTER_DATE"] = "2026-01-21T12:00:00Z";
        foreach (string arg in new[] { "commit", "--quiet", "-m", "later committed change" }) commit.ArgumentList.Add(arg);
        Assert.Equal(0, (await RunAsync(commit)).ExitCode);
        async Task<ChangePortfolioComparisonReport> Report(string from, string to, string field = "committer")
        {
            ProcessResult result = await RunCliAsync("calendar", repo.RootPath, "--from", from, "--to", to,
                "--timezone", "UTC", "--format", "json", "--date-field", field,
                "--checkpoint", Path.Combine(external.RootPath, field + from + to));
            Assert.True(result.ExitCode == 0, result.StandardError);
            var report = ContractJson.Deserialize<ChangePortfolioComparisonReport>(result.StandardOutput);
            Assert.Empty(ContractValidation.Validate(report));
            Assert.True(ContractSchemaValidator.Validate("change-portfolio-comparison-report.schema.json", result.StandardOutput).IsValid);
            return report;
        }
        ChangePortfolioComparisonReport wide = await Report("2026-01-19", "2026-01-21");
        ChangePortfolioComparisonReport narrow = await Report("2026-01-20", "2026-01-20");
        ChangePortfolioComparisonReport warm = await Report("2026-01-20", "2026-01-20");
        Assert.Equal(ChangePortfolioDailyNormalization.Policy, wide.Verification.BucketAllocationPolicy);
        Assert.Equal(ChangePortfolioDateField.Committer, wide.Selection.AuthorPeriodManifest!.DateField);
        Assert.Equal(wide.Series.Single(series => series.Kind == ChangePortfolioSeriesKind.Portfolio).Points[1].Effort,
            narrow.Series.Single(series => series.Kind == ChangePortfolioSeriesKind.Portfolio).Points[0].Effort);
        Assert.Equal(ContractJson.SerializeCompact(wide.SourcePortfolio!.DailyNormalization!.Days[1].RepositoryGroups[0].Categories),
            ContractJson.SerializeCompact(narrow.SourcePortfolio!.DailyNormalization!.Days[0].RepositoryGroups[0].Categories));
        Assert.Equal(narrow.Verification.SemanticDigest, warm.Verification.SemanticDigest);
        Assert.Single((await Report("2026-01-21", "2026-01-21")).SourcePortfolio!.Items);
        Assert.Empty((await Report("2026-01-21", "2026-01-21", "author")).SourcePortfolio!.Items);
        Assert.Equal(2, (await Report("2026-01-19", "2026-01-19", "author")).SourcePortfolio!.Items.Count);
        Assert.Equal("", await repo.GitAsync("status", "--porcelain"));
    }
}
