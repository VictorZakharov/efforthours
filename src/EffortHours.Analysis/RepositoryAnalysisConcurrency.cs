using System.Diagnostics;

namespace EffortHours.Analysis;

public readonly record struct RepositoryAnalysisConcurrencyStatistics(
    long Acquisitions,
    TimeSpan OccupiedTime,
    TimeSpan WaitTime,
    int MaximumActive,
    RepositoryAnalysisWorkStatistics CommonFileInspection,
    RepositoryAnalysisWorkStatistics SemanticFileAnalysis,
    RepositoryAnalysisWorkStatistics RepositoryEstimation);

public readonly record struct RepositoryAnalysisWorkStatistics(
    long Acquisitions,
    TimeSpan OccupiedTime,
    TimeSpan WaitTime);

public enum RepositoryAnalysisWorkKind
{
    CommonFileInspection,
    SemanticFileAnalysis,
    RepositoryEstimation,
}

public static class RepositoryAnalysisConcurrency
{
    private const int MaximumCpuWorkers = 24;
    private const int MaximumCommonWorkersPerPipeline = 4;
    private static readonly Lock ConfigurationGate = new();
    private static int FileAnalysisLimit = Math.Min(
        MaximumCpuWorkers,
        Math.Max(1, Environment.ProcessorCount));
    private static SemaphoreSlim CpuAnalysisSlots = new(
        FileAnalysisLimit,
        FileAnalysisLimit);
    private static SemaphoreSlim BufferedFileReadSlots = new(
        FileAnalysisLimit,
        FileAnalysisLimit);
    private static long _acquisitions;
    private static long _occupiedTimestamp;
    private static long _waitTimestamp;
    private static readonly long[] WorkAcquisitions = new long[3];
    private static readonly long[] WorkOccupiedTimestamps = new long[3];
    private static readonly long[] WorkWaitTimestamps = new long[3];
    private static int _active;
    private static int _activeBufferedFileReads;
    private static int _maximumActive;

    public static int MaximumCpuWorkItems => FileAnalysisLimit;
    public static int MaximumFileAnalyses => FileAnalysisLimit;
    public static int MaximumCommonFileInspections => Math.Min(
        MaximumCommonWorkersPerPipeline,
        FileAnalysisLimit);
    public static int MaximumPendingFileInspections =>
        MaximumCommonFileInspections * 2;
    public const int MaximumBufferedFileBytes = 1024 * 1024;

    public static async ValueTask<IDisposable> AcquireFileAnalysisAsync(
        RepositoryAnalysisWorkKind kind,
        CancellationToken cancellationToken)
    {
        long waitStarted = Stopwatch.GetTimestamp();
        await CpuAnalysisSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        long acquired = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref _acquisitions);
        Interlocked.Add(ref _waitTimestamp, acquired - waitStarted);
        int kindIndex = (int)kind;
        Interlocked.Increment(ref WorkAcquisitions[kindIndex]);
        Interlocked.Add(ref WorkWaitTimestamps[kindIndex], acquired - waitStarted);
        int active = Interlocked.Increment(ref _active);
        int observedMaximum;
        while (active > (observedMaximum = Volatile.Read(ref _maximumActive)) &&
            Interlocked.CompareExchange(
                ref _maximumActive,
                active,
                observedMaximum) != observedMaximum)
        {
        }

        return new CpuAnalysisLease(acquired, kindIndex);
    }

    public static async ValueTask<IDisposable> AcquireBufferedFileReadAsync(
        CancellationToken cancellationToken)
    {
        SemaphoreSlim slots = BufferedFileReadSlots;
        await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _activeBufferedFileReads);
        return new BufferedFileReadLease(slots);
    }

    public static RepositoryAnalysisConcurrencyStatistics GetStatistics() => new(
        Interlocked.Read(ref _acquisitions),
        Duration(Interlocked.Read(ref _occupiedTimestamp)),
        Duration(Interlocked.Read(ref _waitTimestamp)),
        Volatile.Read(ref _maximumActive),
        WorkStatistics(RepositoryAnalysisWorkKind.CommonFileInspection),
        WorkStatistics(RepositoryAnalysisWorkKind.SemanticFileAnalysis),
        WorkStatistics(RepositoryAnalysisWorkKind.RepositoryEstimation));

    internal static void ConfigureFileAnalysisLimitForBenchmark(int maximumWorkers)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumWorkers, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            maximumWorkers,
            Math.Max(1, Environment.ProcessorCount));
        lock (ConfigurationGate)
        {
            if (Interlocked.Read(ref _acquisitions) != 0 ||
                Volatile.Read(ref _active) != 0 ||
                Volatile.Read(ref _activeBufferedFileReads) != 0)
            {
                throw new InvalidOperationException(
                    "Benchmark concurrency must be configured before repository analysis starts.");
            }

            SemaphoreSlim previous = CpuAnalysisSlots;
            SemaphoreSlim previousReadSlots = BufferedFileReadSlots;
            FileAnalysisLimit = maximumWorkers;
            CpuAnalysisSlots = new SemaphoreSlim(maximumWorkers, maximumWorkers);
            BufferedFileReadSlots = new SemaphoreSlim(maximumWorkers, maximumWorkers);
            previous.Dispose();
            previousReadSlots.Dispose();
        }
    }

    private static TimeSpan Duration(long timestamp) => TimeSpan.FromSeconds(
        (double)timestamp / Stopwatch.Frequency);

    private static RepositoryAnalysisWorkStatistics WorkStatistics(
        RepositoryAnalysisWorkKind kind)
    {
        int index = (int)kind;
        return new RepositoryAnalysisWorkStatistics(
            Interlocked.Read(ref WorkAcquisitions[index]),
            Duration(Interlocked.Read(ref WorkOccupiedTimestamps[index])),
            Duration(Interlocked.Read(ref WorkWaitTimestamps[index])));
    }

    private sealed class CpuAnalysisLease(long acquired, int kindIndex) : IDisposable
    {
        private readonly long _acquired = acquired;
        private readonly int _kindIndex = kindIndex;
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            long occupied = Stopwatch.GetTimestamp() - _acquired;
            Interlocked.Add(ref _occupiedTimestamp, occupied);
            Interlocked.Add(ref WorkOccupiedTimestamps[_kindIndex], occupied);
            Interlocked.Decrement(ref _active);
            CpuAnalysisSlots.Release();
        }
    }

    private sealed class BufferedFileReadLease(SemaphoreSlim slots) : IDisposable
    {
        private readonly SemaphoreSlim _slots = slots;
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            Interlocked.Decrement(ref _activeBufferedFileReads);
            _slots.Release();
        }
    }
}
