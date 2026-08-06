using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

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
    public async Task ModelInfoReportsBundledOfflineSeedArtifact()
    {
        ProcessResult result = await RunCliAsync("model", "info");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal("seed-rules", document.RootElement.GetProperty("id").GetString());
        Assert.Equal("0.2.0", document.RootElement.GetProperty("version").GetString());
        Assert.Equal(
            "experimental-uncalibrated",
            document.RootElement.GetProperty("status").GetString());
        Assert.StartsWith(
            "sha256:",
            document.RootElement.GetProperty("sha256").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RateInfoReportsBundledAuditableDefault()
    {
        ProcessResult result = await RunCliAsync("rate", "info");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal("us-senior-software-contractor", document.RootElement.GetProperty("id").GetString());
        Assert.Equal("2026.1", document.RootElement.GetProperty("version").GetString());
        Assert.Equal(160m, document.RootElement.GetProperty("hourlyRate").GetDecimal());
        Assert.Equal(
            125m,
            document.RootElement.GetProperty("marketRange").GetProperty("low").GetDecimal());
        Assert.Equal(
            200m,
            document.RootElement.GetProperty("marketRange").GetProperty("high").GetDecimal());
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
        Assert.Equal("seed-rules/0.2.0", document.RootElement.GetProperty("estimatorVersion").GetString());
        Assert.True(document.RootElement.GetProperty("totalEffort").GetProperty("expected").GetDecimal() > 0m);
        Assert.True(document.RootElement.GetProperty("workItems").GetArrayLength() > 0);
        Assert.Equal(
            "us-senior-software-contractor/2026.1",
            document.RootElement.GetProperty("rateCard").GetProperty("id").GetString());
        Assert.Equal(160m, document.RootElement.GetProperty("rateCard").GetProperty("hourlyRate").GetDecimal());
        Assert.True(document.RootElement.GetProperty("totalCost").GetProperty("expected").GetDecimal() > 0m);
    }

    [Theory]
    [InlineData("minimal-dotnet.repository-evidence.json", "dotnet")]
    [InlineData("minimal-javascript.repository-evidence.json", "javascript")]
    [InlineData("minimal.repository-evidence.json", "typescript")]
    public async Task CuratedEcosystemEvidenceProducesCompactRepositoryView(
        string fixtureName,
        string expectedEcosystem)
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "evidence", fixtureName);

        ProcessResult result = await RunCliAsync(
            "estimate",
            fixture,
            "--view",
            "repository",
            "--no-rate",
            "--compact");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal("repository", document.RootElement.GetProperty("view").GetString());
        Assert.Contains(
            document.RootElement.GetProperty("repository").GetProperty("ecosystems").EnumerateArray(),
            ecosystem => ecosystem.GetString() == expectedEcosystem);
        Assert.False(document.RootElement.TryGetProperty("rateCard", out _));
    }

    [Fact]
    public async Task EstimateCanExplicitlyOmitRateAndCost()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "evidence", "minimal.repository-evidence.json");

        ProcessResult result = await RunCliAsync("estimate", fixture, "--no-rate", "--compact");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.False(document.RootElement.TryGetProperty("rateCard", out _));
        Assert.False(document.RootElement.TryGetProperty("totalCost", out _));
        Assert.DoesNotContain('\n', result.StandardOutput);
    }

    [Fact]
    public async Task CompactReviewAndExplainOutputsAreTraceable()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "evidence", "minimal.repository-evidence.json");
        ProcessResult workItems = await RunCliAsync(
            "estimate",
            fixture,
            "--view",
            "work-item",
            "--compact");
        Assert.Equal(0, workItems.ExitCode);
        using JsonDocument workItemDocument = JsonDocument.Parse(workItems.StandardOutput);
        string capabilityId = workItemDocument.RootElement
            .GetProperty("capabilities")[0]
            .GetProperty("id")
            .GetString()!;

        ProcessResult review = await RunCliAsync(
            "estimate",
            fixture,
            "--view",
            "review",
            "--compact");
        ProcessResult explanation = await RunCliAsync(
            "explain",
            fixture,
            "--item",
            capabilityId,
            "--compact");

        Assert.Equal(0, review.ExitCode);
        Assert.Equal(string.Empty, review.StandardError);
        using JsonDocument reviewDocument = JsonDocument.Parse(review.StandardOutput);
        Assert.Equal("review", reviewDocument.RootElement.GetProperty("view").GetString());
        Assert.InRange(reviewDocument.RootElement.GetProperty("reviewQueue").GetArrayLength(), 1, 12);

        Assert.Equal(0, explanation.ExitCode);
        Assert.Equal(string.Empty, explanation.StandardError);
        using JsonDocument explanationDocument = JsonDocument.Parse(explanation.StandardOutput);
        Assert.Equal(capabilityId, explanationDocument.RootElement.GetProperty("requestedId").GetString());
        Assert.Equal("capability", explanationDocument.RootElement.GetProperty("matchKind").GetString());
        Assert.True(explanationDocument.RootElement.GetProperty("workItems").GetArrayLength() > 0);
        Assert.True(explanationDocument.RootElement.GetProperty("evidenceFacts").GetArrayLength() > 0);
    }

    [Fact]
    public async Task ReportReprojectsASavedCanonicalEstimate()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "evidence", "minimal.repository-evidence.json");
        ProcessResult estimate = await RunCliAsync("estimate", fixture, "--compact");
        Assert.Equal(0, estimate.ExitCode);

        using TemporaryRepository repository = new();
        repository.WriteText("estimate.json", estimate.StandardOutput);
        ProcessResult result = await RunCliAsync(
            "report",
            Path.Combine(repository.RootPath, "estimate.json"),
            "--compact");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal("review", document.RootElement.GetProperty("view").GetString());
        Assert.Equal(
            160m,
            document.RootElement.GetProperty("rateCard").GetProperty("hourlyRate").GetDecimal());
    }

    [Fact]
    public async Task CalibrationValidatesAndEvaluatesARepositoryHeldOutPartition()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "evidence", "minimal.repository-evidence.json");
        ProcessResult estimate = await RunCliAsync("estimate", fixture, "--no-rate", "--compact");
        Assert.Equal(0, estimate.ExitCode);

        using TemporaryRepository repository = new();
        repository.WriteText("estimate.json", estimate.StandardOutput);
        repository.WriteText("corpus.json", CreateCalibrationCorpus(estimate.StandardOutput));
        string corpusPath = Path.Combine(repository.RootPath, "corpus.json");
        string estimatePath = Path.Combine(repository.RootPath, "estimate.json");

        ProcessResult validation = await RunCliAsync(
            "calibration",
            "validate",
            corpusPath,
            "--compact");
        ProcessResult evaluation = await RunCliAsync(
            "calibration",
            "evaluate",
            corpusPath,
            estimatePath,
            "--partition",
            "test",
            "--compact");
        ProcessResult wrongPartition = await RunCliAsync(
            "calibration",
            "evaluate",
            corpusPath,
            estimatePath,
            "--partition",
            "development",
            "--compact");

        Assert.Equal(0, validation.ExitCode);
        Assert.Equal(string.Empty, validation.StandardError);
        using JsonDocument validationDocument = JsonDocument.Parse(validation.StandardOutput);
        Assert.True(validationDocument.RootElement.GetProperty("valid").GetBoolean());
        Assert.Equal(1, validationDocument.RootElement.GetProperty("repositoryCount").GetInt32());

        Assert.Equal(0, evaluation.ExitCode);
        Assert.Equal(string.Empty, evaluation.StandardError);
        using JsonDocument evaluationDocument = JsonDocument.Parse(evaluation.StandardOutput);
        Assert.Equal(
            "calibration-metrics/1.0.0",
            evaluationDocument.RootElement.GetProperty("metricVersion").GetString());
        Assert.Equal(
            0m,
            evaluationDocument.RootElement
                .GetProperty("repositoryTotals")
                .GetProperty("expected")
                .GetProperty("weightedAbsolutePercentageError")
                .GetDecimal());
        Assert.Equal(
            1m,
            evaluationDocument.RootElement.GetProperty("match").GetProperty("targetMatchRate").GetDecimal());

        Assert.Equal(3, wrongPartition.ExitCode);
        Assert.Equal(string.Empty, wrongPartition.StandardOutput);
        Assert.Contains("no 'Development' records", wrongPartition.StandardError, StringComparison.Ordinal);
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
        Assert.Contains("Profile: `recreation`", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Estimator: `seed-rules/0.2.0`", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("| Human-hours |", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("| Replacement cost (USD) |", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanProducesRepositoryEvidenceFromDirectory()
    {
        using TemporaryRepository repository = new();
        repository.WriteText("Sample.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        repository.WriteText(
            "Program.cs",
            "public static class Program { private const string Marker = \"not-in-evidence\"; }\n");
        repository.WriteText("README.md", "# Sample\n");

        ProcessResult result = await RunCliAsync("scan", repository.RootPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.DoesNotContain("not-in-evidence", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(repository.RootPath, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal("1.0.0", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Contains(
            document.RootElement.GetProperty("repository").GetProperty("ecosystems").EnumerateArray(),
            ecosystem => ecosystem.GetString() == "dotnet");
        Assert.Contains(
            document.RootElement.GetProperty("facts").EnumerateArray(),
            fact => fact.GetProperty("id").GetString() == "inventory:repository");
    }

    [Fact]
    public async Task EstimateAcceptsRepositoryDirectory()
    {
        using TemporaryRepository repository = new();
        repository.WriteText("package.json", "{\"name\":\"sample\"}\n");
        repository.WriteText("src/index.ts", "export const value = 1;\n");
        repository.WriteText("tests/index.test.ts", "export const covered = true;\n");

        ProcessResult result = await RunCliAsync(
            "estimate",
            repository.RootPath,
            "--profile",
            "implementation",
            "--format",
            "json");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal("seed-rules/0.2.0", document.RootElement.GetProperty("estimatorVersion").GetString());
        Assert.True(
            document.RootElement.GetProperty("totalEffort").GetProperty("expected").GetDecimal() > 0m);
        Assert.Contains(
            document.RootElement.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "FB1000");
    }

    [Fact]
    public async Task ScanProducesStaticJavaScriptAndTypeScriptEvidenceWithoutSourceText()
    {
        using TemporaryRepository repository = new();
        repository.WriteText(
            "package.json",
            "{\"name\":\"sample\",\"dependencies\":{\"express\":\"5.0.0\"}}\n");
        repository.WriteText(
            "src/server.ts",
            "import express from 'express'; const app = express(); app.get('/private-route', () => 'private-marker');\n");

        ProcessResult result = await RunCliAsync("scan", repository.RootPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.DoesNotContain("/private-route", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("private-marker", result.StandardOutput, StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.Contains(
            document.RootElement.GetProperty("facts").EnumerateArray(),
            fact => fact.GetProperty("id").GetString() == "javascript:api:src/server.ts");
        Assert.Contains(
            document.RootElement.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "FB4000");
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

    private static string CreateCalibrationCorpus(string estimateJson)
    {
        JsonObject estimate = JsonNode.Parse(estimateJson)!.AsObject();
        JsonArray targets = [];
        foreach (JsonNode? node in estimate["workItems"]!.AsArray())
        {
            JsonObject item = node!.AsObject();
            targets.Add(new JsonObject
            {
                ["id"] = $"target:{item["id"]!.GetValue<string>()}",
                ["category"] = item["category"]!.DeepClone(),
                ["title"] = item["title"]!.DeepClone(),
                ["scope"] = item["scope"]!.DeepClone(),
                ["sourceWorkItemIds"] = new JsonArray(item["id"]!.DeepClone()),
                ["evidenceIds"] = item["evidenceIds"]!.DeepClone(),
                ["hours"] = item["hours"]!.DeepClone(),
                ["rationale"] = "Synthetic CLI calibration target.",
                ["uncertaintyReasons"] = item["uncertaintyReasons"]!.DeepClone(),
            });
        }

        JsonObject corpus = new()
        {
            ["schemaVersion"] = "1.0.0",
            ["id"] = "synthetic-cli-corpus",
            ["version"] = "1.0.0",
            ["description"] = "Process-level synthetic calibration fixture.",
            ["rubric"] = new JsonObject
            {
                ["id"] = "ehe-work-item",
                ["version"] = "1.0.0",
            },
            ["records"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "record:minimal:implementation",
                    ["repository"] = new JsonObject
                    {
                        ["id"] = "repository:minimal",
                        ["name"] = estimate["repository"]!["name"]!.DeepClone(),
                        ["sourceDigest"] = estimate["repository"]!["sourceDigest"]!.DeepClone(),
                    },
                    ["profile"] = estimate["profile"]!.DeepClone(),
                    ["baselineId"] = estimate["baseline"]!["id"]!.DeepClone(),
                    ["partition"] = "test",
                    ["sourceEstimatorVersion"] = estimate["estimatorVersion"]!.DeepClone(),
                    ["sourceEstimateDigest"] = "sha256:synthetic-cli-source-estimate",
                    ["source"] = new JsonObject
                    {
                        ["dataClassification"] = "synthetic",
                        ["sourceReference"] = "synthetic:end-to-end-fixture",
                        ["revision"] = "1",
                        ["licenseExpression"] = "MIT",
                        ["redistributionAllowed"] = true,
                    },
                    ["review"] = new JsonObject
                    {
                        ["status"] = "teacher-estimate",
                        ["completedOn"] = "2026-08-06",
                        ["reviewers"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["id"] = "teacher:e2e",
                                ["kind"] = "host-ai",
                                ["role"] = "teacher",
                                ["modelId"] = "synthetic",
                                ["modelVersion"] = "1",
                            },
                        },
                    },
                    ["targets"] = targets,
                },
            },
        };

        return corpus.ToJsonString();
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

    private sealed class TemporaryRepository : IDisposable
    {
        public TemporaryRepository()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "fairbill-e2e-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void WriteText(string relativePath, string content)
        {
            string path = Path.Combine(
                RootPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
