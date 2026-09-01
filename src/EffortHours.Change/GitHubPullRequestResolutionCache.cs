using System.Text;
using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed class GitHubPullRequestResolutionCache
{
    private const int MaximumBytes = 64 * 1024;
    private const string Protocol = "github-pull-request-resolution-cache/1.0.0";
    private readonly string _root;

    public GitHubPullRequestResolutionCache()
        : this(GitHubRepositoryCache.ResolveRoot())
    {
    }

    internal GitHubPullRequestResolutionCache(string root)
    {
        _root = Path.GetFullPath(root);
    }

    public Task<FileStream> AcquireUpdateLockAsync(
        GitHubPullRequestLocator locator,
        CancellationToken cancellationToken) =>
        AcquireLockAsync(ResolutionPath(locator) + ".lock", cancellationToken);

    public async Task<ResolvedPullRequest?> LoadAsync(
        GitHubPullRequestLocator locator,
        string input,
        CancellationToken cancellationToken)
    {
        string path = ResolutionPath(locator);
        if (!File.Exists(path))
        {
            return null;
        }

        FileInfo file = new(path);
        if (file.Length is <= 0 or > MaximumBytes)
        {
            throw new InvalidOperationException(
                "The managed pull-request resolution cache entry is invalid.");
        }

        try
        {
            string json = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);
            CachedPullRequestResolution cached = JsonSerializer.Deserialize<CachedPullRequestResolution>(
                    json,
                    JsonOptions)
                ?? throw new JsonException("The cached document was empty.");
            Validate(cached, locator);
            return new ResolvedPullRequest
            {
                BaseObjectId = cached.BaseObjectId,
                HeadObjectId = cached.HeadObjectId,
                BaseRefName = cached.BaseRefName,
                FetchSource = FetchSource(locator.RepositoryIdentity),
                ChangedFileCount = cached.ChangedFileCount,
                Reference = new PullRequestReference
                {
                    Input = input,
                    Number = locator.Number,
                    Repository = locator.RepositoryIdentity,
                    Url = cached.Url,
                },
            };
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "The managed pull-request resolution cache entry is unavailable or invalid. " +
                "Retry with --fetch-missing to resolve and refresh the immutable provider identity.",
                exception);
        }
    }

    public async Task SaveAsync(
        GitHubPullRequestLocator locator,
        ResolvedPullRequest resolved,
        CancellationToken cancellationToken)
    {
        CachedPullRequestResolution cached = new()
        {
            Protocol = Protocol,
            RepositoryIdentity = locator.RepositoryIdentity,
            Number = locator.Number,
            Url = resolved.Reference.Url,
            BaseObjectId = resolved.BaseObjectId,
            HeadObjectId = resolved.HeadObjectId,
            BaseRefName = resolved.BaseRefName!,
            ChangedFileCount = resolved.ChangedFileCount,
        };
        Validate(cached, locator);
        string path = ResolutionPath(locator);
        string directory = Path.GetDirectoryName(path) ??
            throw new InvalidOperationException("The managed resolution cache path has no parent.");
        Directory.CreateDirectory(directory);
        SetPrivateDirectoryMode(directory);
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            string json = JsonSerializer.Serialize(cached, JsonOptions);
            if (Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            {
                throw new InvalidOperationException(
                    "The managed pull-request resolution cache entry exceeds its size bound.");
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
                "EffortHours could not publish its managed pull-request resolution cache entry.",
                exception);
        }
    }

    private string ResolutionPath(GitHubPullRequestLocator locator)
    {
        string[] parts = locator.RepositoryIdentity.Split('/');
        string path = Path.GetFullPath(Path.Combine(
            _root,
            ".pull-requests",
            parts[0],
            parts[1],
            $"pull-{locator.Number}.json"));
        string relative = Path.GetRelativePath(_root, path);
        if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The managed resolution cache path escaped its root.");
        }

        return path;
    }

    private static void Validate(
        CachedPullRequestResolution cached,
        GitHubPullRequestLocator locator)
    {
        if (cached.Protocol != Protocol ||
            !string.Equals(
                cached.RepositoryIdentity,
                locator.RepositoryIdentity,
                StringComparison.Ordinal) ||
            cached.Number != locator.Number ||
            string.IsNullOrWhiteSpace(cached.BaseRefName) ||
            !ValidObjectId(cached.BaseObjectId) ||
            !ValidObjectId(cached.HeadObjectId) ||
            cached.ChangedFileCount < 0)
        {
            throw new InvalidOperationException(
                "The managed pull-request resolution cache entry failed validation.");
        }

        GitHubPullRequestLocator cachedUrl = GitHubPullRequestLocatorParser.Parse(
            cached.Url ?? string.Empty,
            cached.RepositoryIdentity);
        if (cachedUrl.Number != locator.Number)
        {
            throw new InvalidOperationException(
                "The managed pull-request resolution cache entry has an inconsistent URL.");
        }
    }

    private static bool ValidObjectId(string value) =>
        value.Length is 40 or 64 && value.All(Uri.IsHexDigit);

    private static string FetchSource(string repositoryIdentity) =>
        "https://github.com/" + repositoryIdentity + ".git";

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

    private sealed record CachedPullRequestResolution
    {
        public required string Protocol { get; init; }

        public required string RepositoryIdentity { get; init; }

        public required int Number { get; init; }

        public string? Url { get; init; }

        public required string BaseObjectId { get; init; }

        public required string HeadObjectId { get; init; }

        public required string BaseRefName { get; init; }

        public int? ChangedFileCount { get; init; }
    }

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        WriteIndented = false,
    };
}
