using EffortHours.Change;

namespace EffortHours.EndToEndTests;

public sealed partial class SnapshotPortfolioCliTests : ChangeCliTestSupport
{
    private static async Task<string> SnapshotGitAsync(string root, params string[] arguments)
    {
        System.Diagnostics.ProcessStartInfo start = StartInfo("git", root);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        ProcessResult result = await RunAsync(start);
        Assert.True(result.ExitCode == 0, result.StandardError);
        return result.StandardOutput.Trim();
    }

    [Fact]
    public async Task SnapshotExplicitManagedHistoryCompletionLeavesShallowSourceUntouchedAndReusesOffline()
    {
        using GitFixture source = await SnapshotFixtureAsync();
        source.WriteText("src/Later.cs", "public class Later { public bool Ready => true; }\n");
        string head = await SnapshotCommitAtAsync(source, "later", "2026-03-10T12:00:00Z");
        using GitFixture execution = await GitFixture.CreateAsync();
        string cacheRoot = Path.Combine(execution.RootPath, "managed");
        string bare = Path.Combine(cacheRoot, "acme", "demo.git");
        Directory.CreateDirectory(Path.GetDirectoryName(bare)!);
        string sourceUri = new Uri(source.RootPath + Path.DirectorySeparatorChar).AbsoluteUri;
        string userClone = Path.Combine(execution.RootPath, "user-shallow");
        _ = await SnapshotGitAsync(execution.RootPath, "clone", "--depth", "1", sourceUri, userClone);
        string shallowMarker = await File.ReadAllTextAsync(Path.Combine(userClone, ".git", "shallow"), System.Text.Encoding.UTF8);
        string userRefs = await SnapshotGitAsync(userClone, "for-each-ref", "--format=%(refname):%(objectname)");
        _ = await SnapshotGitAsync(execution.RootPath, "clone", "--bare", "--depth", "1", sourceUri, bare);
        Assert.Equal("true", await SnapshotGitAsync(bare, "rev-parse", "--is-shallow-repository"));
        string originalHead = await source.GitAsync("rev-parse", "HEAD");
        string originalStatus = await source.GitAsync("status", "--porcelain");
        GitHubRepositoryCache cache = new(new ExternalCommandRunner(), new GitClient(), cacheRoot, _ => sourceUri);
        await cache.CompleteHistoryAsync("acme/demo", head, fetchMissing: false, CancellationToken.None);
        Assert.True((await new GitClient().ReadSnapshotHistoryAsync(bare, head)).Shallow);
        await cache.CompleteHistoryAsync("acme/demo", head, fetchMissing: true, CancellationToken.None);
        (IReadOnlyList<SnapshotHistoryCommit> history, bool shallow) = await new GitClient().ReadSnapshotHistoryAsync(bare, head);
        Assert.False(shallow);
        Assert.Equal(2, history.Count);
        string refs = await SnapshotGitAsync(bare, "for-each-ref", "--format=%(refname):%(objectname)");
        Assert.False(File.Exists(Path.Combine(bare, "FETCH_HEAD")));
        Assert.False(File.Exists(Path.Combine(bare, "index")));
        Assert.Equal(originalHead, await source.GitAsync("rev-parse", "HEAD"));
        Assert.Equal(originalStatus, await source.GitAsync("status", "--porcelain"));
        Assert.Equal("true", await SnapshotGitAsync(userClone, "rev-parse", "--is-shallow-repository"));
        Assert.Equal(shallowMarker, await File.ReadAllTextAsync(Path.Combine(userClone, ".git", "shallow"), System.Text.Encoding.UTF8));
        Assert.Equal(userRefs, await SnapshotGitAsync(userClone, "for-each-ref", "--format=%(refname):%(objectname)"));
        Assert.Equal(head, await SnapshotGitAsync(userClone, "rev-parse", "HEAD"));
        Assert.Equal("", await SnapshotGitAsync(userClone, "status", "--porcelain"));
        // A failing provider cannot be called by offline reuse.
        GitHubRepositoryCache offline = new(new ExternalCommandRunner(), new GitClient(), cacheRoot, _ => throw new InvalidOperationException("network forbidden"));
        await offline.CompleteHistoryAsync("acme/demo", head, fetchMissing: false, CancellationToken.None);
        string[] beforeOffline = [.. Directory.EnumerateFiles(cacheRoot, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];
        ManagedGitQueryPlanner planner = new(new SnapshotNoNetworkResolver(), offline,
            new GitHubRevisionResolutionCache(cacheRoot), new GitChangePlanner());
        ManagedRepositoryHead cachedHead = await planner.PrepareSnapshotHeadAsync("acme/demo", head, fetchMissing: false);
        Assert.Equal(head, cachedHead.ObjectId);
        Assert.Equal(bare, cachedHead.RepositoryPath);
        Assert.Equal(beforeOffline, Directory.EnumerateFiles(cacheRoot, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        Assert.Equal(history, (await new GitClient().ReadSnapshotHistoryAsync(bare, head)).Commits);
        Assert.Equal(refs, await SnapshotGitAsync(bare, "for-each-ref", "--format=%(refname):%(objectname)"));
        string brokenRoot = Path.Combine(execution.RootPath, "broken");
        string brokenBare = Path.Combine(brokenRoot, "acme", "demo.git");
        Directory.CreateDirectory(Path.GetDirectoryName(brokenBare)!);
        _ = await SnapshotGitAsync(execution.RootPath, "clone", "--bare", "--depth", "1", sourceUri, brokenBare);
        GitHubRepositoryCache broken = new(new ExternalCommandRunner(), new GitClient(), brokenRoot, _ => Path.Combine(execution.RootPath, "missing-provider"));
        await Assert.ThrowsAsync<ExternalCommandException>(() => broken.CompleteHistoryAsync("acme/demo", head, fetchMissing: true, CancellationToken.None));
        Assert.True((await new GitClient().ReadSnapshotHistoryAsync(brokenBare, head)).Shallow);
    }

    private sealed class SnapshotNoNetworkResolver : IGitHubRevisionResolver
    {
        public Task<IReadOnlyList<ResolvedGitRevision>> ResolveAsync(string repositoryIdentity,
            IReadOnlyList<string> selectors, CancellationToken cancellationToken) => throw new InvalidOperationException("Offline planning called the provider.");
    }
}
