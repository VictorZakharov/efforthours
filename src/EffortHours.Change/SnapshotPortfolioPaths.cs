namespace EffortHours.Change;

/// <summary>Resolves existing directory aliases before checking source containment.</summary>
public static class SnapshotPortfolioPaths
{
    public static bool IsWithin(string root, string path)
    {
        string relative = Path.GetRelativePath(Canonical(root), Canonical(path));
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !Path.IsPathRooted(relative);
    }

    private static string Canonical(string path)
    {
        string full = Path.GetFullPath(path);
        string current = Path.GetPathRoot(full)!;
        foreach (string part in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            DirectoryInfo directory = new(current);
            if (directory.Exists && directory.LinkTarget is not null)
                current = directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                    ?? throw new InvalidDataException("Could not resolve a source/output directory alias.");
        }
        return Path.GetFullPath(current);
    }
}
