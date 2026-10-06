using System.Globalization;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static class ChangeRewriteReviewMarkdownRenderer
{
    public static string Render(ChangeRewriteReviewReport report)
    {
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
        StringBuilder text = new();
        text.AppendLine("# Immutable rewrite replay review").AppendLine();
        text.AppendLine(report.Boundary).AppendLine();
        text.Append("Replay confidence: `").Append(report.ReplayConfidence).AppendLine("`.");
        text.Append("Event attribution: `").Append(report.EventAttributionStatus).AppendLine("`.");
        text.Append("Replay provenance: `").Append(report.ReplayProvenanceId ?? "unavailable").AppendLine("`.");
        text.Append("Event provenance: `").Append(report.EventProvenanceId ?? "unavailable").AppendLine("`.");
        text.Append("Event instant: `").Append(report.EventTimestamp?.ToString("O", CultureInfo.InvariantCulture) ?? "unresolved").AppendLine("`.");
        text.Append("Period novel expected EHE: ").Append(report.EventAttributedNovelEffort?.Expected.ToString("0.00", CultureInfo.InvariantCulture) ?? "unavailable").AppendLine(".").AppendLine();
        if (report.MissingObjectIds is not null)
            text.AppendLine("Required immutable objects are unavailable. Restore the declared objects outside this offline review; attribution remains unresolved.").AppendLine();
        text.AppendLine("| Non-additive comparison | Low EHE | Expected EHE | High EHE |");
        text.AppendLine("| --- | ---: | ---: | ---: |");
        foreach (ChangeRewriteComparison comparison in report.Comparisons)
            text.Append("| ").Append(comparison.Role).Append(" | ").Append(Number(comparison.Effort.Low)).Append(" | ")
                .Append(Number(comparison.Effort.Expected)).Append(" | ").Append(Number(comparison.Effort.High)).AppendLine(" |");
        text.AppendLine().AppendLine("Only retained-feature describes the final feature relative to the new upstream. Do not sum these comparisons or add this review to a portfolio.");
        return text.ToString().ReplaceLineEndings("\n");
    }

    private static string Number(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
