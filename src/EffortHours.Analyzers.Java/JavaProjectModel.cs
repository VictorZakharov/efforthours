using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Java;

internal sealed record JavaProjectModel
{
    public required string Directory { get; init; }

    public required string Name { get; init; }

    public string? Coordinate { get; init; }

    public string? Packaging { get; init; }

    public required string Role { get; init; }

    public IReadOnlyList<string> BuildSystems { get; init; } = [];

    public IReadOnlyList<string> ManifestPaths { get; init; } = [];

    public IReadOnlyList<string> Dependencies { get; init; } = [];

    public IReadOnlyList<string> Plugins { get; init; } = [];

    public IReadOnlyList<string> LocalProjectDirectories { get; init; } = [];

    public int UnresolvedValues { get; init; }

    public int MavenProfiles { get; init; }

    public int AnnotationProcessors { get; init; }
}

internal sealed record JavaProjectReadResult(
    IReadOnlyList<JavaProjectModel> Projects,
    IReadOnlyList<Diagnostic> Diagnostics);

internal sealed class JavaProjectMetadata
{
    public string? Name { get; set; }

    public string? Coordinate { get; set; }

    public string? Packaging { get; set; }

    public HashSet<string> BuildSystems { get; } = new(StringComparer.Ordinal);

    public HashSet<string> ManifestPaths { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Dependencies { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Plugins { get; } = new(StringComparer.Ordinal);

    public HashSet<string> LocalProjectDirectories { get; } = new(StringComparer.Ordinal);

    public int UnresolvedValues { get; set; }

    public int MavenProfiles { get; set; }

    public int AnnotationProcessors { get; set; }
}

internal static class JavaPath
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
