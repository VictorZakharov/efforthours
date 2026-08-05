using Fairbill.Contracts.V1;

namespace Fairbill.Tests;

internal static class TestRepositoryEvidence
{
    public static RepositoryEvidence Create()
    {
        EvidenceProvenance provenance = new()
        {
            SourceKind = EvidenceSourceKind.Observed,
            Analyzer = "test-fixture",
            AnalyzerVersion = "1.0.0",
            Method = "synthetic unit test",
        };

        return new RepositoryEvidence
        {
            Repository = new RepositoryDescriptor
            {
                Name = "Synthetic repository",
                Scope = ".",
                Ecosystems = ["dotnet", "typescript"],
                SourceDigest = "sha256:synthetic",
            },
            Facts =
            [
                Fact("component:billing", EvidenceKinds.Component, "src/Billing", "billing component", provenance),
                Fact("integration:payments", EvidenceKinds.Integration, "src/Billing", "payment provider", provenance),
                Fact("tests:billing", EvidenceKinds.TestSuite, "tests/Billing.Tests", "billing unit tests", provenance, "unit"),
                Fact("documentation:readme", EvidenceKinds.Documentation, ".", "repository onboarding", provenance),
                Fact("build:solution", EvidenceKinds.BuildConfiguration, ".", "solution build", provenance),
            ],
        };
    }

    public static EvidenceFact Fact(
        string id,
        string kind,
        string scope,
        string summary,
        EvidenceProvenance provenance,
        params string[] tags)
    {
        return new EvidenceFact
        {
            Id = id,
            Kind = kind,
            Scope = scope,
            Summary = summary,
            Provenance = provenance,
            Locations = [new EvidenceLocation { Path = scope == "." ? "README.md" : $"{scope}/sample.cs" }],
            Tags = tags.Length == 0 ? ["complexity:moderate"] : tags,
        };
    }
}
