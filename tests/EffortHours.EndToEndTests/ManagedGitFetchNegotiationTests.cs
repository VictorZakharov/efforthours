using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class PullRequestSelectionGitTests
{
    [Fact]
    public async Task AdvancingManagedHeadsReuseHistoryAcrossInstancesWithoutRefs()
    {
        using GitFixture provider = await GitFixture.CreateAsync();
        for (int index = 0; index < 8; index++)
        {
            provider.WriteText($"File{index}.txt", $"Synthetic payload {index}\n");
        }

        string before = await provider.CommitAsync("base");
        string cacheRoot = TemporaryCacheRoot();
        try
        {
            FetchRecordingRunner runner = new();
            RepositoryAcquisitionResult initial = await NegotiatingCache(cacheRoot, provider, runner)
                .EnsureAsync("example/repository", [new("default", before, "refs/heads/main")],
                    CancellationToken.None);
            string coldRoot = Path.Combine(cacheRoot, "control");
            FetchRecordingRunner controlRunner = new();
            RepositoryAcquisitionResult controlInitial = await NegotiatingCache(coldRoot, provider, controlRunner)
                .EnsureAsync("example/repository", [new("default", before, "refs/heads/main")],
                    CancellationToken.None);
            File.Delete(GitHubFetchNegotiationCache.CachePath(controlInitial.RepositoryPath));
            Assert.Equal(10, initial.AcquiredObjectCount);
            Assert.Equal(10, controlInitial.AcquiredObjectCount);
            Assert.DoesNotContain(Assert.Single(runner.Fetches),
                argument => argument.StartsWith("--negotiation-tip=", StringComparison.Ordinal));

            provider.WriteText("Feature.cs", "namespace Demo; public sealed class Feature { }\n");
            string after = await provider.CommitAsync("advance");
            string providerRefs = await provider.GitAsync("for-each-ref", "--format=%(refname):%(objectname)");
            string providerStatus = await provider.GitAsync("status", "--porcelain=v1");
            DiscoveredHead[] heads = [new("default", after, "refs/heads/main")];

            // New cache instances model later invocations; the per-repository lock also
            // makes concurrent callers recheck the first caller's completed acquisition.
            RepositoryAcquisitionResult[] concurrent = await Task.WhenAll(Enumerable.Range(0, 4)
                .Select(_ => NegotiatingCache(cacheRoot, provider, runner)
                    .EnsureAsync("example/repository", heads, CancellationToken.None)));
            RepositoryAcquisitionResult incremental = Assert.Single(concurrent, result => result.AcquiredHeadCount == 1);
            RepositoryAcquisitionResult control = await NegotiatingCache(coldRoot, provider, controlRunner)
                .EnsureAsync("example/repository", heads, CancellationToken.None);

            Assert.Equal(3, runner.TransferredObjectCounts.Last());
            Assert.Equal(13, controlRunner.TransferredObjectCounts.Last());
            Assert.Equal(after, await new GitClient().ResolveCommitAsync(control.RepositoryPath, after));
            Assert.Equal(3, concurrent.Count(result => result.LocalHeadCount == 1));
            Assert.Equal(2, runner.Fetches.Count);
            Assert.Contains("--negotiation-tip=" + before, runner.Fetches.Last());
            Assert.DoesNotContain(controlRunner.Fetches.Last(),
                argument => argument.StartsWith("--negotiation-tip=", StringComparison.Ordinal));
            Assert.Equal([after], await GitHubFetchNegotiationCache.ReadAsync(
                incremental.RepositoryPath, CancellationToken.None));
            Assert.Equal(
                await provider.GitAsync("rev-list", "--objects", after),
                await GitFixture.RunGitAsync(incremental.RepositoryPath, "rev-list", "--objects", after));
            await AssertBareCacheHasNoMutableSelectionStateAsync(incremental.RepositoryPath);
            Assert.Equal(providerRefs, await provider.GitAsync("for-each-ref", "--format=%(refname):%(objectname)"));
            Assert.Equal(providerStatus, await provider.GitAsync("status", "--porcelain=v1"));

            GitChangePlanner planner = new();
            ChangeEstimator estimator = new();
            var localReport = await estimator.EstimateAsync(
                await planner.PlanCommitAsync(provider.RootPath, after, parentRevision: null),
                EstimationProfile.Implementation);
            var cachedReport = await estimator.EstimateAsync(
                await planner.PlanCommitAsync(incremental.RepositoryPath, after, parentRevision: null),
                EstimationProfile.Implementation);
            Assert.Equal(localReport.TotalEffort, cachedReport.TotalEffort);
            Assert.Equal(localReport.Evidence.BaseEvidenceDigest, cachedReport.Evidence.BaseEvidenceDigest);
            Assert.Equal(localReport.Evidence.HeadEvidenceDigest, cachedReport.Evidence.HeadEvidenceDigest);
        }
        finally
        {
            DeleteCacheRoot(cacheRoot);
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("oversized")]
    [InlineData("future-version")]
    [InlineData("missing-object")]
    [InlineData("blob-object")]
    [InlineData("unwritable")]
    public async Task UnusableFetchHintsFallBackToCompleteAcquisition(string hintState)
    {
        using GitFixture provider = await GitFixture.CreateAsync();
        provider.WriteText("App.cs", "namespace Demo; class App { }\n");
        string before = await provider.CommitAsync("base");
        string cacheRoot = TemporaryCacheRoot();
        try
        {
            FetchRecordingRunner runner = new();
            RepositoryAcquisitionResult initial = await NegotiatingCache(cacheRoot, provider, runner)
                .EnsureAsync("example/repository", [new("default", before, "refs/heads/main")],
                    CancellationToken.None);
            string hintPath = GitHubFetchNegotiationCache.CachePath(initial.RepositoryPath);
            if (hintState is "missing" or "unwritable")
            {
                File.Delete(hintPath);
                if (hintState == "unwritable")
                {
                    Directory.CreateDirectory(hintPath);
                }
            }
            else
            {
                string content = hintState switch
                {
                    "malformed" => "not a hint document",
                    "oversized" => new string('x', GitHubFetchNegotiationCache.MaximumBytes + 1),
                    "future-version" => "future-version\n" + before,
                    "missing-object" => GitHubFetchNegotiationCache.Protocol + "\n" + new string('f', 40),
                    "blob-object" => GitHubFetchNegotiationCache.Protocol + "\n" +
                        await provider.GitAsync("rev-parse", before + ":App.cs"),
                    _ => throw new InvalidOperationException(),
                };
                await File.WriteAllTextAsync(hintPath, content);
            }

            provider.WriteText("Feature.cs", "namespace Demo; class Feature { }\n");
            string after = await provider.CommitAsync("advance");
            RepositoryAcquisitionResult result = await NegotiatingCache(cacheRoot, provider, runner)
                .EnsureAsync("example/repository", [new("default", after, "refs/heads/main")],
                    CancellationToken.None);

            Assert.Equal(1, result.AcquiredHeadCount);
            Assert.DoesNotContain(runner.Fetches.Last(),
                argument => argument.StartsWith("--negotiation-tip=", StringComparison.Ordinal));
            Assert.True(await new GitClient().CommitExistsAsync(result.RepositoryPath, after));
            Assert.Equal(
                await provider.GitAsync("rev-list", "--objects", after),
                await GitFixture.RunGitAsync(result.RepositoryPath, "rev-list", "--objects", after));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(hintPath)!, "*.tmp-*"));
            await AssertBareCacheHasNoMutableSelectionStateAsync(result.RepositoryPath);
        }
        finally
        {
            DeleteCacheRoot(cacheRoot);
        }
    }

    [Fact]
    public async Task UnverifiedAcquisitionAndOfflineReuseDoNotReplaceFetchHints()
    {
        using GitFixture provider = await GitFixture.CreateAsync();
        provider.WriteText("App.cs", "namespace Demo; class App { }\n");
        string before = await provider.CommitAsync("base");
        string cacheRoot = TemporaryCacheRoot();
        try
        {
            FetchRecordingRunner runner = new();
            GitHubRepositoryCache cache = NegotiatingCache(cacheRoot, provider, runner);
            RepositoryAcquisitionResult initial = await cache.EnsureAsync(
                "example/repository", [new("default", before, "refs/heads/main")], CancellationToken.None);
            string path = GitHubFetchNegotiationCache.CachePath(initial.RepositoryPath);
            string hints = await File.ReadAllTextAsync(path);

            await cache.EnsureAsync("example/repository",
                [new("default", before, "refs/heads/main")], fetchMissing: false, CancellationToken.None);
            Assert.Single(runner.Fetches);
            Assert.Equal(hints, await File.ReadAllTextAsync(path));

            provider.WriteText("Feature.cs", "namespace Demo; class Feature { }\n");
            _ = await provider.CommitAsync("provider ref moved");
            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.EnsureAsync(
                "example/repository", [new("default", new string('f', 40), "refs/heads/main")],
                CancellationToken.None));
            Assert.Equal(hints, await File.ReadAllTextAsync(path));

            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GitHubFetchNegotiationCache.WriteAsync(
                initial.RepositoryPath, [new string('a', 40)], cancellation.Token));
            Assert.Equal(hints, await File.ReadAllTextAsync(path));
        }
        finally
        {
            DeleteCacheRoot(cacheRoot);
        }
    }

    private static GitHubRepositoryCache NegotiatingCache(
        string root,
        GitFixture provider,
        FetchRecordingRunner runner) => new(
            runner,
            new GitClient(runner, (_, _, _) => throw new InvalidOperationException()),
            root,
            _ => new Uri(provider.RootPath + Path.DirectorySeparatorChar).AbsoluteUri);

    [GeneratedRegex(@"Total (\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex TransferredObjectCountPattern();

    private sealed class FetchRecordingRunner : IExternalCommandRunner
    {
        public ConcurrentQueue<IReadOnlyList<string>> Fetches { get; } = new();
        public ConcurrentQueue<int> TransferredObjectCounts { get; } = new();

        public async Task<ExternalCommandResult> RunAsync(
            string executable,
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            bool requireSuccess = true)
        {
            if (arguments.Contains("fetch"))
            {
                Fetches.Enqueue([.. arguments]);
                arguments = [.. arguments, "--progress"];
            }

            ExternalCommandResult result = await new ExternalCommandRunner().RunAsync(
                executable, workingDirectory, arguments, cancellationToken, requireSuccess);
            if (arguments.Contains("fetch"))
            {
                // The sender's total excludes bases added while fixing a thin pack.
                // Net object-store growth is not a wire-transfer measurement.
                Match total = TransferredObjectCountPattern().Match(result.StandardError);
                Assert.True(total.Success, result.StandardError);
                TransferredObjectCounts.Enqueue(int.Parse(total.Groups[1].Value, CultureInfo.InvariantCulture));
            }

            return result;
        }
    }
}
