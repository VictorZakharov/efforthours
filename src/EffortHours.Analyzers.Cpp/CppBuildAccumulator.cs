namespace EffortHours.Analyzers.Cpp;

internal sealed class CppBuildAccumulator(string directory)
{
    public string Directory { get; } = directory;
    public HashSet<string> ManifestPaths { get; } = new(StringComparer.Ordinal);
    public HashSet<string> BuildSystems { get; } = new(StringComparer.Ordinal);
    public HashSet<string> ExplicitSources { get; } = new(StringComparer.Ordinal);
    public HashSet<string> IncludeRoots { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Dependencies { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> TargetNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> LocalReferenceDirectories { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Standards { get; } = new(StringComparer.Ordinal);
    public HashSet<string> DeclaredLanguages { get; } = new(StringComparer.Ordinal);
    public int Targets { get; set; }
    public int Executables { get; set; }
    public int Libraries { get; set; }
    public int Plugins { get; set; }
    public int Tests { get; set; }
    public int Benchmarks { get; set; }
    public int InstallRules { get; set; }
    public int GenerationSignals { get; set; }
    public int ConfigurationVariants { get; set; }
    public int CompileDefinitions { get; set; }
    public int Unresolved { get; set; }
    public int LocalReferences { get; set; }

    public CppProjectModel ToModel()
    {
        string role = Tests > 0 && Executables + Libraries == 0 ? "test" :
            Executables > 0 ? "application" : Libraries > 0 ? "library" : "package";
        return new CppProjectModel
        {
            Directory = Directory,
            Role = role,
            ManifestPaths = [.. ManifestPaths.Order(StringComparer.Ordinal)],
            BuildSystems = [.. BuildSystems.Order(StringComparer.Ordinal)],
            ExplicitSources = [.. ExplicitSources.Order(StringComparer.Ordinal)],
            IncludeRoots = [.. IncludeRoots.Order(StringComparer.Ordinal)],
            DependencyNames = [.. Dependencies.Where(name => !TargetNames.Contains(name))
                .Order(StringComparer.OrdinalIgnoreCase)],
            TargetNames = [.. TargetNames.Order(StringComparer.OrdinalIgnoreCase)],
            LocalReferenceDirectories = [.. LocalReferenceDirectories.Order(StringComparer.Ordinal)],
            DeclaredLanguages = [.. DeclaredLanguages.Order(StringComparer.Ordinal)],
            Standards = [.. Standards.Order(StringComparer.Ordinal)],
            Targets = Targets,
            Executables = Executables,
            Libraries = Libraries,
            Plugins = Plugins,
            TestTargets = Tests,
            BenchmarkTargets = Benchmarks,
            InstallRules = InstallRules,
            GenerationSignals = GenerationSignals,
            ConfigurationVariants = ConfigurationVariants,
            CompileDefinitions = CompileDefinitions,
            UnresolvedValues = Unresolved,
            LocalReferences = LocalReferences,
        };
    }
}
