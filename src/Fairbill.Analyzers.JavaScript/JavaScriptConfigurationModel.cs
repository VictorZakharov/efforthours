namespace Fairbill.Analyzers.JavaScript;

internal sealed record JavaScriptConfigurationModel
{
    public required string Path { get; init; }

    public required string Scope { get; init; }

    public required string Kind { get; init; }

    public string? Extends { get; init; }

    public IReadOnlyList<string> References { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];
}

internal sealed record JavaScriptConfigurationReadResult(
    IReadOnlyList<JavaScriptConfigurationModel> Configurations,
    IReadOnlyList<Fairbill.Contracts.V1.Diagnostic> Diagnostics);

internal sealed record JavaScriptWorkspaceDeclaration(
    string Scope,
    string SourcePath,
    string PackageManager,
    IReadOnlyList<string> Patterns);
