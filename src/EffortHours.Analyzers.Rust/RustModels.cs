using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Rust;

internal sealed record CargoDependency(
    string Name,
    string Kind,
    string? PackageName = null,
    string? PathDirectory = null,
    bool WorkspaceInherited = false);

internal sealed record CargoTarget(string Kind, string? Name, string? Path);

internal sealed record CargoPackageModel
{
    public required string Directory { get; init; }

    public required string Name { get; init; }

    public required string Role { get; init; }

    public IReadOnlyList<string> ManifestPaths { get; init; } = [];

    public IReadOnlyList<CargoDependency> Dependencies { get; init; } = [];

    public IReadOnlyList<CargoTarget> Targets { get; init; } = [];

    public IReadOnlyList<string> WorkspaceMembers { get; init; } = [];

    public int Features { get; init; }

    public int BuildScripts { get; init; }

    public int CrateTypes { get; init; }

    public int UnresolvedValues { get; init; }

    public bool IsWorkspace { get; init; }

    public bool IsVirtualWorkspace { get; init; }

    public bool IsProcMacro { get; init; }
}

internal sealed record CargoReadResult(
    IReadOnlyList<CargoPackageModel> Packages,
    IReadOnlyList<Diagnostic> Diagnostics);

internal sealed record RustFileAnalysis(
    EvidenceFact File,
    CargoPackageModel Package,
    RustSyntaxAnalysis Syntax);

internal static class RustPath
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

    public static string? ResolveWithinRepository(string directory, string value)
    {
        string normalized = value.Replace('\\', '/').Trim().TrimEnd('/');
        if (normalized.Length == 0 || Path.IsPathRooted(normalized) || normalized.Contains(':')) return null;
        List<string> segments = directory == "."
            ? []
            : [.. directory.Split('/', StringSplitOptions.RemoveEmptyEntries)];
        foreach (string segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (segments.Count == 0) return null;
                segments.RemoveAt(segments.Count - 1);
            }
            else
            {
                segments.Add(segment);
            }
        }

        return segments.Count == 0 ? "." : string.Join('/', segments);
    }
}
