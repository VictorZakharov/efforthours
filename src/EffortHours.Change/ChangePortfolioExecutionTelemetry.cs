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

/// <summary>
/// Collects non-semantic wall-clock phase measurements for stderr diagnostics.
/// Timings never enter report contracts, estimator rules, or deterministic JSON.
/// </summary>
public sealed class ChangePortfolioExecutionTelemetry
{
    private readonly Dictionary<string, TimeSpan> _elapsed = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public IDisposable Measure(string phase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        return new PhaseMeasurement(this, phase, Stopwatch.GetTimestamp());
    }

    public IReadOnlyList<ChangePortfolioPhaseTiming> GetTimings()
    {
        lock (_gate)
        {
            return
            [
                .. ChangePortfolioExecutionPhases.Ordered.Select(phase =>
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
