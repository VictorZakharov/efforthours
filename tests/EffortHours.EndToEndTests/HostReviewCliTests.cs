using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EffortHours.EndToEndTests;

public sealed class HostReviewCliTests
{
    [Fact]
    public async Task PacketAndAllQueryKindsAreDeterministicDigestBoundAndExplicit()
    {
        using ReviewRepository repository = new();
        repository.WriteText(
            "Demo.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
        repository.WriteText(
            "Program.cs",
            "namespace Demo;\npublic static class Program\n{\n    public static int Main() => 0;\n}\n");
        repository.WriteText("README.md", "# Demo\n\nA deterministic host-review fixture.\n");
        repository.WriteText(".gitignore", ".secret\n");
        repository.WriteText(".secret", "do-not-disclose\n");

        ProcessResult first = await RunCliAsync(
            "review",
            "packet",
            repository.RootPath,
            "--model",
            "fixture-model",
            "--provider",
            "fixture-provider",
            "--compact");
        ProcessResult second = await RunCliAsync(
            "review",
            "packet",
            repository.RootPath,
            "--model",
            "fixture-model",
            "--provider",
            "fixture-provider",
            "--compact");

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(string.Empty, first.StandardError);
        Assert.Equal(first.StandardOutput, second.StandardOutput);
        using JsonDocument packet = JsonDocument.Parse(first.StandardOutput);
        JsonElement root = packet.RootElement;
        string digest = root.GetProperty("inputDigest").GetString()!;
        JsonElement candidate = root.GetProperty("candidates")[0];
        string capabilityId = candidate.GetProperty("capability").GetProperty("id").GetString()!;
        string evidenceId = candidate.GetProperty("evidenceIds")[0].GetString()!;
        string scope = candidate.GetProperty("capability").GetProperty("scope").GetString()!;
        Assert.False(root.GetProperty("pricingIncluded").GetBoolean());
        Assert.True(root.GetProperty("localBaselineComplete").GetBoolean());
        Assert.Equal("fixture-model", root.GetProperty("reviewerModel").GetProperty("model").GetString());

        ProcessResult capability = await QueryAsync(
            repository.RootPath,
            digest,
            "--capability",
            capabilityId);
        ProcessResult evidence = await QueryAsync(
            repository.RootPath,
            digest,
            "--evidence",
            evidenceId);
        ProcessResult scopeResult = await QueryAsync(
            repository.RootPath,
            digest,
            "--scope",
            scope,
            "--limit",
            "1");
        ProcessResult source = await QueryAsync(
            repository.RootPath,
            digest,
            "--source",
            "Program.cs",
            "--start-line",
            "2",
            "--line-count",
            "2");

        AssertSuccessfulMetadataQuery(capability, "capability");
        AssertSuccessfulMetadataQuery(evidence, "evidence");
        AssertSuccessfulMetadataQuery(scopeResult, "scope");
        Assert.Equal(0, source.ExitCode);
        Assert.Equal(string.Empty, source.StandardError);
        Assert.DoesNotContain(repository.RootPath, source.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("do-not-disclose", source.StandardOutput, StringComparison.Ordinal);
        using JsonDocument sourceDocument = JsonDocument.Parse(source.StandardOutput);
        Assert.True(sourceDocument.RootElement.GetProperty("containsSourceExcerpt").GetBoolean());
        JsonElement excerpt = sourceDocument.RootElement.GetProperty("sourceExcerpt");
        Assert.Equal("Program.cs", excerpt.GetProperty("path").GetString());
        Assert.Equal(2, excerpt.GetProperty("lines").GetArrayLength());

        ProcessResult ignored = await QueryAsync(
            repository.RootPath,
            digest,
            "--source",
            ".secret");
        Assert.Equal(3, ignored.ExitCode);
        Assert.Equal(string.Empty, ignored.StandardOutput);
        Assert.Contains("not an admitted scanner file", ignored.StandardError, StringComparison.Ordinal);

        ProcessResult stale = await QueryAsync(
            repository.RootPath,
            "sha256:" + new string('0', 64),
            "--capability",
            capabilityId);
        Assert.Equal(3, stale.ExitCode);
        Assert.Equal(string.Empty, stale.StandardOutput);
        Assert.Contains("does not match", stale.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdjustmentValidationIsStructuredAndNeverAppliesChanges()
    {
        using ReviewRepository repository = new();
        repository.WriteText(
            "Demo.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
        repository.WriteText("Program.cs", "namespace Demo; public static class Program { }\n");
        ProcessResult packetResult = await RunCliAsync(
            "review",
            "packet",
            repository.RootPath,
            "--compact");
        Assert.Equal(0, packetResult.ExitCode);

        JsonObject packet = JsonNode.Parse(packetResult.StandardOutput)!.AsObject();
        JsonObject candidate = packet["candidates"]![0]!.AsObject();
        string packetPath = repository.WriteText("review-packet.json", packetResult.StandardOutput);
        string validPath = repository.WriteText(
            "valid-adjustment.json",
            CreateAdjustment(candidate, packet["inputDigest"]!.GetValue<string>()));
        ProcessResult valid = await RunCliAsync(
            "review",
            "validate",
            packetPath,
            validPath,
            "--compact");

        Assert.Equal(0, valid.ExitCode);
        Assert.Equal(string.Empty, valid.StandardError);
        using JsonDocument validDocument = JsonDocument.Parse(valid.StandardOutput);
        Assert.True(validDocument.RootElement.GetProperty("isValid").GetBoolean());
        Assert.False(validDocument.RootElement.GetProperty("adjustmentsApplied").GetBoolean());

        string stalePath = repository.WriteText(
            "stale-adjustment.json",
            CreateAdjustment(candidate, "sha256:" + new string('0', 64)));
        ProcessResult stale = await RunCliAsync(
            "review",
            "validate",
            packetPath,
            stalePath,
            "--compact");

        Assert.Equal(3, stale.ExitCode);
        Assert.Contains("validation failed", stale.StandardError, StringComparison.Ordinal);
        using JsonDocument staleDocument = JsonDocument.Parse(stale.StandardOutput);
        Assert.False(staleDocument.RootElement.GetProperty("isValid").GetBoolean());
        Assert.False(staleDocument.RootElement.GetProperty("adjustmentsApplied").GetBoolean());
        Assert.Contains(
            staleDocument.RootElement.GetProperty("errors").EnumerateArray(),
            error => error.GetString()!.Contains("input digest", StringComparison.Ordinal));
    }

    private static string CreateAdjustment(
        JsonObject candidate,
        string inputDigest)
    {
        JsonObject capability = candidate["capability"]!.AsObject();
        JsonNode originalHours = capability["hours"]!.DeepClone();
        string evidenceId = candidate["evidenceIds"]![0]!.GetValue<string>();
        return new JsonObject
        {
            ["schemaVersion"] = "1.0.0",
            ["protocolVersion"] = "host-review/1.0.0",
            ["inputDigest"] = inputDigest,
            ["reviewerModel"] = new JsonObject
            {
                ["isAvailable"] = true,
                ["model"] = "fixture-reviewer",
            },
            ["adjustments"] = new JsonArray
            {
                new JsonObject
                {
                    ["targetId"] = capability["id"]!.GetValue<string>(),
                    ["decision"] = "affirm",
                    ["originalHours"] = originalHours,
                    ["evidenceIds"] = new JsonArray(evidenceId),
                    ["reason"] = "The cited evidence supports the local range.",
                },
            },
            ["notes"] = new JsonArray(),
        }.ToJsonString();
    }

    private static async Task<ProcessResult> QueryAsync(
        string repositoryPath,
        string digest,
        params string[] selectorArguments)
    {
        List<string> arguments =
        [
            "review",
            "query",
            repositoryPath,
            "--input-digest",
            digest,
            .. selectorArguments,
            "--reason",
            "Resolve one bounded uncertainty.",
            "--compact",
        ];
        return await RunCliAsync([.. arguments]);
    }

    private static void AssertSuccessfulMetadataQuery(ProcessResult result, string expectedKind)
    {
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal(
            expectedKind,
            document.RootElement.GetProperty("query").GetProperty("kind").GetString());
        Assert.False(document.RootElement.GetProperty("containsSourceExcerpt").GetBoolean());
        Assert.False(document.RootElement.TryGetProperty("sourceExcerpt", out _));
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
            ?? throw new InvalidOperationException("Could not start the EffortHours CLI process.");
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

    private sealed class ReviewRepository : IDisposable
    {
        public ReviewRepository()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "efforthours-host-review-e2e",
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
