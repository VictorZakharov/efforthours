namespace EffortHours.Change;

internal static class SnapshotAtomicReplacement
{
    internal const int MaximumAttempts = 8;

    public static Task ReplaceAsync(string temporary, string destination, CancellationToken cancellationToken) =>
        ExecuteAsync(() => File.Move(temporary, destination, overwrite: true), Task.Delay,
            OperatingSystem.IsWindows(), cancellationToken);

    internal static async Task ExecuteAsync(Action replace, Func<TimeSpan, CancellationToken, Task> wait,
        bool isWindows, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { replace(); return; }
            catch (Exception exception) when (isWindows && attempt < MaximumAttempts && IsSharingFailure(exception))
            {
                // MoveFileEx can report access denied as well as sharing/lock violations.
                // Retain the staged bytes and atomic rename; never delete/copy the destination.
                await wait(TimeSpan.FromMilliseconds(Math.Min(attempt * 50, 200)), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsSharingFailure(Exception exception) =>
        (exception is IOException or UnauthorizedAccessException) &&
        unchecked((uint)exception.HResult) is 0x80070005U or 0x80070020U or 0x80070021U;
}
