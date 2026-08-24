namespace EffortHours.Change;

internal sealed record RepositoryAcquisitionResult(
    string RepositoryPath,
    int LocalHeadCount,
    int AcquiredObjectCount,
    long AcquiredBytes);

internal sealed class GitHubRepositoryCache
{
    private const string CacheEnvironmentVariable = "EFFORTHOURS_REPOSITORY_CACHE";
    private readonly IExternalCommandRunner _commands;
    private readonly GitClient _git;
    private readonly string _root;
    private readonly Func<string, string> _fetchSource;

    public GitHubRepositoryCache()
        : this(new ExternalCommandRunner(), new GitClient(), ResolveRoot())
    {
    }

    internal GitHubRepositoryCache(
        IExternalCommandRunner commands,
        GitClient git,
        string root,
        Func<string, string>? fetchSource = null)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _git = git ?? throw new ArgumentNullException(nameof(git));
        _root = Path.GetFullPath(root);
        _fetchSource = fetchSource ?? (identity => "https://github.com/" + identity + ".git");
    }

    public async Task<RepositoryAcquisitionResult> EnsureAsync(
        string repositoryIdentity,
        IReadOnlyList<DiscoveredHead> heads,
        CancellationToken cancellationToken)
    {
        string path = RepositoryPath(repositoryIdentity);
        await EnsureBareRepositoryAsync(path, cancellationToken).ConfigureAwait(false);
        GitObjectStorage before = await MeasureAsync(path, cancellationToken).ConfigureAwait(false);
        List<DiscoveredHead> missing = [];
        int local = 0;
        foreach (DiscoveredHead head in heads)
        {
            if (await _git.CommitExistsAsync(path, head.ObjectId, cancellationToken)
                .ConfigureAwait(false))
            {
                local++;
            }
            else
            {
                missing.Add(head);
            }
        }

        if (missing.Count > 0)
        {
            await _git.FetchAuthorPeriodHeadObjectsAsync(
                path,
                _fetchSource(repositoryIdentity),
                [.. missing.Select(head => head.FetchRef)],
                cancellationToken).ConfigureAwait(false);
            foreach (DiscoveredHead head in missing)
            {
                if (!await _git.CommitExistsAsync(path, head.ObjectId, cancellationToken)
                    .ConfigureAwait(false))
                {
                    throw new InvalidOperationException(
                        "A discovered immutable head is unavailable after narrow managed-cache acquisition; " +
                        "the provider ref may have moved during discovery.");
                }
            }
        }

        GitObjectStorage after = await MeasureAsync(path, cancellationToken).ConfigureAwait(false);
        return new RepositoryAcquisitionResult(
            path,
            local,
            checked((int)Math.Min(int.MaxValue, Math.Max(0, after.ObjectCount - before.ObjectCount))),
            Math.Max(0, after.Bytes - before.Bytes));
    }

    private async Task EnsureBareRepositoryAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(path))
        {
            string parent = Path.GetDirectoryName(path) ??
                throw new InvalidOperationException("The managed repository cache path has no parent.");
            Directory.CreateDirectory(_root);
            SetPrivateDirectoryMode(_root);
            Directory.CreateDirectory(parent);
            SetPrivateDirectoryMode(parent);
            await _commands.RunAsync(
                "git",
                parent,
                ["init", "--bare", path],
                cancellationToken).ConfigureAwait(false);
        }

        ExternalCommandResult result = await _commands.RunAsync(
            "git",
            path,
            ["rev-parse", "--is-bare-repository"],
            cancellationToken).ConfigureAwait(false);
        if (!result.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The managed repository cache entry is not a bare Git repository.");
        }
    }

    private async Task<GitObjectStorage> MeasureAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ExternalCommandResult result = await _commands.RunAsync(
            "git",
            path,
            ["count-objects", "-v"],
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, long> values = result.StandardOutput.ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length == 2 && long.TryParse(parts[1], out _))
            .ToDictionary(
                parts => parts[0].TrimEnd(':'),
                parts => long.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                StringComparer.Ordinal);
        long objects = values.GetValueOrDefault("count") + values.GetValueOrDefault("in-pack");
        long bytes = checked((values.GetValueOrDefault("size") +
            values.GetValueOrDefault("size-pack")) * 1024L);
        return new GitObjectStorage(objects, bytes);
    }

    private string RepositoryPath(string identity)
    {
        string[] parts = identity.Split('/');
        if (parts.Length != 2 || parts.Any(part => string.IsNullOrWhiteSpace(part)) ||
            parts.Any(part => part.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.')))
        {
            throw new ArgumentException("The GitHub repository identity is invalid.", nameof(identity));
        }

        string path = Path.GetFullPath(Path.Combine(
            _root,
            parts[0].ToLowerInvariant(),
            parts[1].ToLowerInvariant() + ".git"));
        string relative = Path.GetRelativePath(_root, path);
        if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The managed repository cache path escaped its root.");
        }

        return path;
    }

    private static void SetPrivateDirectoryMode(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static string ResolveRoot()
    {
        string? configured = Environment.GetEnvironmentVariable(CacheEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
        {
            throw new InvalidOperationException(
                "The host does not provide a local application-data directory for the managed repository cache.");
        }

        return Path.Combine(local, "EffortHours", "repositories", "github");
    }

    private sealed record GitObjectStorage(long ObjectCount, long Bytes);
}
