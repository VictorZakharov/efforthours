using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static class ChangeEstimateExplainer
{
    public static ChangeEstimateExplanation Explain(ChangeEstimateReport report, string itemId)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        WorkItem item = report.WorkItems
            .Concat(report.ProfessionalizationGap)
            .SingleOrDefault(candidate => string.Equals(candidate.Id, itemId, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Change work item '{itemId}' was not found.");
        Dictionary<string, ChangePathEvidence> evidence = report.Evidence.Paths
            .ToDictionary(path => path.Id, StringComparer.Ordinal);
        ChangePathEvidence[] resolved = [.. item.EvidenceIds
            .Where(evidence.ContainsKey)
            .Select(id => evidence[id])
            .OrderBy(path => path.Id, StringComparer.Ordinal)];
        string[] unresolved = [.. item.EvidenceIds
            .Where(id => !evidence.ContainsKey(id))
            .Order(StringComparer.Ordinal)];
        ChangeEstimateExplanation explanation = new()
        {
            EstimatorVersion = report.EstimatorVersion,
            Selection = report.Selection,
            RequestedId = itemId,
            WorkItems = [item],
            Evidence = resolved,
            UnresolvedEvidenceIds = unresolved,
        };
        IReadOnlyList<string> errors = ContractValidation.Validate(explanation);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "The change explanation is invalid: " + string.Join(" ", errors));
        }

        return explanation;
    }
}

public sealed class ChangeEstimateExplanationJsonRenderer(bool compact = false)
{
    public string Render(ChangeEstimateExplanation explanation)
    {
        ArgumentNullException.ThrowIfNull(explanation);
        IReadOnlyList<string> errors = ContractValidation.Validate(explanation);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The change explanation is invalid: " + string.Join(" ", errors),
                nameof(explanation));
        }

        string json = compact
            ? ContractJson.SerializeCompact(explanation)
            : ContractJson.Serialize(explanation);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangeEstimateExplanation,
            json);
        if (!schema.IsValid)
        {
            throw new InvalidOperationException(
                "The change explanation does not satisfy its JSON Schema: " +
                string.Join(" ", schema.Errors));
        }

        return json;
    }
}

public static class ChangeEstimateExplanationMarkdownRenderer
{
    public static string Render(ChangeEstimateExplanation explanation)
    {
        ArgumentNullException.ThrowIfNull(explanation);
        IReadOnlyList<string> errors = ContractValidation.Validate(explanation);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The change explanation is invalid: " + string.Join(" ", errors),
                nameof(explanation));
        }

        WorkItem item = explanation.WorkItems[0];
        StringBuilder markdown = new();
        markdown.Append("# Change estimate explanation - ")
            .AppendLine(ReportFormatting.Escape(item.Title));
        markdown.AppendLine();
        markdown.Append("Requested ID: `").Append(ReportFormatting.Escape(explanation.RequestedId)).AppendLine("`  ");
        markdown.Append("Rule: `").Append(ReportFormatting.Escape(item.Estimator.Id)).Append("` / `")
            .Append(ReportFormatting.Escape(item.Estimator.Version)).AppendLine("`");
        markdown.AppendLine();
        markdown.AppendLine("| Low | Expected | High | Confidence |");
        markdown.AppendLine("| ---: | ---: | ---: | ---: |");
        markdown.Append("| ").Append(ReportFormatting.Hours(item.Hours.Low)).Append(" | ")
            .Append(ReportFormatting.Hours(item.Hours.Expected)).Append(" | ")
            .Append(ReportFormatting.Hours(item.Hours.High)).Append(" | ")
            .Append(ReportFormatting.Percent(item.Confidence)).AppendLine(" |");
        markdown.AppendLine();
        markdown.Append("Reason: ").AppendLine(ReportFormatting.Escape(item.Reason));
        markdown.AppendLine();
        markdown.AppendLine("## Final path evidence");
        markdown.AppendLine();
        markdown.AppendLine("| ID | Status | Path | Classification | Regions |");
        markdown.AppendLine("| --- | --- | --- | --- | ---: |");
        foreach (ChangePathEvidence path in explanation.Evidence)
        {
            markdown.Append("| `").Append(ReportFormatting.Escape(path.Id)).Append("` | ")
                .Append(ReportFormatting.Kebab(path.Status)).Append(" | ")
                .Append(ReportFormatting.Escape(path.Path)).Append(" | ")
                .Append(ReportFormatting.Kebab(path.Classification)).Append(" | ")
                .Append(path.EditRegions).AppendLine(" |");
        }

        return markdown.ToString().TrimEnd() + "\n";
    }
}
