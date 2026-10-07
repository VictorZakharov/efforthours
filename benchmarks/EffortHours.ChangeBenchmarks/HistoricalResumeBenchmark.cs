using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EffortHours.Change;

namespace EffortHours.ChangeBenchmarks;

internal static class HistoricalResumeBenchmark
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static async Task<int> RunAsync(string[] arguments)
    {
        if (arguments.Length != 3 || !int.TryParse(arguments[1], CultureInfo.InvariantCulture, out int latency) || latency is < 0 or > 1000)
            throw new ArgumentException("Use --historical-resume <latency-ms:0-1000> <new-private-cache-directory>.");
        string root = Path.GetFullPath(arguments[2]);
        if (Directory.Exists(root) || File.Exists(root)) throw new ArgumentException("The benchmark cache directory must be new.");
        Directory.CreateDirectory(root);
        List<object> results = [];
        foreach (var (scope, count, annual) in new[] { ("five-day-single", 1, false), ("annual-single", 1, true), ("annual-multi", 16, true) })
        {
            MemoryPullMetadataCache cache = new();
            foreach (string state in new[] { "cold", "warm" })
            {
                HistoricalPullProviderFixture runner = new(440, latency) { UnrelatedPullCount = 15944, RepositoryCount = count };
                ProviderQueryCounters counters = new() { PullMetadataCache = cache };
                Stopwatch watch = Stopwatch.StartNew();
                var selected = await HistoricalPullProviderFixture.DiscoverOptimizedAsync(runner, counters, count, annual).ConfigureAwait(false);
                watch.Stop();
                results.Add(Row(scope, state, counters, runner, selected!, watch.Elapsed));
            }
        }
        using CancellationTokenSource cancellation = new();
        using StopAfterCache interruptedCache = new(new GitHubPullMetadataCache(root, "synthetic-viewer"), cancellation);
        HistoricalPullProviderFixture first = new(440, latency) { UnrelatedPullCount = 15944 };
        ProviderQueryCounters pending = new() { PullMetadataCache = interruptedCache };
        Stopwatch timer = Stopwatch.StartNew();
        try
        {
            await HistoricalPullProviderFixture.DiscoverOptimizedAsync(first, pending, token: cancellation.Token).ConfigureAwait(false);
            throw new InvalidOperationException("The partial checkpoint did not interrupt.");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        timer.Stop();
        results.Add(Row("five-day-single", "interrupted-after-204", pending, first, [], timer.Elapsed));
        HistoricalPullProviderFixture second = new(440, latency) { UnrelatedPullCount = 15944 };
        // Reopen persisted sidecars, as a separate consumer invocation would do.
        ProviderQueryCounters resumed = new() { PullMetadataCache = new GitHubPullMetadataCache(root, "synthetic-viewer") };
        timer.Restart();
        var complete = await HistoricalPullProviderFixture.DiscoverOptimizedAsync(second, resumed).ConfigureAwait(false);
        timer.Stop();
        results.Add(Row("five-day-single", "resume-204-of-440", resumed, second, complete!, timer.Elapsed));
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            protocol = "historical-resume-checkpoint/1.0.0",
            runtime = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription,
            processors = Environment.ProcessorCount,
            population = 16384,
            authoredCandidates = 440,
            simulatedLatencyMilliseconds = latency,
            inventoryPageRows = 100,
            metadataBatchRows = 12,
            readerLimit = 4,
            inventoryPageCharacterLimit = 1048576,
            inventoryLedgerByteLimit = 16777216,
            adapterRequestLimit = 2048,
            metadataEntryByteLimit = 65536,
            metadataRetentionLimit = 1000,
            results,
            boundary = "Synthetic provider; physical atomic sidecars for interruption/resume only. No network, provider children, acquisition or static analysis. Timing is diagnostic, not a CI threshold or NDA field claim."
        }, Options));
        return 0;
    }

    private static object Row(string scope, string state, ProviderQueryCounters counters, HistoricalPullProviderFixture runner,
        IReadOnlyList<DiscoveredRepository> selected, TimeSpan elapsed) => new
        {
            scope,
            state,
            elapsedMilliseconds = elapsed.TotalMilliseconds,
            providerQueries = counters.QueryCount,
            providerPages = counters.PageCount,
            adapterProcesses = counters.ProcessCount,
            peakAdapterConcurrency = runner.Peak,
            metadataRequestedCandidates = runner.Calls.Where(call => call.Any(value => value.Contains("nodes{commit", StringComparison.Ordinal)))
                .Sum(call => call.Count(value => value.StartsWith("number", StringComparison.Ordinal))),
            selectedHeads = selected.Sum(repository => repository.Heads.Count),
            selectionDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",
                selected.SelectMany(repository => repository.Heads.Select(head => repository.RepositoryId + ":" + head.ObjectId)).Order(StringComparer.Ordinal))))).ToLowerInvariant(),
            metadata = counters.Diagnostics("not-observed").HistoricalPullRequests,
            acquiredObjects = 0,
            acquiredBytes = 0,
            acquisitionExecuted = false
        };

    private sealed class StopAfterCache(IGitHubPullMetadataCache inner, CancellationTokenSource cancellation) : IGitHubPullMetadataCache, IDisposable
    {
        private readonly SemaphoreSlim _writes = new(1);
        private int _completed;
        public void Dispose() => _writes.Dispose();
        public Task<GitHubPullMetadata?> ReadAsync(string repository, int number, string head, string baseHead, int count, CancellationToken token) =>
            inner.ReadAsync(repository, number, head, baseHead, count, token);
        public async Task<bool> WriteAsync(string repository, int number, GitHubPullMetadata metadata, CancellationToken token)
        {
            await _writes.WaitAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                bool saved = await inner.WriteAsync(repository, number, metadata, token).ConfigureAwait(false);
                if (saved && ++_completed == 204) cancellation.Cancel();
                return saved;
            }
            finally { _writes.Release(); }
        }
    }
}
