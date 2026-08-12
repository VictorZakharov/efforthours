using System.Diagnostics;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class TerraformCliTests
{
    [Fact]
    public async Task ScanAndEstimateProduceStaticTerraformEvidenceWithoutSourceDisclosure()
    {
        using TerraformRepository repository = new();
        repository.WriteText(
            "main.tf",
            """
            provider "aws" { region = "ca-central-1" }
            resource "aws_s3_bucket" "assets" {
              bucket = "synthetic-private-bucket-marker"
            }
            """);

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
        Assert.DoesNotContain("synthetic-private-bucket-marker", scan.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(repository.RootPath, scan.StandardOutput, StringComparison.OrdinalIgnoreCase);
        using JsonDocument scanDocument = JsonDocument.Parse(scan.StandardOutput);
        Assert.Contains(
            scanDocument.RootElement.GetProperty("repository").GetProperty("ecosystems").EnumerateArray(),
            ecosystem => ecosystem.GetString() == "terraform");
        JsonElement infrastructure = Assert.Single(
            scanDocument.RootElement.GetProperty("facts").EnumerateArray(),
            fact => fact.GetProperty("kind").GetString() == "infrastructure" &&
                fact.GetProperty("provenance").GetProperty("analyzer").GetString() ==
                    "efforthours.terraform-analyzer");
        Assert.Contains(
            infrastructure.GetProperty("measurements").EnumerateArray(),
            measurement => measurement.GetProperty("name").GetString() == "resources" &&
                measurement.GetProperty("value").GetDecimal() == 1m);

        using JsonDocument estimateDocument = JsonDocument.Parse(estimate.StandardOutput);
        Assert.Equal(
            "seed-rules/0.4.0",
            estimateDocument.RootElement.GetProperty("estimatorVersion").GetString());
        Assert.Contains(
            estimateDocument.RootElement.GetProperty("categories").EnumerateArray(),
            category => category.GetProperty("category").GetString() ==
                "ci-cd-and-infrastructure-as-code");
    }

    [Fact]
    public async Task ChangeUsesHclNormalizationAndSemanticRoutingThroughTheCli()
    {
        using TerraformRepository before = new();
        using TerraformRepository after = new();
        before.WriteText("main.tf", "resource \"aws_s3_bucket\" \"assets\" {\n  bucket = \"before\"\n}\n");
        after.WriteText("main.tf", "resource \"aws_s3_bucket\" \"assets\" {\n  bucket = \"after\"\n}\n");

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
            category => category.GetProperty("category").GetString() ==
                "ci-cd-and-infrastructure-as-code");
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

    private sealed class TerraformRepository : IDisposable
    {
        public TerraformRepository()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "efforthours-terraform-e2e",
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
                "efforthours-terraform-e2e"));
            if (resolved.StartsWith(expectedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(resolved, recursive: true);
        }
    }
}
