using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Estimation;

internal sealed partial class SeedCapabilityBuilder
{
    private void AddCoverageCapabilities(List<CapabilityUnit> capabilities)
    {
        foreach (IGrouping<string, EvidenceFact> group in _index.FactsOfKind(EvidenceKinds.Coverage)
            .Where(fact => fact.Measurements.Any(measurement => measurement.Unit == "percent"))
            .Where(fact => _index.FindScope(fact) is not null)
            .GroupBy(fact => _index.FindScope(fact)!.Id, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            EvidenceFact[] available = [.. group.OrderBy(fact => fact.Id, StringComparer.Ordinal)];
            EvidenceFact[] measured = [.. available.Where(fact =>
                fact.Provenance.SourceKind == EvidenceSourceKind.Measured ||
                fact.Tags.Contains("coverage:measured", StringComparer.Ordinal))];
            bool isMeasured = measured.Length > 0;
            EvidenceFact[] selected = isMeasured
                ? measured
                : [.. available.Where(fact => fact.Provenance.SourceKind == EvidenceSourceKind.DeclaredAssumed)];
            if (selected.Length == 0)
            {
                continue;
            }

            SeedEstimationScope scope = _index.FindScope(selected[0])!;
            decimal target = selected
                .SelectMany(fact => fact.Measurements)
                .Where(measurement => measurement.Unit == "percent")
                .Average(measurement => measurement.Value);
            decimal sourceExpected = _sourceExpectedByScope.GetValueOrDefault(scope.Id);
            decimal coveredImplementationHours = sourceExpected * target / 100m;
            capabilities.Add(new CapabilityUnit
            {
                Id = $"scope:{scope.Id}:{(isMeasured ? "measured" : "declared")}-coverage",
                RuleId = "coverage-achievement",
                Title = isMeasured
                    ? $"Recreate the measured coverage level for {DisplayScope(scope)}"
                    : $"Achieve the declared coverage level for {DisplayScope(scope)}",
                Scope = scope.Scope,
                Quantity = decimal.Max(0.01m, coveredImplementationHours),
                Drivers = Drivers(("covered-implementation-hours", coveredImplementationHours)),
                Complexity = target >= 100m
                    ? ComplexityLevel.High
                    : target >= 80m
                        ? ComplexityLevel.Moderate
                        : ComplexityLevel.Routine,
                Reason = CoverageReason(isMeasured, target),
                Evidence = selected,
                Profiles = BothProfiles,
                CorrelationGroup = $"scope:{scope.Id}:tests",
                Assumptions = CoverageAssumptions(isMeasured, available.Length > selected.Length),
                UncertaintyReasons = CoverageUncertainty(isMeasured),
                ConfidencePenalty = 0.08m,
            });
        }
    }

    private static string CoverageReason(bool isMeasured, decimal target) =>
        $"A {(isMeasured ? "measured" : "declared-and-assumed")} average coverage level of {target.ToString("0.##", CultureInfo.InvariantCulture)}% represents breadth and edge-case effort beyond test syntax counts alone.";

    private static List<string> CoverageAssumptions(
        bool isMeasured,
        bool supersededDeclaration)
    {
        if (!isMeasured)
        {
            return
            [
                "The declared configured coverage threshold is assumed achieved on the default static path.",
                "This item values represented coverage breadth; it is not measured coverage.",
            ];
        }

        List<string> assumptions =
        [
            "The checked-in coverage report represents the repository's current test level; EffortHours did not rerun it.",
        ];
        if (supersededDeclaration)
        {
            assumptions.Add("Measured coverage supersedes declared thresholds for the same scope.");
        }

        return assumptions;
    }

    private static IReadOnlyList<string> CoverageUncertainty(bool isMeasured) =>
        isMeasured
            ?
            [
                "A checked-in coverage report can be stale or produced from a different source snapshot.",
                "Static source construction is a proxy for the amount of behavior covered.",
            ]
            :
            [
                "Declared coverage was not executed or measured by EffortHours.",
                "Static source construction is a proxy for the amount of behavior covered.",
            ];
}
