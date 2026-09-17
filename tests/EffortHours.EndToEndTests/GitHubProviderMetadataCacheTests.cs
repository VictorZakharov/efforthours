using EffortHours.Change;

namespace EffortHours.EndToEndTests;

public sealed class GitHubProviderMetadataCacheTests
{
    [Fact]
    public async Task OwnerOnlyCacheDoesNotInventViewerEmailsOrExtendTheirExpiry()
    {
        string root = Path.Combine(Path.GetTempPath(), "efforthours-provider-cache-e2e", Guid.NewGuid().ToString("N"));
        DateTimeOffset now = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
        try
        {
            GitHubProviderMetadataCache cache = new(root);
            Assert.Equal("missing", (await cache.ReadWithStatusAsync("owner", "viewer", now, default)).Status);
            await cache.WriteAsync("owner", "viewer", "organization", null, now, default);
            GitHubProviderMetadataRead owner = await cache.ReadWithStatusAsync("owner", "viewer", now, default);
            Assert.Equal("hit-owner-only", owner.Status);
            Assert.Null(owner.Metadata!.VerifiedEmails);
            await cache.WriteAsync("owner", "viewer", "organization", ["viewer@example.test"],
                now.AddHours(1), default, owner.Metadata);
            GitHubProviderMetadataRead identity = await cache.ReadWithStatusAsync(
                "owner", "viewer", now.AddHours(2), default);
            Assert.Equal("hit", identity.Status);
            await cache.WriteAsync("owner", "viewer", "organization", identity.Metadata!.VerifiedEmails,
                now.AddHours(3), default, identity.Metadata);
            GitHubProviderMetadataRead repeat = await cache.ReadWithStatusAsync(
                "owner", "viewer", now.AddHours(4), default);
            Assert.Equal(identity.Metadata.IdentityFreshUntil, repeat.Metadata!.IdentityFreshUntil);
            Assert.Equal(owner.Metadata.OwnerFreshUntil, repeat.Metadata.OwnerFreshUntil);
            Assert.Equal("expired",
                (await cache.ReadWithStatusAsync("owner", "viewer", now.AddHours(24), default)).Status);

            string entry = Assert.Single(Directory.GetFiles(root, "*.json"));
            await File.WriteAllTextAsync(entry, "{malformed");
            Assert.Equal("invalid-content", (await cache.ReadWithStatusAsync("owner", "viewer", now, default)).Status);
            await File.WriteAllTextAsync(entry,
                """{"Protocol":"github-provider-metadata-cache/1.0.0","Owner":"owner","AuthenticatedLogin":"viewer","OwnerType":"organization"}""");
            Assert.Equal("unsupported-protocol", (await cache.ReadWithStatusAsync("owner", "viewer", now, default)).Status);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CacheIsOwnerAccountBoundAndIdentityFreshnessExpires()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "efforthours-provider-cache-e2e",
            Guid.NewGuid().ToString("N"));
        DateTimeOffset observedAt = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        try
        {
            GitHubProviderMetadataCache cache = new(root);
            await cache.WriteAsync(
                "example-owner",
                "viewer",
                "organization",
                ["verified@example.test"],
                observedAt,
                CancellationToken.None);

            GitHubProviderMetadata? current = await cache.ReadAsync(
                "EXAMPLE-OWNER",
                "VIEWER",
                observedAt.AddMinutes(1),
                CancellationToken.None);
            Assert.NotNull(current);
            Assert.Equal("organization", current.OwnerType);
            Assert.Equal(["verified@example.test"], current.VerifiedEmails);
            Assert.Null(await cache.ReadAsync(
                "different-owner",
                "viewer",
                observedAt.AddMinutes(1),
                CancellationToken.None));
            Assert.Null(await cache.ReadAsync(
                "example-owner",
                "different-viewer",
                observedAt.AddMinutes(1),
                CancellationToken.None));
            Assert.Null(await cache.ReadAsync(
                "example-owner",
                "viewer",
                observedAt.AddHours(25),
                CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
