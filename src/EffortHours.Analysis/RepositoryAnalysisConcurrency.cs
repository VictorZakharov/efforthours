using System.Diagnostics;

namespace EffortHours.Analysis;

public readonly record struct RepositoryAnalysisConcurrencyStatistics(
    long Acquisitions,
    TimeSpan OccupiedTime,
    TimeSpan WaitTime,
    int MaximumActive,
    RepositoryAnalysisWorkStatistics GitTreeRead,
    RepositoryAnalysisWorkStatistics CommonFileInspection,
    RepositoryAnalysisWorkStatistics SemanticFileAnalysis,
    RepositoryAnalysisWorkStatistics RepositoryEstimation);

public readonly record struct RepositoryAnalysisWorkStatistics(
    long Acquisitions,
    TimeSpan OccupiedTime,
    TimeSpan WaitTime,
    TimeSpan ElapsedTime,
    TimeSpan MaximumOccupiedTime,
    TimeSpan ExternalProcessCpuTime,
    long OutputBytes,
    int MaximumActive);

public enum RepositoryAnalysisWorkKind
{
    GitTreeRead,
    CommonFileInspection,
    SemanticFileAnalysis,
    RepositoryEstimation,
}

public static class RepositoryAnalysisConcurrency
{
    private const int MaximumCpuWorkers = 24;
    private const int MaximumGitTreeWorkers = 8;
    private const int MaximumCommonWorkersPerPipeline = 4;
    private static readonly Lock ConfigurationGate = new();
    private static int FileAnalysisLimit = Math.Min(
        MaximumCpuWorkers,
        Math.Max(1, Environment.ProcessorCount));
    private static int GitTreeReadLimit = Math.Min(
        MaximumGitTreeWorkers,
        Math.Max(1, Environment.ProcessorCount));
    private static SemaphoreSlim CpuAnalysisSlots = new(
        FileAnalysisLimit,
        FileAnalysisLimit);
    private static SemaphoreSlim GitTreeReadSlots = new(
        GitTreeReadLimit,
        GitTreeReadLimit);
    private static SemaphoreSlim BufferedFileReadSlots = new(
        FileAnalysisLimit,
        FileAnalysisLimit);
    private static long _acquisitions;
    private static long _occupiedTimestamp;
    private static long _waitTimestamp;
    private static int _analysisStarted;
    private static readonly long[] WorkAcquisitions = new long[4];
    private static readonly long[] WorkOccupiedTimestamps = new long[4];
    private static readonly long[] WorkWaitTimestamps = new long[4];
    private static readonly long[] WorkFirstWaitTimestamps = new long[4];
    private static readonly long[] WorkLastCompletedTimestamps = new long[4];
    private static readonly long[] WorkMaximumOccupiedTimestamps = new long[4];
    private static readonly long[] WorkExternalProcessCpuTicks = new long[4];
    private static readonly long[] WorkOutputBytes = new long[4];
    private static readonly int[] WorkActive = new int[4];
    private static readonly int[] WorkMaximumActive = new int[4];
    private static int _active;
    private static int _activeBufferedFileReads;
    private static int _maximumActive;

    public static int MaximumCpuWorkItems => FileAnalysisLimit;
    public static int MaximumGitTreeReads => GitTreeReadLimit;
    public static int MaximumFileAnalyses => FileAnalysisLimit;
    public static int MaximumCommonFileInspections => Math.Min(
        MaximumCommonWorkersPerPipeline,
        FileAnalysisLimit);
    public static int MaximumPendingFileInspections =>
        MaximumCommonFileInspections * 2;
    public const int MaximumBufferedFileBytes = 1024 * 1024;

    public static void RecordExternalProcessMetrics(
        RepositoryAnalysisWorkKind kind,
        TimeSpan processCpuTime,
        long outputBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(outputBytes);
        int kindIndex = (int)kind;
        Interlocked.Add(ref WorkExternalProcessCpuTicks[kindIndex], processCpuTime.Ticks);
        Interlocked.Add(ref WorkOutputBytes[kindIndex], outputBytes);
    }

    public static async ValueTask<IDisposable> AcquireFileAnalysisAsync(
        RepositoryAnalysisWorkKind kind,
        CancellationToken cancellationToken)
    {
        if (kind == RepositoryAnalysisWorkKind.GitTreeRead)
        {
            throw new ArgumentException(
                "Git tree reads use their separate bounded I/O scheduler.",
                nameof(kind));
        }

        long waitStarted = Stopwatch.GetTimestamp();
        int kindIndex = (int)kind;
        Volatile.Write(ref _analysisStarted, 1);
        await CpuAnalysisSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.CompareExchange(
            ref WorkFirstWaitTimestamps[kindIndex],
            waitStarted,
            comparand: 0);
        long acquired = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref _acquisitions);
        Interlocked.Add(ref _waitTimestamp, acquired - waitStarted);
        Interlocked.Increment(ref WorkAcquisitions[kindIndex]);
        Interlocked.Add(ref WorkWaitTimestamps[kindIndex], acquired - waitStarted);
        RecordWorkActive(kindIndex);

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

    public static async ValueTask<IDisposable> AcquireGitTreeReadAsync(
        CancellationToken cancellationToken)
    {
        const int KindIndex = (int)RepositoryAnalysisWorkKind.GitTreeRead;
        long waitStarted = Stopwatch.GetTimestamp();
        SemaphoreSlim slots = GitTreeReadSlots;
        Volatile.Write(ref _analysisStarted, 1);
        await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.CompareExchange(
            ref WorkFirstWaitTimestamps[KindIndex],
            waitStarted,
            comparand: 0);
        long acquired = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref WorkAcquisitions[KindIndex]);
        Interlocked.Add(ref WorkWaitTimestamps[KindIndex], acquired - waitStarted);
        RecordWorkActive(KindIndex);
        return new GitTreeReadLease(slots, acquired);
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
        WorkStatistics(RepositoryAnalysisWorkKind.GitTreeRead),
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
            if (Volatile.Read(ref _analysisStarted) != 0 ||
                Volatile.Read(ref _active) != 0 ||
                Volatile.Read(ref _activeBufferedFileReads) != 0)
            {
                throw new InvalidOperationException(
                    "Benchmark concurrency must be configured before repository analysis starts.");
            }

            SemaphoreSlim previous = CpuAnalysisSlots;
            SemaphoreSlim previousGitTreeReadSlots = GitTreeReadSlots;
            SemaphoreSlim previousReadSlots = BufferedFileReadSlots;
            FileAnalysisLimit = maximumWorkers;
            GitTreeReadLimit = Math.Min(maximumWorkers, MaximumGitTreeWorkers);
            CpuAnalysisSlots = new SemaphoreSlim(maximumWorkers, maximumWorkers);
            GitTreeReadSlots = new SemaphoreSlim(GitTreeReadLimit, GitTreeReadLimit);
            BufferedFileReadSlots = new SemaphoreSlim(maximumWorkers, maximumWorkers);
            previous.Dispose();
            previousGitTreeReadSlots.Dispose();
            previousReadSlots.Dispose();
        }
    }

    internal static void ResetStatisticsForBenchmark()
    {
        lock (ConfigurationGate)
        {
            if (Volatile.Read(ref _active) != 0 ||
                Volatile.Read(ref _activeBufferedFileReads) != 0 ||
                HasActiveWork())
            {
                throw new InvalidOperationException(
                    "Benchmark statistics cannot be reset while repository analysis is active.");
            }

            Interlocked.Exchange(ref _acquisitions, 0);
            Interlocked.Exchange(ref _occupiedTimestamp, 0);
            Interlocked.Exchange(ref _waitTimestamp, 0);
            Volatile.Write(ref _maximumActive, 0);
            Array.Clear(WorkAcquisitions);
            Array.Clear(WorkOccupiedTimestamps);
            Array.Clear(WorkWaitTimestamps);
            Array.Clear(WorkFirstWaitTimestamps);
            Array.Clear(WorkLastCompletedTimestamps);
            Array.Clear(WorkMaximumOccupiedTimestamps);
            Array.Clear(WorkExternalProcessCpuTicks);
            Array.Clear(WorkOutputBytes);
            Array.Clear(WorkMaximumActive);
        }
    }

    private static bool HasActiveWork()
    {
        for (int index = 0; index < WorkActive.Length; index++)
        {
            if (Volatile.Read(ref WorkActive[index]) != 0)
            {
                return true;
            }
        }

        return false;
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
            Duration(Interlocked.Read(ref WorkWaitTimestamps[index])),
            Elapsed(index),
            Duration(Interlocked.Read(ref WorkMaximumOccupiedTimestamps[index])),
            TimeSpan.FromTicks(Interlocked.Read(ref WorkExternalProcessCpuTicks[index])),
            Interlocked.Read(ref WorkOutputBytes[index]),
            Volatile.Read(ref WorkMaximumActive[index]));
    }

    private static TimeSpan Elapsed(int index)
    {
        long first = Interlocked.Read(ref WorkFirstWaitTimestamps[index]);
        long last = Interlocked.Read(ref WorkLastCompletedTimestamps[index]);
        return first == 0 || last <= first ? TimeSpan.Zero : Duration(last - first);
    }

    private static void RecordWorkActive(int kindIndex)
    {
        int workActive = Interlocked.Increment(ref WorkActive[kindIndex]);
        int observedWorkMaximum;
        while (workActive >
                (observedWorkMaximum = Volatile.Read(ref WorkMaximumActive[kindIndex])) &&
            Interlocked.CompareExchange(
                ref WorkMaximumActive[kindIndex],
                workActive,
                observedWorkMaximum) != observedWorkMaximum)
        {
        }
    }

    private static void RecordWorkCompleted(int kindIndex, long occupied)
    {
        Interlocked.Add(ref WorkOccupiedTimestamps[kindIndex], occupied);
        long observedMaximum;
        while (occupied >
                (observedMaximum = Volatile.Read(
                    ref WorkMaximumOccupiedTimestamps[kindIndex])) &&
            Interlocked.CompareExchange(
                ref WorkMaximumOccupiedTimestamps[kindIndex],
                occupied,
                observedMaximum) != observedMaximum)
        {
        }

        Volatile.Write(
            ref WorkLastCompletedTimestamps[kindIndex],
            Stopwatch.GetTimestamp());
        Interlocked.Decrement(ref WorkActive[kindIndex]);
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
            RecordWorkCompleted(_kindIndex, occupied);
            Interlocked.Decrement(ref _active);
            CpuAnalysisSlots.Release();
        }
    }

    private sealed class GitTreeReadLease(SemaphoreSlim slots, long acquired) : IDisposable
    {
        private readonly SemaphoreSlim _slots = slots;
        private readonly long _acquired = acquired;
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            RecordWorkCompleted(
                (int)RepositoryAnalysisWorkKind.GitTreeRead,
                Stopwatch.GetTimestamp() - _acquired);
            _slots.Release();
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
