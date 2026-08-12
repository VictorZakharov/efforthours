using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Docker;

internal enum DockerArtifactKind
{
    Dockerfile,
    Compose,
    DockerIgnore,
}

internal sealed record DockerAnalyzedFile(
    EvidenceFact File,
    DockerArtifactKind Kind,
    DockerfileAnalysis? Dockerfile,
    ComposeAnalysis? Compose,
    DockerIgnoreAnalysis? DockerIgnore,
    bool ExactDuplicate);

internal sealed record DockerfileAnalysis
{
    public int Instructions { get; init; }

    public int Stages { get; init; }

    public int ExternalBaseImages { get; init; }

    public int LocalStageReferences { get; init; }

    public int BuildSteps { get; init; }

    public int RunInstructions { get; init; }

    public int CopyInstructions { get; init; }

    public int AddInstructions { get; init; }

    public int Arguments { get; init; }

    public int EnvironmentEntries { get; init; }

    public int WorkDirectories { get; init; }

    public int Users { get; init; }

    public int ExposedPorts { get; init; }

    public int Volumes { get; init; }

    public int HealthChecks { get; init; }

    public int RuntimeCommands { get; init; }

    public int MultiStageCopies { get; init; }

    public int SecretOrSshMounts { get; init; }

    public int CacheOrBindMounts { get; init; }

    public int RemoteAdds { get; init; }

    public int Heredocs { get; init; }

    public int ParserDirectives { get; init; }

    public int UnresolvedValues { get; init; }

    public int UnknownInstructions { get; init; }

    public int Safeguards { get; init; }

    public string Confidence { get; init; } = "high";

    public decimal SemanticUnits { get; init; }
}

internal sealed record ComposeAnalysis
{
    public int Documents { get; init; }

    public int Services { get; init; }

    public int BuildDefinitions { get; init; }

    public int ImageReferences { get; init; }

    public int Commands { get; init; }

    public int Ports { get; init; }

    public int EnvironmentEntries { get; init; }

    public int EnvironmentFiles { get; init; }

    public int VolumeMounts { get; init; }

    public int Networks { get; init; }

    public int Dependencies { get; init; }

    public int HealthChecks { get; init; }

    public int Profiles { get; init; }

    public int Secrets { get; init; }

    public int Configs { get; init; }

    public int DeploySettings { get; init; }

    public int SecuritySettings { get; init; }

    public int RestartPolicies { get; init; }

    public int Extensions { get; init; }

    public int Includes { get; init; }

    public int AnchorsAliasesAndMerges { get; init; }

    public int Interpolations { get; init; }

    public int BlockScalars { get; init; }

    public int DynamicValues { get; init; }

    public int UnknownTopLevelKeys { get; init; }

    public int Safeguards { get; init; }

    public string Confidence { get; init; } = "high";

    public decimal SemanticUnits { get; init; }

    public IReadOnlyList<ComposeBuildReference> BuildReferences { get; init; } = [];
}

internal sealed record ComposeBuildReference(
    string Service,
    string? Context,
    string? Dockerfile,
    bool Dynamic);

internal sealed record DockerIgnoreAnalysis
{
    public int Rules { get; init; }

    public int Negations { get; init; }

    public int DirectoryRules { get; init; }

    public int UnresolvedRules { get; init; }

    public decimal SemanticUnits { get; init; }
}
