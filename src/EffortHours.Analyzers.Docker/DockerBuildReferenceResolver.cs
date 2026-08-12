namespace EffortHours.Analyzers.Docker;

internal static class DockerBuildReferenceResolver
{
    public static ComposeReferenceResolution Resolve(
        string composePath,
        IReadOnlyList<ComposeBuildReference> references,
        IReadOnlySet<string> admittedPaths)
    {
        string composeDirectory = DirectoryOf(composePath);
        List<ResolvedDockerfileReference> resolved = [];
        int missing = 0;
        int unresolved = 0;
        int external = 0;
        foreach (ComposeBuildReference reference in references)
        {
            if (reference.Dynamic)
            {
                unresolved++;
                continue;
            }

            string context = reference.Context ?? ".";
            if (IsExternal(context))
            {
                external++;
                continue;
            }

            string? contextPath = NormalizeRelative(composeDirectory, context);
            string? targetPath = contextPath is null
                ? null
                : NormalizeRelative(contextPath, reference.Dockerfile ?? "Dockerfile");
            if (targetPath is null)
            {
                unresolved++;
                continue;
            }

            if (!admittedPaths.Contains(targetPath))
            {
                missing++;
                continue;
            }

            resolved.Add(new ResolvedDockerfileReference(composePath, targetPath));
        }

        return new ComposeReferenceResolution(
            [.. resolved.Distinct().OrderBy(item => item.TargetPath, StringComparer.Ordinal)],
            missing,
            unresolved,
            external);
    }

    private static string? NormalizeRelative(string baseDirectory, string value)
    {
        string normalized = value.Replace('\\', '/').Trim();
        if (normalized.Length == 0 || normalized.StartsWith('/') || normalized.Contains(':')) return null;
        List<string> segments = [.. baseDirectory
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment != ".")];
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

    private static bool IsExternal(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("git://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("docker-image://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("service:", StringComparison.OrdinalIgnoreCase);

    private static string DirectoryOf(string path)
    {
        int separator = path.LastIndexOf('/');
        return separator < 0 ? "." : path[..separator];
    }
}

internal sealed record ResolvedDockerfileReference(string SourcePath, string TargetPath);

internal sealed record ComposeReferenceResolution(
    IReadOnlyList<ResolvedDockerfileReference> Resolved,
    int Missing,
    int Unresolved,
    int External);
