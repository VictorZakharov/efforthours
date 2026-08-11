using EffortHours.Contracts.V1;

namespace EffortHours.Estimation;

internal sealed partial class SeedCapabilityBuilder
{
    private void AddAccessibilitySurfaces(List<CapabilityUnit> capabilities)
    {
        foreach (FactAggregate aggregate in GroupByScope(EvidenceKinds.Accessibility))
        {
            decimal units = decimal.Max(1m, aggregate.Measurement("accessibility-units"));
            capabilities.Add(SemanticCapability(
                aggregate,
                "security-surface",
                "accessibility",
                $"Implement represented accessibility behavior for {DisplayScope(aggregate.Scope)}",
                Drivers(
                    ("security-surfaces", decimal.Max(1m, aggregate.NormalizedCount)),
                    ("security-configuration-calls", 0m),
                    ("authorization-attributes", 0m),
                    ("security-usages", units)),
                units,
                ComplexityLevel.Moderate,
                "The existing combined security/accessibility prior values explicit labels, alternative text, keyboard and focus behavior, ARIA semantics, and careful validation beyond residual markup construction.",
                ["Static signals establish represented accessibility work, not standards conformance or runtime behavior."]));
        }
    }
}
