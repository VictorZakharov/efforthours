using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class ChangeWorkItemBuilder
{
    private static bool HasSupportedCapabilityGrowth(Capability before, Capability after,
        Dictionary<string, EvidenceFact> baseFacts, Dictionary<string, EvidenceFact> headFacts,
        ChangePathEvidence[] paths)
    {
        bool tests = after.Category is EffortCategory.UnitTesting or EffortCategory.IntegrationContractAndComponentTesting or EffortCategory.EndToEndAndUiTesting;
        if (!tests && after.Category != EffortCategory.ProductionImplementation) return false;
        string[] measures = tests ? ["test-methods", "test-cases", "parameterized-cases", "assertions"] : ["methods", "functions", "types"];
        bool bound = after.EvidenceIds.Where(headFacts.ContainsKey).Select(id => headFacts[id]).Any(fact =>
            fact.Measurements.Any(measurement => measures.Contains(measurement.Name, StringComparer.Ordinal)) &&
            (FactTouches(fact, paths) || fact.Kind == EvidenceKinds.SourceStructure && fact.Scope == after.Scope &&
                paths.Any(path => InScope(path.Path, after.Scope))));
        if (!bound) return false;
        return measures.Any(name => Units(after, headFacts, name) > Units(before, baseFacts, name));

        static bool InScope(string path, string scope)
        {
            string normalized = NormalizePath(scope);
            int separator = normalized.LastIndexOf('/');
            string root = separator < 0 ? "" : normalized[..(separator + 1)];
            return path.StartsWith(root, StringComparison.Ordinal);
        }

        decimal Units(Capability capability, Dictionary<string, EvidenceFact> facts, string name) => capability.EvidenceIds
            .Where(facts.ContainsKey).Select(id => facts[id])
            .Where(fact => fact.Kind != EvidenceKinds.File)
            .Sum(fact => fact.Measurements.Where(measurement => measurement.Name == name).Sum(measurement => measurement.Value));
    }
}
