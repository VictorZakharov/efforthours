using System.Diagnostics;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class ScriptingCliTests
{
    [Fact]
    public async Task ScanAndEstimateProduceStaticScriptingEvidenceWithoutSourceDisclosure()
    {
        using ScriptRepository repository = new();
        repository.WriteText(
            "bin/status.sh",
            "#!/usr/bin/env bash\nstatus() { curl https://example.invalid/status; }\nstatus \"$@\"\n");
        repository.WriteText(
            "src/Status.psm1",
            "function Get-Status { param($ApiToken); Invoke-RestMethod https://example.invalid/status }\n" +
            "$privateMarker = 'must-not-leak'\nExport-ModuleMember -Function Get-Status\n");

        ProcessResult scan = await RunCliAsync("scan", repository.RootPath);
        ProcessResult estimate = await RunCliAsync(
            "estimate",
            repository.RootPath,
            "--no-rate",
            "--compact");

        Assert.Equal(0, scan.ExitCode);
        Assert.Equal(0, estimate.ExitCode);
        Assert.Equal(string.Empty, scan.StandardError);
        Assert.Equal(string.Empty, estimate.StandardError);
        Assert.DoesNotContain("privateMarker", scan.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("must-not-leak", scan.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(repository.RootPath, scan.StandardOutput, StringComparison.OrdinalIgnoreCase);
        using JsonDocument scanDocument = JsonDocument.Parse(scan.StandardOutput);
        Assert.Contains(
            scanDocument.RootElement.GetProperty("repository").GetProperty("ecosystems").EnumerateArray(),
            ecosystem => ecosystem.GetString() == "shell");
        Assert.Contains(
            scanDocument.RootElement.GetProperty("repository").GetProperty("ecosystems").EnumerateArray(),
            ecosystem => ecosystem.GetString() == "powershell");
        Assert.Equal(2, scanDocument.RootElement.GetProperty("facts").EnumerateArray().Count(fact =>
            fact.GetProperty("kind").GetString() == "source-structure" &&
            fact.GetProperty("provenance").GetProperty("analyzer").GetString() ==
                "efforthours.scripting-analyzer"));

        using JsonDocument estimateDocument = JsonDocument.Parse(estimate.StandardOutput);
        Assert.Equal(
            "seed-rules/0.4.0",
            estimateDocument.RootElement.GetProperty("estimatorVersion").GetString());
        Assert.Contains(
            estimateDocument.RootElement.GetProperty("categories").EnumerateArray(),
            category => category.GetProperty("category").GetString() == "external-integrations-and-protocols");
    }

    [Fact]
    public async Task ChangeUsesScriptingNormalizationAndSemanticRoutingThroughTheCli()
    {
        using ScriptRepository before = new();
        using ScriptRepository after = new();
        before.WriteText("src/status.sh", "#!/bin/sh\nstatus() { printf '%s' idle; }\n");
        after.WriteText("src/status.sh", "#!/bin/sh\nstatus() { curl https://example.invalid/status; }\n");

        ProcessResult result = await RunCliAsync(
            "change",
            "--base-path",
            before.RootPath,
            "--head-path",
            after.RootPath,
            "--no-rate",
            "--compact");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.DoesNotContain(before.RootPath, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(after.RootPath, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal(
            "change-seed/0.13.0+seed-rules/0.4.0",
            document.RootElement.GetProperty("estimatorVersion").GetString());
        Assert.Contains(
            document.RootElement.GetProperty("categories").EnumerateArray(),
            category => category.GetProperty("category").GetString() == "external-integrations-and-protocols");
    }

    private static async Task<ProcessResult> RunCliAsync(params string[] arguments)
    {
        string repositoryRoot = FindRepositoryRoot();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine the test build configuration.");
        string cliAssembly = Path.Combine(
            repositoryRoot,
            "src",
            "EffortHours.Cli",
            "bin",
            configuration,
            "net10.0",
            "efforthours.dll");
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
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);

        using Process process = Process.Start(startInfo)!;
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
            if (File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class ScriptRepository : IDisposable
    {
        public ScriptRepository()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "efforthours-scripting-e2e",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void WriteText(string path, string content)
        {
            string fullPath = Path.Combine(RootPath, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        public void Dispose()
        {
            string resolved = Path.GetFullPath(RootPath);
            string expectedParent = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "efforthours-scripting-e2e"));
            if (resolved.StartsWith(expectedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(resolved, recursive: true);
        }
    }
}
