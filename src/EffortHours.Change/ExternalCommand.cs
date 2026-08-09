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
        CancellationToken cancellationToken)
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

        await using MemoryStream stdout = new();
        Task copy = process.StandardOutput.BaseStream.CopyToAsync(stdout, cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await Task.WhenAll(copy, process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
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

        return stdout.ToArray();
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
