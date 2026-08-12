using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Docker;

internal static class DockerFactFactory
{
    public static EvidenceFact Dockerfile(DockerAnalyzedFile file)
    {
        DockerfileAnalysis analysis = file.Dockerfile!;
        return DockerEvidence.Fact(
            $"docker:dockerfile:{DockerEvidence.IdToken(file.File.Scope)}",
            EvidenceKinds.ContainerConfiguration,
            ScopeOf(file.File.Scope),
            $"Bounded static Dockerfile build and runtime configuration in '{file.File.Scope}'.",
            EvidenceSourceKind.Measured,
            "scanner-admitted Dockerfile logical-instruction analysis without Docker execution",
            [DockerEvidence.Location(file.File.Scope)],
            [
                Measure("container-units", file.ExactDuplicate ? 0m : analysis.SemanticUnits, "units"),
                Measure("instructions", analysis.Instructions, "instructions"),
                Measure("stages", analysis.Stages, "stages"),
                Measure("external-base-images", analysis.ExternalBaseImages, "images"),
                Measure("local-stage-references", analysis.LocalStageReferences, "references"),
                Measure("build-steps", analysis.BuildSteps, "steps"),
                Measure("run-instructions", analysis.RunInstructions, "instructions"),
                Measure("copy-instructions", analysis.CopyInstructions, "instructions"),
                Measure("add-instructions", analysis.AddInstructions, "instructions"),
                Measure("arguments", analysis.Arguments, "arguments"),
                Measure("environment-entries", analysis.EnvironmentEntries, "entries"),
                Measure("work-directories", analysis.WorkDirectories, "directories"),
                Measure("users", analysis.Users, "users"),
                Measure("exposed-ports", analysis.ExposedPorts, "ports"),
                Measure("volumes", analysis.Volumes, "volumes"),
                Measure("health-checks", analysis.HealthChecks, "checks"),
                Measure("runtime-commands", analysis.RuntimeCommands, "commands"),
                Measure("multi-stage-copies", analysis.MultiStageCopies, "copies"),
                Measure("secret-or-ssh-mounts", analysis.SecretOrSshMounts, "mounts"),
                Measure("cache-or-bind-mounts", analysis.CacheOrBindMounts, "mounts"),
                Measure("remote-adds", analysis.RemoteAdds, "adds"),
                Measure("heredocs", analysis.Heredocs, "heredocs"),
                Measure("parser-directives", analysis.ParserDirectives, "directives"),
                Measure("unresolved-values", analysis.UnresolvedValues, "values"),
                Measure("unknown-instructions", analysis.UnknownInstructions, "instructions"),
            ],
            CommonTags("dockerfile", analysis.Confidence, file.ExactDuplicate).Concat(
            [
                "dockerfile:logical-instructions", "docker-build:not-executed",
                analysis.Stages > 1 ? "dockerfile:multi-stage" : "dockerfile:single-stage-or-empty",
                analysis.Heredocs > 0 ? "dockerfile:heredoc-present" : "dockerfile:heredoc-absent",
            ]));
    }

    public static EvidenceFact Compose(
        DockerAnalyzedFile file,
        ComposeReferenceResolution resolution)
    {
        ComposeAnalysis analysis = file.Compose!;
        return DockerEvidence.Fact(
            $"docker:compose:{DockerEvidence.IdToken(file.File.Scope)}",
            EvidenceKinds.ContainerConfiguration,
            ScopeOf(file.File.Scope),
            $"Bounded static Docker Compose service and orchestration configuration in '{file.File.Scope}'.",
            EvidenceSourceKind.Measured,
            "filename-qualified bounded YAML structure and Compose-key analysis without interpolation or Docker execution",
            [DockerEvidence.Location(file.File.Scope)],
            [
                Measure("container-units", file.ExactDuplicate ? 0m : analysis.SemanticUnits, "units"),
                Measure("documents", analysis.Documents, "documents"),
                Measure("services", analysis.Services, "services"),
                Measure("build-definitions", analysis.BuildDefinitions, "builds"),
                Measure("image-references", analysis.ImageReferences, "images"),
                Measure("commands", analysis.Commands, "commands"),
                Measure("ports", analysis.Ports, "ports"),
                Measure("environment-entries", analysis.EnvironmentEntries, "entries"),
                Measure("environment-files", analysis.EnvironmentFiles, "files"),
                Measure("volume-mounts", analysis.VolumeMounts, "mounts"),
                Measure("networks", analysis.Networks, "networks"),
                Measure("dependencies", analysis.Dependencies, "dependencies"),
                Measure("health-checks", analysis.HealthChecks, "checks"),
                Measure("profiles", analysis.Profiles, "profiles"),
                Measure("secrets", analysis.Secrets, "secrets"),
                Measure("configs", analysis.Configs, "configs"),
                Measure("deploy-settings", analysis.DeploySettings, "settings"),
                Measure("security-settings", analysis.SecuritySettings, "settings"),
                Measure("restart-policies", analysis.RestartPolicies, "policies"),
                Measure("extensions", analysis.Extensions, "extensions"),
                Measure("includes", analysis.Includes, "includes"),
                Measure("anchors-aliases-merges", analysis.AnchorsAliasesAndMerges, "constructs"),
                Measure("interpolations", analysis.Interpolations, "expressions"),
                Measure("block-scalars", analysis.BlockScalars, "scalars"),
                Measure("dynamic-values", analysis.DynamicValues, "values"),
                Measure("unknown-top-level-keys", analysis.UnknownTopLevelKeys, "keys"),
                Measure("local-dockerfile-references", resolution.Resolved.Count, "references"),
                Measure("missing-dockerfile-references", resolution.Missing, "references"),
                Measure("unresolved-build-references", resolution.Unresolved, "references"),
                Measure("external-build-contexts", resolution.External, "contexts"),
            ],
            CommonTags("compose", analysis.Confidence, file.ExactDuplicate).Concat(
            [
                "compose:filename-qualified", "compose:bounded-yaml-structure",
                "compose-interpolation:not-evaluated", "compose-includes:not-loaded",
                analysis.Documents > 1 ? "compose:multi-document" : "compose:single-document",
            ]));
    }

    public static EvidenceFact DockerIgnore(DockerAnalyzedFile file)
    {
        DockerIgnoreAnalysis analysis = file.DockerIgnore!;
        return DockerEvidence.Fact(
            $"docker:ignore:{DockerEvidence.IdToken(file.File.Scope)}",
            EvidenceKinds.ContainerConfiguration,
            ScopeOf(file.File.Scope),
            $"Static Docker build-context exclusion configuration in '{file.File.Scope}'.",
            EvidenceSourceKind.Measured,
            "bounded .dockerignore rule inventory without build-context traversal",
            [DockerEvidence.Location(file.File.Scope)],
            [
                Measure("container-units", file.ExactDuplicate ? 0m : analysis.SemanticUnits, "units"),
                Measure("ignore-rules", analysis.Rules, "rules"),
                Measure("negated-rules", analysis.Negations, "rules"),
                Measure("directory-rules", analysis.DirectoryRules, "rules"),
                Measure("unresolved-rules", analysis.UnresolvedRules, "rules"),
            ],
            CommonTags("dockerignore", analysis.UnresolvedRules > 0 ? "medium" : "high", file.ExactDuplicate)
                .Concat(["dockerignore:static-rules", "build-context:not-expanded"]));
    }

    public static EvidenceFact Reference(ResolvedDockerfileReference reference) => DockerEvidence.Fact(
        $"docker:reference:{DockerEvidence.IdToken(reference.SourcePath + "->" + reference.TargetPath)}",
        EvidenceKinds.ProjectReference,
        ScopeOf(reference.SourcePath),
        "Static Compose build configuration references a scanner-admitted repository Dockerfile.",
        EvidenceSourceKind.Observed,
        "literal repository-contained Compose build context and Dockerfile path",
        [DockerEvidence.Location(reference.SourcePath), DockerEvidence.Location(reference.TargetPath)],
        tags: ["ecosystem:docker", "reference-kind:compose-dockerfile", "reference:resolved"]);

    public static EvidenceFact DuplicateExclusion(DockerAnalyzedFile file) => DockerEvidence.Fact(
        $"docker:duplicate:{DockerEvidence.IdToken(file.File.Scope)}",
        EvidenceKinds.ExcludedContent,
        ScopeOf(file.File.Scope),
        $"Byte-identical {ArtifactName(file.Kind)} semantics in '{file.File.Scope}' are normalized to one canonical body.",
        EvidenceSourceKind.Inferred,
        "artifact-kind and common-scanner SHA-256 exact-content normalization",
        [DockerEvidence.Location(file.File.Scope)],
        tags: ["ecosystem:docker", "normalization:exact-duplicate", "source-excerpts:not-emitted"]);

    public static EvidenceFact Repository(
        IReadOnlyList<DockerAnalyzedFile> files,
        IReadOnlyList<ComposeReferenceResolution> resolutions,
        int admitted,
        int skipped) => DockerEvidence.Fact(
        "docker:repository",
        EvidenceKinds.ContainerConfiguration,
        ".",
        "Bounded static Dockerfile, Docker Compose, and Docker build-context evidence.",
        EvidenceSourceKind.Measured,
        "common-scanner-admitted Docker artifact analysis with exact-content normalization",
        files.Select(file => DockerEvidence.Location(file.File.Scope)),
        [
            Measure("admitted-files", admitted, "files"),
            Measure("analyzed-files", files.Count, "files"),
            Measure("dockerfiles", files.Count(file => file.Kind == DockerArtifactKind.Dockerfile), "files"),
            Measure("compose-files", files.Count(file => file.Kind == DockerArtifactKind.Compose), "files"),
            Measure("dockerignore-files", files.Count(file => file.Kind == DockerArtifactKind.DockerIgnore), "files"),
            Measure("exact-duplicates", files.Count(file => file.ExactDuplicate), "files"),
            Measure("skipped-files", skipped, "files"),
            Measure("semantic-container-units", files.Sum(SemanticUnits), "units"),
            Measure("local-dockerfile-references", resolutions.Sum(item => item.Resolved.Count), "references"),
            Measure("unresolved-build-references", resolutions.Sum(item => item.Unresolved + item.Missing), "references"),
        ],
        [
            "ecosystem:docker", "analysis:static-only", "docker:not-invoked",
            "compose-interpolation:not-evaluated", "source-excerpts:not-emitted",
        ]);

    private static IEnumerable<string> CommonTags(string artifact, string confidence, bool duplicate) =>
    [
        "ecosystem:docker", $"docker-artifact:{artifact}", "syntax:bounded-structural",
        $"parser-confidence:{confidence}", "source-excerpts:not-emitted", "docker:not-invoked",
        duplicate ? "normalization:exact-duplicate" : "normalization:canonical-body",
    ];

    private static decimal SemanticUnits(DockerAnalyzedFile file) => file.ExactDuplicate ? 0m : file.Kind switch
    {
        DockerArtifactKind.Dockerfile => file.Dockerfile!.SemanticUnits,
        DockerArtifactKind.Compose => file.Compose!.SemanticUnits,
        DockerArtifactKind.DockerIgnore => file.DockerIgnore!.SemanticUnits,
        _ => 0m,
    };

    private static string ArtifactName(DockerArtifactKind kind) => kind switch
    {
        DockerArtifactKind.Dockerfile => "Dockerfile",
        DockerArtifactKind.Compose => "Compose",
        DockerArtifactKind.DockerIgnore => ".dockerignore",
        _ => "container",
    };

    private static string ScopeOf(string path)
    {
        int separator = path.LastIndexOf('/');
        return separator < 0 ? "." : path[..separator];
    }

    private static EvidenceMeasurement Measure(string name, decimal value, string unit) =>
        DockerEvidence.Measurement(name, value, unit);
}
