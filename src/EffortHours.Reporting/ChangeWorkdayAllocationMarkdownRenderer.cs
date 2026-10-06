using System.Globalization;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static class ChangeWorkdayAllocationMarkdownRenderer
{
    public static string Render(ChangeWorkdayAllocationReport report)
    {
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors), nameof(report));
        StringBuilder text = new();
        text.AppendLine("# Declared workday EHE allocation").AppendLine();
        text.Append("Status: **allocated**; policy: `").Append(report.Policy).AppendLine("`.").AppendLine();
        text.AppendLine(report.Boundary).AppendLine();
        text.Append("Local date timezone: `").Append(report.TimeZone.Replace("`", "\\`", StringComparison.Ordinal)).AppendLine("`.");
        text.Append("Source semantic digest: `").Append(report.SourceSemanticDigest).AppendLine("`.");
        text.Append("Expected EHE conserved: **").Append(Number(report.TotalEffort.Expected)).Append(" hours**; reference capacity: **")
            .Append(report.TotalCapacityHours is { } hours ? Number(hours) : "unavailable").AppendLine(" hours**.").AppendLine();
        text.AppendLine("| Date | External declaration | Original workday | Source attributed EHE | Allocated EHE | Reference capacity | Allocated EHE/capacity |");
        text.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: |");
        foreach (ChangeWorkdayAllocationDay day in report.Days)
        {
            text.Append("| ").Append(day.Date).Append(" | ").Append(day.RecordId ?? "none")
                .Append(" | unresolved | ").Append(Number(day.SourceAttributedEffort.Expected)).Append(" | ")
                .Append(Number(day.AllocatedEffort.Expected)).Append(" | ")
                .Append(day.CapacityHours is { } capacity ? Number(capacity) : "unavailable").Append(" | ")
                .Append(day.CapacityRatio is { } ratio ? Number(ratio.Expected) : "unavailable").AppendLine(" |");
        }
        return text.ToString().ReplaceLineEndings("\n");
    }

    private static string Number(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
