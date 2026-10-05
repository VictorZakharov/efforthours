using EffortHours.Analysis;

namespace EffortHours.Tests;

public sealed class RepositoryArtifactWorkingSetTests
{
    [Fact]
    public void ReplacesObsoleteVersionsAndRequiresExactContextAndType()
    {
        RepositoryArtifactWorkingSet set = new();
        for (int revision = 0; revision < 1000; revision++)
            set.Add("csharp:a.cs", $"content-and-context:{revision}", new Payload("current"));
        Assert.False(set.TryGet("csharp:a.cs", "content-and-context:0", out Payload _));
        Assert.True(set.TryGet("csharp:a.cs", "content-and-context:999", out Payload value));
        Assert.Equal("current", value.Text);
        Assert.Throws<InvalidOperationException>(() => set.TryGet(
            "csharp:a.cs", "content-and-context:999", out string _));
        Assert.False(set.TryGet("sql:a.cs", "content-and-context:999", out Payload _));
        RepositoryArtifactWorkingSetStatistics stats = set.GetStatistics();
        Assert.Equal(1, stats.Entries);
        Assert.Equal(1, stats.PeakEntries);
        Assert.Equal(999, stats.Replacements);
        Assert.True(stats.ChargedBytes <= stats.ByteLimit);
    }

    [Fact]
    public void EntryAndByteBoundsRetainTheSameSlotsRegardlessOfInsertionOrder()
    {
        foreach (long bytes in new long[] { 100_000, 5000 })
        {
            RepositoryArtifactWorkingSet forward = new(3, bytes);
            RepositoryArtifactWorkingSet reverse = new(3, bytes);
            string[] slots = [.. Enumerable.Range(0, 20).Select(index => $"file:{index}")];
            foreach (string slot in slots) forward.Add(slot, slot, new Payload("value"));
            foreach (string slot in slots.Reverse()) reverse.Add(slot, slot, new Payload("value"));
            Assert.Equal(slots.Select(slot => forward.TryGet(slot, slot, out Payload _)),
                slots.Select(slot => reverse.TryGet(slot, slot, out Payload _)));
            Assert.InRange(forward.GetStatistics().Entries, 1, 3);
            Assert.InRange(forward.GetStatistics().PeakChargedBytes, 1, bytes);
            Assert.True(forward.GetStatistics().Evictions > 0);
        }
    }

    [Fact]
    public void OversizedArtifactsAreNotRetained()
    {
        RepositoryArtifactWorkingSet set = new(10, 4096);
        set.Add("large", "content", new Payload(new string('a', 100_000)));
        Assert.False(set.TryGet("large", "content", out Payload _));
        Assert.Equal(0, set.GetStatistics().Entries);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RepositoryArtifactWorkingSet(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RepositoryArtifactWorkingSet(1, 0));
    }

    [Fact]
    public async Task LargeStablePopulationOutlivesTheHistoricalEntryBound()
    {
        RepositoryArtifactWorkingSet set = new();
        RepositoryAnalysisArtifactCache cache = new(2, workingSet: set);
        int factories = 0;
        async Task Populate(int changedRevision)
        {
            for (int index = 0; index < 100; index++)
            {
                string key = $"file:{index}:revision:{(index == 0 ? changedRevision : 0)}";
                await cache.GetOrCreateAsync(key, _ =>
                {
                    factories++;
                    return Task.FromResult(new Payload(key));
                }, CancellationToken.None, $"stage:file:{index}");
            }
        }
        await Populate(0);
        await Populate(1);
        Assert.Equal(101, factories);
        Assert.Equal(99, cache.GetStatistics().Hits);
        Assert.Equal(2, cache.GetStatistics().PeakEntries);
        Assert.Equal(100, set.GetStatistics().Entries);
        Assert.Equal(1, set.GetStatistics().Replacements);
    }

    [Fact]
    public async Task WorkingSlotsPreserveSingleFlightAndRecoverAfterFailure()
    {
        RepositoryArtifactWorkingSet set = new();
        RepositoryAnalysisArtifactCache cache = new(1, workingSet: set);
        TaskCompletionSource<Payload> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Payload> first = cache.GetOrCreateAsync("key", _ => release.Task,
            CancellationToken.None, "slot");
        Task<Payload> second = cache.GetOrCreateAsync<Payload>("key",
            _ => throw new InvalidOperationException("must join"), CancellationToken.None, "slot");
        Payload value = new("result");
        release.SetResult(value);
        Assert.Same(await first, await second);
        Assert.Same(value, await cache.GetOrCreateAsync<Payload>("key",
            _ => throw new InvalidOperationException("must reuse"), CancellationToken.None, "slot"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrCreateAsync<Payload>(
            "failed", _ => throw new InvalidOperationException("expected"), CancellationToken.None, "slot"));
        Assert.True(set.TryGet("slot", "key", out Payload previous));
        Assert.Same(value, previous);
        Payload retried = await cache.GetOrCreateAsync("failed", _ => Task.FromResult(new Payload("retry")),
            CancellationToken.None, "slot");
        Assert.True(set.TryGet("slot", "failed", out Payload retained));
        Assert.Same(retried, retained);
    }

    [Fact]
    public async Task NonRetainedResultsCompleteWaitersAndAllowAValidRetry()
    {
        RepositoryArtifactWorkingSet set = new();
        RepositoryAnalysisArtifactCache cache = new(1, workingSet: set);
        TaskCompletionSource<Payload> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Payload> first = cache.GetOrCreateAsync("key", _ => release.Task,
            CancellationToken.None, "slot", shouldRetain: item => item.Text == "valid");
        Task<Payload> waiter = cache.GetOrCreateAsync<Payload>("key", _ => throw new InvalidOperationException(),
            CancellationToken.None, "slot");
        Payload unavailable = new("unavailable");
        release.SetResult(unavailable);
        Assert.Same(await first, await waiter);
        Assert.False(set.TryGet("slot", "key", out Payload _));
        Payload valid = await cache.GetOrCreateAsync("key", _ => Task.FromResult(new Payload("valid")),
            CancellationToken.None, "slot", shouldRetain: item => item.Text == "valid");
        Assert.True(set.TryGet("slot", "key", out Payload retained));
        Assert.Same(valid, retained);
    }

    private sealed record Payload(string Text);
}
