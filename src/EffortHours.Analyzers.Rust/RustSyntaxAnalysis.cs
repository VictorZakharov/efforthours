namespace EffortHours.Analyzers.Rust;

internal sealed record RustSyntaxAnalysis(
    string Confidence,
    IReadOnlySet<string> ImportedCrates,
    RustSourceMetrics Metrics);

internal sealed class RustImportContext
{
    public HashSet<string> Crates { get; } = new(StringComparer.Ordinal);

    public HashSet<string> LocalCrates { get; } = new(StringComparer.Ordinal);

    public HashSet<string> LocalModules { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Technologies { get; } = new(StringComparer.Ordinal);
}

internal sealed class RustSourceMetrics
{
    public int Modules { get; set; }
    public int Uses { get; set; }
    public int InternalImports { get; set; }
    public int Functions { get; set; }
    public int Methods { get; set; }
    public int Types { get; set; }
    public int Structs { get; set; }
    public int Enums { get; set; }
    public int Traits { get; set; }
    public int Unions { get; set; }
    public int Impls { get; set; }
    public int PublicSymbols { get; set; }
    public int GenericDeclarations { get; set; }
    public int LifetimeUsages { get; set; }
    public int AsyncUnits { get; set; }
    public int AwaitPoints { get; set; }
    public int BranchPoints { get; set; }
    public int UnsafeBlocks { get; set; }
    public int ErrorPaths { get; set; }
    public int DocumentationComments { get; set; }
    public int MacroDefinitions { get; set; }
    public int MacroInvocations { get; set; }
    public int AttributeMacros { get; set; }
    public int FeatureGates { get; set; }
    public int ExternalModules { get; set; }
    public int GeneratedBindingSignals { get; set; }
    public int EntryPoints { get; set; }
    public int ApiSurfaces { get; set; }
    public int DataCalls { get; set; }
    public int IntegrationCalls { get; set; }
    public int SecurityUsages { get; set; }
    public int CliCommands { get; set; }
    public int BackgroundUsages { get; set; }
    public int ConcurrencyUsages { get; set; }
    public int FfiBoundaries { get; set; }
    public int ValidationRules { get; set; }
    public int TestCases { get; set; }
    public int Assertions { get; set; }
    public int IntegrationTests { get; set; }
    public int Benchmarks { get; set; }
    public int Examples { get; set; }
    public int DocumentationTests { get; set; }
    public int ParameterizedCases { get; set; }
    public int MockUsages { get; set; }
    public bool BuildScript { get; set; }
    public bool IsTestTarget { get; set; }
    public HashSet<string> Technologies { get; } = new(StringComparer.Ordinal);
}
