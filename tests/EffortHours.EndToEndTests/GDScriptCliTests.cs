using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed class GDScriptCliTests
{
    [Fact]
    public async Task ScanEstimateAndDirectoryChangeAreDeterministicReadOnlyAndSchemaValid()
    {
        using GDScriptRepository before = new();
        using GDScriptRepository after = new();
        const string source = "extends Node\nfunc value():\n    var private_gdscript_marker = \"must-not-leak\"\n    return 1\n";
        before.WriteText("project.godot", "config_version=5\n");
        before.WriteText("app.gd", source);
        after.WriteText("project.godot", "config_version=5\n");
        after.WriteText("app.gd", source.Replace("    ", "  ", StringComparison.Ordinal));
        Dictionary<string, string> original = Fingerprint(before.RootPath);
        ProcessResult scan = await RunCliAsync("scan", before.RootPath);
        ProcessResult repeated = await RunCliAsync("scan", before.RootPath);
        ProcessResult estimate = await RunCliAsync("estimate", before.RootPath, "--no-rate");
        ProcessResult change = await RunCliAsync("change", "--base-path", before.RootPath,
            "--head-path", after.RootPath, "--no-rate");
        Assert.Equal(scan.StandardOutput, repeated.StandardOutput);
        foreach (ProcessResult result in new[] { scan, estimate, change })
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardError);
            Assert.DoesNotContain("private_gdscript_marker", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("must-not-leak", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(before.RootPath, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        }
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.RepositoryEvidence, scan.StandardOutput).IsValid);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.EstimateReport, estimate.StandardOutput).IsValid);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeEstimateReport, change.StandardOutput).IsValid);
        using JsonDocument scanned = JsonDocument.Parse(scan.StandardOutput);
        Assert.Contains(scanned.RootElement.GetProperty("facts").EnumerateArray(), fact =>
            fact.GetProperty("kind").GetString() == "source-structure" &&
            fact.GetProperty("provenance").GetProperty("analyzer").GetString() == "efforthours.gdscript-analyzer");
        using JsonDocument changed = JsonDocument.Parse(change.StandardOutput);
        Assert.Equal(0m, changed.RootElement.GetProperty("totalEffort").GetProperty("expected").GetDecimal());
        Assert.Equal(original.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            Fingerprint(before.RootPath).OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    private static Dictionary<string, string> Fingerprint(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(root, path),
            path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);

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

        throw new InvalidOperationException("Could not locate the EffortHours repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class GDScriptRepository : IDisposable
    {
        public GDScriptRepository()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "efforthours-gdscript-e2e",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void WriteText(string relativePath, string content)
        {
            string path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose() => Directory.Delete(RootPath, recursive: true);
    }
}
