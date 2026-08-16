using System.Diagnostics;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed class CalibrationUncertaintyGraphCliTests
{
    [Fact]
    public async Task ProjectsLocalGraphDiagnosticsThroughTheCli()
    {
        string root = FindRepositoryRoot();
        using TemporaryRepository repository = new();
        repository.Write("A/A.csproj", Project("../B/B.csproj"));
        repository.Write("B/B.csproj", Project("../A/A.csproj"));
        repository.Write("A/A.cs", "public sealed class A { public void Run() {} }\n");
        repository.Write("B/B.cs", "public sealed class B { public void Run() {} }\n");
        string evidencePath = Path.Combine(repository.RootPath, "evidence.json");
        string estimatePath = Path.Combine(repository.RootPath, "estimate.json");

        ProcessResult scan = await RunCliAsync(
            root,
            "scan",
            repository.RootPath,
            "--output",
            evidencePath);
        ProcessResult estimate = await RunCliAsync(
            root,
            "estimate",
            evidencePath,
            "--no-rate",
            "--compact",
            "--output",
            estimatePath);
        ProcessResult projection = await RunCliAsync(
            root,
            "calibration",
            "uncertainty-graph",
            estimatePath,
            evidencePath,
            "--compact");

        Assert.Equal(0, scan.ExitCode);
        Assert.Equal(0, estimate.ExitCode);
        Assert.Equal(0, projection.ExitCode);
        Assert.Equal(string.Empty, scan.StandardError);
        Assert.Equal(string.Empty, estimate.StandardError);
        Assert.Equal(string.Empty, projection.StandardError);
        CalibrationUncertaintyGraphFeatureReport report =
            ContractJson.Deserialize<CalibrationUncertaintyGraphFeatureReport>(
                projection.StandardOutput);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintyGraphFeatures,
            projection.StandardOutput);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(report));
        Assert.Equal(CalibrationUncertaintyVersions.GraphFeatureContractV1,
            report.FeatureContract.Version);
        Assert.Equal(2, report.Summary.NodeCount);
        Assert.Equal(2, report.Summary.EdgeCount);
        Assert.Equal(2, report.Summary.CyclicNodeCount);
        Assert.True(report.Summary.MappedWorkItemCount > 0);
    }

    private static string Project(string reference) =>
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework></PropertyGroup>" +
        $"<ItemGroup><ProjectReference Include=\"{reference}\" /></ItemGroup></Project>\n";

    private static async Task<ProcessResult> RunCliAsync(
        string root,
        params string[] arguments)
    {
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine test configuration.");
        string cli = Path.Combine(
            root,
            "src",
            "EffortHours.Cli",
            "bin",
            configuration,
            "net10.0",
            "efforthours.dll");
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        foreach (string argument in arguments.Prepend(cli))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start EffortHours CLI.");
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
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null &&
               !File.Exists(Path.Combine(current.FullName, "EffortHours.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class TemporaryRepository : IDisposable
    {
        public TemporaryRepository()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                $"efforthours-uncertainty-graph-{Guid.NewGuid():N}");
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void Write(string name, string content)
        {
            string path = Path.Combine(RootPath, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        public void Dispose() => Directory.Delete(RootPath, recursive: true);
    }
}
