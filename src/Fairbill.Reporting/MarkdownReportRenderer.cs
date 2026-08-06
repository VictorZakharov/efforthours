using System.Text;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Reporting;

public sealed class MarkdownReportRenderer : IReportRenderer
{
    public string Render(EstimateReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The estimate report is invalid: " + string.Join(" ", errors),
                nameof(report));
        }

        StringBuilder markdown = new();
        markdown.Append("# Fairbill estimate - ").AppendLine(ReportFormatting.Escape(report.Repository.Name));
        markdown.AppendLine();
        markdown.AppendLine("> Equivalent Human Effort is counterfactual replacement effort, not a record of hours actually worked.");
        markdown.AppendLine();
        markdown.Append("Profile: `").Append(ReportFormatting.Kebab(report.Profile)).AppendLine("`  ");
        markdown.Append("Estimator: `").Append(ReportFormatting.Escape(report.EstimatorVersion)).AppendLine("`  ");
        markdown.Append("Verification: `").Append(ReportFormatting.Kebab(report.Verification.Mode)).Append("` / `")
            .Append(ReportFormatting.Kebab(report.Verification.WorkingState)).AppendLine("`");
        markdown.AppendLine();

        markdown.AppendLine("## Summary");
        markdown.AppendLine();
        markdown.AppendLine("| Measure | Low | Expected | High |");
        markdown.AppendLine("| --- | ---: | ---: | ---: |");
        markdown.Append("| Human-hours | ").Append(ReportFormatting.Hours(report.TotalEffort.Low)).Append(" | ")
            .Append(ReportFormatting.Hours(report.TotalEffort.Expected)).Append(" | ")
            .Append(ReportFormatting.Hours(report.TotalEffort.High)).AppendLine(" |");

        if (report.TotalCost is not null)
        {
            markdown.Append("| Replacement cost (").Append(ReportFormatting.Escape(report.TotalCost.Currency)).Append(") | ")
                .Append(ReportFormatting.Money(report.TotalCost.Low)).Append(" | ")
                .Append(ReportFormatting.Money(report.TotalCost.Expected)).Append(" | ")
                .Append(ReportFormatting.Money(report.TotalCost.High)).AppendLine(" |");
        }

        markdown.AppendLine();
        markdown.AppendLine("## Categories");
        markdown.AppendLine();
        markdown.AppendLine("| Category | Low | Expected | High |");
        markdown.AppendLine("| --- | ---: | ---: | ---: |");
        foreach (CategoryEstimate category in report.Categories)
        {
            markdown.Append("| ").Append(ReportFormatting.Display(category.Category)).Append(" | ")
                .Append(ReportFormatting.Hours(category.Hours.Low)).Append(" | ")
                .Append(ReportFormatting.Hours(category.Hours.Expected)).Append(" | ")
                .Append(ReportFormatting.Hours(category.Hours.High)).AppendLine(" |");
        }

        markdown.AppendLine();
        markdown.AppendLine("## Work items");
        markdown.AppendLine();
        markdown.AppendLine("| Work item | Category | Expected | Confidence | Evidence |");
        markdown.AppendLine("| --- | --- | ---: | ---: | --- |");
        foreach (WorkItem item in report.WorkItems)
        {
            markdown.Append("| ").Append(ReportFormatting.Escape(item.Title)).Append(" | ")
                .Append(ReportFormatting.Display(item.Category)).Append(" | ")
                .Append(ReportFormatting.Hours(item.Hours.Expected)).Append(" | ")
                .Append(ReportFormatting.Percent(item.Confidence)).Append(" | ")
                .Append(ReportFormatting.Escape(string.Join(", ", item.EvidenceIds))).AppendLine(" |");
        }

        AppendList(markdown, "Assumptions", report.Assumptions);

        if (report.Diagnostics.Count > 0)
        {
            AppendList(
                markdown,
                "Diagnostics",
                report.Diagnostics.Select(diagnostic =>
                    $"`{diagnostic.Code}` ({ReportFormatting.Kebab(diagnostic.Severity)}): {diagnostic.Message}"));
        }

        if (report.ProfessionalizationGap.Count > 0)
        {
            markdown.AppendLine();
            markdown.AppendLine("## Professionalization gap (excluded from EHE and cost)");
            markdown.AppendLine();
            markdown.AppendLine("| Gap item | Category | Low | Expected | High | Evidence |");
            markdown.AppendLine("| --- | --- | ---: | ---: | ---: | --- |");
            foreach (WorkItem item in report.ProfessionalizationGap)
            {
                markdown.Append("| ").Append(ReportFormatting.Escape(item.Title)).Append(" | ")
                    .Append(ReportFormatting.Display(item.Category)).Append(" | ")
                    .Append(ReportFormatting.Hours(item.Hours.Low)).Append(" | ")
                    .Append(ReportFormatting.Hours(item.Hours.Expected)).Append(" | ")
                    .Append(ReportFormatting.Hours(item.Hours.High)).Append(" | ")
                    .Append(ReportFormatting.Escape(string.Join(", ", item.EvidenceIds))).AppendLine(" |");
            }
        }

        if (!string.IsNullOrWhiteSpace(report.Verification.Note))
        {
            AppendList(markdown, "Verification note", [report.Verification.Note]);
        }

        return markdown.ToString().TrimEnd() + "\n";
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
