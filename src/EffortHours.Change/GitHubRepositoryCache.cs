namespace EffortHours.Change;

internal sealed record RepositoryAcquisitionResult(
    string RepositoryPath,
    int LocalHeadCount,
    int AcquiredObjectCount,
    long AcquiredBytes,
    int AcquiredHeadCount);

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
        CancellationToken cancellationToken) => await EnsureAsync(
            repositoryIdentity,
            heads,
            fetchMissing: true,
            cancellationToken).ConfigureAwait(false);

    public async Task<RepositoryAcquisitionResult> EnsureAsync(
        string repositoryIdentity,
        IReadOnlyList<DiscoveredHead> heads,
        bool fetchMissing,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(heads);
        if (heads.Count is < 1 or > 32)
        {
            throw new ArgumentException(
                "Managed repository acquisition requires between 1 and 32 immutable heads.",
                nameof(heads));
        }

        string path = RepositoryPath(repositoryIdentity);
        await using FileStream cacheLock = await AcquireLockAsync(path, cancellationToken)
            .ConfigureAwait(false);
        if (!Directory.Exists(path) && !fetchMissing)
        {
            throw MissingCacheException();
        }

        await EnsureBareRepositoryAsync(path, fetchMissing, cancellationToken).ConfigureAwait(false);
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
            if (!fetchMissing)
            {
                throw MissingCacheException();
            }

            await _git.FetchManagedObjectsAsync(
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
            Math.Max(0, after.Bytes - before.Bytes),
            missing.Count);
    }

    public async Task<string> UseExistingAsync(
        string repositoryIdentity,
        IReadOnlyList<DiscoveredHead> heads,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(heads);
        string path = RepositoryPath(repositoryIdentity);
        if (!Directory.Exists(path))
        {
            throw MissingCacheException();
        }

        await ValidateBareRepositoryAsync(path, cancellationToken).ConfigureAwait(false);
        foreach (DiscoveredHead head in heads)
        {
            if (!await _git.CommitExistsAsync(path, head.ObjectId, cancellationToken)
                .ConfigureAwait(false))
            {
                throw MissingCacheException();
            }
        }

        return path;
    }

    private async Task EnsureBareRepositoryAsync(
        string path,
        bool allowCreate,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(path))
        {
            if (!allowCreate)
            {
                throw MissingCacheException();
            }

            string parent = Path.GetDirectoryName(path) ??
                throw new InvalidOperationException("The managed repository cache path has no parent.");
            Directory.CreateDirectory(_root);
            SetPrivateDirectoryMode(_root);
            Directory.CreateDirectory(parent);
            SetPrivateDirectoryMode(parent);
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                await _commands.RunAsync(
                    "git",
                    parent,
                    ["init", "--bare", temporary],
                    cancellationToken).ConfigureAwait(false);
                await ValidateBareRepositoryAsync(temporary, cancellationToken).ConfigureAwait(false);
                Directory.Move(temporary, path);
            }
            catch (Exception exception) when (
                exception is ExternalCommandException or IOException or UnauthorizedAccessException)
            {
                TryDeleteDirectory(temporary);
                throw new InvalidOperationException(
                    "EffortHours could not initialize its managed bare repository cache entry.",
                    exception);
            }
        }

        await ValidateBareRepositoryAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private async Task ValidateBareRepositoryAsync(
        string path,
        CancellationToken cancellationToken)
    {
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
        string normalized = GitHubRepositoryIdentity.Normalize(identity);
        string[] parts = normalized.Split('/');

        string path = Path.GetFullPath(Path.Combine(
            _root,
            parts[0],
            parts[1] + ".git"));
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

    internal static string ResolveRoot()
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

    private static InvalidOperationException MissingCacheException() => new(
        "The managed repository cache does not contain the selected immutable Git objects. " +
        "Retry the query with --fetch-missing to resolve or acquire only its required objects; " +
        "EffortHours does not access the network without that explicit authorization.");

    private static async Task<FileStream> AcquireLockAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        string parent = Path.GetDirectoryName(repositoryPath) ??
            throw new InvalidOperationException("The managed repository cache path has no parent.");
        Directory.CreateDirectory(parent);
        SetPrivateDirectoryMode(parent);
        string lockPath = repositoryPath + ".lock";
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        while (System.Diagnostics.Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(30))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.Asynchronous);
            }
            catch (IOException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new InvalidOperationException(
                    "EffortHours could not access its managed repository cache lock.",
                    exception);
            }
        }

        throw new InvalidOperationException(
            "Timed out waiting for another EffortHours process to finish managed repository cache acquisition.");
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record GitObjectStorage(long ObjectCount, long Bytes);
}
