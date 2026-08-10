using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class ChangeWorkItemBuilder
{
    private static bool SupportsCapabilityDelta(
        EffortCategory category,
        Capability? baseCapability,
        Capability? headCapability)
    {
        if (category is
            EffortCategory.SpecificationComprehensionAndDomainLearning or
            EffortCategory.ManualValidationDebuggingAndHardening or
            EffortCategory.SelfReviewAndSystemIntegration)
        {
            return false;
        }

        return category is not (
            EffortCategory.RepositoryAndSolutionSetup or
            EffortCategory.ArchitectureAndTechnicalDesign) ||
            baseCapability is null ||
            headCapability is null;
    }

    private static ChangePathEvidence[] DistinctLogicalPaths(
        IEnumerable<ChangePathEvidence> touched) =>
        [.. touched.DistinctBy(path => path.Id, StringComparer.Ordinal)];

    private static bool HasMaterialCapabilityEvidenceChange(
        Capability baseCapability,
        Capability headCapability,
        Dictionary<string, EvidenceFact> baseFacts,
        Dictionary<string, EvidenceFact> headFacts,
        IReadOnlyList<ChangePathEvidence> touched)
    {
        foreach (string evidenceId in baseCapability.EvidenceIds
            .Union(headCapability.EvidenceIds, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            baseFacts.TryGetValue(evidenceId, out EvidenceFact? baseFact);
            headFacts.TryGetValue(evidenceId, out EvidenceFact? headFact);
            EvidenceFact? fact = headFact ?? baseFact;
            if (fact is null || fact.Kind == EvidenceKinds.File || !FactTouches(fact, touched))
            {
                continue;
            }

            if (baseFact is null || headFact is null || !EquivalentValuingEvidence(baseFact, headFact))
            {
                return true;
            }
        }

        return false;
    }

    private static bool FactTouches(
        EvidenceFact fact,
        IReadOnlyList<ChangePathEvidence> touched)
    {
        HashSet<string> paths = FactPaths(fact).ToHashSet(StringComparer.Ordinal);
        return touched.Any(path => Touches(paths, path));
    }

    private static bool EquivalentValuingEvidence(EvidenceFact left, EvidenceFact right) =>
        left.Kind == right.Kind &&
        left.Scope == right.Scope &&
        left.Measurements
            .OrderBy(measurement => measurement.Name, StringComparer.Ordinal)
            .ThenBy(measurement => measurement.Unit, StringComparer.Ordinal)
            .SequenceEqual(right.Measurements
                .OrderBy(measurement => measurement.Name, StringComparer.Ordinal)
                .ThenBy(measurement => measurement.Unit, StringComparer.Ordinal)) &&
        left.Tags.Order(StringComparer.Ordinal).SequenceEqual(right.Tags.Order(StringComparer.Ordinal));

    private static decimal StatusFactor(ChangePathStatus status) => status switch
    {
        ChangePathStatus.Modified => 0.3m,
        ChangePathStatus.Removed => 0.75m,
        _ => 1m,
    };
}
