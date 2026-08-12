namespace EffortHours.Analyzers.Rust;

internal sealed class CargoManifestMetadata(string path, string directory)
{
    public string Path { get; } = path;
    public string Directory { get; } = directory;
    public string? Name { get; set; }
    public string? DefaultRun { get; set; }
    public string? ExplicitBuild { get; set; }
    public string? LibName { get; set; }
    public string? LibPath { get; set; }
    public bool HasPackage { get; set; }
    public bool HasWorkspace { get; set; }
    public bool HasLib { get; set; }
    public bool AutoBins { get; set; } = true;
    public bool AutoExamples { get; set; } = true;
    public bool AutoTests { get; set; } = true;
    public bool AutoBenches { get; set; } = true;
    public bool BuildDisabled { get; set; }
    public bool IsProcMacro { get; set; }
    public bool Malformed { get; set; }
    public int Features { get; set; }
    public int BuildScripts { get; set; }
    public int CrateTypes { get; set; }
    public int UnresolvedValues { get; set; }
    public List<string> MemberPatterns { get; } = [];
    public List<string> ExcludePatterns { get; } = [];
    public List<string> DefaultMemberPatterns { get; } = [];
    public List<CargoDependencyBuilder> Dependencies { get; } = [];
    public List<CargoTargetBuilder> Targets { get; } = [];
}

internal sealed record CargoDependencyBuilder(
    string Name,
    string Kind,
    string? PackageName,
    string? Path,
    bool WorkspaceInherited);

internal sealed class CargoTargetBuilder(string kind)
{
    public string Kind { get; } = kind;
    public string? Name { get; set; }
    public string? Path { get; set; }
}
