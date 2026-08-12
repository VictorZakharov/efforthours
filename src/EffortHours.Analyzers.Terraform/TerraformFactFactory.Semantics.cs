using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Terraform;

internal static partial class TerraformFactFactory
{
    public static IEnumerable<EvidenceFact> SemanticFacts(TerraformModuleModel module)
    {
        TerraformSemanticMetrics metrics = Aggregate(module.CanonicalFiles);
        EvidenceLocation[] locations = [.. module.CanonicalFiles
            .OrderBy(file => file.File.Scope, StringComparer.Ordinal)
            .Select(file => TerraformEvidence.Location(file.File.Scope))];
        if (metrics.ProviderFamilies.Count + metrics.BackendTypes.Count +
            metrics.ModuleSources.Count(source => source.Kind != "local") > 0)
        {
            yield return Integration(module, metrics, locations);
        }

        if (metrics.SensitiveInterfaces + metrics.CredentialLikeAttributes +
            metrics.PolicyOrSecuritySurfaces > 0)
        {
            yield return Security(module, metrics, locations);
        }

        if (metrics.ValidationRules > 0)
        {
            yield return Validation(module, metrics, locations);
        }

        if (metrics.TestRuns + metrics.TestAssertions > 0 ||
            module.CanonicalFiles.Any(file => file.Analysis.Artifact.IsTest))
        {
            yield return Tests(module, metrics, locations);
        }

        if (metrics.DescriptionAttributes > 0)
        {
            yield return Documentation(module, metrics, locations);
        }

        if (metrics.TerraformBlocks + metrics.Backends > 0)
        {
            yield return Delivery(module, metrics, locations);
        }

        int cliFiles = module.CanonicalFiles.Count(file => file.Analysis.Artifact.IsCliConfiguration);
        if (cliFiles > 0)
        {
            yield return BuildConfiguration(module, cliFiles, locations);
        }
    }

    private static EvidenceFact Integration(
        TerraformModuleModel module,
        TerraformSemanticMetrics metrics,
        IReadOnlyList<EvidenceLocation> locations)
    {
        string[] moduleKinds = [.. metrics.ModuleSources
            .Where(source => source.Kind != "local")
            .Select(source => source.Kind)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        List<string> tags =
        [
            "ecosystem:terraform",
            "integration-source:static-configuration",
            .. metrics.ProviderFamilies.Order(StringComparer.Ordinal)
                .Select(family => $"technology:terraform-provider-{family}"),
            .. metrics.BackendTypes.Order(StringComparer.Ordinal)
                .Select(backend => $"technology:terraform-backend-{backend}"),
            .. moduleKinds.Select(kind => $"technology:terraform-module-{kind}"),
        ];
        return TerraformEvidence.Fact(
            $"terraform:integration:{TerraformEvidence.IdToken(module.Directory)}",
            EvidenceKinds.Integration,
            module.Directory,
            $"Terraform provider, backend, and external module boundaries for '{module.Directory}'.",
            EvidenceSourceKind.Inferred,
            "provider/resource prefixes, backend labels, and literal external module-source classes",
            locations,
            [
                TerraformEvidence.Measurement("integration-calls",
                    metrics.ProviderFamilies.Count + metrics.BackendTypes.Count +
                    metrics.ModuleSources.Count(source => source.Kind != "local"), "boundaries"),
                TerraformEvidence.Measurement("integration-namespaces",
                    metrics.ProviderFamilies.Count + metrics.BackendTypes.Count + moduleKinds.Length,
                    "families"),
            ],
            tags);
    }

    private static EvidenceFact Security(
        TerraformModuleModel module,
        TerraformSemanticMetrics metrics,
        IReadOnlyList<EvidenceLocation> locations) => TerraformEvidence.Fact(
            $"terraform:security:{TerraformEvidence.IdToken(module.Directory)}",
            EvidenceKinds.SecurityConfiguration,
            module.Directory,
            $"Terraform sensitive-interface, policy, credential-surface, and security-resource evidence for '{module.Directory}'.",
            EvidenceSourceKind.Inferred,
            "explicit sensitive flags plus bounded credential/policy/security resource and attribute names; literal values are never emitted",
            locations,
            [
                TerraformEvidence.Measurement("security-surfaces", Math.Max(1,
                    metrics.SensitiveInterfaces + metrics.CredentialLikeAttributes +
                    metrics.PolicyOrSecuritySurfaces), "surfaces"),
                TerraformEvidence.Measurement("security-configuration-calls",
                    metrics.PolicyOrSecuritySurfaces, "surfaces"),
                TerraformEvidence.Measurement("security-usages",
                    metrics.SensitiveInterfaces + metrics.CredentialLikeAttributes,
                    "usages"),
            ],
            [
                "ecosystem:terraform",
                "secret-values:not-emitted",
                "security-runtime:not-verified",
            ]);

    private static EvidenceFact Validation(
        TerraformModuleModel module,
        TerraformSemanticMetrics metrics,
        IReadOnlyList<EvidenceLocation> locations) => TerraformEvidence.Fact(
            $"terraform:validation:{TerraformEvidence.IdToken(module.Directory)}",
            EvidenceKinds.Validation,
            module.Directory,
            $"Terraform validation, precondition, postcondition, and check evidence for '{module.Directory}'.",
            EvidenceSourceKind.Inferred,
            "static validation/check/assert block and condition structure",
            locations,
            [
                TerraformEvidence.Measurement("validator-types", 1, "families"),
                TerraformEvidence.Measurement("validation-rules", metrics.ValidationRules, "rules"),
                TerraformEvidence.Measurement("validation-usages", metrics.ValidationRules, "usages"),
            ],
            ["ecosystem:terraform", "validation-runtime:not-verified"]);

    private static EvidenceFact Tests(
        TerraformModuleModel module,
        TerraformSemanticMetrics metrics,
        IReadOnlyList<EvidenceLocation> locations)
    {
        int files = module.CanonicalFiles.Count(file => file.Analysis.Artifact.IsTest);
        int cases = Math.Max(files, metrics.TestRuns);
        return TerraformEvidence.Fact(
            $"terraform:test:{TerraformEvidence.IdToken(module.Directory)}",
            EvidenceKinds.EcosystemTest,
            module.Directory,
            $"Static Terraform test-run and assertion evidence for '{module.Directory}'.",
            EvidenceSourceKind.Measured,
            "scanner test role plus bounded run/assert HCL structure",
            locations,
            [
                TerraformEvidence.Measurement("test-methods", Math.Max(1, cases), "runs"),
                TerraformEvidence.Measurement("test-cases", Math.Max(1, cases), "cases"),
                TerraformEvidence.Measurement("assertions", metrics.TestAssertions, "assertions"),
            ],
            ["ecosystem:terraform", "test-type:integration", "terraform-tests:not-executed"]);
    }

    private static EvidenceFact Documentation(
        TerraformModuleModel module,
        TerraformSemanticMetrics metrics,
        IReadOnlyList<EvidenceLocation> locations) => TerraformEvidence.Fact(
            $"terraform:documentation:{TerraformEvidence.IdToken(module.Directory)}",
            EvidenceKinds.Documentation,
            module.Directory,
            $"Maintained Terraform input/output descriptions for '{module.Directory}'.",
            EvidenceSourceKind.Inferred,
            "literal or expression-backed description attributes on maintained HCL interfaces",
            locations,
            [
                TerraformEvidence.Measurement("files", 1, "documents"),
                TerraformEvidence.Measurement("description-attributes", metrics.DescriptionAttributes, "attributes"),
                TerraformEvidence.Measurement("physical-lines", 0, "lines"),
            ],
            ["ecosystem:terraform", "documentation-kind:interface-descriptions"]);

    private static EvidenceFact Delivery(
        TerraformModuleModel module,
        TerraformSemanticMetrics metrics,
        IReadOnlyList<EvidenceLocation> locations) => TerraformEvidence.Fact(
            $"terraform:delivery:{TerraformEvidence.IdToken(module.Directory)}",
            EvidenceKinds.DeliveryAutomation,
            module.Directory,
            $"Terraform version, provider-installation, and backend delivery configuration for '{module.Directory}'.",
            EvidenceSourceKind.Inferred,
            "static terraform/backend configuration structure without initialization, planning, provider installation, or backend contact",
            locations,
            [
                TerraformEvidence.Measurement("files", 1, "configuration-files"),
                TerraformEvidence.Measurement("terraform-blocks", metrics.TerraformBlocks, "blocks"),
                TerraformEvidence.Measurement("backend-configurations", metrics.Backends, "backends"),
            ],
            [
                "ecosystem:terraform",
                "delivery-kind:terraform-configuration",
                "terraform-init:not-performed",
                "backend-contact:not-performed",
            ]);

    private static EvidenceFact BuildConfiguration(
        TerraformModuleModel module,
        int files,
        IReadOnlyList<EvidenceLocation> locations) => TerraformEvidence.Fact(
            $"terraform:cli-configuration:{TerraformEvidence.IdToken(module.Directory)}",
            EvidenceKinds.BuildConfiguration,
            module.Directory,
            "Static Terraform CLI installation, cache, credential-helper, or provider-discovery configuration.",
            EvidenceSourceKind.Inferred,
            "recognized Terraform CLI HCL configuration path and bounded static structure",
            locations,
            [
                TerraformEvidence.Measurement("files", files, "files"),
                TerraformEvidence.Measurement("scripts", 0, "scripts"),
            ],
            ["ecosystem:terraform", "terraform-cli:not-invoked", "secret-values:not-emitted"]);
}
