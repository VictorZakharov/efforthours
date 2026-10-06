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
        Exception? failure = null;
        try
        {
            string pidPath = Path.Combine(root, "pid.txt");
            bool windows = OperatingSystem.IsWindows();
            string script = Path.Combine(root, windows ? "provider.ps1" : "provider.sh");
            await File.WriteAllTextAsync(script, windows
                ? "param([string]$OutPath)\n[IO.File]::WriteAllText($OutPath + \".tmp\", [string]$PID)\n[IO.File]::Move($OutPath + \".tmp\", $OutPath)\nStart-Sleep -Seconds 300\n"
                : "printf '%s' $$ > \"$1.tmp\"\nmv \"$1.tmp\" \"$1\"\nexec sleep 300\n");
            running = new ExternalCommandRunner().RunAsync(windows ? "powershell.exe" : "sh", root,
                windows ? ["-NoProfile", "-NonInteractive", "-File", script, pidPath] : [script, pidPath], cancellation.Token);
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(60);
            while (!File.Exists(pidPath) && !running.IsCompleted && DateTimeOffset.UtcNow < deadline)
                await Task.Delay(20);
            if (running.IsCompleted) await running; // Preserve an early provider/startup failure.
            Assert.True(File.Exists(pidPath), "Synthetic provider did not reach its ready signal.");
            int pid = int.Parse(await File.ReadAllTextAsync(pidPath), CultureInfo.InvariantCulture);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
            try { using Process process = Process.GetProcessById(pid); Assert.True(process.HasExited); }
            catch (ArgumentException) { } // The exited process has already been reaped.
        }
        catch (Exception exception) { failure = exception; throw; }
        finally
        {
            await cancellation.CancelAsync();
            if (running is not null)
            {
                try { await running; }
                catch (OperationCanceledException) { }
                catch (Exception) when (failure is not null) { } // Keep the primary assertion/startup failure.
            }
            try { await DeleteFixtureAsync(root); }
            catch (IOException) when (failure is not null) { } // Cleanup must not hide the test failure.
        }
    }

    private static async Task DeleteFixtureAsync(string root)
    {
        // The provider must already have exited; this only tolerates delayed Windows file release.
        for (int attempt = 0; ; attempt++)
        {
            if (!Directory.Exists(root)) return;
            try { Directory.Delete(root, recursive: true); return; }
            catch (IOException) when (OperatingSystem.IsWindows() && attempt < 19)
            { await Task.Delay(100); }
        }
    }
}
