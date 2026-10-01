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

    private static string Canonical(string path, int remainingLinks = 64)
    {
        if (remainingLinks == 0) throw new InvalidDataException("Directory aliases exceed the bounded resolution depth.");
        string full = Path.GetFullPath(path);
        string current = Path.GetPathRoot(full)!;
        foreach (string part in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            DirectoryInfo directory = new(current);
            if (directory.Exists && directory.LinkTarget is not null)
            {
                string target = directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                    ?? throw new InvalidDataException("Could not resolve a source/output directory alias.");
                // A final link target can still contain aliases in its parent directories (macOS /var).
                current = Canonical(target, remainingLinks - 1);
            }
        }
        return Path.GetFullPath(current);
    }
}
