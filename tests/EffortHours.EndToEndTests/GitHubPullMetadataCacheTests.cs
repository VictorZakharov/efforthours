using EffortHours.Change;

namespace EffortHours.EndToEndTests;

public sealed class GitHubPullMetadataCacheTests
{
    [Fact]
    public async Task CompleteMetadataReusesOnlySameViewerRepositoryHeadBaseAndCountAndRejectsBrokenEntries()
    {
        string root = Path.Combine(Path.GetTempPath(), "efforthours-pull-metadata", Guid.NewGuid().ToString("N"));
        try
        {
            GitHubPullMetadataCache cache = new(root, "viewer");
            string head = new('a', 40), baseHead = new('b', 40);
            GitHubPullMetadata value = new(head, baseHead, [new(new() { ObjectId = head, ParentObjectIds = [baseHead],
                Author = new("Selected", "selected@example.invalid"), Committer = new("Integrator", "integrator@example.invalid"),
                AuthorTimestamp = DateTimeOffset.Parse("2026-01-19T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                CommitterTimestamp = DateTimeOffset.Parse("2026-03-13T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture) }, "selected")]);
            Assert.True(await cache.WriteAsync("owner/project", 7, value, CancellationToken.None));
            Assert.NotNull(await cache.ReadAsync("owner/project", 7, head, baseHead, 1, CancellationToken.None));
            Assert.Null(await cache.ReadAsync("owner/project", 7, new('c', 40), baseHead, 1, CancellationToken.None));
            Assert.Null(await cache.ReadAsync("owner/project", 7, head, new('c', 40), 1, CancellationToken.None));
            Assert.Null(await cache.ReadAsync("owner/project", 7, head, baseHead, 2, CancellationToken.None));
            Assert.Null(await new GitHubPullMetadataCache(root, "other").ReadAsync("owner/project", 7, head, baseHead, 1, CancellationToken.None));
            string path = Assert.Single(Directory.GetFiles(root, "*.json"));
            Assert.DoesNotContain("message", await File.ReadAllTextAsync(path), StringComparison.OrdinalIgnoreCase);
            string validJson = await File.ReadAllTextAsync(path);
            await File.WriteAllTextAsync(path, validJson.Replace("selected@example.invalid", "other@example.invalid", StringComparison.Ordinal));
            Assert.Null(await cache.ReadAsync("owner/project", 7, head, baseHead, 1, CancellationToken.None));
            await File.WriteAllTextAsync(path, "{broken}");
            Assert.Null(await cache.ReadAsync("owner/project", 7, head, baseHead, 1, CancellationToken.None));
            Assert.True(await cache.WriteAsync("owner/project", 7, value, CancellationToken.None));
            string json = await File.ReadAllTextAsync(path);
            await File.WriteAllTextAsync(path, json.Replace("github-pull-commit-metadata/1.0.0", "unknown", StringComparison.Ordinal));
            Assert.Null(await cache.ReadAsync("owner/project", 7, head, baseHead, 1, CancellationToken.None));
            GitHubPullMetadata oversized = value with
            {
                Commits = [value.Commits[0] with { Commit = value.Commits[0].Commit with
                    { Author = new(new string('x', 65536), "selected@example.invalid") } }]
            };
            Assert.False(await cache.WriteAsync("owner/project", 8, oversized, CancellationToken.None));
            Assert.Single(Directory.GetFiles(root, "*.json"));
            Assert.Empty(Directory.GetFiles(root, "*.tmp-*"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
