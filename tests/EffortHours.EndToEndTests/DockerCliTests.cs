using System.Diagnostics;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class DockerCliTests
{
    [Fact]
    public async Task ScanAndEstimateProduceStaticDockerEvidenceWithoutValueDisclosure()
    {
        using DockerRepository repository = new();
        repository.WriteText(
            "containers/api.Dockerfile",
            "FROM private-registry.example/synthetic-private-image AS build\n" +
            "RUN --mount=type=secret,id=synthetic-private-secret echo build\n" +
            "FROM scratch\nCOPY --from=build /app /app\nENTRYPOINT [\"/app\"]\n");
        repository.WriteText(
            "deploy/compose.yml",
            "services:\n  api:\n    build:\n      context: ../containers\n      dockerfile: api.Dockerfile\n" +
            "    environment:\n      PRIVATE_VALUE: synthetic-private-environment\n    ports:\n      - 8080:8080\n");
        repository.WriteText(".dockerignore", "synthetic-private-ignore-pattern/\n");

        ProcessResult scan = await RunCliAsync("scan", repository.RootPath);
        ProcessResult estimate = await RunCliAsync("estimate", repository.RootPath, "--no-rate", "--compact");

        Assert.Equal(0, scan.ExitCode);
        Assert.Equal(0, estimate.ExitCode);
        Assert.Equal(string.Empty, scan.StandardError);
        Assert.Equal(string.Empty, estimate.StandardError);
        Assert.DoesNotContain("synthetic-private", scan.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(repository.RootPath, scan.StandardOutput, StringComparison.OrdinalIgnoreCase);
        using JsonDocument scanDocument = JsonDocument.Parse(scan.StandardOutput);
        Assert.Contains(
            scanDocument.RootElement.GetProperty("repository").GetProperty("ecosystems").EnumerateArray(),
            ecosystem => ecosystem.GetString() == "docker");
        Assert.Contains(
            scanDocument.RootElement.GetProperty("facts").EnumerateArray(),
            fact => fact.GetProperty("kind").GetString() == "container-configuration" &&
                fact.GetProperty("provenance").GetProperty("analyzer").GetString() ==
                    "efforthours.docker-analyzer");
        Assert.Contains(
            scanDocument.RootElement.GetProperty("facts").EnumerateArray(),
            fact => fact.GetProperty("kind").GetString() == "project-reference" &&
                fact.GetProperty("provenance").GetProperty("analyzer").GetString() ==
                    "efforthours.docker-analyzer");

        using JsonDocument estimateDocument = JsonDocument.Parse(estimate.StandardOutput);
        Assert.Equal("seed-rules/0.4.0", estimateDocument.RootElement.GetProperty("estimatorVersion").GetString());
        Assert.Contains(
            estimateDocument.RootElement.GetProperty("categories").EnumerateArray(),
            category => category.GetProperty("category").GetString() ==
                "packaging-deployment-and-release-artifacts");
    }

    [Fact]
    public async Task ChangeUsesDockerNormalizationAndPackagingRoutingThroughTheCli()
    {
        using DockerRepository before = new();
        using DockerRepository after = new();
        before.WriteText("compose.yml", "services:\n  app:\n    image: example/app:1\n");
        after.WriteText(
            "compose.yml",
            "services:\n  app:\n    image: example/app:2\n    ports:\n      - 8080:8080\n" +
            "    healthcheck:\n      test: [CMD, health]\n");

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
            "change-seed/0.18.1+seed-rules/0.4.0",
            document.RootElement.GetProperty("estimatorVersion").GetString());
        Assert.Contains(
            document.RootElement.GetProperty("categories").EnumerateArray(),
            category => category.GetProperty("category").GetString() ==
                "packaging-deployment-and-release-artifacts");
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

    private sealed class DockerRepository : IDisposable
    {
        public DockerRepository()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "efforthours-docker-e2e", Guid.NewGuid().ToString("N"));
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
            string expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "efforthours-docker-e2e"));
            if (resolved.StartsWith(expectedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(resolved, recursive: true);
        }
    }
}
