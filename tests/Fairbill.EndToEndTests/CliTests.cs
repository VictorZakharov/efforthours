using System.Diagnostics;
using System.Text.Json;

namespace Fairbill.EndToEndTests;

public sealed class CliTests
{
    [Fact]
    public async Task HelpPrintsUsageToStandardOutput()
    {
        ProcessResult result = await RunCliAsync("--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("fairbill estimate", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task EstimateProducesSchemaVersionedJsonWithoutDiagnosticsOnStandardError()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "evidence", "minimal.repository-evidence.json");

        ProcessResult result = await RunCliAsync(
            "estimate",
            fixture,
            "--profile",
            "implementation",
            "--format",
            "json");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal("1.0.0", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(14m, document.RootElement.GetProperty("totalEffort").GetProperty("expected").GetDecimal());
    }

    [Fact]
    public async Task EstimateCanRenderRecreationMarkdownWithCallerRate()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "evidence", "minimal.repository-evidence.json");

        ProcessResult result = await RunCliAsync(
            "estimate",
            fixture,
            "--profile",
            "recreation",
            "--format",
            "markdown",
            "--hourly-rate",
            "100");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("| Human-hours | 8.75 | 15.5 | 27.5 |", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("| Replacement cost (USD) | 875.00 | 1,550.00 | 2,750.00 |", result.StandardOutput, StringComparison.Ordinal);
    }

    private static async Task<ProcessResult> RunCliAsync(params string[] arguments)
    {
        string repositoryRoot = FindRepositoryRoot();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine the test build configuration.");
        string cliAssembly = Path.Combine(
            repositoryRoot,
            "src",
            "Fairbill.Cli",
            "bin",
            configuration,
            "net10.0",
            "fairbill.dll");

        Assert.True(File.Exists(cliAssembly), $"CLI assembly was not built: {cliAssembly}");

        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.ArgumentList.Add(cliAssembly);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the Fairbill CLI process.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);

        return new ProcessResult(
            process.ExitCode,
            (await stdout).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd(),
            (await stderr).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd());
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Fairbill.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the Fairbill repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
