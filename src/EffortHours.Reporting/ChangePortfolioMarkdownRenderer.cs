using System.Globalization;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static partial class ChangePortfolioMarkdownRenderer
{
    public static string Render(ChangePortfolioReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The change portfolio report is invalid: " + string.Join(" ", errors),
                nameof(report));
        }

        StringBuilder markdown = new();
        markdown.AppendLine("# EffortHours change portfolio");
        markdown.AppendLine();
        markdown.AppendLine(
            "> Portfolio Change EHE is repository-attributed replacement effort, not actual labor, productivity, personal value, sole-authorship proof, a performance grade, or compensation advice.");
        markdown.AppendLine();
        markdown.Append("Selection: `").Append(ReportFormatting.Kebab(report.Selection.Kind)).AppendLine("`  ");
        markdown.Append("Profile: `").Append(ReportFormatting.Kebab(report.Profile)).AppendLine("`  ");
        markdown.Append("Estimator: `").Append(ReportFormatting.Escape(report.EstimatorVersion)).AppendLine("`");
        if (report.Selection.AuthorPeriod is not null)
        {
            AppendAuthorSelection(markdown, report.Selection.AuthorPeriod);
        }
        else if (report.Selection.AuthorPeriodManifest is not null)
        {
            AppendAuthorPeriodManifestSelection(markdown, report.Selection.AuthorPeriodManifest);
        }

        markdown.AppendLine();
        markdown.AppendLine("## Summary");
        markdown.AppendLine();
        markdown.AppendLine("| Measure | Low | Expected | High |");
        markdown.AppendLine("| --- | ---: | ---: | ---: |");
        AppendRange(markdown, "Isolated selected-change sum", report.IsolatedEffort);
        AppendRange(markdown, "Repository-normalized portfolio EHE", report.TotalEffort);
        if (report.TotalCost is not null)
        {
            markdown.Append("| Equivalent replacement cost (")
                .Append(ReportFormatting.Escape(report.TotalCost.Currency)).Append(") | ")
                .Append(ReportFormatting.Money(report.TotalCost.Low)).Append(" | ")
                .Append(ReportFormatting.Money(report.TotalCost.Expected)).Append(" | ")
                .Append(ReportFormatting.Money(report.TotalCost.High)).AppendLine(" |");
        }

        if (report.Aggregation is not null)
        {
            AppendAggregation(markdown, report.Aggregation);
        }

        markdown.AppendLine();
        markdown.AppendLine("## Selected changes and exact allocations");
        markdown.AppendLine();
        markdown.AppendLine("| Change | Repository | Base context | Selector | Attribution | Isolated expected | Allocated expected | Uncertainty |");
        markdown.AppendLine("| --- | --- | --- | --- | --- | ---: | ---: | ---: |");
        foreach (ChangePortfolioItemEstimate item in report.Items)
        {
            markdown.Append("| `").Append(ReportFormatting.Escape(DisplaySelectorId(item.SelectorId))).Append("` | ")
                .Append(ReportFormatting.Escape(item.RepositoryId)).Append(" | `")
                .Append(ReportFormatting.Escape(Short(item.BaseContextId))).Append("` | ")
                .Append(ReportFormatting.Escape(Selector(item.Selection))).Append(" | ")
                .Append(ReportFormatting.Escape(Attribution(item.Attribution))).Append(" | ")
                .Append(ReportFormatting.Hours(item.IsolatedEffort.Expected)).Append(" | ")
                .Append(ReportFormatting.Hours(item.AllocatedExpectedHours)).Append(" | ")
                .Append(item.UncertaintyReasons.Count).AppendLine(" |");
        }

        AppendPullRequestVerification(markdown, report.Items);

        markdown.AppendLine();
        markdown.AppendLine("Allocations sum exactly to normalized expected EHE. They are structural attribution, not timesheet or personal-credit entries.");
        markdown.AppendLine();
        markdown.AppendLine("## Repository normalization");
        markdown.AppendLine();
        markdown.AppendLine("| Repository | Base contexts | Items | Order policy | Isolated expected | Normalized expected | Assessment | Uncertainty |");
        markdown.AppendLine("| --- | ---: | ---: | --- | ---: | ---: | --- | ---: |");
        foreach (ChangePortfolioRepositoryGroup group in report.RepositoryGroups)
        {
            markdown.Append("| ").Append(ReportFormatting.Escape(group.RepositoryId)).Append(" | ")
                .Append(group.BaseContexts.Count).Append(" | ")
                .Append(group.ItemIds.Count).Append(" | ")
                .Append(ReportFormatting.Kebab(group.OrderPolicy)).Append(" | ")
                .Append(ReportFormatting.Hours(group.IsolatedEffort.Expected)).Append(" | ")
                .Append(ReportFormatting.Hours(group.NormalizedEffort.Expected)).Append(" | ")
                .Append(ReportFormatting.Escape(group.Assessment)).Append(" | ")
                .Append(group.UncertaintyReasons.Count).AppendLine(" |");
        }

        markdown.AppendLine();
        markdown.AppendLine("## Immutable base contexts");
        markdown.AppendLine();
        markdown.AppendLine("| Repository | Context | Pinned base object | Items |");
        markdown.AppendLine("| --- | --- | --- | ---: |");
        foreach (ChangePortfolioRepositoryGroup group in report.RepositoryGroups)
        {
            foreach (ChangePortfolioBaseContext context in group.BaseContexts)
            {
                markdown.Append("| ").Append(ReportFormatting.Escape(group.RepositoryId)).Append(" | `")
                    .Append(ReportFormatting.Escape(Short(context.Id))).Append("` | `")
                    .Append(ReportFormatting.Escape(ShortObject(context.BaseObjectId))).Append("` | ")
                    .Append(context.ItemIds.Count).AppendLine(" |");
            }
        }

        AppendCategories(markdown, report.Categories);
        AppendAdjustments(markdown, report.Adjustments);
        AppendUncertainty(markdown, report);
        AppendList(markdown, "Assumptions and safeguards", report.Assumptions);
        AppendList(markdown, "Diagnostics", report.Diagnostics.Select(diagnostic =>
            $"`{diagnostic.Code}` ({ReportFormatting.Kebab(diagnostic.Severity)}): {diagnostic.Message}"));
        if (!string.IsNullOrWhiteSpace(report.Verification.Note))
        {
            AppendList(markdown, "Verification note", [report.Verification.Note]);
        }

        return markdown.ToString().TrimEnd() + "\n";
    }

    private static void AppendAuthorSelection(
        StringBuilder markdown,
        ChangePortfolioAuthorPeriodSelection selection)
    {
        markdown.AppendLine("  ");
        markdown.Append("Aliases: ")
            .AppendLine(string.Join(", ", selection.Aliases.Select(alias => $"`{ReportFormatting.Escape(alias)}`")) + "  ");
        markdown.Append("Interval: `")
            .Append(selection.SinceInclusive.ToString("O", CultureInfo.InvariantCulture)).Append("` through `")
            .Append(selection.UntilExclusive.ToString("O", CultureInfo.InvariantCulture))
            .AppendLine("` (start inclusive, end exclusive)  ");
        markdown.Append("Timezone: `").Append(ReportFormatting.Escape(selection.TimeZone)).Append("`; date field: `")
            .Append(ReportFormatting.Kebab(selection.DateField)).Append("`; merges: `")
            .Append(ReportFormatting.Kebab(selection.MergePolicy)).Append("`; co-authors: `")
            .Append(ReportFormatting.Kebab(selection.CoauthorPolicy)).AppendLine("`");
    }

    private static void AppendPullRequestVerification(
        StringBuilder markdown,
        IReadOnlyList<ChangePortfolioItemEstimate> items)
    {
        ChangePortfolioItemEstimate[] pullRequests =
        [
            .. items.Where(item => item.Selection.PullRequest is not null),
        ];
        if (pullRequests.Length == 0)
        {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Pull-request comparison verification");
        markdown.AppendLine();
        markdown.AppendLine("| Change | Provider base | Comparison base | Head | Policy | Acquisition | Provider files | Analyzed paths | Represented paths | Status |");
        markdown.AppendLine("| --- | --- | --- | --- | --- | --- | ---: | ---: | ---: | --- |");
        foreach (ChangePortfolioItemEstimate item in pullRequests)
        {
            PullRequestReference pullRequest = item.Selection.PullRequest!;
            markdown.Append("| `").Append(ReportFormatting.Escape(DisplaySelectorId(item.SelectorId)))
                .Append("` | `").Append(ReportFormatting.Escape(ShortObject(
                    pullRequest.ProviderBaseObjectId ?? "unavailable")))
                .Append("` | `").Append(ReportFormatting.Escape(ShortObject(item.Selection.Base.ObjectId)))
                .Append("` | `").Append(ReportFormatting.Escape(ShortObject(item.Selection.Head.ObjectId)))
                .Append("` | ").Append(pullRequest.ComparisonBasePolicy is null
                    ? "unavailable"
                    : ReportFormatting.Kebab(pullRequest.ComparisonBasePolicy.Value))
                .Append(" | ").Append(pullRequest.ObjectAcquisition is null
                    ? "unavailable"
                    : ReportFormatting.Kebab(pullRequest.ObjectAcquisition.Value))
                .Append(" | ").Append(Count(pullRequest.ProviderChangedFileCount))
                .Append(" | ").Append(Count(pullRequest.AnalyzedChangedPathCount))
                .Append(" | ").Append(Count(pullRequest.RepresentedChangedPathCount))
                .Append(" | ").Append(pullRequest.PathCountStatus is null
                    ? "unavailable"
                    : ReportFormatting.Kebab(pullRequest.PathCountStatus.Value)).AppendLine(" |");
        }
    }

    private static string Count(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "unavailable";

    private static void AppendAuthorPeriodManifestSelection(
        StringBuilder markdown,
        ChangePortfolioAuthorPeriodManifestSelection selection)
    {
        markdown.AppendLine("  ");
        markdown.Append("Manifest digest: `")
            .Append(ReportFormatting.Escape(selection.ManifestDigest)).AppendLine("`  ");
        markdown.Append("Contributor IDs: ")
            .AppendLine(string.Join(", ", selection.ContributorIds.Select(
                contributorId => $"`{ReportFormatting.Escape(contributorId)}`")) + "  ");
        markdown.Append("Interval: `")
            .Append(selection.SinceInclusive.ToString("O", CultureInfo.InvariantCulture)).Append("` through `")
            .Append(selection.UntilExclusive.ToString("O", CultureInfo.InvariantCulture))
            .AppendLine("` (start inclusive, end exclusive)  ");
        markdown.Append("Timezone: `").Append(ReportFormatting.Escape(selection.TimeZone)).Append("`; date field: `")
            .Append(ReportFormatting.Kebab(selection.DateField)).Append("`; merges: `")
            .Append(ReportFormatting.Kebab(selection.MergePolicy)).Append("`; co-authors: `")
            .Append(ReportFormatting.Kebab(selection.CoauthorPolicy)).AppendLine("`  ");
        markdown.Append("Pinned repositories: ")
            .AppendLine(string.Join(", ", selection.Repositories.Select(repository =>
                $"`{ReportFormatting.Escape(repository.Id)}` ({repository.Heads.Count} head(s))")) + "  ");
        markdown.Append("Pinned heads: ").AppendLine(string.Join(", ", selection.Repositories
            .SelectMany(repository => repository.Heads.Select(head =>
                $"`{ReportFormatting.Escape(repository.Id)}/{ReportFormatting.Escape(head.Id)}` " +
                $"(`{ReportFormatting.Escape(ShortObject(head.ObjectId))}`)"))));
    }

    private static void AppendCategories(
        StringBuilder markdown,
        IReadOnlyList<CategoryEstimate> categories)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Normalized categories");
        markdown.AppendLine();
        markdown.AppendLine("| Category | Low | Expected | High |");
        markdown.AppendLine("| --- | ---: | ---: | ---: |");
        foreach (CategoryEstimate category in categories)
        {
            AppendRange(markdown, ReportFormatting.Display(category.Category), category.Hours);
        }
    }

    private static void AppendAdjustments(
        StringBuilder markdown,
        IReadOnlyList<ChangePortfolioAdjustment> adjustments)
    {
        if (adjustments.Count == 0)
        {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Reconciliation adjustments");
        markdown.AppendLine();
        markdown.AppendLine("| Kind | Low delta | Expected delta | High delta | Paths | Reason |");
        markdown.AppendLine("| --- | ---: | ---: | ---: | ---: | --- |");
        foreach (ChangePortfolioAdjustment adjustment in adjustments)
        {
            markdown.Append("| ").Append(ReportFormatting.Kebab(adjustment.Kind)).Append(" | ")
                .Append(ReportFormatting.Hours(adjustment.EffortDelta.Low)).Append(" | ")
                .Append(ReportFormatting.Hours(adjustment.EffortDelta.Expected)).Append(" | ")
                .Append(ReportFormatting.Hours(adjustment.EffortDelta.High)).Append(" | ")
                .Append(adjustment.AffectedPathCount).Append(" | ")
                .Append(ReportFormatting.Escape(adjustment.Reason)).AppendLine(" |");
        }
    }

    private static void AppendUncertainty(StringBuilder markdown, ChangePortfolioReport report)
    {
        IEnumerable<string> values = report.RepositoryGroups
            .SelectMany(group => group.UncertaintyReasons)
            .Concat(report.Items.SelectMany(item => item.UncertaintyReasons));
        if (report.Aggregation is not null)
        {
            values = values
                .Concat(report.Aggregation.ContributorGroups.SelectMany(group => group.UncertaintyReasons))
                .Concat(report.Aggregation.Repositories.SelectMany(repository => repository.UncertaintyReasons))
                .Concat(report.Aggregation.Repositories
                    .SelectMany(repository => repository.HeadGroups)
                    .SelectMany(group => group.UncertaintyReasons));
        }

        string[] uncertainty = [.. values
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        AppendList(markdown, "Attribution uncertainty", uncertainty);
    }

    private static void AppendRange(StringBuilder markdown, string title, EffortRange range)
    {
        markdown.Append("| ").Append(ReportFormatting.Escape(title)).Append(" | ")
            .Append(ReportFormatting.Hours(range.Low)).Append(" | ")
            .Append(ReportFormatting.Hours(range.Expected)).Append(" | ")
            .Append(ReportFormatting.Hours(range.High)).AppendLine(" |");
    }

    private static void AppendList(StringBuilder markdown, string heading, IEnumerable<string> values)
    {
        string[] materialized = [.. values];
        if (materialized.Length == 0)
        {
            return;
        }

        markdown.AppendLine();
        markdown.Append("## ").AppendLine(heading);
        markdown.AppendLine();
        foreach (string value in materialized)
        {
            markdown.Append("- ").AppendLine(ReportFormatting.Escape(value));
        }
    }

    private static string Selector(ChangeSelection selection) => selection.Kind switch
    {
        ChangeSelectionKind.PullRequest =>
            $"PR #{selection.PullRequest!.Number} ({ShortObject(selection.Base.ObjectId)}..{ShortObject(selection.Head.ObjectId)})",
        ChangeSelectionKind.Commit => $"commit {ShortObject(selection.Head.ObjectId)}",
        _ => ReportFormatting.Kebab(selection.Kind),
    };

    private static string Attribution(ChangePortfolioAttribution attribution)
    {
        string kind = ReportFormatting.Kebab(attribution.Kind);
        if (attribution.ContributorMatches is null || attribution.HeadIds is null)
        {
            return kind;
        }

        string contributors = string.Join(", ", attribution.ContributorMatches.Select(match =>
            $"{match.ContributorId}/{ReportFormatting.Kebab(match.Kind)}"));
        return $"{kind}; {contributors}; heads: {string.Join(", ", attribution.HeadIds)}";
    }

    private static string Short(string value) => value.Length <= 12 ? value : value[^12..];

    private static string ShortObject(string value) => value.Length <= 12 ? value : value[..12];

    private static string DisplaySelectorId(string value) =>
        value.StartsWith("commit:", StringComparison.Ordinal) && value.Length > 19
            ? value[..19]
            : value;
}
