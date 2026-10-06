using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using EffortHours.Change;

namespace EffortHours.ChangeBenchmarks;

internal static class HistoricalPullDiscoveryBenchmark
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static async Task<int> RunAsync(string[] arguments)
    {
        int latency = arguments.Length > 1 ? int.Parse(arguments[1], CultureInfo.InvariantCulture) : 50;
        if (latency is < 0 or > 1000) throw new ArgumentException("Simulated provider latency must be 0-1000 milliseconds.");
        List<object> results = [];
        foreach ((string scope, int count, bool annual) in new[] { ("narrow-five-day", 1, false), ("broad-annual", 16, true) })
        {
            MemoryPullMetadataCache cache = new();
            foreach (string state in new[] { "cold", "warm" })
            {
                HistoricalPullProviderFixture runner = new(258, latency) { RepositoryCount = count };
                ProviderQueryCounters counters = new() { PullMetadataCache = cache };
                Stopwatch watch = Stopwatch.StartNew();
                IReadOnlyList<DiscoveredRepository>? repositories = await HistoricalPullProviderFixture.DiscoverScopeAsync(runner, counters, annual, count).ConfigureAwait(false);
                watch.Stop();
                results.Add(new
                {
                    scope,
                    includedRepositories = count,
                    state,
                    sinceInclusive = annual ? "2025-01-01T00:00:00Z" : HistoricalPullProviderFixture.Since.ToString("O", CultureInfo.InvariantCulture),
                    untilExclusive = annual ? "2026-01-01T00:00:00Z" : HistoricalPullProviderFixture.Since.AddDays(5).ToString("O", CultureInfo.InvariantCulture),
                    elapsedMilliseconds = watch.Elapsed.TotalMilliseconds,
                    queries = counters.QueryCount,
                    pages = counters.PageCount,
                    adapterProcesses = counters.ProcessCount,
                    selectedHeads = repositories!.Sum(repository => repository.Heads.Count),
                    matchingPrRepresentations = counters.Diagnostics("not-observed").HistoricalPullRequests!.SelectedCount,
                    acquisitionExecuted = false,
                    acquiredObjects = 0,
                    acquiredBytes = 0,
                    peakAdapterConcurrency = runner.Peak,
                    plan = counters.Diagnostics("not-observed").HistoricalPullRequests,
                });
            }
        }
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            population = 258,
            excludedPrRepositories = 1,
            simulatedLatencyMilliseconds = latency,
            batchPrLimit = 12,
            concurrentAdapterLimit = 4,
            responseCharacterLimit = 16777216,
            adapterRequestLimit = 2048,
            headLimitPerRepository = 512,
            metadataEntryByteLimit = 65536,
            metadataRetentionLimit = 1000,
            runtime = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription,
            processors = Environment.ProcessorCount,
            results,
            boundary = "In-memory provider latency/process accounting simulation; no GitHub/network or physical provider children. Not field latency or a CI timing threshold."
        }, Options));
        return 0;
    }
}
