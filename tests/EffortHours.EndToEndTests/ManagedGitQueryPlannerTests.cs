using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class PullRequestSelectionGitTests
{
    [Fact]
    public async Task ManagedPullRequestAcquiresNarrowObjectsThenReusesThemOfflineWithParity()
    {
        using GitFixture provider = await GitFixture.CreateAsync();
        provider.WriteText("Demo.csproj", ProjectFile);
        string branchPoint = await provider.CommitAsync("base");
        provider.WriteText("BaseOnly.cs", "namespace Demo; internal sealed class BaseOnly { }\n");
        string baseTip = await provider.CommitAsync("base drift");
        await provider.GitAsync("switch", "--quiet", "-c", "feature", branchPoint);
        provider.WriteText("Feature.cs", "namespace Demo; public sealed class Feature { }\n");
        string head = await provider.CommitAsync("feature");
        await provider.GitAsync("update-ref", "refs/pull/17/head", head);
        string providerRefs = await provider.GitAsync(
            "for-each-ref",
            "--format=%(refname):%(objectname)");
        string providerStatus = await provider.GitAsync("status", "--porcelain=v1");
        string cacheRoot = TemporaryCacheRoot();

        try
        {
            GitHubRepositoryCache repositoryCache = RepositoryCache(cacheRoot, provider.RootPath);
            ManagedPullRequestPlanner live = new(
                new FixedPullRequestResolver(
                    baseTip,
                    head,
                    baseRefName: "main",
                    fetchSource: provider.RootPath),
                repositoryCache,
                new GitHubPullRequestResolutionCache(cacheRoot),
                new GitChangePlanner());

            await Assert.ThrowsAsync<InvalidOperationException>(() => live.PlanAsync(
                "17",
                "acme/demo",
                fetchMissing: false));
            GitChangePlan cold = await live.PlanAsync(
                "17",
                "acme/demo",
                fetchMissing: true);
            ManagedPullRequestPlanner offline = new(
                new ThrowingPullRequestResolver(),
                repositoryCache,
                new GitHubPullRequestResolutionCache(cacheRoot),
                new GitChangePlanner());
            GitChangePlan warm = await offline.PlanAsync(
                "17",
                "acme/demo",
                fetchMissing: false);
            GitChangePlan local = await new GitChangePlanner(
                new GitClient(),
                new FixedPullRequestResolver(baseTip, head, baseRefName: "main"))
                .PlanPullRequestAsync(provider.RootPath, "17", "acme/demo");

            ChangeEstimator estimator = new();
            ChangeEstimateReport coldReport = await estimator.EstimateAsync(
                cold,
                EstimationProfile.Implementation);
            ChangeEstimateReport warmReport = await estimator.EstimateAsync(
                warm,
                EstimationProfile.Implementation);
            ChangeEstimateReport localReport = await estimator.EstimateAsync(
                local,
                EstimationProfile.Implementation);

            Assert.Equal(PullRequestObjectAcquisition.ManagedCacheFetch,
                coldReport.Selection.PullRequest!.ObjectAcquisition);
            Assert.Equal(PullRequestObjectAcquisition.ManagedCacheReuse,
                warmReport.Selection.PullRequest!.ObjectAcquisition);
            Assert.Equal(localReport.TotalEffort, coldReport.TotalEffort);
            Assert.Equal(localReport.Evidence.BaseEvidenceDigest, coldReport.Evidence.BaseEvidenceDigest);
            Assert.Equal(localReport.Evidence.HeadEvidenceDigest, warmReport.Evidence.HeadEvidenceDigest);
            Assert.DoesNotContain(cold.RepositoryPath, ContractJson.Serialize(coldReport), StringComparison.Ordinal);
            await AssertBareCacheHasNoMutableSelectionStateAsync(cold.RepositoryPath);
            Assert.Equal(providerRefs, await provider.GitAsync(
                "for-each-ref",
                "--format=%(refname):%(objectname)"));
            Assert.Equal(providerStatus, await provider.GitAsync("status", "--porcelain=v1"));
        }
        finally
        {
            DeleteCacheRoot(cacheRoot);
        }
    }

    [Fact]
    public async Task ManagedCommitRangeAndBaseHeadShareBareObjectsAndRunOffline()
    {
        using GitFixture provider = await GitFixture.CreateAsync();
        provider.WriteText("Demo.csproj", ProjectFile);
        string before = await provider.CommitAsync("base");
        provider.WriteText("Feature.cs", "namespace Demo; public sealed class Feature { }\n");
        string after = await provider.CommitAsync("feature");
        string cacheRoot = TemporaryCacheRoot();

        try
        {
            Dictionary<string, string> revisions = new(StringComparer.Ordinal)
            {
                ["base"] = before,
                ["head"] = after,
            };
            GitHubRepositoryCache repositoryCache = RepositoryCache(cacheRoot, provider.RootPath);
            ManagedGitQueryPlanner live = new(
                new FixedRevisionResolver(revisions),
                repositoryCache,
                new GitHubRevisionResolutionCache(cacheRoot),
                new GitChangePlanner());

            await Assert.ThrowsAsync<InvalidOperationException>(() => live.PlanCommitAsync(
                "acme/demo",
                "head",
                parentRevision: null,
                fetchMissing: false));
            GitChangePlan coldCommit = await live.PlanCommitAsync(
                "acme/demo", "head", parentRevision: null, fetchMissing: true);
            GitChangePlan coldRange = await live.PlanRangeAsync(
                "acme/demo", "base..head", fetchMissing: true);
            GitChangePlan coldBaseHead = await live.PlanBaseHeadAsync(
                "acme/demo", "base", "head", fetchMissing: true);
            ManagedRepositoryHead coldAuthorHead = await live.PrepareHeadAsync(
                "acme/demo", "head", fetchMissing: true);
            string coldManifestRepository = await live.PreparePinnedObjectsAsync(
                "acme/demo", [before, after], fetchMissing: true);

            ManagedGitQueryPlanner offline = new(
                new ThrowingRevisionResolver(),
                repositoryCache,
                new GitHubRevisionResolutionCache(cacheRoot),
                new GitChangePlanner());
            GitChangePlan warmCommit = await offline.PlanCommitAsync(
                "acme/demo", "head", parentRevision: null, fetchMissing: false);
            GitChangePlan warmRange = await offline.PlanRangeAsync(
                "acme/demo", "base..head", fetchMissing: false);
            GitChangePlan warmBaseHead = await offline.PlanBaseHeadAsync(
                "acme/demo", "base", "head", fetchMissing: false);
            ManagedRepositoryHead warmAuthorHead = await offline.PrepareHeadAsync(
                "acme/demo", "head", fetchMissing: false);
            string warmManifestRepository = await offline.LocatePinnedObjectsAsync(
                "acme/demo", [before, after]);

            GitChangePlanner localPlanner = new();
            GitChangePlan localCommit = await localPlanner.PlanCommitAsync(
                provider.RootPath, after, parentRevision: null);
            GitChangePlan localRange = await localPlanner.PlanRangeAsync(
                provider.RootPath, before + ".." + after);
            GitChangePlan localBaseHead = await localPlanner.PlanBaseHeadAsync(
                provider.RootPath, before, after);
            ChangeEstimator estimator = new();
            ChangeEstimateReport localReport = await estimator.EstimateAsync(
                localCommit,
                EstimationProfile.Implementation);

            foreach (GitChangePlan plan in new[]
                {
                    coldCommit, coldRange, coldBaseHead,
                    warmCommit, warmRange, warmBaseHead,
                })
            {
                ChangeEstimateReport report = await estimator.EstimateAsync(
                    plan,
                    EstimationProfile.Implementation);
                Assert.Equal(localReport.TotalEffort, report.TotalEffort);
                Assert.Contains(plan.Diagnostics, diagnostic => diagnostic.Code == "FB5108");
            }

            Assert.Equal(localCommit.Selection.Base.ObjectId, warmCommit.Selection.Base.ObjectId);
            Assert.Equal(localRange.Selection.Head.ObjectId, warmRange.Selection.Head.ObjectId);
            Assert.Equal(localBaseHead.Selection.Base.ObjectId, warmBaseHead.Selection.Base.ObjectId);
            Assert.Equal("head", warmCommit.Selection.Head.Selector);
            Assert.Equal("base..head", warmRange.Selection.Range);
            Assert.Equal("base", warmBaseHead.Selection.Base.Selector);
            Assert.Equal(after, coldAuthorHead.ObjectId);
            Assert.Equal(after, warmAuthorHead.ObjectId);
            Assert.True(coldAuthorHead.ProviderResolved);
            Assert.False(warmAuthorHead.ProviderResolved);
            Assert.Equal(coldManifestRepository, warmManifestRepository);
            await AssertBareCacheHasNoMutableSelectionStateAsync(coldCommit.RepositoryPath);
        }
        finally
        {
            DeleteCacheRoot(cacheRoot);
        }
    }

    private static GitHubRepositoryCache RepositoryCache(string root, string source) => new(
        new ExternalCommandRunner(),
        new GitClient(),
        root,
        _ => source);

    private static async Task AssertBareCacheHasNoMutableSelectionStateAsync(string path)
    {
        Assert.Equal(string.Empty, await GitFixture.RunGitAsync(
            path,
            "for-each-ref",
            "--format=%(refname):%(objectname)"));
        Assert.False(File.Exists(Path.Combine(path, "FETCH_HEAD")));
        Assert.False(File.Exists(Path.Combine(path, "index")));
        Assert.False(Directory.Exists(Path.Combine(path, "worktrees")));
    }

    private static string TemporaryCacheRoot()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "efforthours-managed-query-e2e",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteCacheRoot(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }

    private sealed class ThrowingPullRequestResolver : IPullRequestResolver
    {
        public Task<ResolvedPullRequest> ResolveAsync(
            string repositoryPath,
            string input,
            string? repository,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Offline reuse called the provider resolver.");
    }

    private sealed class FixedRevisionResolver(IReadOnlyDictionary<string, string> revisions)
        : IGitHubRevisionResolver
    {
        public Task<IReadOnlyList<ResolvedGitRevision>> ResolveAsync(
            string repositoryIdentity,
            IReadOnlyList<string> selectors,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("acme/demo", repositoryIdentity);
            return Task.FromResult<IReadOnlyList<ResolvedGitRevision>>([.. selectors.Select(
                selector => new ResolvedGitRevision(selector, revisions[selector]))]);
        }
    }

    private sealed class ThrowingRevisionResolver : IGitHubRevisionResolver
    {
        public Task<IReadOnlyList<ResolvedGitRevision>> ResolveAsync(
            string repositoryIdentity,
            IReadOnlyList<string> selectors,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Offline reuse called the provider resolver.");
    }
}
