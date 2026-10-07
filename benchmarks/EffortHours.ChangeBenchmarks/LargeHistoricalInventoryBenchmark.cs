using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using EffortHours.Change;

namespace EffortHours.ChangeBenchmarks;

internal static class LargeHistoricalInventoryBenchmark
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static async Task<int> RunAsync()
    {
        List<object> results = [];
        foreach (var (scope, count, annual) in new[] { ("five-day-single", 1, false), ("annual-single", 1, true), ("annual-multi", 16, true) })
        {
            MemoryPullMetadataCache cache = new();
            foreach (string state in new[] { "cold", "warm" })
            {
                HistoricalPullProviderFixture runner = new(258) { UnrelatedPullCount = 16126, RepositoryCount = count };
                ProviderQueryCounters counters = new() { PullMetadataCache = cache };
                await using WorkingSetSampler memory = WorkingSetSampler.Start();
                long before = GC.GetTotalAllocatedBytes(true);
                Stopwatch watch = Stopwatch.StartNew();
                var repositories = await HistoricalPullProviderFixture.DiscoverOptimizedAsync(runner, counters, count, annual: annual).ConfigureAwait(false);
                watch.Stop();
                results.Add(new
                {
                    scope,
                    repositoryCount = count,
                    state,
                    population = 16384,
                    authoredCandidates = 258,
                    elapsedMilliseconds = watch.Elapsed.TotalMilliseconds,
                    allocatedBytes = GC.GetTotalAllocatedBytes(true) - before,
                    sampledPeakWorkingSetBytes = await memory.StopAsync().ConfigureAwait(false),
                    providerQueries = counters.QueryCount,
                    providerPages = counters.PageCount,
                    adapterProcesses = counters.ProcessCount,
                    peakAdapterConcurrency = runner.Peak,
                    selectedHeads = repositories!.Sum(repository => repository.Heads.Count),
                    metadata = counters.Diagnostics("not-observed").HistoricalPullRequests,
                    acquisitionExecuted = false,
                    acquiredObjects = 0,
                    acquiredBytes = 0,
                });
            }
        }
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            runtime = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription,
            pageRows = 100,
            pageCharacterLimit = 1048576,
            inventoryLedgerByteLimit = 16777216,
            authoredCandidateLimitPerRepository = 1000,
            adapterRequestLimit = 2048,
            concurrentReaders = 4,
            results,
            boundary = "Synthetic in-memory provider; no real network, child startup, acquisition or static analysis. Memory includes the fixture/runtime. Timing is diagnostic, never a CI threshold or NDA field claim."
        }, Options));
        return 0;
    }
}
