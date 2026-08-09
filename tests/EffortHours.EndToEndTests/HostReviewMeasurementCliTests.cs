using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EffortHours.EndToEndTests;

public sealed class HostReviewMeasurementCliTests
{
    [Fact]
    public async Task MeasureAndBenchmarkAreStructuredSanitizedAndDeterministic()
    {
        using MeasurementRepository repository = new();
        repository.WriteText(
            "Demo.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
        repository.WriteText(
            "Program.cs",
            "namespace Demo; public static class Program { public static int Main() => 0; }\n");
        repository.WriteText("README.md", "# Host review measurement fixture\n");
        ProcessResult packetResult = await RunCliAsync(
            "review",
            "packet",
            repository.RootPath,
            "--provider",
            "fixture-provider",
            "--model",
            "fixture-model",
            "--model-version",
            "fixture-version",
            "--compact");
        Assert.Equal(0, packetResult.ExitCode);

        JsonObject packet = JsonNode.Parse(packetResult.StandardOutput)!.AsObject();
        string digest = packet["inputDigest"]!.GetValue<string>();
        string capabilityId = packet["candidates"]![0]!["capability"]!["id"]!.GetValue<string>();
        ProcessResult queryResult = await RunCliAsync(
            "review",
            "query",
            repository.RootPath,
            "--input-digest",
            digest,
            "--capability",
            capabilityId,
            "--reason",
            "Resolve a bounded fixture question.",
            "--compact");
        Assert.Equal(0, queryResult.ExitCode);
        string packetPath = repository.WriteText("packet.json", packetResult.StandardOutput);
        string queryPath = repository.WriteText("query.json", queryResult.StandardOutput);

        string compactAdjustment = CreateCompleteAdjustment(packet, 0.85m);
        string sourceAdjustment = CreateCompleteAdjustment(packet, 0.65m);
        string compactAdjustmentPath = repository.WriteText("compact-adjustment.json", compactAdjustment);
        string sourceAdjustmentPath = repository.WriteText("source-adjustment.json", sourceAdjustment);
        ProcessResult compact = await RunCliAsync(
            "review",
            "measure",
            packetPath,
            compactAdjustmentPath,
            "--subject",
            "fixture-shape-a",
            "--session",
            "compact-session",
            "--context",
            "compact",
            "--query-result",
            queryPath,
            "--elapsed-ms",
            "1000",
            "--provider-input-tokens",
            "1000",
            "--provider-output-tokens",
            "100",
            "--token-basis",
            "fixture provider usage",
            "--cost",
            "0.25",
            "--currency",
            "USD",
            "--cost-basis",
            "fixture cost",
            "--input-complete",
            "--compact");
        ProcessResult compactAgain = await RunCliAsync(
            "review",
            "measure",
            packetPath,
            compactAdjustmentPath,
            "--subject",
            "fixture-shape-a",
            "--session",
            "compact-session",
            "--context",
            "compact",
            "--query-result",
            queryPath,
            "--elapsed-ms",
            "1000",
            "--provider-input-tokens",
            "1000",
            "--provider-output-tokens",
            "100",
            "--token-basis",
            "fixture provider usage",
            "--cost",
            "0.25",
            "--currency",
            "USD",
            "--cost-basis",
            "fixture cost",
            "--input-complete",
            "--compact");

        Assert.Equal(0, compact.ExitCode);
        Assert.Equal(string.Empty, compact.StandardError);
        Assert.Equal(compact.StandardOutput, compactAgain.StandardOutput);
        Assert.DoesNotContain(repository.RootPath, compact.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Resolve a bounded fixture question", compact.StandardOutput, StringComparison.Ordinal);
        using JsonDocument compactDocument = JsonDocument.Parse(compact.StandardOutput);
        Assert.False(compactDocument.RootElement.GetProperty("privacy").GetProperty("sourceTextCopied").GetBoolean());
        Assert.True(compactDocument.RootElement.GetProperty("privacy").GetProperty("callerSuppliedTextRetained").GetBoolean());
        Assert.Equal(1, compactDocument.RootElement.GetProperty("queries").GetArrayLength());
        string compactPath = repository.WriteText("compact-measurement.json", compact.StandardOutput);

        string sourceOutputPath = Path.Combine(repository.RootPath, "source-measurement.json");
        ProcessResult source = await RunCliAsync(
            "review",
            "measure",
            packetPath,
            sourceAdjustmentPath,
            "--subject",
            "fixture-shape-a",
            "--session",
            "source-session",
            "--context",
            "broader-source",
            "--additional-input-bytes",
            "25000",
            "--additional-input-characters",
            "23000",
            "--additional-input-basis",
            "fixture maintained text",
            "--elapsed-ms",
            "5000",
            "--cost",
            "2.00",
            "--currency",
            "USD",
            "--cost-basis",
            "fixture cost",
            "--input-complete",
            "--compact",
            "--output",
            sourceOutputPath);
        Assert.Equal(0, source.ExitCode);
        Assert.Equal(string.Empty, source.StandardOutput);
        Assert.Equal(string.Empty, source.StandardError);
        Assert.True(File.Exists(sourceOutputPath));

        ProcessResult benchmark = await RunCliAsync(
            "review",
            "benchmark",
            compactPath,
            sourceOutputPath,
            "--compact");
        Assert.Equal(0, benchmark.ExitCode);
        Assert.Equal(string.Empty, benchmark.StandardError);
        using JsonDocument benchmarkDocument = JsonDocument.Parse(benchmark.StandardOutput);
        JsonElement benchmarkRoot = benchmarkDocument.RootElement;
        Assert.Equal(1, benchmarkRoot.GetProperty("subjectCount").GetInt32());
        Assert.False(benchmarkRoot.GetProperty("budgetDecision").GetProperty("defaultBudgetSelected").GetBoolean());
        Assert.True(benchmarkRoot.GetProperty("repositoryTotals")
            .GetProperty("expectedAbsoluteErrorReductionHours").GetDecimal() > 0m);
    }

    [Fact]
    public async Task MeasureRejectsIncompleteDecisionCoverageWithoutStructuredOutput()
    {
        using MeasurementRepository repository = new();
        repository.WriteText("Demo.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        repository.WriteText("Program.cs", "public sealed class Demo { }\n");
        ProcessResult packetResult = await RunCliAsync(
            "review",
            "packet",
            repository.RootPath,
            "--compact");
        Assert.Equal(0, packetResult.ExitCode);
        JsonObject packet = JsonNode.Parse(packetResult.StandardOutput)!.AsObject();
        string packetPath = repository.WriteText("packet.json", packetResult.StandardOutput);
        string incompletePath = repository.WriteText(
            "incomplete.json",
            CreateCompleteAdjustment(packet, 1m, takeOnlyFirst: true));

        ProcessResult result = await RunCliAsync(
            "review",
            "measure",
            packetPath,
            incompletePath,
            "--subject",
            "fixture",
            "--session",
            "compact",
            "--context",
            "compact",
            "--compact");

        Assert.Equal(3, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains("every packet candidate", result.StandardError, StringComparison.Ordinal);
    }

    private static string CreateCompleteAdjustment(
        JsonObject packet,
        decimal firstFactor,
        bool takeOnlyFirst = false)
    {
        JsonArray adjustments = [];
        JsonArray candidates = packet["candidates"]!.AsArray();
        int count = takeOnlyFirst ? 1 : candidates.Count;
        for (int index = 0; index < count; index++)
        {
            JsonObject candidate = candidates[index]!.AsObject();
            JsonObject capability = candidate["capability"]!.AsObject();
            JsonObject adjustment = new()
            {
                ["targetId"] = capability["id"]!.GetValue<string>(),
                ["decision"] = index == 0 && firstFactor != 1m ? "replace" : "affirm",
                ["originalHours"] = capability["hours"]!.DeepClone(),
                ["evidenceIds"] = new JsonArray(candidate["evidenceIds"]![0]!.GetValue<string>()),
                ["reason"] = "Fixture review decision.",
            };
            if (index == 0 && firstFactor != 1m)
            {
                JsonObject hours = capability["hours"]!.AsObject();
                adjustment["replacement"] = new JsonObject
                {
                    ["category"] = capability["category"]!.GetValue<string>(),
                    ["hours"] = new JsonObject
                    {
                        ["low"] = Scale(hours["low"]!.GetValue<decimal>(), firstFactor),
                        ["expected"] = Scale(hours["expected"]!.GetValue<decimal>(), firstFactor),
                        ["high"] = Scale(hours["high"]!.GetValue<decimal>(), firstFactor),
                    },
                    ["confidence"] = capability["confidence"]!.GetValue<decimal>(),
                    ["assumptions"] = candidate["assumptions"]!.DeepClone(),
                    ["exclusions"] = candidate["exclusions"]!.DeepClone(),
                    ["uncertaintyReasons"] = candidate["uncertaintyReasons"]!.DeepClone(),
                };
            }

            adjustments.Add(adjustment);
        }

        return new JsonObject
        {
            ["schemaVersion"] = "1.0.0",
            ["protocolVersion"] = "host-review/1.0.0",
            ["inputDigest"] = packet["inputDigest"]!.GetValue<string>(),
            ["reviewerModel"] = packet["reviewerModel"]!.DeepClone(),
            ["adjustments"] = adjustments,
            ["notes"] = new JsonArray(),
        }.ToJsonString();
    }

    private static decimal Scale(decimal value, decimal factor) =>
        decimal.Round(value * factor, 4, MidpointRounding.AwayFromZero);

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

    private sealed class MeasurementRepository : IDisposable
    {
        public MeasurementRepository()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "efforthours-host-review-measurement-e2e",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public string WriteText(string relativePath, string content)
        {
            string path = Path.Combine(RootPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose() => Directory.Delete(RootPath, recursive: true);
    }
}
