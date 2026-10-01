using System.Diagnostics;

namespace EffortHours.Cli;

internal sealed class SnapshotPortfolioResourceBudget : IAsyncDisposable
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly Timer _timer;
    private readonly CancellationTokenSource _cancellation;
    private readonly long _maximumBytes;
    private int _exceeded;

    public SnapshotPortfolioResourceBudget(CancellationTokenSource cancellation, int memoryMiB)
    {
        _cancellation = cancellation;
        _maximumBytes = memoryMiB * 1024L * 1024L;
        _timer = new Timer(Check, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(200));
    }

    public bool Exceeded => Volatile.Read(ref _exceeded) != 0;

    private void Check(object? state)
    {
        _ = state;
        _process.Refresh();
        if (_process.WorkingSet64 <= _maximumBytes) return;
        Interlocked.Exchange(ref _exceeded, 1);
        _cancellation.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        await _timer.DisposeAsync().ConfigureAwait(false);
        _process.Dispose();
    }
}
