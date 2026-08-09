using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static class EstimateExplanationMarkdownRenderer
{
    public static string Render(EstimateExplanation explanation)
    {
        ArgumentNullException.ThrowIfNull(explanation);
        IReadOnlyList<string> errors = ContractValidation.Validate(explanation);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The estimate explanation is invalid: " + string.Join(" ", errors),
                nameof(explanation));
        }

        StringBuilder markdown = new();
        markdown.Append("# EffortHours explanation - `")
            .Append(ReportFormatting.Escape(explanation.RequestedId))
            .AppendLine("`");
        markdown.AppendLine();
        markdown.Append("Match: `").Append(ReportFormatting.Kebab(explanation.MatchKind))
            .AppendLine("`  ");
        markdown.Append("Repository: `").Append(ReportFormatting.Escape(explanation.Repository.Name))
            .AppendLine("`  ");
        markdown.Append("Profile: `").Append(ReportFormatting.Kebab(explanation.Profile))
            .AppendLine("`  ");
        markdown.Append("Estimator: `").Append(ReportFormatting.Escape(explanation.EstimatorVersion))
            .AppendLine("`");

        CapabilityViewEntry capability = explanation.Capability;
        markdown.AppendLine();
        markdown.AppendLine("## Capability");
        markdown.AppendLine();
        markdown.AppendLine("| Title | Scope | Category | Low | Expected | High | Confidence | Parts | Evidence facts |");
        markdown.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        markdown.Append("| ").Append(ReportFormatting.Escape(capability.Title)).Append(" | `")
            .Append(ReportFormatting.Escape(capability.Scope)).Append("` | ")
            .Append(ReportFormatting.Display(capability.Category)).Append(" | ")
            .Append(ReportFormatting.Hours(capability.Hours.Low)).Append(" | ")
            .Append(ReportFormatting.Hours(capability.Hours.Expected)).Append(" | ")
            .Append(ReportFormatting.Hours(capability.Hours.High)).Append(" | ")
            .Append(ReportFormatting.Percent(capability.Confidence)).Append(" | ")
            .Append(capability.WorkItemCount).Append(" | ")
            .Append(capability.EvidenceCount).AppendLine(" |");

        markdown.AppendLine();
        markdown.AppendLine("## Canonical work items");
        markdown.AppendLine();
        markdown.AppendLine("| Work-item ID | Expected | Confidence | Rule | Evidence IDs |");
        markdown.AppendLine("| --- | ---: | ---: | --- | --- |");
        foreach (WorkItem item in explanation.WorkItems)
        {
            markdown.Append("| `").Append(ReportFormatting.Escape(item.Id)).Append("` | ")
                .Append(ReportFormatting.Hours(item.Hours.Expected)).Append(" | ")
                .Append(ReportFormatting.Percent(item.Confidence)).Append(" | `")
                .Append(ReportFormatting.Escape(item.Estimator.Id)).Append('@')
                .Append(ReportFormatting.Escape(item.Estimator.Version)).Append("` | ")
                .Append(ReportFormatting.Escape(string.Join(", ", item.EvidenceIds)))
                .AppendLine(" |");
        }

        markdown.AppendLine();
        markdown.AppendLine("## Calculation reasons");
        markdown.AppendLine();
        foreach (WorkItem item in explanation.WorkItems)
        {
            markdown.Append("- `").Append(ReportFormatting.Escape(item.Id)).Append("`: ")
                .AppendLine(ReportFormatting.Escape(item.Reason));
        }

        if (explanation.EvidenceFacts.Count > 0)
        {
            markdown.AppendLine();
            markdown.AppendLine("## Evidence facts");
            markdown.AppendLine();
            markdown.AppendLine("| Evidence ID | Kind | Scope | Provenance | Summary |");
            markdown.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (EvidenceFact fact in explanation.EvidenceFacts)
            {
                markdown.Append("| `").Append(ReportFormatting.Escape(fact.Id)).Append("` | `")
                    .Append(ReportFormatting.Escape(fact.Kind)).Append("` | `")
                    .Append(ReportFormatting.Escape(fact.Scope)).Append("` | ")
                    .Append(ReportFormatting.Kebab(fact.Provenance.SourceKind)).Append(" / `")
                    .Append(ReportFormatting.Escape(fact.Provenance.Analyzer)).Append('@')
                    .Append(ReportFormatting.Escape(fact.Provenance.AnalyzerVersion)).Append("` | ")
                    .Append(ReportFormatting.Escape(fact.Summary)).AppendLine(" |");
            }
        }

        AppendList(markdown, "Missing evidence IDs", explanation.MissingEvidenceIds);
        AppendList(markdown, "Assumptions", explanation.Assumptions);
        AppendList(markdown, "Exclusions", explanation.Exclusions);
        AppendList(markdown, "Correlation groups", explanation.CorrelationGroups);
        AppendList(markdown, "Uncertainty", explanation.UncertaintyReasons);
        AppendList(
            markdown,
            "Diagnostics",
            explanation.Diagnostics.Select(diagnostic =>
                $"`{diagnostic.Code}` ({ReportFormatting.Kebab(diagnostic.Severity)}): " +
                diagnostic.Message));

        if (!string.IsNullOrWhiteSpace(explanation.Verification.Note))
        {
            AppendList(markdown, "Verification note", [explanation.Verification.Note]);
        }

        return markdown.ToString().TrimEnd() + "\n";
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
