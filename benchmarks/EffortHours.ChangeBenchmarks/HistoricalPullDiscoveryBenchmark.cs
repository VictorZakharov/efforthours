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
        HistoricalPullProviderFixture runner = new(258, latency);
        MemoryPullMetadataCache cache = new();
        List<object> results = [];
        foreach (string state in new[] { "cold", "warm" })
        {
            ProviderQueryCounters counters = new() { PullMetadataCache = cache };
            Stopwatch watch = Stopwatch.StartNew();
            IReadOnlyList<DiscoveredRepository>? repositories = await HistoricalPullProviderFixture.DiscoverAsync(runner, counters).ConfigureAwait(false);
            watch.Stop();
            results.Add(new
            {
                state,
                elapsedMilliseconds = watch.Elapsed.TotalMilliseconds,
                queries = counters.QueryCount,
                pages = counters.PageCount,
                adapterProcesses = counters.ProcessCount,
                selectedHeads = repositories!.Sum(repository => repository.Heads.Count),
                plan = counters.Diagnostics("not-observed").HistoricalPullRequests
            });
        }
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            population = 258,
            includedRepositories = 1,
            excludedPrRepositories = 1,
            simulatedLatencyMilliseconds = latency,
            runtime = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription,
            processors = Environment.ProcessorCount,
            results,
            boundary = "In-memory provider latency/process accounting simulation; no GitHub/network or physical provider children. Not field latency or a CI timing threshold."
        }, Options));
        return 0;
    }
}
