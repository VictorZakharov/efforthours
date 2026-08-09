using System.Diagnostics;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class CoverageCliTests
{
    [Fact]
    public async Task ScanAndEstimateUseMeasuredCoverageWithoutLeakingReportedSourcePath()
    {
        using CoverageRepository repository = new();
        repository.WriteText(
            "package.json",
            """
            {
              "name": "coverage-cli-fixture",
              "private": true,
              "type": "module",
              "jest": { "coverageThreshold": { "global": { "lines": 100 } } }
            }
            """);
        repository.WriteText(
            "src/math.js",
            "export function add(a, b) { return a + b; }\n");
        repository.WriteText(
            "test/math.test.js",
            "import { add } from '../src/math.js'; test('add', () => expect(add(1, 2)).toBe(3));\n");
        repository.WriteText(
            "coverage/lcov.info",
            "SF:C:\\private-client\\checkout\\src\\math.js\nLF:10\nLH:8\nend_of_record\n");

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
        Assert.DoesNotContain("private-client", scan.StandardOutput, StringComparison.Ordinal);
        using JsonDocument scanDocument = JsonDocument.Parse(scan.StandardOutput);
        JsonElement measured = Assert.Single(
            scanDocument.RootElement.GetProperty("facts").EnumerateArray(),
            fact => fact.GetProperty("tags")
                .EnumerateArray()
                .Any(tag => tag.GetString() == "coverage:measured"));
        Assert.Equal("measured", measured.GetProperty("provenance").GetProperty("sourceKind").GetString());
        Assert.Equal(80m, measured.GetProperty("measurements")
            .EnumerateArray()
            .Single(measurement => measurement.GetProperty("name").GetString() == "lines")
            .GetProperty("value")
            .GetDecimal());

        using JsonDocument estimateDocument = JsonDocument.Parse(estimate.StandardOutput);
        JsonElement coverageItem = Assert.Single(
            estimateDocument.RootElement.GetProperty("workItems").EnumerateArray(),
            item => item.GetProperty("estimator").GetProperty("id").GetString() ==
                "seed-rule:coverage-achievement");
        Assert.Contains("measured", coverageItem.GetProperty("title").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.All(
            coverageItem.GetProperty("evidenceIds").EnumerateArray(),
            id => Assert.StartsWith("coverage:measured:", id.GetString()));
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
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

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
            if (File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the EffortHours repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class CoverageRepository : IDisposable
    {
        public CoverageRepository()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "efforthours-coverage-e2e",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void WriteText(string relativePath, string content)
        {
            string path = Path.Combine(RootPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose() => Directory.Delete(RootPath, recursive: true);
    }
}
