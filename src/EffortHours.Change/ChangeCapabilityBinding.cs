using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class ChangeWorkItemBuilder
{
    private static bool IsSourceBackbone(string rule) => rule is
        "seed-rule:dotnet-source-backbone" or
        "seed-rule:javascript-source-backbone" or
        "seed-rule:polyglot-source-backbone";

    private static HashSet<string> SourceBackboneTestPaths(IEnumerable<EvidenceFact> facts) => facts
        .Where(fact => fact.Kind is EvidenceKinds.DotNetTest or EvidenceKinds.JavaScriptTest or EvidenceKinds.EcosystemTest ||
            fact.Kind == EvidenceKinds.File &&
            (fact.Tags.Contains("role:test", StringComparer.Ordinal) ||
                fact.Tags.Contains("classification:test", StringComparer.Ordinal)))
        .SelectMany(FactPaths).ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<Diagnostic> UnboundGrowthDiagnostics(int count)
    {
        if (count > 0)
        {
            yield return new Diagnostic
            {
                Code = "FB5211",
                Severity = DiagnosticSeverity.Warning,
                Message = "Positive stock capabilities without represented path lineage were not charged " +
                    "to unrelated change paths; unboundCapabilities=" + count.ToString(CultureInfo.InvariantCulture) +
                    ". Unexplained maintained paths retain the ordinary fallback budget.",
            };
        }
    }
}
