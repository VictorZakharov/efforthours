using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EffortHours.Change;

internal sealed record GitHubPullCommitEvidence(GitCommitMetadata Commit, string? Login);
internal sealed record GitHubPullMetadata(string Head, string Base, IReadOnlyList<GitHubPullCommitEvidence> Commits);

internal interface IGitHubPullMetadataCache
{
    public Task<GitHubPullMetadata?> ReadAsync(string repository, int number, string head, string baseHead, int count, CancellationToken token);
    public Task WriteAsync(string repository, int number, GitHubPullMetadata metadata, CancellationToken token);
}

internal sealed class GitHubPullMetadataCache(string root, string viewer) : IGitHubPullMetadataCache
{
    private const int MaximumBytes = 64 * 1024;
    private const int MaximumEntries = 1000;
    private const string Protocol = "github-pull-commit-metadata/1.0.0";
    private static readonly TimeSpan Freshness = TimeSpan.FromHours(24);

    public async Task<GitHubPullMetadata?> ReadAsync(string repository, int number, string head, string baseHead, int count, CancellationToken token)
    {
        string path = CachePath(repository, number, head, baseHead, count);
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length is <= 0 or > MaximumBytes) return null;
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            byte[] buffer = new byte[MaximumBytes + 1];
            int length = 0;
            while (length < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(length), token).ConfigureAwait(false);
                if (read == 0) break;
                length += read;
            }
            if (length > MaximumBytes) return null;
            ReadOnlySpan<byte> bytes = buffer.AsSpan(0, length);
            Entry? entry = JsonSerializer.Deserialize<Entry>(bytes);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (entry is null || entry.Protocol != Protocol || entry.Key != Path.GetFileNameWithoutExtension(path) ||
                entry.Expires <= now || entry.Expires > now + Freshness || entry.Value.Head != head || entry.Value.Base != baseHead ||
                entry.Value.Commits.Count != count || count is <= 0 or > 250 || entry.Value.Commits[^1].Commit.ObjectId != head ||
                entry.Digest != Digest(entry.Value) || entry.Value.Commits.Any(value => !Valid(value)) ||
                entry.Value.Commits.Select(value => value.Commit.ObjectId).Distinct(StringComparer.Ordinal).Count() != count)
                return null;
            return entry.Value;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NullReferenceException)
        {
            return null; // Cache is optional; incomplete/invalid content always refreshes from the provider.
        }
    }

    public async Task WriteAsync(string repository, int number, GitHubPullMetadata metadata, CancellationToken token)
    {
        string path = CachePath(repository, number, metadata.Head, metadata.Base, metadata.Commits.Count);
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new Entry(Protocol, Path.GetFileNameWithoutExtension(path),
            DateTimeOffset.UtcNow + Freshness, Digest(metadata), metadata));
        if (bytes.Length > MaximumBytes) return;
        try
        {
            Directory.CreateDirectory(root);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            await File.WriteAllBytesAsync(temporary, bytes, token).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
            foreach (FileInfo stale in new DirectoryInfo(root).EnumerateFiles("*.json")
                .OrderByDescending(file => file.LastWriteTimeUtc).ThenBy(file => file.Name, StringComparer.Ordinal).Skip(MaximumEntries))
                stale.Delete();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    private string CachePath(string repository, int number, string head, string baseHead, int count)
    {
        string key = $"{Protocol}\n{viewer.ToLowerInvariant()}\n{repository.ToLowerInvariant()}\n{number}\n{head}\n{baseHead}\n{count}";
        return Path.Combine(root, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant() + ".json");
    }

    private static string Digest(GitHubPullMetadata metadata) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(metadata))).ToLowerInvariant();
    private static bool Valid(GitHubPullCommitEvidence value) => value?.Commit is { } commit &&
        IsObjectId(commit.ObjectId) && commit.ParentObjectIds is not null && commit.ParentObjectIds.All(IsObjectId) &&
        commit.Author is { Name: not null, Email: not null } && commit.Committer is { Name: not null, Email: not null } &&
        commit.Coauthors is not null && commit.Coauthors.All(identity => identity is { Name: not null, Email: not null });
    private static bool IsObjectId(string value) => value is { Length: 40 or 64 } && value.All(Uri.IsHexDigit);
    private sealed record Entry(string Protocol, string Key, DateTimeOffset Expires, string Digest, GitHubPullMetadata Value);
}
