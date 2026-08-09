using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static class EstimateViewMarkdownRenderer
{
    public static string Render(EstimateViewReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The estimate view is invalid: " + string.Join(" ", errors),
                nameof(report));
        }

        StringBuilder markdown = new();
        markdown.Append("# EffortHours ")
            .Append(ReportFormatting.Kebab(report.View))
            .Append(" view - ")
            .AppendLine(ReportFormatting.Escape(report.Repository.Name));
        markdown.AppendLine();
        markdown.AppendLine(
            "> Equivalent Human Effort is counterfactual replacement effort, not a record of hours actually worked.");
        markdown.AppendLine();
        markdown.Append("Profile: `").Append(ReportFormatting.Kebab(report.Profile)).AppendLine("`  ");
        markdown.Append("Estimator: `").Append(ReportFormatting.Escape(report.EstimatorVersion)).AppendLine("`  ");
        markdown.Append("Source digest: `")
            .Append(ReportFormatting.Escape(report.Repository.SourceDigest ?? "unavailable"))
            .AppendLine("`  ");
        markdown.Append("Verification: `")
            .Append(ReportFormatting.Kebab(report.Verification.Mode))
            .Append("` / `")
            .Append(ReportFormatting.Kebab(report.Verification.WorkingState))
            .AppendLine("`");

        AppendSummary(markdown, report);
        AppendCounts(markdown, report);
        AppendCategories(markdown, report.Categories);
        AppendScopes(markdown, report.Scopes);
        AppendCapabilities(markdown, "Capabilities", report.Capabilities);
        AppendCapabilities(markdown, "Review queue", report.ReviewQueue);
        AppendCapabilities(
            markdown,
            "Professionalization gap (excluded from EHE and cost)",
            report.ProfessionalizationGap);
        AppendOmissions(markdown, report.Omissions);
        AppendList(markdown, "Assumptions", report.Assumptions);
        AppendList(
            markdown,
            "Diagnostics",
            report.Diagnostics.Select(diagnostic =>
                $"`{diagnostic.Code}` ({ReportFormatting.Kebab(diagnostic.Severity)}): " +
                diagnostic.Message));

        if (!string.IsNullOrWhiteSpace(report.Verification.Note))
        {
            AppendList(markdown, "Verification note", [report.Verification.Note]);
        }

        return markdown.ToString().TrimEnd() + "\n";
    }

    private static void AppendSummary(StringBuilder markdown, EstimateViewReport report)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Summary");
        markdown.AppendLine();
        markdown.AppendLine("| Measure | Low | Expected | High |");
        markdown.AppendLine("| --- | ---: | ---: | ---: |");
        markdown.Append("| Human-hours | ")
            .Append(ReportFormatting.Hours(report.TotalEffort.Low)).Append(" | ")
            .Append(ReportFormatting.Hours(report.TotalEffort.Expected)).Append(" | ")
            .Append(ReportFormatting.Hours(report.TotalEffort.High)).AppendLine(" |");
        if (report.TotalCost is not null)
        {
            markdown.Append("| Replacement cost (")
                .Append(ReportFormatting.Escape(report.TotalCost.Currency)).Append(") | ")
                .Append(ReportFormatting.Money(report.TotalCost.Low)).Append(" | ")
                .Append(ReportFormatting.Money(report.TotalCost.Expected)).Append(" | ")
                .Append(ReportFormatting.Money(report.TotalCost.High)).AppendLine(" |");
        }

        if (report.RateCard is not null)
        {
            markdown.AppendLine();
            markdown.Append("Rate: `")
                .Append(ReportFormatting.Escape(report.RateCard.Id))
                .Append("` at ")
                .Append(ReportFormatting.Money(report.RateCard.HourlyRate))
                .Append(' ')
                .Append(ReportFormatting.Escape(report.RateCard.Currency))
                .AppendLine("/hour.  ");
            if (report.RateCard.MarketRange is not null)
            {
                markdown.Append("Published market reference: ")
                    .Append(ReportFormatting.Money(report.RateCard.MarketRange.Low)).Append(" / ")
                    .Append(ReportFormatting.Money(report.RateCard.MarketRange.Expected)).Append(" / ")
                    .Append(ReportFormatting.Money(report.RateCard.MarketRange.High)).Append(' ')
                    .Append(ReportFormatting.Escape(report.RateCard.Currency))
                    .AppendLine(" per hour.");
            }
        }
    }

    private static void AppendCounts(StringBuilder markdown, EstimateViewReport report)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Inventory");
        markdown.AppendLine();
        markdown.AppendLine("| Represented items | Capability groups | Scopes | Gap items |");
        markdown.AppendLine("| ---: | ---: | ---: | ---: |");
        markdown.Append("| ").Append(report.Counts.RepresentedWorkItems).Append(" | ")
            .Append(report.Counts.CapabilityGroups).Append(" | ")
            .Append(report.Counts.Scopes).Append(" | ")
            .Append(report.Counts.ProfessionalizationGapItems).AppendLine(" |");
    }

    private static void AppendCategories(
        StringBuilder markdown,
        IReadOnlyList<CategoryViewEntry> categories)
    {
        if (categories.Count == 0)
        {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Categories");
        markdown.AppendLine();
        markdown.AppendLine("| Category | Low | Expected | High | Confidence | Capabilities | Items |");
        markdown.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (CategoryViewEntry category in categories)
        {
            markdown.Append("| ").Append(ReportFormatting.Display(category.Category)).Append(" | ")
                .Append(ReportFormatting.Hours(category.Hours.Low)).Append(" | ")
                .Append(ReportFormatting.Hours(category.Hours.Expected)).Append(" | ")
                .Append(ReportFormatting.Hours(category.Hours.High)).Append(" | ")
                .Append(ReportFormatting.Percent(category.Confidence)).Append(" | ")
                .Append(category.CapabilityCount).Append(" | ")
                .Append(category.WorkItemCount).AppendLine(" |");
        }
    }

    private static void AppendScopes(
        StringBuilder markdown,
        IReadOnlyList<ScopeViewEntry> scopes)
    {
        if (scopes.Count == 0)
        {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Scopes");
        markdown.AppendLine();
        markdown.AppendLine("| Scope | Low | Expected | High | Confidence | Capabilities | Items |");
        markdown.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (ScopeViewEntry scope in scopes)
        {
            markdown.Append("| `").Append(ReportFormatting.Escape(scope.Scope)).Append("` | ")
                .Append(ReportFormatting.Hours(scope.Hours.Low)).Append(" | ")
                .Append(ReportFormatting.Hours(scope.Hours.Expected)).Append(" | ")
                .Append(ReportFormatting.Hours(scope.Hours.High)).Append(" | ")
                .Append(ReportFormatting.Percent(scope.Confidence)).Append(" | ")
                .Append(scope.CapabilityCount).Append(" | ")
                .Append(scope.WorkItemCount).AppendLine(" |");
        }
    }

    private static void AppendCapabilities(
        StringBuilder markdown,
        string heading,
        IReadOnlyList<CapabilityViewEntry> capabilities)
    {
        if (capabilities.Count == 0)
        {
            return;
        }

        markdown.AppendLine();
        markdown.Append("## ").AppendLine(heading);
        markdown.AppendLine();
        markdown.AppendLine("| Capability ID | Title | Scope | Category | Expected | Confidence | Parts | Review reason |");
        markdown.AppendLine("| --- | --- | --- | --- | ---: | ---: | ---: | --- |");
        foreach (CapabilityViewEntry capability in capabilities)
        {
            markdown.Append("| `").Append(ReportFormatting.Escape(capability.Id)).Append("` | ")
                .Append(ReportFormatting.Escape(capability.Title)).Append(" | `")
                .Append(ReportFormatting.Escape(capability.Scope)).Append("` | ")
                .Append(ReportFormatting.Display(capability.Category)).Append(" | ")
                .Append(ReportFormatting.Hours(capability.Hours.Expected)).Append(" | ")
                .Append(ReportFormatting.Percent(capability.Confidence)).Append(" | ")
                .Append(capability.WorkItemCount).Append(" | ")
                .Append(ReportFormatting.Escape(string.Join(", ", capability.ReviewReasons)))
                .AppendLine(" |");
        }
    }

    private static void AppendOmissions(StringBuilder markdown, ProjectionOmissions omissions)
    {
        if (omissions.ScopeCount == 0 && omissions.CapabilityCount == 0)
        {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Projection omissions");
        markdown.AppendLine();
        markdown.Append("This view omits ").Append(omissions.ScopeCount)
            .Append(" scope row(s) representing ")
            .Append(ReportFormatting.Hours(omissions.ScopeExpectedHours))
            .Append(" expected hours and ").Append(omissions.CapabilityCount)
            .Append(" capability row(s) representing ")
            .Append(ReportFormatting.Hours(omissions.CapabilityExpectedHours))
            .AppendLine(" expected hours. Totals remain complete; use another view or `explain` to drill down.");
    }

    private static void AppendList(
        StringBuilder markdown,
        string heading,
        IEnumerable<string> values)
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
