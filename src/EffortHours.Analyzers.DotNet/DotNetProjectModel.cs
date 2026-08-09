namespace EffortHours.Analyzers.DotNet;

internal sealed record DotNetProjectModel
{
    public required string Path { get; init; }

    public required string Directory { get; init; }

    public required string Name { get; init; }

    public string? Sdk { get; init; }

    public required string Role { get; init; }

    public IReadOnlyList<string> TargetFrameworks { get; init; } = [];

    public IReadOnlyList<DotNetPackageReference> Packages { get; init; } = [];

    public IReadOnlyList<DotNetProjectReference> ProjectReferences { get; init; } = [];

    public IReadOnlyList<string> FrameworkReferences { get; init; } = [];

    public string? OutputType { get; init; }

    public string? LanguageVersion { get; init; }

    public string? Nullable { get; init; }

    public string? RuntimeIdentifier { get; init; }

    public bool IsPackable { get; init; }

    public bool PublishAot { get; init; }

    public bool UsesWpf { get; init; }

    public bool UsesWindowsForms { get; init; }

    public int UnresolvedValueCount { get; init; }
}

internal sealed record DotNetPackageReference(string Id, string? Version);

internal sealed record DotNetProjectReference(
    string DeclaredPath,
    string? ResolvedPath,
    bool Exists,
    bool OutsideScope);

internal sealed record DotNetSolutionModel(
    string Path,
    IReadOnlyList<string> DeclaredProjectPaths,
    IReadOnlyList<string> ResolvedProjectPaths);
