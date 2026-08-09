using EffortHours.Cli;

namespace EffortHours.Tests;

public sealed class CliCancellationTests
{
    [Fact]
    public async Task PreCancelledCommandUsesDedicatedExitCodeAndStderrOnly()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        using StringWriter standardOutput = new();
        using StringWriter standardError = new();

        int exitCode = await new EffortHoursApplication().RunAsync(
            ["version"],
            standardOutput,
            standardError,
            cancellation.Token);

        Assert.Equal(CliExitCodes.Cancelled, exitCode);
        Assert.Equal(130, exitCode);
        Assert.Equal(string.Empty, standardOutput.ToString());
        Assert.Equal(
            "The operation was cancelled." + Environment.NewLine,
            standardError.ToString());
    }
}
