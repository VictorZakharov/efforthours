using EffortHours.Change;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Fact]
    public async Task CalendarRepositoryCellsConserveEveryBoundAndRenderSafeOfflineViews()
    {
        ChangeAuthorPeriodManifest manifest = Manifest();
        ChangePortfolioReport source = await SourceReportAsync(manifest);
        ChangePortfolioComparisonReport report = ChangePortfolioComparisonBuilder.Build(source, BuildOptions(manifest));
        IReadOnlyList<ChangePortfolioComparisonSeries> projects = ChangePortfolioComparisonBuilder.BuildRepositorySeries(report);
        ChangePortfolioComparisonSeries portfolio = report.Series.Single(s => s.Kind == ChangePortfolioSeriesKind.Portfolio);
        for (int i = 0; i < report.Buckets.Count; i++)
            Assert.Equal(portfolio.Points[i].Effort, Sum(projects.Select(p => p.Points[i].Effort)));
        string html = ChangeCalendarRenderer.Render(report, projects, html: true);
        string text = ChangeCalendarRenderer.Render(report, projects, html: false);
        Assert.Contains("Content-Security-Policy", html, StringComparison.Ordinal);
        Assert.Contains("script-src 'sha256-", html, StringComparison.Ordinal);
        Assert.Contains("No source, raw aliases, or local paths", html, StringComparison.Ordinal);
        Assert.Contains("<noscript>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", html, StringComparison.Ordinal);
        Assert.DoesNotContain("person-a@example.test", html, StringComparison.Ordinal);
        Assert.DoesNotContain("private-repository-path", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script src", html, StringComparison.Ordinal);
        Assert.Contains("Source semantic digest: " + report.Verification.SemanticDigest, text, StringComparison.Ordinal);
        Assert.Contains("not actual labor", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', text);
        Assert.Throws<ArgumentException>(() => ChangeCalendarRenderer.Render(report, [], html: true));
        Assert.Equal(html, ChangeCalendarRenderer.Render(report, projects, html: true));
    }
}
