using System.ComponentModel;
using System.Diagnostics;
using System.Text;

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

internal readonly record struct ExternalCommandResult(int ExitCode, string StandardOutput, string StandardError);

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
}

internal sealed class ExternalCommandRunner : IExternalCommandRunner
{
    public Task<ExternalCommandResult> RunAsync(
        string executable,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool requireSuccess = true) => ExternalCommand.RunAsync(
            executable,
            workingDirectory,
            arguments,
            cancellationToken,
            requireSuccess);
}

internal static class ExternalCommand
{
    public static async Task<ExternalCommandResult> RunAsync(
        string executable,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool requireSuccess = true)
    {
        ProcessStartInfo startInfo = CreateStartInfo(executable, workingDirectory, arguments);
        using Process process = new() { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new ExternalCommandException(
                    executable,
                    null,
                    $"Could not start required executable '{executable}'.");
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            throw new ExternalCommandException(
                executable,
                null,
                $"Could not start required executable '{executable}': {exception.Message}",
                exception);
        }

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        ExternalCommandResult result = new(
            process.ExitCode,
            await stdout.ConfigureAwait(false),
            await stderr.ConfigureAwait(false));
        if (requireSuccess && result.ExitCode != 0)
        {
            string detail = result.StandardError.Trim();
            throw new ExternalCommandException(
                executable,
                result.ExitCode,
                detail.Length == 0
                    ? $"'{executable}' exited with code {result.ExitCode}."
                    : $"'{executable}' failed: {detail}");
        }

        return result;
    }

    public static async Task<byte[]> RunBinaryAsync(
        string executable,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) => (await RunBinaryCoreAsync(
                executable,
                workingDirectory,
                arguments,
                standardInputLines: null,
                maximumOutputBytes: null,
                cancellationToken).ConfigureAwait(false)).StandardOutput;

    public static async Task<byte[]> RunBinaryAsync(
        string executable,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> standardInputLines,
        CancellationToken cancellationToken) => (await RunBinaryCoreAsync(
                executable,
                workingDirectory,
                arguments,
                (IReadOnlyList<string>?)standardInputLines,
                maximumOutputBytes: null,
                cancellationToken).ConfigureAwait(false)).StandardOutput;

    public static async Task<byte[]> RunBinaryAsync(
        string executable,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> standardInputLines,
        int maximumOutputBytes,
        CancellationToken cancellationToken) => (await RunBinaryCoreAsync(
                executable,
                workingDirectory,
                arguments,
                standardInputLines,
                maximumOutputBytes,
                cancellationToken).ConfigureAwait(false)).StandardOutput;

    public static Task<ExternalBinaryCommandResult> RunBinaryMeasuredAsync(
        string executable,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) => RunBinaryCoreAsync(
            executable,
            workingDirectory,
            arguments,
            standardInputLines: null,
            maximumOutputBytes: null,
            cancellationToken);

    private static async Task<ExternalBinaryCommandResult> RunBinaryCoreAsync(
        string executable,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string>? standardInputLines,
        int? maximumOutputBytes,
        CancellationToken cancellationToken)
    {
        if (maximumOutputBytes is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOutputBytes.Value);
        }

        ProcessStartInfo startInfo = CreateStartInfo(executable, workingDirectory, arguments);
        using Process process = new() { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new ExternalCommandException(
                    executable,
                    null,
                    $"Could not start required executable '{executable}'.");
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            throw new ExternalCommandException(
                executable,
                null,
                $"Could not start required executable '{executable}': {exception.Message}",
                exception);
        }

        await using MemoryStream stdout = new();
        Task copy = maximumOutputBytes is null
            ? process.StandardOutput.BaseStream.CopyToAsync(stdout, cancellationToken)
            : CopyBoundedAsync(
                process,
                executable,
                process.StandardOutput.BaseStream,
                stdout,
                maximumOutputBytes.Value,
                cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        Task input = standardInputLines is null
            ? Task.CompletedTask
            : WriteStandardInputAsync(process, standardInputLines, cancellationToken);
        try
        {
            await Task.WhenAll(input, copy, process.WaitForExitAsync(cancellationToken))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch
        {
            TryKill(process);
            throw;
        }

        string error = await stderr.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new ExternalCommandException(
                executable,
                process.ExitCode,
                error.Trim().Length == 0
                    ? $"'{executable}' exited with code {process.ExitCode}."
                    : $"'{executable}' failed: {error.Trim()}");
        }

        return new ExternalBinaryCommandResult(stdout.ToArray(), ReadProcessorTime(process));
    }

    private static TimeSpan ReadProcessorTime(Process process)
    {
        try
        {
            return process.TotalProcessorTime;
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            // Unix can discard process statistics as soon as the child is reaped.
            // CPU telemetry is observational and must never fail a successful command.
            return TimeSpan.Zero;
        }
    }

    private static async Task CopyBoundedAsync(
        Process process,
        string executable,
        Stream source,
        Stream destination,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            byte[] buffer = new byte[81_920];
            int total = 0;
            while (true)
            {
                int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return;
                }

                if (total > maximumBytes - read)
                {
                    throw new ExternalCommandOutputLimitException(executable, maximumBytes);
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
                total += read;
            }
        }
        catch
        {
            TryKill(process);
            throw;
        }
    }

    private static async Task WriteStandardInputAsync(
        Process process,
        IReadOnlyList<string> lines,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (string line in lines)
            {
                await process.StandardInput.WriteLineAsync(line.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
            }

            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            TryKill(process);
            throw;
        }
        finally
        {
            process.StandardInput.Dispose();
        }
    }

    public static ProcessStartInfo CreateStartInfo(
        string executable,
        string workingDirectory,
        IReadOnlyList<string> arguments)
    {
        ProcessStartInfo startInfo = new(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
        };
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
