using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static class ChangePortfolioReplayDiagnostics
{
    public static IEnumerable<Diagnostic> Create(ChangePortfolioReplayEvent declaration)
    {
        if (declaration.EventTimestamp is null) yield return new Diagnostic
        {
            Code = "FB5344",
            Severity = DiagnosticSeverity.Warning,
            Message = "Replay event date is unavailable: event-day attribution is unresolved. Retained-code dates and blank buckets do not establish zero integration labor or recover lost workdays.",
        };
        if (declaration.ReplayObjectId is null) yield return new Diagnostic
        {
            Code = "FB5345",
            Severity = DiagnosticSeverity.Warning,
            Message = "Replay baseline is unavailable: novel event attribution is unresolved. Ordinary retained-code allocation does not establish resolution effort.",
        };
    }
}
