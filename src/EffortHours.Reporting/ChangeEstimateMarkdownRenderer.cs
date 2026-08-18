using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static class ChangeEstimateMarkdownRenderer
{
    public static string Render(ChangeEstimateReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The change estimate report is invalid: " + string.Join(" ", errors),
                nameof(report));
        }

        StringBuilder markdown = new();
        markdown.Append("# EffortHours change estimate - ")
            .AppendLine(ReportFormatting.Escape(report.Repository.Name));
        markdown.AppendLine();
        markdown.AppendLine(
            "> Change EHE is counterfactual non-AI replacement effort represented by the final artifact delta, not historical hours worked.");
        markdown.AppendLine();
        markdown.Append("Selection: `").Append(ReportFormatting.Kebab(report.Selection.Kind)).AppendLine("`  ");
        markdown.Append("Base: `").Append(ReportFormatting.Escape(report.Selection.Base.ObjectId)).AppendLine("`  ");
        markdown.Append("Head: `").Append(ReportFormatting.Escape(report.Selection.Head.ObjectId)).AppendLine("`  ");
        markdown.Append("Profile: `").Append(ReportFormatting.Kebab(report.Profile)).AppendLine("`  ");
        markdown.Append("Estimator: `").Append(ReportFormatting.Escape(report.EstimatorVersion)).AppendLine("`");
        if (report.Selection.PullRequest is not null)
        {
            AppendPullRequestVerification(markdown, report.Selection);
        }

        markdown.AppendLine();
        markdown.AppendLine("## Summary");
        markdown.AppendLine();
        markdown.AppendLine("| Measure | Low | Expected | High |");
        markdown.AppendLine("| --- | ---: | ---: | ---: |");
        AppendRangeRow(markdown, "Normalized Change EHE", report.TotalEffort);
        AppendRangeRow(markdown, "Isolated component sum", report.Reconciliation.IsolatedComponentSum);
        if (report.TotalCost is not null)
        {
            markdown.Append("| Equivalent replacement cost (")
                .Append(ReportFormatting.Escape(report.TotalCost.Currency)).Append(") | ")
                .Append(ReportFormatting.Money(report.TotalCost.Low)).Append(" | ")
                .Append(ReportFormatting.Money(report.TotalCost.Expected)).Append(" | ")
                .Append(ReportFormatting.Money(report.TotalCost.High)).AppendLine(" |");
        }

        markdown.AppendLine();
        markdown.Append("Additivity assessment: `")
            .Append(ReportFormatting.Escape(report.Reconciliation.Assessment)).Append("` (difference ")
            .Append(ReportFormatting.Hours(report.Reconciliation.ExpectedDifferenceHours))
            .Append("; tolerance ")
            .Append(ReportFormatting.Hours(report.Reconciliation.AdditivityToleranceHours))
            .AppendLine(")");

        if (report.Reconciliation.Normalization is not null)
        {
            AppendNormalization(markdown, report.Reconciliation.Normalization);
        }

        markdown.AppendLine();
        markdown.AppendLine("## Final path evidence");
        markdown.AppendLine();
        markdown.Append("Unchanged context paths: ")
            .Append(report.Evidence.UnchangedContextPathCount).AppendLine();
        markdown.AppendLine();
        markdown.AppendLine("| Status | Path | Classification | Regions | Represented | Reason |");
        markdown.AppendLine("| --- | --- | --- | ---: | --- | --- |");
        foreach (ChangePathEvidence path in report.Evidence.Paths)
        {
            markdown.Append("| ").Append(ReportFormatting.Kebab(path.Status)).Append(" | ")
                .Append(ReportFormatting.Escape(path.PreviousPath is null
                    ? path.Path
                    : $"{path.PreviousPath} -> {path.Path}"))
                .Append(" | ").Append(ReportFormatting.Kebab(path.Classification)).Append(" | ")
                .Append(path.EditRegions).Append(" | ")
                .Append(path.Represented ? "yes" : "no").Append(" | ")
                .Append(ReportFormatting.Escape(path.Reason)).AppendLine(" |");
        }

        markdown.AppendLine();
        markdown.AppendLine("## Categories");
        markdown.AppendLine();
        markdown.AppendLine("| Category | Low | Expected | High |");
        markdown.AppendLine("| --- | ---: | ---: | ---: |");
        foreach (CategoryEstimate category in report.Categories)
        {
            AppendRangeRow(markdown, ReportFormatting.Display(category.Category), category.Hours);
        }

        markdown.AppendLine();
        markdown.AppendLine("## Work items");
        markdown.AppendLine();
        markdown.AppendLine("| ID | Work item | Category | Expected | Confidence | Evidence |");
        markdown.AppendLine("| --- | --- | --- | ---: | ---: | --- |");
        foreach (WorkItem item in report.WorkItems)
        {
            markdown.Append("| `").Append(ReportFormatting.Escape(item.Id)).Append("` | ")
                .Append(ReportFormatting.Escape(item.Title)).Append(" | ")
                .Append(ReportFormatting.Display(item.Category)).Append(" | ")
                .Append(ReportFormatting.Hours(item.Hours.Expected)).Append(" | ")
                .Append(ReportFormatting.Percent(item.Confidence)).Append(" | ")
                .Append(ReportFormatting.Escape(string.Join(", ", item.EvidenceIds))).AppendLine(" |");
        }

        markdown.AppendLine();
        markdown.AppendLine("## Reconciliation and allocations");
        markdown.AppendLine();
        markdown.AppendLine("| Component | Kind | Isolated expected | Allocated normalized expected |");
        markdown.AppendLine("| --- | --- | ---: | ---: |");
        foreach (ChangeComponentEstimate component in report.Reconciliation.Components)
        {
            markdown.Append("| `").Append(ReportFormatting.Escape(component.Selector)).Append("` | ")
                .Append(ReportFormatting.Kebab(component.Kind)).Append(" | ")
                .Append(ReportFormatting.Hours(component.IsolatedEffort.Expected)).Append(" | ")
                .Append(ReportFormatting.Hours(component.AllocatedExpectedHours)).AppendLine(" |");
        }

        if (report.Reconciliation.Adjustments.Count > 0)
        {
            markdown.AppendLine();
            markdown.AppendLine("| Adjustment ID | Kind | Low delta | Expected delta | High delta | Reason |");
            markdown.AppendLine("| --- | --- | ---: | ---: | ---: | --- |");
            foreach (ChangeAdjustment adjustment in report.Reconciliation.Adjustments)
            {
                markdown.Append("| `").Append(ReportFormatting.Escape(adjustment.Id)).Append("` | ")
                    .Append(ReportFormatting.Kebab(adjustment.Kind)).Append(" | ")
                    .Append(ReportFormatting.Hours(adjustment.EffortDelta.Low)).Append(" | ")
                    .Append(ReportFormatting.Hours(adjustment.EffortDelta.Expected)).Append(" | ")
                    .Append(ReportFormatting.Hours(adjustment.EffortDelta.High)).Append(" | ")
                    .Append(ReportFormatting.Escape(adjustment.Reason)).AppendLine(" |");
            }
        }

        AppendList(markdown, "Assumptions", report.Assumptions);
        if (report.Diagnostics.Count > 0)
        {
            AppendList(markdown, "Diagnostics", report.Diagnostics.Select(diagnostic =>
                $"`{diagnostic.Code}` ({ReportFormatting.Kebab(diagnostic.Severity)}): {diagnostic.Message}"));
        }

        if (!string.IsNullOrWhiteSpace(report.Verification.Note))
        {
            AppendList(markdown, "Verification note", [report.Verification.Note]);
        }

        return markdown.ToString().TrimEnd() + "\n";
    }

    private static void AppendNormalization(
        StringBuilder markdown,
        ChangeNormalizationSummary summary)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Gross-to-final normalization");
        markdown.AppendLine();
        markdown.AppendLine(
            "> These percentages diagnose structural normalization in an explicit multi-commit range. They are not historical rework, actual labor, a productivity score, or effort multipliers.");
        markdown.AppendLine();
        markdown.Append("Explanation ID: `")
            .Append(ReportFormatting.Escape(summary.Id)).AppendLine("`  ");
        markdown.AppendLine(
            "Shares use gross isolated expected EHE as the denominator and four-decimal structured rounding. Low/high planning bounds remain dependent hour ranges and are not converted to percentages.");
        markdown.AppendLine();
        markdown.AppendLine("| Measure | Expected hours | Share of gross isolated EHE |");
        markdown.AppendLine("| --- | ---: | ---: |");
        AppendNormalizationRow(markdown, "Gross isolated EHE", summary.GrossIsolatedEffort.Expected, null);
        AppendNormalizationRow(
            markdown,
            "Normalized final-delta EHE (authoritative)",
            summary.NormalizedFinalDeltaEffort.Expected,
            null);
        AppendNormalizationRow(
            markdown,
            "Gross-to-final normalization",
            summary.ExpectedGrossToFinalNormalizationHours,
            summary.ExpectedGrossToFinalNormalizationShare);
        AppendNormalizationRow(
            markdown,
            "Rework-like (explicit overlap + revert only)",
            summary.ExpectedReworkLikeHours,
            summary.ExpectedReworkLikeShare);
        AppendNormalizationRow(
            markdown,
            "Other normalization",
            summary.ExpectedOtherNormalizationHours,
            summary.ExpectedOtherNormalizationShare);
        if (summary.ExpectedPositiveInteractionHours > 0m)
        {
            AppendNormalizationRow(
                markdown,
                "Positive interaction adjustment",
                summary.ExpectedPositiveInteractionHours,
                null);
        }
    }

    private static void AppendPullRequestVerification(
        StringBuilder markdown,
        ChangeSelection selection)
    {
        PullRequestReference pullRequest = selection.PullRequest!;
        markdown.AppendLine("  ");
        markdown.Append("PR provider base: `")
            .Append(ReportFormatting.Escape(pullRequest.ProviderBaseObjectId ?? "unavailable"))
            .Append("`; comparison base: `")
            .Append(ReportFormatting.Escape(selection.Base.ObjectId))
            .Append("`; head: `")
            .Append(ReportFormatting.Escape(selection.Head.ObjectId)).AppendLine("`  ");
        markdown.Append("PR comparison policy: `")
            .Append(pullRequest.ComparisonBasePolicy is null
                ? "unavailable"
                : ReportFormatting.Kebab(pullRequest.ComparisonBasePolicy.Value))
            .Append("`; object acquisition: `")
            .Append(pullRequest.ObjectAcquisition is null
                ? "unavailable"
                : ReportFormatting.Kebab(pullRequest.ObjectAcquisition.Value)).AppendLine("`  ");
        markdown.Append("PR changed paths: provider `")
            .Append(Count(pullRequest.ProviderChangedFileCount))
            .Append("`; analyzed `").Append(Count(pullRequest.AnalyzedChangedPathCount))
            .Append("`; represented `").Append(Count(pullRequest.RepresentedChangedPathCount))
            .Append("`; status `")
            .Append(pullRequest.PathCountStatus is null
                ? "unavailable"
                : ReportFormatting.Kebab(pullRequest.PathCountStatus.Value)).AppendLine("`");
    }

    private static string Count(int? value) => value?.ToString(
        System.Globalization.CultureInfo.InvariantCulture) ?? "unavailable";

    private static void AppendNormalizationRow(
        StringBuilder markdown,
        string title,
        decimal hours,
        decimal? share)
    {
        markdown.Append("| ").Append(ReportFormatting.Escape(title)).Append(" | ")
            .Append(ReportFormatting.Hours(hours)).Append(" | ")
            .Append(share is null ? "—" : ReportFormatting.SharePercent(share.Value))
            .AppendLine(" |");
    }

    private static void AppendRangeRow(StringBuilder markdown, string title, EffortRange range)
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
}
