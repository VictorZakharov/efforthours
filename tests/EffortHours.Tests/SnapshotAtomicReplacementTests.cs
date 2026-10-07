using EffortHours.Change;

namespace EffortHours.Tests;

public sealed class SnapshotAtomicReplacementTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(32)]
    [InlineData(33)]
    public async Task WindowsSharingFailuresRetryOnlyTheRenameWithBoundedWaits(int nativeError)
    {
        int attempts = 0;
        List<TimeSpan> waits = [];
        await SnapshotAtomicReplacement.ExecuteAsync(() =>
        {
            if (++attempts < 3) throw Failure(nativeError);
        }, (delay, token) => { waits.Add(delay); return Task.CompletedTask; }, true, CancellationToken.None);
        Assert.Equal(3, attempts);
        Assert.Equal([TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100)], waits);
    }

    [Theory]
    [InlineData(112, true)]
    [InlineData(3, true)]
    [InlineData(32, false)]
    [InlineData(5, false)]
    public async Task UnrelatedFailuresAndNonWindowsErrorsFailWithoutRetry(int nativeError, bool windows)
    {
        IOException failure = Failure(nativeError);
        int attempts = 0, waits = 0;
        var actual = await Assert.ThrowsAsync<IOException>(() => SnapshotAtomicReplacement.ExecuteAsync(() =>
        { attempts++; throw failure; }, (_, _) => { waits++; return Task.CompletedTask; }, windows, CancellationToken.None));
        Assert.Same(failure, actual);
        Assert.Equal(1, attempts);
        Assert.Equal(0, waits);
    }

    [Fact]
    public async Task PermanentAccessDenialPropagatesAfterEightAttempts()
    {
        UnauthorizedAccessException failure = new("Access remains denied.");
        int attempts = 0;
        List<TimeSpan> waits = [];
        var actual = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SnapshotAtomicReplacement.ExecuteAsync(() =>
        { attempts++; throw failure; }, (delay, _) => { waits.Add(delay); return Task.CompletedTask; }, true, CancellationToken.None));
        Assert.Same(failure, actual);
        Assert.Equal(SnapshotAtomicReplacement.MaximumAttempts, attempts);
        Assert.Equal(7, waits.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(1100), TimeSpan.FromTicks(waits.Sum(value => value.Ticks)));
        Assert.All(waits, value => Assert.InRange(value, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public async Task CancellationDuringRetryWaitStopsBeforeAnotherRename()
    {
        using CancellationTokenSource cancelled = new();
        int attempts = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SnapshotAtomicReplacement.ExecuteAsync(() =>
        { attempts++; throw new UnauthorizedAccessException(); }, (_, token) =>
        {
            Assert.Equal(cancelled.Token, token);
            cancelled.Cancel();
            return Task.FromCanceled(token);
        }, true, cancelled.Token));
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task CancellationBeforeReplacementMakesNoAttempt()
    {
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        int attempts = 0, waits = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SnapshotAtomicReplacement.ExecuteAsync(() =>
        { attempts++; }, (_, _) => { waits++; return Task.CompletedTask; }, true, cancelled.Token));
        Assert.Equal(0, attempts);
        Assert.Equal(0, waits);
    }

    private static IOException Failure(int code) => new("Native file operation failed.", unchecked((int)(0x80070000U | (uint)code)));
}
