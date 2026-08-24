using EffortHours.Change;

namespace EffortHours.EndToEndTests;

public sealed class GitHubProviderMetadataCacheTests
{
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
                [new GitHubDiscoveryRepository(
                    "42",
                    "example-owner/repository",
                    "main")],
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
