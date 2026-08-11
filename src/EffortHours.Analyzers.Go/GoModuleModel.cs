using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Go;

internal sealed record GoModuleModel
{
    public required string Directory { get; init; }

    public required string ModulePath { get; init; }

    public required string Role { get; init; }

    public IReadOnlyList<string> ManifestPaths { get; init; } = [];

    public IReadOnlyList<string> Dependencies { get; init; } = [];

    public IReadOnlyList<GoLocalReplacement> LocalReplacements { get; init; } = [];
}

internal sealed record GoLocalReplacement(string ModulePath, string TargetDirectory);

internal sealed record GoWorkspaceModel(
    string Path,
    IReadOnlyList<string> ModuleDirectories,
    IReadOnlyList<GoLocalReplacement> LocalReplacements);

internal sealed record GoProjectReadResult(
    IReadOnlyList<GoModuleModel> Modules,
    GoWorkspaceModel? Workspace,
    IReadOnlyList<Diagnostic> Diagnostics);

internal sealed class GoModuleMetadata
{
    public string? ModulePath { get; set; }

    public HashSet<string> Dependencies { get; } = new(StringComparer.Ordinal);

    public List<GoLocalReplacement> LocalReplacements { get; } = [];
}

internal static class GoPath
{
    public static string Directory(string path)
    {
        string normalized = path.Replace('\\', '/');
        int separator = normalized.LastIndexOf('/');
        return separator < 0 ? "." : normalized[..separator];
    }

    public static bool IsWithin(string path, string directory) =>
        directory == "." || path.Equals(directory, StringComparison.Ordinal) ||
        path.StartsWith(directory + "/", StringComparison.Ordinal);
}
