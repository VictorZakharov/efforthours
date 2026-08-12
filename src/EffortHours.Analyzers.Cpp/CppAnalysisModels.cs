using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Cpp;

internal sealed record CppInclude(string Value, bool Quoted, int Line);

internal sealed record CppPreprocessorAnalysis
{
    public IReadOnlyList<CppInclude> Includes { get; init; } = [];
    public int Directives { get; init; }
    public int MacroDefinitions { get; init; }
    public int FunctionLikeMacros { get; init; }
    public int VariadicMacros { get; init; }
    public int StringifyOrPasteMacros { get; init; }
    public int ConditionalGroups { get; init; }
    public int ConditionalBranches { get; init; }
    public int MaximumDepth { get; init; }
    public int FeatureTests { get; init; }
    public int Diagnostics { get; init; }
    public int Embeds { get; init; }
    public bool IncludeGuard { get; init; }
    public bool PragmaOnce { get; init; }
    public bool Malformed { get; init; }
}

internal sealed record CppSyntaxAnalysis(
    string Confidence,
    CppPreprocessorAnalysis Preprocessor,
    CppSourceMetrics Metrics);

internal sealed class CppSourceMetrics
{
    public int Files { get; set; } = 1;
    public int TranslationUnits { get; set; }
    public int Headers { get; set; }
    public int Namespaces { get; set; }
    public int Functions { get; set; }
    public int Methods { get; set; }
    public int Types { get; set; }
    public int Classes { get; set; }
    public int Structs { get; set; }
    public int Unions { get; set; }
    public int Enums { get; set; }
    public int Typedefs { get; set; }
    public int PublicSymbols { get; set; }
    public int Templates { get; set; }
    public int Concepts { get; set; }
    public int Lambdas { get; set; }
    public int Modules { get; set; }
    public int Imports { get; set; }
    public int AsyncUnits { get; set; }
    public int BranchPoints { get; set; }
    public int ErrorPaths { get; set; }
    public int ConcurrencyUsages { get; set; }
    public int FfiBoundaries { get; set; }
    public int EntryPoints { get; set; }
    public int ApiSurfaces { get; set; }
    public int CliCommands { get; set; }
    public int DataCalls { get; set; }
    public int IntegrationCalls { get; set; }
    public int SecurityUsages { get; set; }
    public int ValidationRules { get; set; }
    public int UiSurfaces { get; set; }
    public int TestCases { get; set; }
    public int Assertions { get; set; }
    public int Benchmarks { get; set; }
    public int FuzzTargets { get; set; }
    public int DocumentationComments { get; set; }
    public int UnkeyedPublicSymbols { get; set; }
    public HashSet<string> Technologies { get; } = new(StringComparer.Ordinal);
    public HashSet<string> PublicDeclarationKeys { get; } = new(StringComparer.Ordinal);
}

internal sealed record CppProjectModel
{
    public required string Directory { get; init; }
    public required string Role { get; init; }
    public IReadOnlyList<string> ManifestPaths { get; init; } = [];
    public IReadOnlyList<string> BuildSystems { get; init; } = [];
    public IReadOnlyList<string> ExplicitSources { get; init; } = [];
    public IReadOnlyList<string> IncludeRoots { get; init; } = [];
    public IReadOnlyList<string> DependencyNames { get; init; } = [];
    public IReadOnlyList<string> TargetNames { get; init; } = [];
    public IReadOnlyList<string> LocalReferenceDirectories { get; init; } = [];
    public IReadOnlyList<string> DeclaredLanguages { get; init; } = [];
    public IReadOnlyList<string> Standards { get; init; } = [];
    public int Targets { get; init; }
    public int Executables { get; init; }
    public int Libraries { get; init; }
    public int Plugins { get; init; }
    public int TestTargets { get; init; }
    public int BenchmarkTargets { get; init; }
    public int InstallRules { get; init; }
    public int GenerationSignals { get; init; }
    public int ConfigurationVariants { get; init; }
    public int CompileDefinitions { get; init; }
    public int UnresolvedValues { get; init; }
    public int LocalReferences { get; init; }
}

internal sealed record CppBuildReadResult(
    IReadOnlyList<CppProjectModel> Projects,
    IReadOnlyList<Diagnostic> Diagnostics);

internal sealed record CppFileAnalysis(
    EvidenceFact File,
    CppProjectModel Project,
    CppSyntaxAnalysis Syntax,
    IReadOnlyList<string> LocalIncludes,
    bool OwnershipAmbiguous);

internal sealed record CppRawFileAnalysis(
    EvidenceFact File,
    CppSyntaxAnalysis Syntax);
