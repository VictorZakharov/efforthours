using System.Diagnostics;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class JavaCliTests
{
    [Fact]
    public async Task ScanAndEstimateProduceStaticJavaEvidenceWithoutSourceDisclosure()
    {
        using JavaRepository repository = new();
        repository.WriteText(
            "pom.xml",
            "<project><groupId>com.example</groupId><artifactId>orders</artifactId>" +
            "<dependencies><dependency><groupId>org.springframework.boot</groupId>" +
            "<artifactId>spring-boot-starter-web</artifactId></dependency></dependencies></project>");
        repository.WriteText(
            "src/main/java/com/example/Orders.java",
            """
            package com.example;
            import org.springframework.web.bind.annotation.GetMapping;
            import org.springframework.web.bind.annotation.RestController;
            @RestController public final class Orders {
                @GetMapping("/orders") public String list() {
                    String privateJavaMarker = "must-not-leak";
                    return privateJavaMarker;
                }
            }
            """);

        ProcessResult scan = await RunCliAsync("scan", repository.RootPath);
        ProcessResult estimate = await RunCliAsync("estimate", repository.RootPath, "--no-rate", "--compact");

        Assert.Equal(0, scan.ExitCode);
        Assert.Equal(0, estimate.ExitCode);
        Assert.Equal(string.Empty, scan.StandardError);
        Assert.Equal(string.Empty, estimate.StandardError);
        Assert.DoesNotContain("privateJavaMarker", scan.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("must-not-leak", scan.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(repository.RootPath, scan.StandardOutput, StringComparison.OrdinalIgnoreCase);
        using JsonDocument scanDocument = JsonDocument.Parse(scan.StandardOutput);
        Assert.Contains(
            scanDocument.RootElement.GetProperty("repository").GetProperty("ecosystems").EnumerateArray(),
            ecosystem => ecosystem.GetString() == "java");
        JsonElement structure = Assert.Single(
            scanDocument.RootElement.GetProperty("facts").EnumerateArray(),
            fact => fact.GetProperty("kind").GetString() == "source-structure" &&
                fact.GetProperty("provenance").GetProperty("analyzer").GetString() ==
                "efforthours.java-analyzer");
        Assert.Contains(structure.GetProperty("tags").EnumerateArray(),
            tag => tag.GetString() == "syntax:token-backed");
        Assert.Contains(scanDocument.RootElement.GetProperty("facts").EnumerateArray(),
            fact => fact.GetProperty("kind").GetString() == "api-surface");

        using JsonDocument estimateDocument = JsonDocument.Parse(estimate.StandardOutput);
        Assert.Equal("seed-rules/0.4.0",
            estimateDocument.RootElement.GetProperty("estimatorVersion").GetString());
        Assert.Contains(estimateDocument.RootElement.GetProperty("categories").EnumerateArray(),
            category => category.GetProperty("category").GetString() == "production-implementation");
    }

    [Fact]
    public async Task ChangeUsesJavaNormalizationAndSemanticRoutingThroughTheCli()
    {
        using JavaRepository before = new();
        using JavaRepository after = new();
        before.WriteText("pom.xml", "<project><artifactId>change</artifactId></project>");
        after.WriteText("pom.xml", "<project><artifactId>change</artifactId></project>");
        before.WriteText("Server.java", "import org.springframework.web.bind.annotation.GetMapping; class Server { void ready() { } }");
        after.WriteText("Server.java", "import org.springframework.web.bind.annotation.GetMapping; class Server { @GetMapping(\"/ready\") void ready() { } }");

        ProcessResult result = await RunCliAsync(
            "change", "--base-path", before.RootPath, "--head-path", after.RootPath,
            "--no-rate", "--compact");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.DoesNotContain(before.RootPath, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(after.RootPath, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal("change-seed/0.15.0+seed-rules/0.4.0",
            document.RootElement.GetProperty("estimatorVersion").GetString());
        Assert.True(document.RootElement.GetProperty("totalEffort").GetProperty("expected").GetDecimal() > 0m);
        Assert.Contains(document.RootElement.GetProperty("categories").EnumerateArray(),
            category => category.GetProperty("category").GetString() == "production-implementation");
    }

    private static async Task<ProcessResult> RunCliAsync(params string[] arguments)
    {
        string repositoryRoot = FindRepositoryRoot();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine the test build configuration.");
        string cliAssembly = Path.Combine(repositoryRoot, "src", "EffortHours.Cli", "bin",
            configuration, "net10.0", "efforthours.dll");
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

    private sealed class JavaRepository : IDisposable
    {
        public JavaRepository()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "efforthours-java-e2e", Guid.NewGuid().ToString("N"));
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
