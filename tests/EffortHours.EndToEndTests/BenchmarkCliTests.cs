using System.Diagnostics;
using System.Globalization;

namespace EffortHours.EndToEndTests;

public sealed class BenchmarkCliTests
{
    [Fact]
    public async Task MixedBenchmarkReportsPeakMemoryAndReadOnlySafety()
    {
        ProcessResult result = await RunBenchmarkAsync(
            "--files",
            "12",
            "--lines-per-file",
            "10",
            "--mixed",
            "--warm-cache");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Dictionary<string, string> measurements = ParseMeasurements(result.StandardOutput);
        Assert.Equal("mixed-static", measurements["mode"]);
        Assert.Equal("true", measurements["generated-fixture"]);
        Assert.Equal("true", measurements["target-metadata-unchanged"]);
        Assert.Equal("not-performed", measurements["target-execution"]);
        Assert.Equal("not-performed", measurements["dependency-installation"]);
        Assert.Equal("not-performed", measurements["network-access"]);
        Assert.StartsWith("sha256:", measurements["target-metadata-digest"], StringComparison.Ordinal);
        AssertPositive(measurements, "scan-peak-working-set-mib");
        AssertPositive(measurements, "warm-cache-peak-working-set-mib");
        AssertPositive(measurements, "analyzed-text-lines");
    }

    [Fact]
    public async Task ExistingRepositoryBenchmarkLeavesTargetByteIdentical()
    {
        using TemporaryTarget target = new();
        target.WriteText(
            "Demo.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
        target.WriteText(
            "src/Feature.cs",
            "namespace Demo; public sealed class Feature { public int Value => 1; }\n");
        target.WriteText(
            "web/package.json",
            "{\"name\":\"demo\",\"type\":\"module\"}\n");
        target.WriteText(
            "web/src/index.ts",
            "export const value: number = 1;\n");
        Dictionary<string, string> before = target.ReadAllText();

        ProcessResult result = await RunBenchmarkAsync(
            "--repository",
            target.RootPath,
            "--warm-cache");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Dictionary<string, string> measurements = ParseMeasurements(result.StandardOutput);
        Assert.Equal("repository-static", measurements["mode"]);
        Assert.Equal("false", measurements["generated-fixture"]);
        Assert.Equal("true", measurements["target-metadata-unchanged"]);
        Dictionary<string, string> after = target.ReadAllText();
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        Assert.All(before, pair => Assert.Equal(pair.Value, after[pair.Key]));
    }

    [Fact]
    public async Task PythonBenchmarkRunsInFreshProcessWithStaticSafetySignals()
    {
        ProcessResult result = await RunBenchmarkAsync(
            "--files",
            "12",
            "--lines-per-file",
            "10",
            "--python");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Dictionary<string, string> measurements = ParseMeasurements(result.StandardOutput);
        Assert.Equal("python-static", measurements["mode"]);
        Assert.Equal("true", measurements["generated-fixture"]);
        Assert.Equal("true", measurements["target-metadata-unchanged"]);
        Assert.Equal("not-performed", measurements["target-execution"]);
        Assert.Equal("not-performed", measurements["dependency-installation"]);
        Assert.Equal("not-performed", measurements["network-access"]);
        AssertPositive(measurements, "scan-peak-working-set-mib");
        AssertPositive(measurements, "analyzed-text-lines");
    }

    [Fact]
    public async Task GoBenchmarkRunsInFreshProcessWithStaticSafetySignals()
    {
        ProcessResult result = await RunBenchmarkAsync(
            "--files",
            "12",
            "--lines-per-file",
            "10",
            "--go");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Dictionary<string, string> measurements = ParseMeasurements(result.StandardOutput);
        Assert.Equal("go-static", measurements["mode"]);
        Assert.Equal("true", measurements["generated-fixture"]);
        Assert.Equal("true", measurements["target-metadata-unchanged"]);
        Assert.Equal("not-performed", measurements["target-execution"]);
        Assert.Equal("not-performed", measurements["dependency-installation"]);
        Assert.Equal("not-performed", measurements["network-access"]);
        AssertPositive(measurements, "scan-peak-working-set-mib");
        AssertPositive(measurements, "analyzed-text-lines");
    }

    [Fact]
    public async Task JavaBenchmarkRunsInFreshProcessWithStaticSafetySignals()
    {
        ProcessResult result = await RunBenchmarkAsync(
            "--files",
            "12",
            "--lines-per-file",
            "10",
            "--java");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Dictionary<string, string> measurements = ParseMeasurements(result.StandardOutput);
        Assert.Equal("java-static", measurements["mode"]);
        Assert.Equal("true", measurements["generated-fixture"]);
        Assert.Equal("true", measurements["target-metadata-unchanged"]);
        Assert.Equal("not-performed", measurements["target-execution"]);
        Assert.Equal("not-performed", measurements["dependency-installation"]);
        Assert.Equal("not-performed", measurements["network-access"]);
        AssertPositive(measurements, "scan-peak-working-set-mib");
        AssertPositive(measurements, "analyzed-text-lines");
    }

    [Fact]
    public async Task KotlinBenchmarkRunsInFreshProcessWithStaticSafetySignals()
    {
        ProcessResult result = await RunBenchmarkAsync(
            "--files",
            "12",
            "--lines-per-file",
            "10",
            "--kotlin");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Dictionary<string, string> measurements = ParseMeasurements(result.StandardOutput);
        Assert.Equal("kotlin-static", measurements["mode"]);
        Assert.Equal("true", measurements["generated-fixture"]);
        Assert.Equal("true", measurements["target-metadata-unchanged"]);
        Assert.Equal("not-performed", measurements["target-execution"]);
        Assert.Equal("not-performed", measurements["dependency-installation"]);
        Assert.Equal("not-performed", measurements["network-access"]);
        AssertPositive(measurements, "scan-peak-working-set-mib");
        AssertPositive(measurements, "analyzed-text-lines");
    }

    [Theory]
    [InlineData("--shell", "shell-static")]
    [InlineData("--powershell", "powershell-static")]
    [InlineData("--php", "php-composer-static")]
    [InlineData("--rust", "rust-cargo-static")]
    [InlineData("--terraform", "terraform-hcl-static")]
    [InlineData("--docker", "docker-compose-static")]
    [InlineData("--jupyter", "jupyter-notebook-static")]
    public async Task StaticEcosystemBenchmarksRunInFreshProcessesWithSafetySignals(
        string option,
        string expectedMode)
    {
        ProcessResult result = await RunBenchmarkAsync(
            "--files",
            "12",
            "--lines-per-file",
            "10",
            option);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Dictionary<string, string> measurements = ParseMeasurements(result.StandardOutput);
        Assert.Equal(expectedMode, measurements["mode"]);
        Assert.Equal("true", measurements["target-metadata-unchanged"]);
        Assert.Equal("not-performed", measurements["target-execution"]);
        Assert.Equal("not-performed", measurements["dependency-installation"]);
        Assert.Equal("not-performed", measurements["network-access"]);
        AssertPositive(measurements, "scan-peak-working-set-mib");
        AssertPositive(measurements, "analyzed-text-lines");
    }

    private static void AssertPositive(Dictionary<string, string> measurements, string name)
    {
        Assert.True(
            decimal.TryParse(
                measurements[name],
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal value));
        Assert.True(value > 0m, $"Expected '{name}' to be positive, but it was {value}.");
    }

    private static Dictionary<string, string> ParseMeasurements(string output) => output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Split('=', 2))
        .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

    private static async Task<ProcessResult> RunBenchmarkAsync(params string[] arguments)
    {
        string repositoryRoot = FindRepositoryRoot();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine the test build configuration.");
        string benchmarkAssembly = Path.Combine(
            repositoryRoot,
            "benchmarks",
            "EffortHours.ScannerBenchmarks",
            "bin",
            configuration,
            "net10.0",
            "EffortHours.ScannerBenchmarks.dll");
        Assert.True(File.Exists(benchmarkAssembly), $"Benchmark assembly was not built: {benchmarkAssembly}");

        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.ArgumentList.Add(benchmarkAssembly);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the scanner benchmark process.");
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

    private sealed class TemporaryTarget : IDisposable
    {
        public TemporaryTarget()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "efforthours-benchmark-e2e",
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

        public Dictionary<string, string> ReadAllText() => Directory
            .GetFiles(RootPath, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(RootPath, path).Replace('\\', '/'),
                File.ReadAllText,
                StringComparer.Ordinal);

        public void Dispose() => Directory.Delete(RootPath, recursive: true);
    }
}
