using System.Diagnostics;
using System.Globalization;
using EffortHours.Change;

namespace EffortHours.EndToEndTests;

public sealed class ExternalProviderCancellationTests
{
    [Fact]
    public async Task CancelledTextProviderProcessExitsAndDrainsBeforeReturning()
    {
        string root = Path.Combine(Path.GetTempPath(), "efforthours-provider-cancel", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using CancellationTokenSource cancellation = new();
        Task<ExternalCommandResult>? running = null;
        try
        {
            string pidPath = Path.Combine(root, "pid.txt");
            bool windows = OperatingSystem.IsWindows();
            string script = Path.Combine(root, windows ? "provider.ps1" : "provider.sh");
            await File.WriteAllTextAsync(script, windows
                ? "param([string]$OutPath)\n[IO.File]::WriteAllText($OutPath, [string]$PID)\nStart-Sleep -Seconds 60\n"
                : "printf '%s' $$ > \"$1\"\nexec sleep 60\n");
            running = new ExternalCommandRunner().RunAsync(windows ? "powershell.exe" : "sh", root,
                windows ? ["-NoProfile", "-NonInteractive", "-File", script, pidPath] : [script, pidPath], cancellation.Token);
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(20);
            while ((!File.Exists(pidPath) || new FileInfo(pidPath).Length == 0) && !running.IsCompleted && DateTimeOffset.UtcNow < deadline)
                await Task.Delay(20);
            Assert.True(File.Exists(pidPath), "Synthetic provider did not reach its ready signal.");
            int pid = int.Parse(await File.ReadAllTextAsync(pidPath), CultureInfo.InvariantCulture);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
            try { using Process process = Process.GetProcessById(pid); Assert.True(process.HasExited); }
            catch (ArgumentException) { } // The exited process has already been reaped.
        }
        finally
        {
            await cancellation.CancelAsync();
            if (running is not null)
            {
                try { await running; }
                catch (OperationCanceledException) { }
            }
            Directory.Delete(root, recursive: true);
        }
    }
}
