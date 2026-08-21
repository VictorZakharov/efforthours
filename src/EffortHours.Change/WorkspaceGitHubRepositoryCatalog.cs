using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed record WorkspaceGitHubRepository(
    string RootPath,
    IReadOnlyList<string> RepositoryIdentities,
    IReadOnlyList<string> IdentityAliases);

internal static class WorkspaceGitHubRepositoryCatalog
{
    private const int MaximumVisitedDirectories = 4_096;
    private const int MaximumRepositories = ChangeAuthorPeriodManifestLimits.MaximumRepositories;
    private const int MaximumDepth = 3;

    public static async Task<IReadOnlyList<WorkspaceGitHubRepository>> DiscoverAsync(
        string workspacePath,
        IExternalCommandRunner commands,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(commands);
        string workspace = Path.GetFullPath(workspacePath);
        Queue<(string Path, int Depth)> pending = new();
        pending.Enqueue((workspace, 0));
        HashSet<string> visited = new(PathComparer);
        List<WorkspaceGitHubRepository> repositories = [];
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (string path, int depth) = pending.Dequeue();
            if (!visited.Add(path))
            {
                continue;
            }

            if (visited.Count > MaximumVisitedDirectories)
            {
                throw new InvalidOperationException(
                    $"Workspace discovery exceeded {MaximumVisitedDirectories} directories.");
            }

            bool isRepositoryBoundary = false;
            if (IsGitWorktree(path))
            {
                (bool isRepository, WorkspaceGitHubRepository? repository) = await ReadRepositoryAsync(
                    path,
                    commands,
                    cancellationToken).ConfigureAwait(false);
                isRepositoryBoundary = isRepository;
                if (repository is not null)
                {
                    repositories.Add(repository);
                    if (repositories.Count > MaximumRepositories)
                    {
                        throw new InvalidOperationException(
                            $"Workspace discovery exceeded {MaximumRepositories} Git repositories.");
                    }
                }

            }

            if (isRepositoryBoundary || depth >= MaximumDepth)
            {
                continue;
            }

            foreach (string child in EnumerateDirectories(path))
            {
                pending.Enqueue((child, depth + 1));
            }
        }

        return [.. repositories
            .DistinctBy(repository => repository.RootPath, PathComparer)
            .OrderBy(repository => repository.RootPath, PathComparer)];
    }

    private static async Task<(bool IsRepository, WorkspaceGitHubRepository? Repository)> ReadRepositoryAsync(
        string path,
        IExternalCommandRunner commands,
        CancellationToken cancellationToken)
    {
        ExternalCommandResult root = await commands.RunAsync(
            "git",
            path,
            ["rev-parse", "--show-toplevel"],
            cancellationToken,
            requireSuccess: false).ConfigureAwait(false);
        if (root.ExitCode != 0 || string.IsNullOrWhiteSpace(root.StandardOutput))
        {
            if (root.StandardError.Contains("dubious ownership", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "A workspace repository was rejected by Git's dubious-ownership safety check. " +
                    "Configure only that checkout as a process-local safe.directory and retry.");
            }

            if (root.StandardError.Contains("not a git repository", StringComparison.OrdinalIgnoreCase) ||
                root.StandardError.Contains("invalid gitfile format", StringComparison.OrdinalIgnoreCase))
            {
                return (false, null);
            }

            throw new InvalidOperationException(
                "A discovered Git worktree could not be opened for remote-identity mapping.");
        }

        string canonicalRoot = Path.GetFullPath(root.StandardOutput.Trim());
        ExternalCommandResult remotes = await commands.RunAsync(
            "git",
            canonicalRoot,
            ["config", "--get-regexp", "^remote\\..*\\.url$"],
            cancellationToken,
            requireSuccess: false).ConfigureAwait(false);
        string[] identities = remotes.ExitCode == 0
            ? [.. remotes.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length == 2)
                .Select(parts => TryNormalizeRemote(parts[1]))
                .Where(identity => identity is not null)
                .Select(identity => identity!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)]
            : [];
        if (identities.Length == 0)
        {
            return (true, null);
        }

        List<string> aliases = [];
        await AddConfigValueAsync(commands, canonicalRoot, "user.name", aliases, cancellationToken)
            .ConfigureAwait(false);
        await AddConfigValueAsync(commands, canonicalRoot, "user.email", aliases, cancellationToken)
            .ConfigureAwait(false);
        return (
            true,
            new WorkspaceGitHubRepository(
                canonicalRoot,
                identities,
                [.. aliases
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)]));
    }

    private static async Task AddConfigValueAsync(
        IExternalCommandRunner commands,
        string repositoryPath,
        string key,
        List<string> aliases,
        CancellationToken cancellationToken)
    {
        ExternalCommandResult result = await commands.RunAsync(
            "git",
            repositoryPath,
            ["config", "--get", key],
            cancellationToken,
            requireSuccess: false).ConfigureAwait(false);
        if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            aliases.Add(result.StandardOutput.Trim());
        }
    }

    internal static string? TryNormalizeRemote(string value)
    {
        string remote = value.Trim();
        if (remote.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizePath(remote["git@github.com:".Length..]);
        }

        if (!Uri.TryCreate(remote, UriKind.Absolute, out Uri? uri) ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme is not ("https" or "ssh" or "git"))
        {
            return null;
        }

        return NormalizePath(uri.AbsolutePath.TrimStart('/'));
    }

    private static string? NormalizePath(string path)
    {
        string normalized = path.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? path[..^4]
            : path;
        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || parts.Any(part => part is "." or ".." ||
            part.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.')))
        {
            return null;
        }

        return (parts[0] + "/" + parts[1]).ToLowerInvariant();
    }

    private static bool IsGitWorktree(string path) =>
        Directory.Exists(Path.Combine(path, ".git")) || File.Exists(Path.Combine(path, ".git"));

    private static IReadOnlyList<string> EnumerateDirectories(string path)
    {
        try
        {
            return [.. Directory.EnumerateDirectories(path)
                .Where(directory =>
                    !PathComparer.Equals(Path.GetFileName(directory), ".git") &&
                    !IsReparsePoint(directory))
                .Order(PathComparer)];
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return [];
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or FileNotFoundException)
        {
            return true;
        }
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
