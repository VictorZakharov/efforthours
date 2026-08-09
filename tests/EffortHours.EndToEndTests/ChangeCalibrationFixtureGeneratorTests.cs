using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed class ChangeCalibrationFixtureGeneratorTests
{
    [Fact]
    public async Task GeneratorProducesDeterministicReportAndBlindPacket()
    {
        using TemporaryDirectory directory = new();
        string suitePath = directory.Write("fixtures.json", FixtureJson);
        string outputPath = Path.Combine(directory.RootPath, "generated");

        ProcessResult first = await RunGeneratorAsync(suitePath, outputPath);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(string.Empty, first.StandardError);
        string indexPath = Path.Combine(outputPath, "index.json");
        string firstIndex = File.ReadAllText(indexPath);
        using JsonDocument index = JsonDocument.Parse(firstIndex);
        JsonElement fixture = Assert.Single(index.RootElement.GetProperty("cases").EnumerateArray());
        Assert.Equal("change:e2e-generator", fixture.GetProperty("id").GetString());
        string reportPath = Path.Combine(
            outputPath,
            fixture.GetProperty("estimatePath").GetString()!.Replace('/', Path.DirectorySeparatorChar));
        string packetPath = Path.Combine(
            outputPath,
            fixture.GetProperty("blindPacketPath").GetString()!.Replace('/', Path.DirectorySeparatorChar));
        string firstReport = File.ReadAllText(reportPath);
        string firstPacket = File.ReadAllText(packetPath);
        ChangeEstimateReport report = ContractJson.Deserialize<ChangeEstimateReport>(firstReport);
        CalibrationAuthoringPacket packet = ContractJson.Deserialize<CalibrationAuthoringPacket>(firstPacket);
        Assert.Empty(ContractValidation.Validate(report));
        Assert.Empty(ContractValidation.Validate(packet));
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.ChangeEstimateReport,
            firstReport).IsValid);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationAuthoringPacket,
            firstPacket).IsValid);
        Assert.Null(packet.Candidate.TotalHours);
        Assert.All(packet.Targets, target => Assert.Null(target.Candidate.Hours));

        ProcessResult second = await RunGeneratorAsync(suitePath, outputPath);

        Assert.Equal(0, second.ExitCode);
        Assert.Equal(firstIndex, File.ReadAllText(indexPath));
        Assert.Equal(firstReport, File.ReadAllText(reportPath));
        Assert.Equal(firstPacket, File.ReadAllText(packetPath));
    }

    [Fact]
    public async Task TeacherPolicyMapsToSchemaValidReviewPlan()
    {
        using TemporaryDirectory directory = new();
        string suitePath = directory.Write("fixtures.json", FixtureJson);
        string generatedPath = Path.Combine(directory.RootPath, "generated");
        Assert.Equal(0, (await RunGeneratorAsync(suitePath, generatedPath)).ExitCode);
        string policyPath = directory.Write("teacher-policy.json", TeacherPolicyJson);
        string planPath = Path.Combine(directory.RootPath, "teacher-plan.json");

        ProcessResult result = await RunToolAsync(
            "--teacher-policy",
            policyPath,
            "--index",
            Path.Combine(generatedPath, "index.json"),
            "--output",
            planPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        string json = File.ReadAllText(planPath);
        CalibrationReviewPlan plan = ContractJson.Deserialize<CalibrationReviewPlan>(json);
        CalibrationReviewPlanRecord record = Assert.Single(plan.Records);
        Assert.NotNull(record.Change);
        Assert.Equal(3, record.Capabilities.Count);
        Assert.All(record.Capabilities, capability => Assert.Single(capability.Targets));
        Assert.Empty(ContractValidation.Validate(plan));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.CalibrationReviewPlan, json).IsValid);
    }

    private static Task<ProcessResult> RunGeneratorAsync(string suitePath, string outputPath) =>
        RunToolAsync("--suite", suitePath, "--output", outputPath);

    private static async Task<ProcessResult> RunToolAsync(params string[] arguments)
    {
        string root = FindRepositoryRoot();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine test configuration.");
        string generator = Path.Combine(
            root,
            "tools",
            "EffortHours.ChangeCalibration",
            "bin",
            configuration,
            "net10.0",
            "EffortHours.ChangeCalibration.dll");
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.ArgumentList.Add(generator);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start fixture generator.");
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
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EffortHours.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private const string FixtureJson = """
        {
          "schemaVersion": "1.0.0",
          "id": "e2e-change-fixtures",
          "version": "0.1.0",
          "description": "Small process-level fixture.",
          "cases": [{
            "id": "change:e2e-generator",
            "repositoryFamilyId": "repository:e2e-generator",
            "partition": "development",
            "ecosystem": "javascript",
            "selectionKind": "base-head",
            "baseStateId": "base",
            "headStateId": "head",
            "coverageTags": ["javascript", "production"],
            "states": [
              {"id": "base", "files": {"package.json": "{\"name\":\"fixture\",\"type\":\"module\"}\n"}},
              {"id": "head", "files": {
                "package.json": "{\"name\":\"fixture\",\"type\":\"module\"}\n",
                "src/status.js": "export const status = 'ready';\n"
              }}
            ]
          }]
        }
        """;

    private const string TeacherPolicyJson = """
        {
          "schemaVersion": "1.0.0",
          "id": "e2e-change-teacher",
          "version": "0.1.0",
          "description": "Small process-level teacher policy.",
          "completedOn": "2026-08-06",
          "reviewer": {
            "id": "host-ai:e2e-teacher",
            "kind": "host-ai",
            "role": "teacher",
            "modelId": "test-model",
            "modelVersion": "test-version"
          },
          "reviewNotes": "Process-level teacher mapping test only.",
          "cases": [{
            "id": "change:e2e-generator",
            "rationale": "Review one small maintained JavaScript behavior.",
            "categories": [
              {"category": "specification-comprehension-and-domain-learning", "action": "target", "hours": {"low": 0.25, "expected": 0.5, "high": 1}},
              {"category": "production-implementation", "action": "target", "hours": {"low": 0.5, "expected": 1, "high": 2}},
              {"category": "self-review-and-system-integration", "action": "target", "hours": {"low": 0.25, "expected": 0.5, "high": 1}}
            ]
          }]
        }
        """;

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"efforthours-change-fixtures-{Guid.NewGuid():N}");
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public string Write(string name, string content)
        {
            string path = Path.Combine(RootPath, name);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        public void Dispose() => Directory.Delete(RootPath, recursive: true);
    }
}
