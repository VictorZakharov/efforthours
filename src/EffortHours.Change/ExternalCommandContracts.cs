namespace EffortHours.Change;

public sealed class ExternalCommandException : InvalidOperationException
{
    public ExternalCommandException(string command, int? exitCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        Command = command;
        ExitCode = exitCode;
    }

    public string Command { get; }

    public int? ExitCode { get; }
}

internal readonly record struct ExternalCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public TimeSpan ProcessStartupElapsed { get; init; }
}

internal readonly record struct ExternalBinaryCommandResult(
    byte[] StandardOutput,
    TimeSpan ProcessCpuTime);

internal sealed class ExternalCommandOutputLimitException(string command, int maximumBytes)
    : InvalidOperationException(
        $"'{command}' produced more than the bounded {maximumBytes} output bytes.");

internal interface IExternalCommandRunner
{
    public Task<ExternalCommandResult> RunAsync(
        string executable,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool requireSuccess = true);

    public async Task<ExternalCommandResult> RunStreamingAsync(
        string executable,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        Func<TextReader, CancellationToken, Task> consumeStandardOutput,
        CancellationToken cancellationToken,
        bool requireSuccess = true)
    {
        ArgumentNullException.ThrowIfNull(consumeStandardOutput);
        ExternalCommandResult result = await RunAsync(
            executable,
            workingDirectory,
            arguments,
            cancellationToken,
            requireSuccess).ConfigureAwait(false);
        using StringReader reader = new(result.StandardOutput);
        await consumeStandardOutput(reader, cancellationToken).ConfigureAwait(false);
        return result with { StandardOutput = string.Empty };
    }
}
