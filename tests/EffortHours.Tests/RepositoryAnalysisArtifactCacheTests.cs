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
}
