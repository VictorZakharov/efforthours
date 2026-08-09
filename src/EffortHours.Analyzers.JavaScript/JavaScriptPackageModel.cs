namespace EffortHours.Analyzers.JavaScript;

internal sealed record JavaScriptPackageModel
{
    public required string ManifestPath { get; init; }

    public required string Scope { get; init; }

    public string? Name { get; init; }

    public string? Version { get; init; }

    public string? ModuleType { get; init; }

    public string? PackageManager { get; init; }

    public bool IsPrivate { get; init; }

    public bool HasBin { get; init; }

    public bool HasLibraryExports { get; init; }

    public required string Role { get; init; }

    public IReadOnlyList<string> WorkspacePatterns { get; init; } = [];

    public IReadOnlyList<string> ScriptNames { get; init; } = [];

    public IReadOnlyList<JavaScriptDependency> Dependencies { get; init; } = [];

    public IReadOnlyList<string> Technologies { get; init; } = [];

    public JavaScriptCoverageDeclaration? Coverage { get; init; }
}

internal sealed record JavaScriptDependency(
    string Name,
    string Specifier,
    string Kind);

internal sealed record JavaScriptCoverageDeclaration(
    decimal? Lines,
    decimal? Branches,
    decimal? Functions,
    decimal? Statements);

internal sealed record JavaScriptPackageReadResult(
    IReadOnlyList<JavaScriptPackageModel> Packages,
    IReadOnlyList<EffortHours.Contracts.V1.Diagnostic> Diagnostics);
