using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

internal static class ChangePortfolioAttributionMarkdown
{
    public static void Append(StringBuilder text, ChangePortfolioComparisonReport report)
    {
        if (report.AttributionCompleteness is not { } value ||
            value.DeclaredEventStatus == "not-declared" && report.NativePeriod?.RetainedHistory != true) return;
        text.AppendLine();
        text.Append("Declared event attribution: **").Append(value.DeclaredEventStatus).Append("**; missing dates: ")
            .Append(value.MissingEventDateCount).Append("; missing replay baselines: ").Append(value.MissingReplayBaselineCount).AppendLine(".");
        text.AppendLine("Original workdays remain unresolved; intermediate-history availability is unknown. Retained-code dates, declared replay events and external allocated days are separate evidence. A zero EHE cell never establishes zero integration labor.");
        foreach (Diagnostic warning in report.Diagnostics.Where(value => value.Code is "FB5340" or "FB5341" or "FB5344" or "FB5345"))
            text.Append("- `").Append(warning.Code).Append("`: ").AppendLine(warning.Message.ReplaceLineEndings(" "));
    }
}
