using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public static partial class ChangePortfolioComparisonBuilder
{
    private static IReadOnlyList<Diagnostic> ComparisonDiagnostics(
        ChangePortfolioComparisonBuildOptions options) =>
    [
        .. options.NativePeriod is { RetainedHistory: true } ? new[]
        {
            new Diagnostic
            {
                Code = "FB5341", Severity = DiagnosticSeverity.Warning,
                Message = "Retained-history discovery cannot recover discarded pre-rewrite objects or resolution event dates. Daily no-retained-change cells describe available evidence only; use an explicit manifest rewriteEvents pairing for event attribution. Missing historical events are not certified zero workdays.",
            },
        } : [],
        new Diagnostic
        {
            Code = "FB5330",
            Severity = DiagnosticSeverity.Information,
            Message = "Time buckets are one alternative decomposition of the jointly reconciled portfolio. Bucket and contributor counts do not multiply EHE.",
        },
        new Diagnostic
        {
            Code = "FB5331",
            Severity = DiagnosticSeverity.Warning,
            Message = options.CapacityManifest is null
                ? "No reference capacity was supplied; capacity ratios and trend statistics are omitted."
                : "Reference capacity is a caller-supplied comparison denominator, not recorded labor, productivity, compensation, or authorship evidence.",
        },
        new Diagnostic
        {
            Code = "FB5335",
            Severity = DiagnosticSeverity.Information,
            Message = options.ContributorNormalization == ChangePortfolioContributorNormalization.Joint
                ? "Contributor series use jointly normalized exact-match-set allocations and can change when report membership changes."
                : "Contributor series use membership-stable isolated commit estimates. They can overlap on shared commits, are not additive, and do not replace the jointly normalized portfolio total.",
        },
    ];

}
