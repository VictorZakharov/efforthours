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
        bool declared = report.WorkdayResolution is not null;
        text.AppendLine(declared ? "# Declared workday evidence review" : "# Retained workday evidence review").AppendLine();
        text.Append("Status: **").Append(report.Status).AppendLine("**.").AppendLine();
        text.AppendLine(report.Boundary).AppendLine();
        text.Append("Local date timezone: `").Append(report.TimeZone.Replace("`", "\\`", StringComparison.Ordinal)).AppendLine("`.");
        text.Append("Source semantic digest: `").Append(report.SourceSemanticDigest).AppendLine("`.");
        text.Append("Entry policy: `").Append(report.EntryPolicy ?? "none; values unavailable").AppendLine("`.").AppendLine();
        if (report.WorkdayResolution is { } resolution)
        {
            text.Append("External declaration digest: `").Append(resolution.Allocation.WorkdayInputDigest).AppendLine("`.");
            text.Append("Conserved period EHE/8: ").Append(Number(resolution.ExpectedMultiplierTotal)).AppendLine(".");
            text.Append("Source declared events: ").Append(report.AttributionCompleteness?.DeclaredEventStatus ?? "unknown")
                .Append("; missing dates: ").Append(report.AttributionCompleteness?.MissingEventDateCount.ToString(CultureInfo.InvariantCulture) ?? "unknown")
                .Append("; missing replay baselines: ").Append(report.AttributionCompleteness?.MissingReplayBaselineCount.ToString(CultureInfo.InvariantCulture) ?? "unknown").AppendLine(".").AppendLine();
        }
        text.AppendLine(declared ? "| Date | Review state | Retained evidence | Source attributed EHE | Externally allocated EHE | Allocated EHE/8 |"
            : "| Date | Review state | Retained evidence | Source attributed EHE | Matched EHE/8 |");
        text.AppendLine(declared ? "| --- | --- | --- | ---: | ---: | ---: |" : "| --- | --- | --- | ---: | ---: |");
        foreach (ChangeWorkdayReviewDay day in report.Days)
        {
            text.Append("| ").Append(day.Date).Append(" | ").Append(day.Status).Append(" | ").Append(day.RetainedEvidenceStatus)
                .Append(" | ").Append(Number(day.SourceAttributedExpectedHours));
            if (declared) text.Append(" | ").Append(Number(day.AllocatedExpectedHours));
            text.Append(" | ").Append(Number(day.MatchedDailyMultiplier)).AppendLine(" |");
        }
        text.AppendLine().AppendLine(declared ? "Original Git workdays remain unresolved; external declarations supply the allocated dates."
            : "Original workdays remain unresolved for every row, including rows with retained attribution.").AppendLine();
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
