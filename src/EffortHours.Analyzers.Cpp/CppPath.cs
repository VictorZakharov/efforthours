namespace EffortHours.Analyzers.Cpp;

internal static class CppPath
{
    public static string Directory(string path)
    {
        string normalized = Normalize(path);
        int separator = normalized.LastIndexOf('/');
        return separator < 0 ? "." : normalized[..separator];
    }

    public static string Normalize(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.StartsWith("./", StringComparison.Ordinal) ? normalized[2..] : normalized;
    }

    public static bool IsWithin(string path, string directory) =>
        directory == "." || path.Equals(directory, StringComparison.Ordinal) ||
        path.StartsWith(directory + "/", StringComparison.Ordinal);

    public static string? ResolveWithinRepository(string directory, string value)
    {
        string normalized = value.Replace('\\', '/').Trim().Trim('"', '\'').TrimEnd('/');
        if (normalized.Length == 0 || Path.IsPathRooted(normalized) || normalized.Contains(':') ||
            normalized.Contains('$') || normalized.Contains('*') || normalized.Contains('?'))
            return null;
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

    public static bool IsSource(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".c" or ".cc" or ".cpp" or ".cxx" or ".cppm" or ".ixx";

    public static bool IsHeader(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".h" or ".hh" or ".hpp" or ".hxx" or ".inl" or ".ipp" or ".tpp";
}
