using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Estimation;

internal sealed partial class SeedCapabilityBuilder
{
    private void AddCiAndInfrastructure(List<CapabilityUnit> capabilities)
    {
        SeedFileEvidence[] files = [.. _index.Files.Where(file =>
            file.IsMaintained && file.Role is "ci-configuration" or "infrastructure")];
        SeedFileEvidence[] canonical = [.. files
            .Where(file => !file.Ecosystems.Contains("terraform", StringComparer.Ordinal))
            .Where(_index.IsCanonical)];
        EvidenceFact[] terraformFacts = [.. _index.FactsOfKind(EvidenceKinds.Infrastructure)
            .Where(fact => fact.Provenance.Analyzer == "efforthours.terraform-analyzer")];
        EvidenceFact[] aggregateFacts = Evidence(
            _index.FactsOfKind(EvidenceKinds.CiConfiguration),
            _index.FactsOfKind(EvidenceKinds.Infrastructure));
        EvidenceFact[] evidence = Evidence(files.Select(file => file.Fact), aggregateFacts);
        if (evidence.Length == 0)
        {
            return;
        }

        decimal ciFiles = canonical.Count(file => file.Role == "ci-configuration");
        decimal infrastructureFiles = canonical.Count(file => file.Role == "infrastructure") +
            terraformFacts.Sum(fact => SeedEvidenceIndex.Measurement(fact, "infrastructure-units"));
        decimal physicalLines = canonical.Sum(file => file.PhysicalLines);
        if (files.Length == 0 && terraformFacts.Length == 0)
        {
            ciFiles = aggregateFacts
                .Where(fact => fact.Kind == EvidenceKinds.CiConfiguration)
                .Sum(fact => SeedEvidenceIndex.Measurement(fact, "files"));
            infrastructureFiles = aggregateFacts
                .Where(fact => fact.Kind == EvidenceKinds.Infrastructure)
                .Where(fact => fact.Provenance.Analyzer != "efforthours.terraform-analyzer")
                .Sum(fact => SeedEvidenceIndex.Measurement(fact, "files"));
            physicalLines = aggregateFacts.Sum(fact =>
                SeedEvidenceIndex.Measurement(fact, "physical-lines"));
        }

        if (ciFiles + infrastructureFiles == 0m)
        {
            return;
        }

        capabilities.Add(new CapabilityUnit
        {
            Id = "repository:ci-infrastructure",
            RuleId = "ci-infrastructure",
            Title = "Implement represented CI/CD and infrastructure configuration",
            Scope = ".",
            Quantity = decimal.Max(1m, ciFiles + infrastructureFiles),
            Drivers = Drivers(
                ("ci-files", ciFiles),
                ("infrastructure-files", infrastructureFiles),
                ("physical-lines", physicalLines)),
            Complexity = infrastructureFiles > 10m ? ComplexityLevel.High : ComplexityLevel.Moderate,
            Reason = "CI/CD and infrastructure artifacts represent maintained automation, environment, and deployment behavior.",
            Evidence = evidence,
            Profiles = BothProfiles,
            CorrelationGroup = "repository:delivery",
        });
    }
}
