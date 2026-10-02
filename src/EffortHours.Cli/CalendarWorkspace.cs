using EffortHours.Change;

namespace EffortHours.Cli;

internal static class CalendarWorkspace
{
    public static async Task<string?> RootAsync(string path, CancellationToken token)
    {
        try { return await new GitClient().ResolveRepositoryRootAsync(path, token).ConfigureAwait(false); }
        catch (ExternalCommandException) { return null; }
    }

    public static IReadOnlyList<(string Id, string Locator, bool Provider)> Discover(string workspace, CancellationToken token)
    {
        string root = Path.GetFullPath(workspace);
        if (!Directory.Exists(root)) throw new ArgumentException("Workspace directory does not exist.");
        Stack<string> pending = new([root]);
        List<string> found = [];
        int visited = 0;
        while (pending.TryPop(out string? path))
        {
            token.ThrowIfCancellationRequested();
            if (++visited > 10000) throw new ArgumentException("Workspace exceeds 10000 directories; choose a narrower workspace or explicit projects.");
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
            if (Directory.Exists(Path.Combine(path, ".git")) || File.Exists(Path.Combine(path, ".git")))
            {
                found.Add(path);
                if (found.Count > 256) throw new ArgumentException("Workspace exceeds 256 repositories; use explicit projects.");
                continue;
            }
            foreach (string child in Directory.EnumerateDirectories(path).Order(StringComparer.Ordinal))
                if (!Path.GetFileName(child).StartsWith('.') && Path.GetFileName(child) is not ("node_modules" or "bin" or "obj"))
                    pending.Push(child);
        }
        if (found.Count == 0) throw new ArgumentException("No Git repositories found; specify --workspace or --project id=path.");
        return [.. found.Order(StringComparer.Ordinal).Select((path, index) =>
            ($"project-{index + 1}", path, false))];
    }
}
