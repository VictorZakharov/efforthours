using System.Text.Json;
using EffortHours.Contracts;

namespace EffortHours.EndToEndTests;

public sealed partial class CliTests
{
    [Theory]
    [InlineData("examples")]
    [InlineData("--examples")]
    public async Task ExamplesAreAvailableThroughInstalledCliEntryPoint(string command)
    {
        ProcessResult result = await RunCliAsync(command, "all");
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardError);
        Assert.Contains("eh change today", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("eh agent codex --install", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("experimental and uncalibrated", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HelpVersionMatchesVersionCommandThroughProcessBoundary()
    {
        ProcessResult version = await RunCliAsync("version");
        ProcessResult help = await RunCliAsync("--help");
        Assert.Equal(0, version.ExitCode);
        Assert.Equal(0, help.ExitCode);
        Assert.StartsWith($"EffortHours {version.StandardOutput}\n", help.StandardOutput);
        Assert.Contains("eh examples", help.StandardOutput, StringComparison.Ordinal);
        Assert.Empty(help.StandardError);
    }

    [Fact]
    public async Task PrintedCompactEstimateRecipeExecutesAndProducesValidReport()
    {
        ProcessResult examples = await RunCliAsync("examples", "repository");
        string command = examples.StandardOutput.Split('\n').Single(line =>
            line.StartsWith("  eh estimate .", StringComparison.Ordinal) && line.Contains("--compact", StringComparison.Ordinal));
        string[] args = command.Trim().Split(' ')[1..];
        using TemporaryRepository target = new();
        using TemporaryRepository output = new();
        target.WriteText("main.py", "def add(a, b):\n    return a + b\n");
        args[1] = target.RootPath;
        args[^1] = Path.Combine(output.RootPath, "estimate.json");
        ProcessResult result = await RunCliAsync(args);
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Empty(result.StandardError);
        string json = await File.ReadAllTextAsync(args[^1]);
        Assert.True(ContractSchemaValidator.Validate("estimate-report.schema.json", json).IsValid);
        using JsonDocument report = JsonDocument.Parse(json);
        Assert.Equal("1.0.0", report.RootElement.GetProperty("schemaVersion").GetString());
        Assert.True(report.RootElement.GetProperty("totalEffort").GetProperty("expected").GetDecimal() > 0);
        Assert.False(report.RootElement.TryGetProperty("rateCard", out _));
    }
}
