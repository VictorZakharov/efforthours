using EffortHours.Analysis;

namespace EffortHours.Tests;

public sealed class RepositoryVersionedAnalysisCacheTests
{
    [Fact]
    public async Task ConcurrentRequestsAreSingleFlight()
    {
        RepositoryVersionedAnalysisCache cache = new();
        TaskCompletionSource signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int factories = 0;

        Task<string> first = cache.GetOrCreateAsync(
            "version-a",
            async cancellationToken =>
            {
                Interlocked.Increment(ref factories);
                await signal.Task.WaitAsync(cancellationToken);
                return new RepositoryVersionedAnalysisArtifact<string>("value", 5);
            },
            CancellationToken.None);
        Task<string> second = cache.GetOrCreateAsync<string>(
            "version-a",
            _ => throw new InvalidOperationException("The second factory must not run."),
            CancellationToken.None);

        signal.SetResult();
        Assert.Equal("value", await first);
        Assert.Equal("value", await second);
        Assert.Equal(1, factories);
        RepositoryVersionedAnalysisCacheStatistics statistics = cache.GetStatistics();
        Assert.Equal(2, statistics.Requests);
        Assert.Equal(1, statistics.Hits);
        Assert.Equal(1, statistics.UniqueKeys);
    }

    [Fact]
    public async Task RetentionEvictsTheLeastRecentlyUsedArtifact()
    {
        RepositoryVersionedAnalysisCache cache = await PopulateAsync(
            ["version-a", "version-b"]);
        Assert.True(cache.TryGetExistingAsync<string>("version-a", out _));
        _ = await cache.GetOrCreateAsync(
            "version-c",
            _ => Task.FromResult(
                new RepositoryVersionedAnalysisArtifact<string>("version-c", 4)),
            CancellationToken.None);

        Assert.True(cache.TryGetExistingAsync<string>("version-a", out _));
        Assert.False(cache.TryGetExistingAsync<string>("version-b", out _));
        Assert.True(cache.TryGetExistingAsync<string>("version-c", out _));
        Assert.Equal(2, cache.GetStatistics().PeakEntries);
        Assert.Equal(8, cache.GetStatistics().PeakRetainedBytes);
        Assert.Equal(1, cache.GetStatistics().Evictions);
    }

    [Fact]
    public async Task RetentionHonorsByteLimitAndDoesNotKeepOversizedArtifacts()
    {
        RepositoryVersionedAnalysisCache cache = new(
            maximumEntries: 4,
            maximumRetainedBytes: 8);
        _ = await AddAsync(cache, "version-a", 6);
        _ = await AddAsync(cache, "version-b", 6);
        Assert.False(cache.TryGetExistingAsync<string>("version-a", out _));
        Assert.True(cache.TryGetExistingAsync<string>("version-b", out _));

        Assert.Equal("version-large", await AddAsync(cache, "version-large", 9));
        Assert.False(cache.TryGetExistingAsync<string>("version-large", out _));
        RepositoryVersionedAnalysisCacheStatistics statistics = cache.GetStatistics();
        Assert.Equal(6, statistics.RetainedBytes);
        Assert.Equal(6, statistics.PeakRetainedBytes);
        Assert.Equal(1, statistics.Evictions);
    }

    private static async Task<RepositoryVersionedAnalysisCache> PopulateAsync(
        IReadOnlyList<string> keys)
    {
        RepositoryVersionedAnalysisCache cache = new(
            maximumEntries: 2,
            maximumRetainedBytes: 8);
        foreach (string key in keys)
        {
            _ = await cache.GetOrCreateAsync(
                key,
                _ => Task.FromResult(
                    new RepositoryVersionedAnalysisArtifact<string>(key, 4)),
                CancellationToken.None);
        }

        return cache;
    }

    private static Task<string> AddAsync(
        RepositoryVersionedAnalysisCache cache,
        string key,
        long retainedBytes) => cache.GetOrCreateAsync(
            key,
            _ => Task.FromResult(
                new RepositoryVersionedAnalysisArtifact<string>(key, retainedBytes)),
            CancellationToken.None);
}
