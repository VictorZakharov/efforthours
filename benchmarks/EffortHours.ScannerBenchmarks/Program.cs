using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.ScannerBenchmarks;

public static class Program
{
    public static async Task<int> Main(string[] arguments)
    {
        BenchmarkOptions options;
        try
        {
            options = BenchmarkOptions.Parse(arguments);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(BenchmarkOptions.Usage);
            return 2;
        }

        BenchmarkTarget? target = null;
        try
        {
            if (options.FileAnalysisWorkers is { } fileAnalysisWorkers)
            {
                RepositoryAnalysisConcurrency.ConfigureFileAnalysisLimitForBenchmark(
                    fileAnalysisWorkers);
            }

            Stopwatch generationTimer = Stopwatch.StartNew();
            target = BenchmarkTarget.Create(options);
            generationTimer.Stop();
            TargetMetadataSnapshot before = TargetMetadataSnapshot.Capture(target.RootPath);

            IRepositoryScanner scanner = options.Shape == BenchmarkShape.Common
                ? new RepositoryScanner()
                : new RepositoryAnalysisPipeline();
            long allocationsBefore = GC.GetTotalAllocatedBytes(precise: true);
            TimeSpan processorTimeBefore = Process.GetCurrentProcess().TotalProcessorTime;
            long workingSetBefore = Environment.WorkingSet;
            await using WorkingSetSampler workingSet = WorkingSetSampler.Start();
            Stopwatch scanTimer = Stopwatch.StartNew();
            RepositoryEvidence evidence = await scanner.ScanAsync(target.RootPath);
            scanTimer.Stop();
            TimeSpan processorTime = Process.GetCurrentProcess().TotalProcessorTime -
                processorTimeBefore;
            long peakWorkingSet = await workingSet.StopAsync().ConfigureAwait(false);
            long workingSetAfter = Environment.WorkingSet;
            long scanAllocations = GC.GetTotalAllocatedBytes(precise: true) - allocationsBefore;

            Stopwatch serializationTimer = Stopwatch.StartNew();
            string json = ContractJson.Serialize(evidence);
            serializationTimer.Stop();

            WarmCacheMeasurements? warm = options.MeasureWarmCache
                ? await MeasureWarmCacheAsync(scanner, target, evidence).ConfigureAwait(false)
                : null;
            TargetMetadataSnapshot after = TargetMetadataSnapshot.Capture(target.RootPath);
            if (before.Digest != after.Digest)
            {
                throw new InvalidOperationException(
                    "The target-tree metadata changed during static analysis; the benchmark refuses to " +
                    "report the run as read-only.");
            }

            WriteResults(
                options,
                target,
                evidence,
                generationTimer.Elapsed,
                scanTimer.Elapsed,
                processorTime,
                serializationTimer.Elapsed,
                scanAllocations,
                workingSetBefore,
                peakWorkingSet,
                workingSetAfter,
                Encoding.UTF8.GetByteCount(json),
                before,
                warm);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Benchmark failed: {exception}");
            return 1;
        }
        finally
        {
            target?.Dispose();
        }
    }

    private static async Task<WarmCacheMeasurements> MeasureWarmCacheAsync(
        IRepositoryScanner scanner,
        BenchmarkTarget target,
        RepositoryEvidence coldEvidence)
    {
        RepositoryScanOptions cacheOptions = new() { CachePath = target.CachePath };
        _ = await scanner.ScanAsync(target.RootPath, cacheOptions).ConfigureAwait(false);
        long allocationsBefore = GC.GetTotalAllocatedBytes(precise: true);
        long workingSetBefore = Environment.WorkingSet;
        await using WorkingSetSampler workingSet = WorkingSetSampler.Start();
        Stopwatch timer = Stopwatch.StartNew();
        RepositoryEvidence evidence = await scanner.ScanAsync(target.RootPath, cacheOptions)
            .ConfigureAwait(false);
        timer.Stop();
        long peakWorkingSet = await workingSet.StopAsync().ConfigureAwait(false);
        long workingSetAfter = Environment.WorkingSet;
        long allocations = GC.GetTotalAllocatedBytes(precise: true) - allocationsBefore;
        if (evidence.Repository.SourceDigest != coldEvidence.Repository.SourceDigest)
        {
            throw new InvalidOperationException("Warm-cache evidence digest differs from the full scan.");
        }

        return new WarmCacheMeasurements(
            timer.Elapsed,
            allocations,
            workingSetBefore,
            peakWorkingSet,
            workingSetAfter);
    }

    private static void WriteResults(
        BenchmarkOptions options,
        BenchmarkTarget target,
        RepositoryEvidence evidence,
        TimeSpan generation,
        TimeSpan scan,
        TimeSpan processorTime,
        TimeSpan serialization,
        long allocations,
        long workingSetBefore,
        long peakWorkingSet,
        long workingSetAfter,
        int jsonBytes,
        TargetMetadataSnapshot metadata,
        WarmCacheMeasurements? warm)
    {
        InventoryMeasurements inventory = InventoryMeasurements.From(evidence);
        Console.WriteLine($"scanner={RepositoryScanner.AnalyzerVersion}");
        Console.WriteLine($"mode={options.Mode}");
        Console.WriteLine($"runtime={RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"os={RuntimeInformation.OSDescription}");
        Console.WriteLine($"architecture={RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"logical-processors={Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"file-analysis-workers=" +
            $"{(options.FileAnalysisWorkers ?? RepositoryAnalysisConcurrency.MaximumCpuWorkItems).ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"gc-available-memory-mib={ToMebibytes(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes)}");
        Console.WriteLine($"generated-fixture={target.OwnsTarget.ToString().ToLowerInvariant()}");
        Console.WriteLine($"requested-lines={target.RequestedLines.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"included-files={inventory.Files.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"included-bytes={inventory.Bytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"analyzed-text-lines={inventory.TextLines.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"target-files={metadata.FileCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"target-bytes={metadata.TotalBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"generation-seconds={Seconds(generation)}");
        Console.WriteLine($"scan-seconds={Seconds(scan)}");
        Console.WriteLine($"scan-cpu-seconds={Seconds(processorTime)}");
        Console.WriteLine(
            $"scan-average-processor-equivalents=" +
            $"{(processorTime.TotalSeconds / scan.TotalSeconds).ToString("F3", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"serialization-seconds={Seconds(serialization)}");
        Console.WriteLine($"scan-lines-per-second={Rate(inventory.TextLines, scan)}");
        Console.WriteLine($"scan-allocated-mib={ToMebibytes(allocations)}");
        Console.WriteLine($"scan-working-set-start-mib={ToMebibytes(workingSetBefore)}");
        Console.WriteLine($"scan-peak-working-set-mib={ToMebibytes(peakWorkingSet)}");
        Console.WriteLine($"scan-working-set-end-mib={ToMebibytes(workingSetAfter)}");
        Console.WriteLine($"json-mib={ToMebibytes(jsonBytes)}");
        Console.WriteLine($"facts={evidence.Facts.Count.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"digest={evidence.Repository.SourceDigest}");
        Console.WriteLine($"target-metadata-digest={metadata.Digest}");
        Console.WriteLine("target-metadata-unchanged=true");
        Console.WriteLine("target-execution=not-performed");
        Console.WriteLine("dependency-installation=not-performed");
        Console.WriteLine("network-access=not-performed");

        if (warm is not null)
        {
            Console.WriteLine($"warm-cache-seconds={Seconds(warm.Elapsed)}");
            Console.WriteLine($"warm-cache-allocated-mib={ToMebibytes(warm.Allocations)}");
            Console.WriteLine($"warm-cache-working-set-start-mib={ToMebibytes(warm.WorkingSetBefore)}");
            Console.WriteLine($"warm-cache-peak-working-set-mib={ToMebibytes(warm.PeakWorkingSet)}");
            Console.WriteLine($"warm-cache-working-set-end-mib={ToMebibytes(warm.WorkingSetAfter)}");
        }
    }

    private static string Seconds(TimeSpan value) =>
        value.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture);

    private static string Rate(long lines, TimeSpan elapsed) =>
        (lines / elapsed.TotalSeconds).ToString("F0", CultureInfo.InvariantCulture);

    private static string ToMebibytes(long bytes) =>
        (bytes / 1024m / 1024m).ToString("F2", CultureInfo.InvariantCulture);

    private sealed record WarmCacheMeasurements(
        TimeSpan Elapsed,
        long Allocations,
        long WorkingSetBefore,
        long PeakWorkingSet,
        long WorkingSetAfter);
}
