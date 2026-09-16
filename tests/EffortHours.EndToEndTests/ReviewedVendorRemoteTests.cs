using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.EndToEndTests;

public sealed partial class PullRequestSelectionGitTests
{
    [Fact]
    public async Task ReviewedVendorManifestIsIdenticalForLocalAndOfflineImmutableRepository()
    {
        using GitFixture provider = await GitFixture.CreateAsync();
        const string library = "export function widget(value) { return value; }\n";
        provider.WriteText("common/widget.js", library);
        provider.WriteText("adapter.js", "export function adapter(widget, value) { return widget(value); }\n");
        string head = await provider.CommitAsync("synthetic reviewed body");
        string cacheRoot = TemporaryCacheRoot();
        try
        {
            ReviewedVendorManifest manifest = new()
            {
                Files = [new ReviewedVendorEntry
                {
                    Path = "common/widget.js", Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(library))).ToLowerInvariant(),
                    Library = "Synthetic", Provenance = "MIT fixture", Rationale = "Reviewed whole copied body",
                }],
            };
            Directory.CreateDirectory(cacheRoot);
            string manifestPath = Path.Combine(cacheRoot, "vendor.json");
            await File.WriteAllTextAsync(manifestPath, ContractJson.Serialize(manifest));
            GitHubRepositoryCache cache = RepositoryCache(cacheRoot, provider.RootPath);
            await cache.EnsureAsync("acme/demo", [new("default", head, "refs/heads/main")], CancellationToken.None);
            GitHubRevisionResolutionCache resolutionCache = new(cacheRoot);
            await resolutionCache.SaveAsync(new("reachable-head", "acme/demo", [new(head, head)]), CancellationToken.None);
            ManagedGitQueryPlanner offline = new(new ThrowingRevisionResolver(), cache,
                new GitHubRevisionResolutionCache(cacheRoot), new GitChangePlanner());
            RepositoryInputLoader loader = CreateRepositoryInputLoader(offline);
            await using RepositoryInputContext remote = await loader.LoadAsync(new RepositoryInputSelection
            {
                GitHubRepository = "acme/demo",
                Revision = head,
                VendorManifestPath = manifestPath,
            }, allowEvidenceFile: false, scanOptions: null, CancellationToken.None);
            RepositoryEvidence local = await new RepositoryAnalysisPipeline().ScanAsync(provider.RootPath,
                new RepositoryScanOptions { VendorManifest = manifest });
            Assert.Equal(ContractJson.Serialize(local with { Repository = local.Repository with { Name = "demo" } }),
                ContractJson.Serialize(remote.Evidence with
                {
                    Diagnostics = [.. remote.Evidence.Diagnostics.Where(diagnostic => diagnostic.Code != "FB5108")],
                }));
            await AssertBareCacheHasNoMutableSelectionStateAsync(Path.Combine(cacheRoot, "acme", "demo.git"));
        }
        finally { DeleteCacheRoot(cacheRoot); }
    }
}
