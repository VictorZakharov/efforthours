using System.Diagnostics;

namespace EffortHours.ScannerBenchmarks;

internal sealed class WorkingSetSampler : IAsyncDisposable
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _sampling;
    private long _peakBytes;
    private bool _stopped;

    private WorkingSetSampler()
    {
        Sample();
        _sampling = SampleAsync();
    }

    public static WorkingSetSampler Start() => new();

    public async Task<long> StopAsync()
    {
        if (!_stopped)
        {
            _stopped = true;
            await _cancellation.CancelAsync().ConfigureAwait(false);
            try
            {
                await _sampling.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            Sample();
        }

        return Interlocked.Read(ref _peakBytes);
    }

    public async ValueTask DisposeAsync()
    {
        _ = await StopAsync().ConfigureAwait(false);
        _cancellation.Dispose();
        _process.Dispose();
    }

    private async Task SampleAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), _cancellation.Token)
                .ConfigureAwait(false);
            Sample();
        }
    }

    private void Sample()
    {
        _process.Refresh();
        long sample = _process.WorkingSet64;
        long current = Interlocked.Read(ref _peakBytes);
        while (sample > current)
        {
            long observed = Interlocked.CompareExchange(ref _peakBytes, sample, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }
}
