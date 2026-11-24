using EffortHours.Analysis;

namespace EffortHours.Tests;

public sealed class RepositoryAnalysisArtifactCacheTests
{
    private static readonly string[] Keys = ["artifact-a", "artifact-b", "artifact-c"];

    [Fact]
    public void CacheRetainsDeterministicBoundedArtifactsAndDistinguishesRevisitMisses()
    {
        RepositoryAnalysisArtifactCache forward = Populate(["artifact-a", "artifact-b", "artifact-c"]);
        RepositoryAnalysisArtifactCache reverse = Populate(["artifact-c", "artifact-b", "artifact-a"]);

        bool[] forwardRetained = Probe(forward);
        bool[] reverseRetained = Probe(reverse);

        Assert.Equal(forwardRetained, reverseRetained);
        Assert.Equal(2, forwardRetained.Count(value => value));
        AssertStatistics(forward.GetStatistics());
        AssertStatistics(reverse.GetStatistics());
    }

    [Fact]
    public async Task ConcurrentRequestsComputeAndRetainOneImmutableArtifact()
    {
        RepositoryAnalysisArtifactCache cache = new();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int factories = 0;
        Task<string> owner = cache.GetOrCreateAsync(
            "shared",
            async cancellationToken =>
            {
                Interlocked.Increment(ref factories);
                started.SetResult();
                await release.Task.WaitAsync(cancellationToken);
                return "value";
            },
            CancellationToken.None);
        await started.Task;
        Task<string>[] followers = [.. Enumerable.Range(0, 7).Select(index =>
            cache.GetOrCreateAsync<string>(
                "shared",
                cancellationToken => throw new InvalidOperationException(
                    $"Follower {index} became the owner."),
                CancellationToken.None))];

        release.SetResult();
        string[] results = await Task.WhenAll([owner, .. followers]);

        Assert.All(results, value => Assert.Equal("value", value));
        Assert.Equal(1, factories);
        Assert.True(cache.TryGet("shared", out string retained));
        Assert.Equal("value", retained);
        RepositoryAnalysisArtifactCacheStatistics statistics = cache.GetStatistics();
        Assert.Equal(9, statistics.Requests);
        Assert.Equal(8, statistics.Hits);
        Assert.Equal(1, statistics.UniqueKeys);
        Assert.Equal(0, statistics.RevisitMisses);
        Assert.Equal(1, statistics.PeakEntries);
    }

    [Fact]
    public async Task FailedOwnerReleasesTheKeyForDeterministicRetry()
    {
        RepositoryAnalysisArtifactCache cache = new();

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrCreateAsync<string>(
            "retry",
            _ => throw new InvalidOperationException("expected failure"),
            CancellationToken.None));
        string result = await cache.GetOrCreateAsync(
            "retry",
            _ => Task.FromResult("recovered"),
            CancellationToken.None);

        Assert.Equal("recovered", result);
        RepositoryAnalysisArtifactCacheStatistics statistics = cache.GetStatistics();
        Assert.Equal(2, statistics.Requests);
        Assert.Equal(0, statistics.Hits);
        Assert.Equal(1, statistics.UniqueKeys);
        Assert.Equal(1, statistics.RevisitMisses);
    }

    private static RepositoryAnalysisArtifactCache Populate(IReadOnlyList<string> keys)
    {
        RepositoryAnalysisArtifactCache cache = new(maximumEntries: 2);
        foreach (string key in keys)
        {
            Assert.False(cache.TryGet(key, out string _));
            cache.Add(key, $"value:{key}");
        }

        return cache;
    }

    private static bool[] Probe(RepositoryAnalysisArtifactCache cache) =>
        [.. Keys.Select(key => cache.TryGet(key, out string _))];

    private static void AssertStatistics(RepositoryAnalysisArtifactCacheStatistics statistics)
    {
        Assert.Equal(6, statistics.Requests);
        Assert.Equal(2, statistics.Hits);
        Assert.Equal(3, statistics.UniqueKeys);
        Assert.Equal(1, statistics.RevisitMisses);
        Assert.Equal(1, statistics.Evictions);
        Assert.Equal(2, statistics.PeakEntries);
        Assert.Equal(2, statistics.EntryLimit);
    }
    [Fact]
    public async Task PersistedLoadsRunOutsideTheGateAndAreNotSavedAgain()
    {
        BlockingStore store = new();
        store.Values["slow"] = "slow-value";
        store.Values["fast"] = "fast-value";
        RepositoryAnalysisArtifactCache cache = new(store: store);
        Task<string> slow = Task.Run(() => cache.Request<string>("slow").Result);
        Assert.True(store.SlowLoadStarted.Wait(TimeSpan.FromSeconds(30)));
        // A blocked disk load must not stop other keys from resolving.
        Assert.Equal("fast-value", await cache.Request<string>("fast").Result);
        Assert.False(slow.IsCompleted);
        store.ReleaseSlow.Set();
        Assert.Equal("slow-value", await slow);
        Assert.Equal("slow-value", await cache.Request<string>("slow").Result);
        Assert.True(cache.TryGet("fast", out string fast));
        Assert.Equal("fast-value", fast);
        RepositoryAnalysisArtifactCache.RepositoryAnalysisArtifactRequest<string> missing = cache.Request<string>("missing");
        Assert.True(missing.IsOwner);
        Assert.Equal(0, store.Saves);
        RepositoryAnalysisArtifactCacheStatistics statistics = cache.GetStatistics();
        Assert.Equal(5, statistics.Requests);
        Assert.Equal(4, statistics.Hits);
    }

    private sealed class BlockingStore : IRepositoryAnalysisArtifactStore
    {
        public System.Collections.Concurrent.ConcurrentDictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
        public ManualResetEventSlim SlowLoadStarted { get; } = new();
        public ManualResetEventSlim ReleaseSlow { get; } = new();
        public int Saves;

        public bool TryLoad<T>(string key, out T value) where T : class
        {
            if (key == "slow" && !ReleaseSlow.IsSet)
            {
                SlowLoadStarted.Set();
                ReleaseSlow.Wait(TimeSpan.FromSeconds(30));
            }
            bool found = Values.TryGetValue(key, out string? text);
            value = (found ? text as T : null)!;
            return found;
        }

        public void Save<T>(string key, T value) where T : class => Interlocked.Increment(ref Saves);
    }
}
