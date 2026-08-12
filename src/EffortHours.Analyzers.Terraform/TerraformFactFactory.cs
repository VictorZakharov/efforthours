using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Terraform;

internal static partial class TerraformFactFactory
{
    public static EvidenceFact Repository(
        IReadOnlyList<EvidenceFact> admittedFiles,
        IReadOnlyList<TerraformModuleModel> modules,
        int analyzed,
        int excluded,
        int skipped)
    {
        TerraformSemanticMetrics metrics = Aggregate(modules.SelectMany(module => module.CanonicalFiles));
        return TerraformEvidence.Fact(
            "terraform:repository",
            EvidenceKinds.TerraformRepository,
            ".",
            "Bounded static Terraform and HCL module, infrastructure, integration, test, and policy inventory.",
            EvidenceSourceKind.Observed,
            "common-scanner-admitted Terraform/HCL files with bounded token and structural analysis",
            admittedFiles.Select(file => TerraformEvidence.Location(file.Scope)),
            [
                TerraformEvidence.Measurement("terraform-hcl-files", admittedFiles.Count, "files"),
                TerraformEvidence.Measurement("analyzed-files", analyzed, "files"),
                TerraformEvidence.Measurement("excluded-files", excluded, "files"),
                TerraformEvidence.Measurement("skipped-files", skipped, "files"),
                TerraformEvidence.Measurement("modules", modules.Count, "modules"),
                TerraformEvidence.Measurement("resources", metrics.Resources, "blocks"),
                TerraformEvidence.Measurement("data-sources", metrics.DataSources, "blocks"),
                TerraformEvidence.Measurement("module-calls", metrics.ModuleCalls, "blocks"),
            ],
            [
                "ecosystem:terraform",
                "parser:bounded-hcl-token-structure",
                "source-excerpts:not-emitted",
                "terraform-execution:not-performed",
            ]);
    }

    public static EvidenceFact Artifact(
        EvidenceFact file,
        TerraformFileAnalysis analysis,
        string moduleDirectory,
        bool exactDuplicate)
    {
        TerraformSemanticMetrics metrics = analysis.Metrics;
        List<string> tags = CommonTags(analysis, moduleDirectory);
        if (exactDuplicate) tags.Add("normalization:exact-content-duplicate");
        return TerraformEvidence.Fact(
            $"terraform:artifact:{TerraformEvidence.IdToken(file.Scope)}",
            EvidenceKinds.TerraformArtifact,
            moduleDirectory,
            $"Static {analysis.Artifact.Dialect.Replace('-', ' ')} analysis for '{file.Scope}'.",
            EvidenceSourceKind.Measured,
            "bounded comment/string/heredoc-aware tokenization and conservative HCL body analysis",
            [TerraformEvidence.Location(file.Scope, analysis.Document.FirstSemanticLine)],
            RawMeasurements(metrics, analysis.Document),
            tags);
    }

    public static EvidenceFact DuplicateExclusion(
        TerraformAnalyzedFile file,
        string canonicalPath) => TerraformEvidence.Fact(
            $"terraform:excluded:{TerraformEvidence.IdToken(file.File.Scope)}",
            EvidenceKinds.ExcludedContent,
            file.ModuleDirectory,
            $"Byte-identical Terraform/HCL file '{file.File.Scope}' remains traceable but does not add semantic effort.",
            EvidenceSourceKind.Inferred,
            "exact maintained-file content digest normalization",
            [TerraformEvidence.Location(file.File.Scope), TerraformEvidence.Location(canonicalPath)],
            tags:
            [
                "classification:exact-duplicate",
                "ecosystem:terraform",
                $"hcl-role:{file.Analysis.Artifact.Role}",
            ]);

    public static EvidenceFact Module(TerraformModuleModel module)
    {
        TerraformSemanticMetrics metrics = Aggregate(module.CanonicalFiles);
        return TerraformEvidence.Fact(
            $"terraform:module:{TerraformEvidence.IdToken(module.Directory)}",
            EvidenceKinds.EcosystemPackage,
            module.Directory,
            $"Static Terraform/HCL module scope '{module.Directory}'.",
            EvidenceSourceKind.Inferred,
            "directory-local HCL configuration ownership with nearest-ancestor test/value attachment",
            Locations(module.Files),
            [
                TerraformEvidence.Measurement("files", module.Files.Count, "files"),
                TerraformEvidence.Measurement("source-files", module.CanonicalFiles.Count(file =>
                    !file.Analysis.Artifact.IsTest), "files"),
                TerraformEvidence.Measurement("dependencies", metrics.ProviderFamilies.Count +
                    metrics.ModuleSources.Count, "boundaries"),
                TerraformEvidence.Measurement("resources", metrics.Resources, "blocks"),
            ],
            [
                "ecosystem:terraform",
                "package-role:infrastructure",
                "scope:analyzed",
                "metadata:static-only",
                "terraform-execution:not-performed",
            ]);
    }

    public static EvidenceFact Infrastructure(TerraformModuleModel module)
    {
        TerraformSemanticMetrics metrics = Aggregate(module.CanonicalFiles.Where(file =>
            file.Analysis.Artifact.SupportsTerraformSemantics &&
            !file.Analysis.Artifact.IsTest && !file.Analysis.Artifact.IsCliConfiguration));
        int files = module.CanonicalFiles.Count(file =>
            file.Analysis.Artifact.SupportsTerraformSemantics &&
            !file.Analysis.Artifact.IsTest && !file.Analysis.Artifact.IsCliConfiguration);
        return TerraformEvidence.Fact(
            $"terraform:infrastructure:{TerraformEvidence.IdToken(module.Directory)}",
            EvidenceKinds.Infrastructure,
            module.Directory,
            $"Terraform/HCL infrastructure semantics for module '{module.Directory}'.",
            EvidenceSourceKind.Inferred,
            "bounded distinct-type, interface, dependency, lifecycle, and expression units; repeated conventional blocks diminish",
            Locations(module.CanonicalFiles),
            [
                TerraformEvidence.Measurement("infrastructure-units", metrics.InfrastructureUnits(files), "units"),
                TerraformEvidence.Measurement("semantic-files", files, "files"),
                TerraformEvidence.Measurement("resources", metrics.Resources, "blocks"),
                TerraformEvidence.Measurement("distinct-resource-types", metrics.ResourceTypes.Count, "types"),
                TerraformEvidence.Measurement("data-sources", metrics.DataSources, "blocks"),
                TerraformEvidence.Measurement("distinct-data-source-types", metrics.DataSourceTypes.Count, "types"),
                TerraformEvidence.Measurement("module-calls", metrics.ModuleCalls, "blocks"),
                TerraformEvidence.Measurement("variables", metrics.Variables, "interfaces"),
                TerraformEvidence.Measurement("outputs", metrics.Outputs, "interfaces"),
                TerraformEvidence.Measurement("input-assignments", metrics.InputAssignments, "assignments"),
                TerraformEvidence.Measurement("local-values", metrics.LocalValues, "values"),
                TerraformEvidence.Measurement("providers", metrics.Providers, "blocks"),
                TerraformEvidence.Measurement("backends", metrics.Backends, "blocks"),
                TerraformEvidence.Measurement("lifecycle-blocks", metrics.LifecycleBlocks, "blocks"),
                TerraformEvidence.Measurement("dynamic-blocks", metrics.DynamicBlocks, "blocks"),
                TerraformEvidence.Measurement("provisioners", metrics.Provisioners, "blocks"),
                TerraformEvidence.Measurement("dependency-expressions", metrics.DependencyExpressions, "expressions"),
                TerraformEvidence.Measurement("expression-complexity-units", metrics.ExpressionComplexityUnits, "units"),
            ],
            [
                "ecosystem:terraform",
                "terraform-ehe-mapping:existing-infrastructure-prior",
                "resource-repetition:diminishing",
                "provider-schemas:not-loaded",
                "runtime-correctness:not-verified",
            ]);
    }

    public static EvidenceFact ProjectReference(TerraformLocalModuleReference reference) =>
        TerraformEvidence.Fact(
            $"terraform:reference:{TerraformEvidence.IdToken(reference.SourceDirectory)}:" +
            $"{TerraformEvidence.IdToken(reference.TargetDirectory ?? reference.SourcePath)}:{reference.Line}",
            EvidenceKinds.ProjectReference,
            reference.SourceDirectory,
            reference.TargetDirectory is null
                ? "A local Terraform module source could not be resolved inside the analyzed repository."
                : $"Terraform module '{reference.SourceDirectory}' references local module '{reference.TargetDirectory}'.",
            EvidenceSourceKind.Inferred,
            "literal relative module source normalized within repository scope and matched to a discovered module",
            [TerraformEvidence.Location(reference.SourcePath, reference.Line)],
            [TerraformEvidence.Measurement("reference-edges", 1, "edges")],
            [
                "ecosystem:terraform",
                "reference-kind:local-module",
                reference.TargetDirectory is null
                    ? "reference:unresolved"
                    : $"target-scope:{reference.TargetDirectory}",
                reference.OutsideRepository ? "reference:outside-scope" :
                    reference.MissingTarget ? "reference:missing-target" : "reference:resolved",
            ]);

    internal static TerraformSemanticMetrics Aggregate(IEnumerable<TerraformAnalyzedFile> files)
    {
        TerraformSemanticMetrics aggregate = new();
        foreach (TerraformAnalyzedFile file in files) aggregate.Add(file.Analysis.Metrics);
        return aggregate;
    }

    private static IEnumerable<EvidenceLocation> Locations(IEnumerable<TerraformAnalyzedFile> files) =>
        files.OrderBy(file => file.File.Scope, StringComparer.Ordinal)
            .Select(file => TerraformEvidence.Location(file.File.Scope));

    private static IEnumerable<EvidenceMeasurement> RawMeasurements(
        TerraformSemanticMetrics metrics,
        HclDocumentAnalysis document) =>
    [
        TerraformEvidence.Measurement("blocks", metrics.Blocks, "blocks"),
        TerraformEvidence.Measurement("attributes", metrics.Attributes, "attributes"),
        TerraformEvidence.Measurement("unknown-constructs", document.UnknownConstructs, "constructs"),
        TerraformEvidence.Measurement("comments", document.CommentCount, "comments"),
        TerraformEvidence.Measurement("heredocs", document.HeredocCount, "strings"),
        TerraformEvidence.Measurement("resources", metrics.Resources, "blocks"),
        TerraformEvidence.Measurement("data-sources", metrics.DataSources, "blocks"),
        TerraformEvidence.Measurement("module-calls", metrics.ModuleCalls, "blocks"),
        TerraformEvidence.Measurement("validation-rules", metrics.ValidationRules, "rules"),
        TerraformEvidence.Measurement("test-runs", metrics.TestRuns, "runs"),
        TerraformEvidence.Measurement("test-assertions", metrics.TestAssertions, "assertions"),
    ];

    private static List<string> CommonTags(
        TerraformFileAnalysis analysis,
        string moduleDirectory)
    {
        HclDocumentAnalysis document = analysis.Document;
        List<string> tags =
        [
            "ecosystem:terraform",
            $"hcl-role:{analysis.Artifact.Role}",
            $"hcl-dialect:{analysis.Artifact.Dialect}",
            "syntax:token-backed",
            "parser:bounded-hcl-token-structure",
            $"parser-confidence:{document.ParserConfidence}",
            $"module-scope:{moduleDirectory}",
            "source-excerpts:not-emitted",
            "terraform-execution:not-performed",
        ];
        if (document.Truncated) tags.Add("analysis:token-limit-reached");
        if (!document.StructurallyBalanced || document.UnterminatedConstruct)
            tags.Add("analysis:structurally-incomplete");
        if (!analysis.Artifact.SupportsTerraformSemantics)
            tags.Add("terraform-semantics:not-assumed");
        return tags;
    }
}
