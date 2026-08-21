using System.Globalization;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static class ChangePortfolioTodayMarkdownRenderer
{
    public static string Render(ChangePortfolioComparisonReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The today-to-date comparison report is invalid: " + string.Join(" ", errors),
                nameof(report));
        }

        DateTimeOffset asOf = report.AsOf ??
            throw new ArgumentException("A today-to-date report requires an as-of instant.", nameof(report));
        ChangePortfolioHostDiscovery discovery = report.Discovery ??
            throw new ArgumentException("A today-to-date report requires discovery metadata.", nameof(report));
        if (report.BucketPolicy.Policy != ChangePortfolioComparisonPolicies.TodayToDateV1 ||
            report.Buckets.Count != 1 || !report.Buckets[0].PartialEnd)
        {
            throw new ArgumentException(
                "A today-to-date report requires one partial daily bucket.",
                nameof(report));
        }

        StringBuilder markdown = new();
        markdown.Append("# ").AppendLine(Escape(report.Title));
        markdown.AppendLine();
        if (report.Status == ChangePortfolioComparisonStatus.Complete)
        {
            AppendCompleteSummary(markdown, report, discovery, asOf);
        }
        else
        {
            markdown.AppendLine("Today-to-date result: **incomplete**");
            markdown.AppendLine();
            markdown.AppendLine(
                "No EHE aggregate or capacity ratio is published because at least one repository shard failed.");
        }

        AppendScope(markdown, report, discovery, asOf);
        AppendFailures(markdown, report);
        return markdown.ToString().ReplaceLineEndings("\n").TrimEnd() + "\n";
    }

    private static void AppendCompleteSummary(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report,
        ChangePortfolioHostDiscovery discovery,
        DateTimeOffset asOf)
    {
        ChangePortfolioComparisonSeries portfolio = report.Series.Single(series =>
            series.Kind == ChangePortfolioSeriesKind.Portfolio);
        ChangePortfolioRatioRange ratio = portfolio.TotalCapacityRatio ??
            throw new ArgumentException("Today-to-date capacity ratios are missing.", nameof(report));
        int changes = portfolio.Points.Single().SelectedChangeCount;
        int shared = report.SourcePortfolio?.Aggregation?.ContributorGroups.Count(group =>
            group.Kind == ChangePortfolioContributorGroupKind.SharedContributors &&
            group.SelectedCommitCount > 0) ?? 0;
        markdown.Append("Today-to-date expected ratio: **")
            .Append(Ratio(ratio.Expected)).AppendLine("**  ");
        markdown.Append("Expected EHE: **").Append(Hours(portfolio.TotalEffort.Expected))
            .AppendLine(" h**  ");
        markdown.Append("Reference capacity: **").Append(Hours(portfolio.TotalCapacityHours!.Value))
            .AppendLine(" h**  ");
        markdown.Append("Range: **").Append(Ratio(ratio.Low)).Append('-')
            .Append(Ratio(ratio.High)).AppendLine("**  ");
        markdown.Append("As of: `").Append(asOf.ToString("O", CultureInfo.InvariantCulture))
            .Append("` (`").Append(Escape(report.Selection.AuthorPeriodManifest!.TimeZone))
            .AppendLine("`)  ");
        markdown.Append("Selected changes: **").Append(changes).AppendLine("**  ");
        markdown.Append("Repositories: **").Append(discovery.ActiveRepositoryCount).AppendLine("**  ");
        markdown.Append("Open PR heads included: **").Append(discovery.OpenPullRequestHeadCount)
            .AppendLine("**  ");
        markdown.Append("Shared-credit groups: **").Append(shared).AppendLine("**");
    }

    private static void AppendScope(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report,
        ChangePortfolioHostDiscovery discovery,
        DateTimeOffset asOf)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Scope and completeness");
        markdown.AppendLine();
        markdown.Append("- Current-day bucket is partial as of `")
            .Append(asOf.ToString("O", CultureInfo.InvariantCulture)).AppendLine("`.");
        markdown.Append("- GitHub discovery was ").Append(discovery.Complete ? "complete" : "incomplete")
            .Append(": ").Append(discovery.ProviderRepositoryCount).Append(" provider repositories, ")
            .Append(discovery.ConsideredRepositoryCount).Append(" mapped checkouts, ")
            .Append(discovery.ProviderPageCount).Append(" pages, and ")
            .Append(discovery.ProviderQueryCount).AppendLine(" queries.");
        markdown.Append("- Immutable heads used ").Append(discovery.LocalObjectCount)
            .Append(" local objects and ").Append(discovery.AcquiredObjectCount)
            .AppendLine(" narrowly acquired objects.");
        markdown.Append("- Discovery took ").Append(Milliseconds(discovery.ElapsedMilliseconds))
            .Append("; repository shards took ")
            .Append(Milliseconds(report.Execution.Repositories.Sum(repository =>
                repository.ElapsedMilliseconds))).AppendLine(" in aggregate.");
        if (report.Execution.EndToEndElapsedMilliseconds is decimal elapsed)
        {
            markdown.Append("- One-command elapsed time through initial rendering was ")
                .Append(Milliseconds(elapsed)).AppendLine(".");
        }
        markdown.AppendLine();
        markdown.AppendLine(
            "> EHE is experimental, uncalibrated replacement effort—not actual labor, a timesheet, productivity, authorship, compensation, or an invoice. The low/high values are planning uncertainty bounds, and capacity is only a caller-supplied reference denominator.");
    }

    private static void AppendFailures(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        if (report.Execution.Failures.Count == 0)
        {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Failures");
        markdown.AppendLine();
        foreach (ChangePortfolioComparisonFailure failure in report.Execution.Failures
            .OrderBy(value => value.RepositoryId, StringComparer.Ordinal)
            .ThenBy(value => value.Phase, StringComparer.Ordinal))
        {
            markdown.Append("- `").Append(Escape(failure.RepositoryId)).Append("` / `")
                .Append(Escape(failure.Phase)).Append("` / `")
                .Append(Escape(failure.Category)).Append("`: ")
                .Append(Escape(failure.Message)).Append(" (`")
                .Append(Escape(failure.MessageDigest)).AppendLine("`)");
        }
    }

    private static string Escape(string value) => ReportFormatting.Escape(value);

    private static string Hours(decimal value) =>
        value.ToString("#,0.00", CultureInfo.InvariantCulture);

    private static string Ratio(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture) + "x";

    private static string Milliseconds(decimal value) =>
        value.ToString("#,0.###", CultureInfo.InvariantCulture) + " ms";
}
