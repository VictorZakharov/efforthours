using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Python;

internal sealed record PythonPackageModel
{
    public required string Directory { get; init; }

    public required string Name { get; init; }

    public required string Role { get; init; }

    public IReadOnlyList<string> ManifestPaths { get; init; } = [];

    public IReadOnlyList<string> Dependencies { get; init; } = [];

    public IReadOnlyList<string> Scripts { get; init; } = [];
}

internal sealed record PythonProjectReadResult(
    IReadOnlyList<PythonPackageModel> Packages,
    IReadOnlyList<Diagnostic> Diagnostics);

internal sealed class PythonMetadata
{
    public string? Name { get; set; }

    public HashSet<string> Dependencies { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Scripts { get; } = new(StringComparer.Ordinal);
}
