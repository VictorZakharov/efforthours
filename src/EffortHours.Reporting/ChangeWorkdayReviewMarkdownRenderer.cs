using System.Globalization;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static class ChangeWorkdayReviewMarkdownRenderer
{
    public static string Render(ChangeWorkdayReviewReport report)
    {
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors), nameof(report));
        StringBuilder text = new();
        text.AppendLine("# Retained workday evidence review").AppendLine();
        text.Append("Status: **").Append(report.Status).AppendLine("**.").AppendLine();
        text.AppendLine(report.Boundary).AppendLine();
        text.Append("Local date timezone: `").Append(report.TimeZone.Replace("`", "\\`", StringComparison.Ordinal)).AppendLine("`.");
        text.Append("Source semantic digest: `").Append(report.SourceSemanticDigest).AppendLine("`.");
        text.Append("Entry policy: `").Append(report.EntryPolicy ?? "none; values unavailable").AppendLine("`.").AppendLine();
        text.AppendLine("| Date | Review state | Retained evidence | Source attributed EHE | Matched EHE/8 |");
        text.AppendLine("| --- | --- | --- | ---: | ---: |");
        foreach (ChangeWorkdayReviewDay day in report.Days)
            text.Append("| ").Append(day.Date).Append(" | ").Append(day.Status).Append(" | ").Append(day.RetainedEvidenceStatus)
                .Append(" | ").Append(Number(day.SourceAttributedExpectedHours)).Append(" | ").Append(Number(day.MatchedDailyMultiplier)).AppendLine(" |");
        text.AppendLine().AppendLine("Original workdays remain unresolved for every row, including rows with retained attribution.").AppendLine();
        text.AppendLine("| Date | Public record | Kind | Review state | Allocated multiplier contribution |");
        text.AppendLine("| --- | --- | --- | --- | ---: |");
        foreach (ChangeWorkdayReviewDay day in report.Days)
            foreach (ChangeWorkRecordReview record in day.Records)
                text.Append("| ").Append(day.Date).Append(" | ").Append(record.RecordId).Append(" | ").Append(record.Kind)
                    .Append(" | ").Append(record.Status).Append(" | ").Append(Number(record.AllocatedMultiplierContribution)).AppendLine(" |");
        return text.ToString().ReplaceLineEndings("\n");
    }

    private static string Number(decimal? value) => value?.ToString("0.00", CultureInfo.InvariantCulture) ?? "unavailable";
}
