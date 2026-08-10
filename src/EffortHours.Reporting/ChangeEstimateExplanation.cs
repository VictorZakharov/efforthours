using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static class ChangeEstimateExplainer
{
    public static ChangeEstimateExplanation Explain(ChangeEstimateReport report, string requestedId)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedId);
        WorkItem? item = report.WorkItems
            .Concat(report.ProfessionalizationGap)
            .SingleOrDefault(candidate => string.Equals(candidate.Id, requestedId, StringComparison.Ordinal));
        if (item is not null)
        {
            return CreateExplanation(report, requestedId, [item], null, null, item.EvidenceIds);
        }

        ChangeNormalizationSummary? normalization = report.Reconciliation.Normalization;
        if (normalization is null ||
            !string.Equals(normalization.Id, requestedId, StringComparison.Ordinal))
        {
            throw new KeyNotFoundException($"Change explanation target '{requestedId}' was not found.");
        }

        HashSet<string> adjustmentIds = normalization.SharedOrRepeatedAdjustmentIds
            .Concat(normalization.OverlapAdjustmentIds)
            .Concat(normalization.RevertAdjustmentIds)
            .Concat(normalization.ResidualInteractionAdjustmentIds)
            .Concat(normalization.PositiveInteractionAdjustmentIds)
            .ToHashSet(StringComparer.Ordinal);
        ChangeAdjustment[] adjustments = [.. report.Reconciliation.Adjustments
            .Where(adjustment => adjustmentIds.Contains(adjustment.Id))
            .OrderBy(adjustment => adjustment.Id, StringComparer.Ordinal)];
        return CreateExplanation(
            report,
            requestedId,
            [],
            normalization,
            adjustments,
            adjustments.SelectMany(adjustment => adjustment.EvidenceIds));
    }

    private static ChangeEstimateExplanation CreateExplanation(
        ChangeEstimateReport report,
        string requestedId,
        IReadOnlyList<WorkItem> workItems,
        ChangeNormalizationSummary? normalization,
        IReadOnlyList<ChangeAdjustment>? adjustments,
        IEnumerable<string> referencedEvidenceIds)
    {
        Dictionary<string, ChangePathEvidence> evidence = report.Evidence.Paths
            .ToDictionary(path => path.Id, StringComparer.Ordinal);
        string[] references = [.. referencedEvidenceIds
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        ChangePathEvidence[] resolved = [.. references
            .Where(evidence.ContainsKey)
            .Select(id => evidence[id])
            .OrderBy(path => path.Id, StringComparer.Ordinal)];
        string[] unresolved = [.. references
            .Where(id => !evidence.ContainsKey(id))
            .Order(StringComparer.Ordinal)];
        ChangeEstimateExplanation explanation = new()
        {
            EstimatorVersion = report.EstimatorVersion,
            Selection = report.Selection,
            RequestedId = requestedId,
            WorkItems = workItems,
            Normalization = normalization,
            Adjustments = adjustments,
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

        if (explanation.Normalization is not null)
        {
            return RenderNormalization(explanation);
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

    private static string RenderNormalization(ChangeEstimateExplanation explanation)
    {
        ChangeNormalizationSummary summary = explanation.Normalization!;
        StringBuilder markdown = new();
        markdown.AppendLine("# Change normalization explanation");
        markdown.AppendLine();
        markdown.Append("Requested ID: `")
            .Append(ReportFormatting.Escape(explanation.RequestedId)).AppendLine("`  ");
        markdown.Append("Calculation: `")
            .Append(ReportFormatting.Escape(summary.CalculationMethod)).AppendLine("`");
        markdown.AppendLine();
        markdown.AppendLine(
            "> This is a structural gross-to-final diagnostic, not historical rework, actual labor, or a productivity score.");
        markdown.AppendLine();
        markdown.AppendLine("| Measure | Expected hours | Share of gross isolated EHE |");
        markdown.AppendLine("| --- | ---: | ---: |");
        AppendNormalizationRow(
            markdown,
            "Gross-to-final normalization",
            summary.ExpectedGrossToFinalNormalizationHours,
            summary.ExpectedGrossToFinalNormalizationShare);
        AppendNormalizationRow(
            markdown,
            "Rework-like (overlap + revert only)",
            summary.ExpectedReworkLikeHours,
            summary.ExpectedReworkLikeShare);
        AppendNormalizationRow(
            markdown,
            "Other normalization",
            summary.ExpectedOtherNormalizationHours,
            summary.ExpectedOtherNormalizationShare);
        markdown.AppendLine();
        markdown.AppendLine("| Contribution | Expected-hour magnitude | Adjustment IDs |");
        markdown.AppendLine("| --- | ---: | --- |");
        AppendContributionRow(
            markdown,
            "Shared or repeated capability work",
            summary.ExpectedSharedOrRepeatedHours,
            summary.SharedOrRepeatedAdjustmentIds);
        AppendContributionRow(
            markdown,
            "Overlapping path work",
            summary.ExpectedOverlapHours,
            summary.OverlapAdjustmentIds);
        AppendContributionRow(
            markdown,
            "Reverted final effects",
            summary.ExpectedRevertHours,
            summary.RevertAdjustmentIds);
        AppendContributionRow(
            markdown,
            "Residual interaction",
            summary.ExpectedResidualInteractionHours,
            summary.ResidualInteractionAdjustmentIds);
        if (summary.ExpectedPositiveInteractionHours > 0m)
        {
            AppendContributionRow(
                markdown,
                "Positive interaction adjustment",
                summary.ExpectedPositiveInteractionHours,
                summary.PositiveInteractionAdjustmentIds);
        }

        return markdown.ToString().TrimEnd() + "\n";
    }

    private static void AppendNormalizationRow(
        StringBuilder markdown,
        string label,
        decimal hours,
        decimal? share)
    {
        markdown.Append("| ").Append(label).Append(" | ")
            .Append(ReportFormatting.Hours(hours)).Append(" | ")
            .Append(share is null ? "not applicable" : ReportFormatting.SharePercent(share.Value))
            .AppendLine(" |");
    }

    private static void AppendContributionRow(
        StringBuilder markdown,
        string label,
        decimal hours,
        IReadOnlyList<string> adjustmentIds)
    {
        markdown.Append("| ").Append(label).Append(" | ")
            .Append(ReportFormatting.Hours(hours)).Append(" | ")
            .Append(ReportFormatting.Escape(string.Join(", ", adjustmentIds)))
            .AppendLine(" |");
    }
}
