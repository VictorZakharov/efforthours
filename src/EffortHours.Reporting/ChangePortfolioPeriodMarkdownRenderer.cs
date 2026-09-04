using System.Globalization;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static class ChangePortfolioPeriodMarkdownRenderer
{
    public static string Render(ChangePortfolioComparisonReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The native named-period report is invalid: " + string.Join(" ", errors),
                nameof(report));
        }

        ChangePortfolioNativePeriod native = report.NativePeriod ??
            throw new ArgumentException("A native named-period report requires period metadata.", nameof(report));
        ChangePortfolioHostDiscovery discovery = report.Discovery!;
        ChangePortfolioAuthorPeriodManifestSelection selection =
            report.Selection.AuthorPeriodManifest!;
        ChangePortfolioContributorSelection contributors = native.ContributorSelection;
        StringBuilder markdown = new();
        markdown.Append("# ").AppendLine(Escape(report.Title));
        markdown.AppendLine();
        markdown.Append("Status: **").Append(report.Status == ChangePortfolioComparisonStatus.Complete
            ? "complete"
            : "incomplete").AppendLine("**");
        markdown.Append("Period: **").Append(Period(native.Kind)).Append("** from `")
            .Append(selection.SinceInclusive.ToString("O", CultureInfo.InvariantCulture))
            .Append("` through `").Append(selection.UntilExclusive.ToString("O", CultureInfo.InvariantCulture))
            .Append("` (`").Append(Escape(selection.TimeZone)).AppendLine("`).");
        markdown.Append("Breakdown: **").Append(native.Breakdown == ChangePortfolioNativeBreakdown.CalendarDay
            ? "calendar day"
            : "full period").Append("**; reference capacity: **")
            .Append(Hours(native.CapacityHoursPerDay)).AppendLine(" hours per local calendar day per contributor**.");
        AppendContributorSelection(markdown, contributors);

        if (report.Status == ChangePortfolioComparisonStatus.Complete)
        {
            AppendSummary(markdown, report);
            if (native.Breakdown == ChangePortfolioNativeBreakdown.CalendarDay)
            {
                AppendDailyBreakdown(markdown, report);
            }
        }
        else
        {
            markdown.AppendLine();
            markdown.AppendLine("No EHE aggregate or multiplier is published because required work is incomplete.");
        }

        AppendCoverage(markdown, report, discovery);
        AppendFailures(markdown, report);
        markdown.AppendLine();
        markdown.AppendLine("## Interpretation limits");
        markdown.AppendLine();
        markdown.AppendLine(
            "> EHE is experimental, uncalibrated replacement effort—not actual labor, a timesheet, productivity, authorship, compensation, or an invoice. Capacity is only a caller-supplied denominator. The overall multiplier is total expected EHE divided by total reference capacity; it is not an unweighted average of daily multipliers.");
        return markdown.ToString().ReplaceLineEndings("\n").TrimEnd() + "\n";
    }

    private static void AppendContributorSelection(
        StringBuilder markdown,
        ChangePortfolioContributorSelection selection)
    {
        markdown.Append("Contributor selection: **")
            .Append(selection.Mode == ChangePortfolioContributorSelectionMode.Team
                ? "team sample"
                : "single contributor").Append("**");
        if (selection.Mode == ChangePortfolioContributorSelectionMode.Team)
        {
            markdown.Append("; seed `").Append(Escape(selection.SampleSeed ?? "unavailable"))
                .Append("`; requested sample **").Append(selection.RequestedSampleSize)
                .Append("**; eligible population **")
                .Append(selection.EligiblePopulationCount?.ToString(CultureInfo.InvariantCulture) ?? "unavailable")
                .Append("**; explicit inclusions **").Append(selection.IncludedContributorIds.Count).Append("**");
        }

        markdown.AppendLine(".");
    }

    private static void AppendSummary(StringBuilder markdown, ChangePortfolioComparisonReport report)
    {
        ChangePortfolioComparisonSeries portfolio = report.Series.Single(series =>
            series.Kind == ChangePortfolioSeriesKind.Portfolio);
        markdown.AppendLine();
        markdown.AppendLine("## Overall");
        markdown.AppendLine();
        markdown.AppendLine("| Series | Selected changes | Expected EHE | Reference capacity | Expected multiplier |");
        markdown.AppendLine("|---|---:|---:|---:|---:|");
        AppendSummaryRow(markdown, "All selected contributors", portfolio);
        foreach (ChangePortfolioComparisonSeries series in ContributorSeries(report))
        {
            AppendSummaryRow(markdown, series.ContributorIds.Single(), series);
        }

        markdown.AppendLine();
        markdown.AppendLine("Overall formula: `total expected EHE / total reference capacity`.");
        markdown.AppendLine(
            "Contributor rows are membership-stable isolated series. Shared commits can appear in more than one row, so contributor rows are non-additive and must not be summed into the portfolio total.");
    }

    private static void AppendSummaryRow(
        StringBuilder markdown,
        string label,
        ChangePortfolioComparisonSeries series)
    {
        markdown.Append("| `").Append(Escape(label)).Append("` | ")
            .Append(series.Points.Sum(point => point.SelectedChangeCount)).Append(" | ")
            .Append(Hours(series.TotalEffort.Expected)).Append(" h | ")
            .Append(Hours(series.TotalCapacityHours)).Append(" h | ")
            .Append(Ratio(series.TotalCapacityRatio?.Expected)).AppendLine(" |");
    }

    private static void AppendDailyBreakdown(StringBuilder markdown, ChangePortfolioComparisonReport report)
    {
        Dictionary<string, ChangePortfolioComparisonBucket> buckets =
            report.Buckets.ToDictionary(bucket => bucket.Id, StringComparer.Ordinal);
        markdown.AppendLine();
        markdown.AppendLine("## Daily breakdown");
        markdown.AppendLine();
        markdown.AppendLine("| Contributor | Day | Selected changes | Expected EHE | Reference capacity | Expected multiplier |");
        markdown.AppendLine("|---|---|---:|---:|---:|---:|");
        ChangePortfolioComparisonSeries portfolio = report.Series.Single(series =>
            series.Kind == ChangePortfolioSeriesKind.Portfolio);
        foreach (ChangePortfolioComparisonSeries series in
            new[] { portfolio }.Concat(ContributorSeries(report)))
        {
            foreach (ChangePortfolioComparisonPoint point in series.Points)
            {
                ChangePortfolioComparisonBucket bucket = buckets[point.BucketId];
                string label = series.Kind == ChangePortfolioSeriesKind.Portfolio
                    ? "All selected contributors"
                    : series.ContributorIds.Single();
                markdown.Append("| `").Append(Escape(label)).Append("` | ")
                    .Append(Escape(bucket.Label)).Append(" | ").Append(point.SelectedChangeCount).Append(" | ")
                    .Append(Hours(point.Effort.Expected)).Append(" h | ")
                    .Append(Hours(point.CapacityHours)).Append(" h | ")
                    .Append(Ratio(point.CapacityRatio?.Expected)).AppendLine(" |");
            }
        }
    }

    private static IEnumerable<ChangePortfolioComparisonSeries> ContributorSeries(
        ChangePortfolioComparisonReport report) => report.Series
            .Where(series => series.Kind == ChangePortfolioSeriesKind.ContributorIsolated)
            .OrderBy(series => series.ContributorIds.Single(), StringComparer.Ordinal);

    private static void AppendCoverage(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report,
        ChangePortfolioHostDiscovery discovery)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Coverage and execution");
        markdown.AppendLine();
        markdown.Append("- GitHub discovery was ").Append(discovery.Complete ? "complete" : "incomplete")
            .Append(": ").Append(discovery.ActiveRepositoryCount).Append(" active repositories, ")
            .Append(discovery.DefaultHeadCount + discovery.OpenPullRequestHeadCount).Append(" heads, ")
            .Append(discovery.ProviderQueryCount).Append(" queries in ")
            .Append(discovery.ProviderProcessCount).AppendLine(" provider process.");
        if (report.ScopeSummary is { } scope)
        {
            markdown.Append("- Selected commits: ").Append(scope.IdentitySelectedCommitCount)
                .Append(" identity-selected; ").Append(scope.AdmittedCommitCount)
                .Append(" engineering; ").Append(scope.ScopeEmptyCommitCount).AppendLine(" scope-empty.");
        }

        markdown.Append("- Managed cache: ").Append(discovery.LocalObjectCount)
            .Append(" existing objects; ").Append(discovery.AcquiredObjectCount)
            .Append(" acquired objects / ").Append(discovery.AcquiredBytes).AppendLine(" bytes.");
    }

    private static void AppendFailures(StringBuilder markdown, ChangePortfolioComparisonReport report)
    {
        if (report.Execution.Failures.Count == 0)
        {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Failures");
        markdown.AppendLine();
        foreach (ChangePortfolioComparisonFailure failure in report.Execution.Failures)
        {
            markdown.Append("- `").Append(Escape(failure.RepositoryId)).Append("` / `")
                .Append(Escape(failure.Phase)).Append("`: ").Append(Escape(failure.Message)).AppendLine();
            if (failure.AgentAction is not null)
            {
                markdown.Append("  - Agent action: `")
                    .Append(Escape(ContractJson.SerializeCompact(failure.AgentAction))).AppendLine("`");
            }
        }
    }

    private static string Period(ChangePortfolioNativePeriodKind value) => value switch
    {
        ChangePortfolioNativePeriodKind.ThisWeek => "this week",
        ChangePortfolioNativePeriodKind.LastWeek => "last week",
        ChangePortfolioNativePeriodKind.ThisMonth => "this month",
        ChangePortfolioNativePeriodKind.LastMonth => "last month",
        _ => value.ToString(),
    };

    private static string Escape(string value) => ReportFormatting.Escape(value);

    private static string Hours(decimal? value) =>
        value?.ToString("#,0.00", CultureInfo.InvariantCulture) ?? "—";

    private static string Ratio(decimal? value) =>
        value?.ToString("0.00×", CultureInfo.InvariantCulture) ?? "—";
}
