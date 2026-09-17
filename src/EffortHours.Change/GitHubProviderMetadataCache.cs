using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EffortHours.Change;

internal sealed record GitHubProviderMetadata(
    string OwnerType,
    IReadOnlyList<string>? VerifiedEmails,
    DateTimeOffset OwnerFreshUntil,
    DateTimeOffset? IdentityFreshUntil);

internal sealed record GitHubProviderMetadataRead(
    GitHubProviderMetadata? Metadata,
    string Status);

internal sealed class GitHubProviderMetadataCache
{
    private const string Protocol = "github-provider-metadata-cache/1.1.0";
    private const int MaximumBytes = 2 * 1024 * 1024;
    private static readonly TimeSpan IdentityFreshness = TimeSpan.FromHours(24);
    private readonly string _root;

    public GitHubProviderMetadataCache()
        : this(ResolveRoot())
    {
    }

    internal GitHubProviderMetadataCache(string root)
    {
        _root = Path.GetFullPath(root);
    }

    public async Task<GitHubProviderMetadata?> ReadAsync(
        string owner,
        string authenticatedLogin,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        (await ReadWithStatusAsync(owner, authenticatedLogin, now, cancellationToken)
            .ConfigureAwait(false)).Metadata;

    public async Task<GitHubProviderMetadataRead> ReadWithStatusAsync(
        string owner,
        string authenticatedLogin,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        string path = CachePath(owner, authenticatedLogin);
        if (!File.Exists(path))
        {
            return new(null, "missing");
        }

        try
        {
            FileInfo info = new(path);
            if (info.Length is <= 0 or > MaximumBytes)
            {
                return new(null, "invalid-size");
            }

            await using FileStream stream = new(
                path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            CacheDocument? document = await JsonSerializer.DeserializeAsync<CacheDocument>(
                stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                return new(null, "invalid-content");
            }

            if (document.Protocol != Protocol)
            {
                return new(null, "unsupported-protocol");
            }

            if (!string.Equals(document.Owner, owner, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(document.AuthenticatedLogin, authenticatedLogin, StringComparison.OrdinalIgnoreCase))
            {
                return new(null, "identity-mismatch");
            }

            if (document.OwnerFreshUntil > now + IdentityFreshness ||
                document.IdentityFreshUntil > now + IdentityFreshness ||
                document.OwnerType is not ("organization" or "user") ||
                document.VerifiedEmails is { Count: > 128 } ||
                document.VerifiedEmails?.Any(string.IsNullOrWhiteSpace) == true ||
                (document.VerifiedEmails is null) != (document.IdentityFreshUntil is null))
            {
                return new(null, "invalid-content");
            }

            if (document.OwnerFreshUntil <= now)
            {
                return new(null, "expired");
            }

            bool freshIdentity = document.IdentityFreshUntil > now;
            return new(new GitHubProviderMetadata(
                document.OwnerType,
                freshIdentity ? document.VerifiedEmails : null,
                document.OwnerFreshUntil,
                freshIdentity ? document.IdentityFreshUntil : null),
                freshIdentity ? "hit" : "hit-owner-only");
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException)
        {
            throw GitHubProviderFailure.ManagedCacheAccessDenied(exception);
        }
        catch (JsonException)
        {
            return new(null, "invalid-content");
        }
    }

    public async Task WriteAsync(
        string owner,
        string authenticatedLogin,
        string ownerType,
        IReadOnlyList<string>? verifiedEmails,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        GitHubProviderMetadata? previous = null)
    {
        CacheDocument document = new()
        {
            Protocol = Protocol,
            Owner = owner,
            AuthenticatedLogin = authenticatedLogin,
            OwnerType = ownerType,
            VerifiedEmails = verifiedEmails ?? previous?.VerifiedEmails,
            OwnerFreshUntil = previous?.OwnerFreshUntil ?? now + IdentityFreshness,
            IdentityFreshUntil = previous?.IdentityFreshUntil ??
                (verifiedEmails is null ? null : now + IdentityFreshness),
        };
        string path = CachePath(owner, authenticatedLogin);
        string directory = Path.GetDirectoryName(path)!;
        string temporary = Path.Combine(directory, "." + Path.GetFileName(path) + ".tmp-" +
            Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            SetPrivateDirectoryMode(_root);
            await using (FileStream stream = new(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, document, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException)
        {
            throw GitHubProviderFailure.ManagedCacheAccessDenied(exception);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or IOException)
            {
            }
        }
    }

    private string CachePath(string owner, string authenticatedLogin)
    {
        string key = owner.ToLowerInvariant() + "\n" + authenticatedLogin.ToLowerInvariant();
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))
            .ToLowerInvariant();
        return Path.Combine(_root, digest + ".json");
    }

    private static string ResolveRoot()
    {
        string? configured = Environment.GetEnvironmentVariable("EFFORTHOURS_PROVIDER_CACHE");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        string? repositories = Environment.GetEnvironmentVariable("EFFORTHOURS_REPOSITORY_CACHE");
        if (!string.IsNullOrWhiteSpace(repositories))
        {
            return Path.Combine(repositories, ".provider-metadata");
        }

        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
        {
            throw new InvalidOperationException(
                "The host does not provide local application data for provider metadata.");
        }

        return Path.Combine(local, "EffortHours", "provider-cache", "github");
    }

    private static void SetPrivateDirectoryMode(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private sealed record CacheDocument
    {
        public required string Protocol { get; init; }
        public required string Owner { get; init; }
        public required string AuthenticatedLogin { get; init; }
        public required string OwnerType { get; init; }
        public IReadOnlyList<string>? VerifiedEmails { get; init; }
        public DateTimeOffset OwnerFreshUntil { get; init; }
        public DateTimeOffset? IdentityFreshUntil { get; init; }
    }
}
