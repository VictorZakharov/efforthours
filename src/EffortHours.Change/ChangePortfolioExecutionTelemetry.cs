using System.Diagnostics;

namespace EffortHours.Change;

public static class ChangePortfolioExecutionPhases
{
    public const string ManifestValidation = "manifest-validation";
    public const string HeadValidation = "head-validation";
    public const string HistoryUnion = "history-union";
    public const string Selection = "selection";
    public const string SnapshotAndDiffConstruction = "snapshot-diff-construction";
    public const string StaticAnalysis = "static-analysis";
    public const string Reconciliation = "reconciliation";
    public const string Allocation = "allocation";
    public const string Rendering = "rendering";

    internal static IReadOnlyList<string> Ordered { get; } =
    [
        ManifestValidation,
        HeadValidation,
        HistoryUnion,
        Selection,
        SnapshotAndDiffConstruction,
        StaticAnalysis,
        Reconciliation,
        Allocation,
        Rendering,
    ];
}

public sealed record ChangePortfolioPhaseTiming
{
    public required string Phase { get; init; }

    public required TimeSpan Elapsed { get; init; }
}

public sealed record ChangePortfolioProgress
{
    public required DateTimeOffset ObservedAt { get; init; }

    public required string Phase { get; init; }

    public int ProcessedUnits { get; init; }

    public int TotalUnits { get; init; }

    public int AnalysisCacheRequests { get; init; }

    public int AnalysisCacheHits { get; init; }

    public required TimeSpan Elapsed { get; init; }

    public long WorkingSetBytes { get; init; }

    public long PeakWorkingSetBytes { get; init; }
}

/// <summary>
/// Collects non-semantic wall-clock phase measurements for stderr diagnostics.
/// Comparison reports may project them as operational observations, but they never
/// enter estimator rules or semantic digests.
/// </summary>
public sealed class ChangePortfolioExecutionTelemetry
{
    private readonly Dictionary<string, TimeSpan> _elapsed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _firstStartedAt = new(StringComparer.Ordinal);
    private readonly HashSet<string> _started = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private readonly Lock _progressNotificationGate = new();
    private readonly Action<string>? _phaseStarted;
    private readonly Action<ChangePortfolioProgress>? _progressReported;
    private string? _lastPhase;
    private long _lastPhaseStarted;
    private DateTimeOffset _lastPhaseObservedAt;
    private long _peakWorkingSetBytes;
    private ChangePortfolioProgress? _lastProgress;
    private string? _lastNotifiedProgressPhase;
    private int _lastNotifiedProcessedUnits;

    public ChangePortfolioExecutionTelemetry(
        Action<string>? phaseStarted = null,
        Action<ChangePortfolioProgress>? progressReported = null)
    {
        _phaseStarted = phaseStarted;
        _progressReported = progressReported;
    }

    public IDisposable Measure(string phase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        Start(phase);
        return new PhaseMeasurement(this, phase, Stopwatch.GetTimestamp());
    }

    public void Start(string phase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        bool notify;
        long now = Stopwatch.GetTimestamp();
        DateTimeOffset observedAt = DateTimeOffset.UtcNow;
        long workingSet = Environment.WorkingSet;
        lock (_gate)
        {
            notify = _started.Add(phase);
            if (notify)
            {
                _firstStartedAt.Add(phase, now);
            }

            _lastPhase = phase;
            _lastPhaseStarted = now;
            _lastPhaseObservedAt = observedAt;
            _peakWorkingSetBytes = Math.Max(_peakWorkingSetBytes, workingSet);
        }

        if (notify)
        {
            _phaseStarted?.Invoke(phase);
        }
    }

    public void ReportProgress(
        string phase,
        int processedUnits,
        int totalUnits,
        int analysisCacheRequests,
        int analysisCacheHits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        if (processedUnits < 0 || totalUnits < 1 || processedUnits > totalUnits ||
            analysisCacheRequests < 0 || analysisCacheHits < 0 ||
            analysisCacheHits > analysisCacheRequests)
        {
            throw new ArgumentOutOfRangeException(nameof(processedUnits));
        }

        long workingSet = Environment.WorkingSet;
        DateTimeOffset observedAt = DateTimeOffset.UtcNow;
        ChangePortfolioProgress progress;
        lock (_gate)
        {
            if (_lastProgress is not null &&
                string.Equals(_lastProgress.Phase, phase, StringComparison.Ordinal) &&
                processedUnits < _lastProgress.ProcessedUnits)
            {
                return;
            }

            _peakWorkingSetBytes = Math.Max(_peakWorkingSetBytes, workingSet);
            long started = _firstStartedAt.GetValueOrDefault(phase, Stopwatch.GetTimestamp());
            progress = new ChangePortfolioProgress
            {
                ObservedAt = observedAt,
                Phase = phase,
                ProcessedUnits = processedUnits,
                TotalUnits = totalUnits,
                AnalysisCacheRequests = analysisCacheRequests,
                AnalysisCacheHits = analysisCacheHits,
                Elapsed = Stopwatch.GetElapsedTime(started),
                WorkingSetBytes = workingSet,
                PeakWorkingSetBytes = _peakWorkingSetBytes,
            };
            _lastProgress = progress;
            _lastPhase = phase;
            _lastPhaseStarted = started;
            _lastPhaseObservedAt = observedAt;
        }

        lock (_progressNotificationGate)
        {
            if (string.Equals(_lastNotifiedProgressPhase, phase, StringComparison.Ordinal) &&
                processedUnits <= _lastNotifiedProcessedUnits)
            {
                return;
            }

            _lastNotifiedProgressPhase = phase;
            _lastNotifiedProcessedUnits = processedUnits;
            _progressReported?.Invoke(progress);
        }
    }

    public ChangePortfolioProgress? GetLastProgress()
    {
        lock (_gate)
        {
            if (_lastPhase is null)
            {
                return null;
            }

            long workingSet = Environment.WorkingSet;
            _peakWorkingSetBytes = Math.Max(_peakWorkingSetBytes, workingSet);
            ChangePortfolioProgress? progress =
                string.Equals(_lastProgress?.Phase, _lastPhase, StringComparison.Ordinal)
                    ? _lastProgress
                    : null;
            long phaseStarted = _firstStartedAt.GetValueOrDefault(
                _lastPhase,
                _lastPhaseStarted);
            return new ChangePortfolioProgress
            {
                ObservedAt = progress?.ObservedAt ?? _lastPhaseObservedAt,
                Phase = _lastPhase,
                ProcessedUnits = progress?.ProcessedUnits ?? 0,
                TotalUnits = progress?.TotalUnits ?? 0,
                AnalysisCacheRequests = progress?.AnalysisCacheRequests ?? 0,
                AnalysisCacheHits = progress?.AnalysisCacheHits ?? 0,
                Elapsed = Stopwatch.GetElapsedTime(phaseStarted),
                WorkingSetBytes = workingSet,
                PeakWorkingSetBytes = _peakWorkingSetBytes,
            };
        }
    }

    public IReadOnlyList<ChangePortfolioPhaseTiming> GetTimings()
    {
        lock (_gate)
        {
            return
            [
                .. ChangePortfolioExecutionPhases.Ordered
                    .Where(_started.Contains)
                    .Select(phase =>
                    new ChangePortfolioPhaseTiming
                    {
                        Phase = phase,
                        Elapsed = _elapsed.GetValueOrDefault(phase),
                    }),
            ];
        }
    }

    internal void Add(string phase, TimeSpan elapsed)
    {
        Start(phase);
        lock (_gate)
        {
            _elapsed[phase] = _elapsed.GetValueOrDefault(phase) + elapsed;
        }
    }

    private sealed class PhaseMeasurement(
        ChangePortfolioExecutionTelemetry owner,
        string phase,
        long started) : IDisposable
    {
        private ChangePortfolioExecutionTelemetry? _owner = owner;

        public void Dispose()
        {
            ChangePortfolioExecutionTelemetry? current = Interlocked.Exchange(ref _owner, null);
            current?.Add(phase, Stopwatch.GetElapsedTime(started));
        }
    }
}
