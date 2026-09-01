using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EffortHours.Change;

internal sealed record ManagedGitResolution(
    string Kind,
    string RepositoryIdentity,
    IReadOnlyList<ResolvedGitRevision> Revisions);

internal sealed class GitHubRevisionResolutionCache
{
    private const int MaximumBytes = 64 * 1024;
    private const string Protocol = "github-git-revision-resolution-cache/1.0.0";
    private readonly string _root;

    public GitHubRevisionResolutionCache()
        : this(GitHubRepositoryCache.ResolveRoot())
    {
    }

    internal GitHubRevisionResolutionCache(string root)
    {
        _root = Path.GetFullPath(root);
    }

    public Task<FileStream> AcquireUpdateLockAsync(
        string kind,
        string repositoryIdentity,
        IReadOnlyList<string> selectors,
        CancellationToken cancellationToken)
    {
        ManagedGitResolution request = Request(kind, repositoryIdentity, selectors);
        return AcquireLockAsync(ResolutionPath(request) + ".lock", cancellationToken);
    }

    public async Task<ManagedGitResolution?> LoadAsync(
        string kind,
        string repositoryIdentity,
        IReadOnlyList<string> selectors,
        CancellationToken cancellationToken)
    {
        ManagedGitResolution request = Request(kind, repositoryIdentity, selectors);
        string path = ResolutionPath(request);
        if (!File.Exists(path))
        {
            return null;
        }

        FileInfo file = new(path);
        if (file.Length is <= 0 or > MaximumBytes)
        {
            throw InvalidCache();
        }

        try
        {
            string json = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);
            CachedGitResolution cached = JsonSerializer.Deserialize<CachedGitResolution>(json, JsonOptions)
                ?? throw new JsonException("The cached document was empty.");
            ManagedGitResolution resolution = Validate(cached, request);
            return resolution;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "The managed Git revision cache entry is unavailable or invalid. " +
                "Retry with --fetch-missing to resolve and refresh the immutable provider revisions.",
                exception);
        }
    }

    public async Task SaveAsync(
        ManagedGitResolution resolution,
        CancellationToken cancellationToken)
    {
        ManagedGitResolution request = Request(
            resolution.Kind,
            resolution.RepositoryIdentity,
            [.. resolution.Revisions.Select(revision => revision.Selector)]);
        CachedGitResolution cached = new()
        {
            Protocol = Protocol,
            Kind = request.Kind,
            RepositoryIdentity = request.RepositoryIdentity,
            Revisions = [.. resolution.Revisions.Select(revision => new CachedRevision
            {
                Selector = revision.Selector,
                ObjectId = revision.ObjectId,
            })],
        };
        _ = Validate(cached, request);
        string path = ResolutionPath(request);
        string directory = Path.GetDirectoryName(path) ??
            throw new InvalidOperationException("The managed Git revision cache path has no parent.");
        Directory.CreateDirectory(directory);
        SetPrivateDirectoryMode(directory);
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            string json = JsonSerializer.Serialize(cached, JsonOptions);
            if (Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            {
                throw new InvalidOperationException(
                    "The managed Git revision cache entry exceeds its size bound.");
            }

            await File.WriteAllTextAsync(
                temporary,
                json,
                new UTF8Encoding(false),
                cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            TryDelete(temporary);
            throw new InvalidOperationException(
                "EffortHours could not publish its managed Git revision cache entry.",
                exception);
        }
    }

    private static ManagedGitResolution Request(
        string kind,
        string repositoryIdentity,
        IReadOnlyList<string> selectors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(selectors);
        if (selectors.Count is < 1 or > 3 || selectors.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Managed Git resolution requires between one and three selectors.",
                nameof(selectors));
        }

        return new ManagedGitResolution(
            kind,
            GitHubRepositoryIdentity.Normalize(repositoryIdentity),
            [.. selectors.Select(selector => new ResolvedGitRevision(selector, string.Empty))]);
    }

    private static ManagedGitResolution Validate(
        CachedGitResolution cached,
        ManagedGitResolution request)
    {
        if (cached.Protocol != Protocol ||
            cached.Kind != request.Kind ||
            !string.Equals(
                cached.RepositoryIdentity,
                request.RepositoryIdentity,
                StringComparison.Ordinal) ||
            cached.Revisions.Count != request.Revisions.Count)
        {
            throw InvalidCache();
        }

        List<ResolvedGitRevision> revisions = [];
        for (int index = 0; index < cached.Revisions.Count; index++)
        {
            CachedRevision cachedRevision = cached.Revisions[index];
            if (cachedRevision.Selector != request.Revisions[index].Selector ||
                cachedRevision.ObjectId.Length is not (40 or 64) ||
                cachedRevision.ObjectId.Any(character => !Uri.IsHexDigit(character)))
            {
                throw InvalidCache();
            }

            revisions.Add(new ResolvedGitRevision(
                cachedRevision.Selector,
                cachedRevision.ObjectId.ToLowerInvariant()));
        }

        return new ManagedGitResolution(request.Kind, request.RepositoryIdentity, revisions);
    }

    private string ResolutionPath(ManagedGitResolution request)
    {
        string[] parts = request.RepositoryIdentity.Split('/');
        string identity = request.Kind + "\n" + string.Join(
            "\n",
            request.Revisions.Select(revision => revision.Selector));
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        string path = Path.GetFullPath(Path.Combine(
            _root,
            ".revisions",
            parts[0],
            parts[1],
            digest + ".json"));
        string relative = Path.GetRelativePath(_root, path);
        if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The managed Git revision cache path escaped its root.");
        }

        return path;
    }

    private static InvalidOperationException InvalidCache() => new(
        "The managed Git revision cache entry failed validation.");

    private static void SetPrivateDirectoryMode(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static async Task<FileStream> AcquireLockAsync(
        string path,
        CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(path) ??
            throw new InvalidOperationException("The managed resolution lock has no parent.");
        Directory.CreateDirectory(directory);
        SetPrivateDirectoryMode(directory);
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        while (System.Diagnostics.Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(30))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    path,
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
                    "EffortHours could not access its managed resolution cache lock.",
                    exception);
            }
        }

        throw new InvalidOperationException(
            "Timed out waiting for another EffortHours process to refresh the same provider identity.");
    }

    private sealed record CachedGitResolution
    {
        public required string Protocol { get; init; }

        public required string Kind { get; init; }

        public required string RepositoryIdentity { get; init; }

        public IReadOnlyList<CachedRevision> Revisions { get; init; } = [];
    }

    private sealed record CachedRevision
    {
        public required string Selector { get; init; }

        public required string ObjectId { get; init; }
    }

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        WriteIndented = false,
    };
}
