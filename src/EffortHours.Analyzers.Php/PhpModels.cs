using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Php;

internal sealed record PhpPackageModel
{
    public required string Directory { get; init; }

    public required string Name { get; init; }

    public required string Role { get; init; }

    public string? PackageType { get; init; }

    public IReadOnlyList<string> ManifestPaths { get; init; } = [];

    public IReadOnlyList<PhpDependency> Dependencies { get; init; } = [];

    public IReadOnlyList<string> AutoloadNamespaces { get; init; } = [];

    public IReadOnlyList<string> AutoloadRoots { get; init; } = [];

    public IReadOnlyList<string> ScriptNames { get; init; } = [];

    public IReadOnlyList<string> PathRepositoryDirectories { get; init; } = [];

    public int AutoloadMappings { get; init; }

    public int AutoloadFiles { get; init; }

    public int BinEntries { get; init; }

    public int ExternalRepositories { get; init; }

    public int UnresolvedPaths { get; init; }
}

internal sealed record PhpDependency(string Name, string Kind);

internal sealed record PhpComposerReadResult(
    IReadOnlyList<PhpPackageModel> Packages,
    IReadOnlyList<Diagnostic> Diagnostics);

internal sealed record PhpFileAnalysis(
    EvidenceFact File,
    PhpPackageModel Package,
    PhpSyntaxAnalysis Syntax,
    PhpTemplateMetrics Template);

internal static class PhpPath
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
