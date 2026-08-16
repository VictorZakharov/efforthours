using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed class LogicalCandidateProjectionCliTests
{
    private const string ModelRelativePath =
        "calibration/corpora/public-readiness/0.5.0.logical-capability-model.json";

    [Fact]
    public async Task CandidateProjectIsDeterministicAndKeepsDiagnosticsOnStandardError()
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "evidence",
            "minimal-dotnet.repository-evidence.json");
        ProcessResult seed = await RunCliAsync(root, "estimate", evidence, "--no-rate");
        Assert.Equal(0, seed.ExitCode);
        string estimate = Path.Combine(Path.GetTempPath(), $"eh-candidate-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(estimate, seed.StandardOutput);
            string model = Path.Combine(root, ModelRelativePath);
            string digest = Digest(await File.ReadAllTextAsync(model));
            ProcessResult first = await RunCalibrationAsync(
                root,
                "candidate-project",
                "--estimate", estimate,
                "--evidence", evidence,
                "--model", model,
                "--expected-model-digest", digest,
                "--primary-stratum", "dotnet");
            ProcessResult second = await RunCalibrationAsync(
                root,
                "candidate-project",
                "--estimate", estimate,
                "--evidence", evidence,
                "--model", model,
                "--expected-model-digest", digest,
                "--primary-stratum", "dotnet");
            ProcessResult outside = await RunCalibrationAsync(
                root,
                "candidate-project",
                "--estimate", estimate,
                "--evidence", evidence,
                "--model", model,
                "--expected-model-digest", digest,
                "--primary-stratum", "python");

            Assert.Equal(0, first.ExitCode);
            Assert.Equal(string.Empty, first.StandardError);
            Assert.Equal(first.StandardOutput, second.StandardOutput);
            Assert.Equal(0, outside.ExitCode);
            Assert.Equal(seed.StandardOutput, outside.StandardOutput);
            Assert.Contains("seed fallback", outside.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(estimate);
        }
    }

    [Fact]
    public async Task CandidateProjectRejectsAModelDigestMismatchWithoutOutput()
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "evidence",
            "minimal-dotnet.repository-evidence.json");
        ProcessResult seed = await RunCliAsync(root, "estimate", evidence, "--no-rate");
        string estimate = Path.Combine(Path.GetTempPath(), $"eh-candidate-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(estimate, seed.StandardOutput);
            ProcessResult result = await RunCalibrationAsync(
                root,
                "candidate-project",
                "--estimate", estimate,
                "--evidence", evidence,
                "--model", Path.Combine(root, ModelRelativePath),
                "--expected-model-digest", $"sha256:{new string('0', 64)}",
                "--primary-stratum", "dotnet");

            Assert.Equal(2, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Contains("model digest differs", result.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(estimate);
        }
    }

    [Fact]
    public async Task CandidateProjectCanWriteAnExplicitOutputArtifact()
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "evidence",
            "minimal-dotnet.repository-evidence.json");
        ProcessResult seed = await RunCliAsync(root, "estimate", evidence, "--no-rate");
        string estimate = Path.Combine(Path.GetTempPath(), $"eh-candidate-source-{Guid.NewGuid():N}.json");
        string output = Path.Combine(Path.GetTempPath(), $"eh-candidate-output-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(estimate, seed.StandardOutput);
            string model = Path.Combine(root, ModelRelativePath);
            string modelDigest = Digest(await File.ReadAllTextAsync(model));
            ProcessResult expected = await RunCalibrationAsync(
                root,
                "candidate-project",
                "--estimate", estimate,
                "--evidence", evidence,
                "--model", model,
                "--expected-model-digest", modelDigest,
                "--primary-stratum", "dotnet");
            ProcessResult result = await RunCalibrationAsync(
                root,
                "candidate-project",
                "--estimate", estimate,
                "--evidence", evidence,
                "--model", model,
                "--expected-model-digest", modelDigest,
                "--primary-stratum", "dotnet",
                "--output", output);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
            Assert.Equal(0, expected.ExitCode);
            Assert.Equal(expected.StandardOutput, Normalize(await File.ReadAllTextAsync(output)));
        }
        finally
        {
            File.Delete(estimate);
            File.Delete(output);
        }
    }

    [Fact]
    public async Task ManualQaCandidateProjectIsDeterministicAndDependencyLinked()
    {
        string root = FindRepositoryRoot();
        string evidence = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "evidence",
            "minimal-dotnet.repository-evidence.json");
        ProcessResult seed = await RunCliAsync(root, "estimate", evidence, "--no-rate");
        Assert.Equal(0, seed.ExitCode);
        string estimate = Path.Combine(Path.GetTempPath(), $"eh-manual-qa-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(estimate, seed.StandardOutput);
            string policy = Path.Combine(
                root,
                "calibration",
                "corpora",
                "public-readiness",
                "1.9.0",
                "manual-qa-coding-ratio-policy.json");
            string digest = Digest(await File.ReadAllTextAsync(policy));
            ProcessResult first = await RunCalibrationAsync(
                root,
                "manual-qa-candidate-project",
                "--estimate", estimate,
                "--policy", policy,
                "--expected-policy-digest", digest);
            ProcessResult second = await RunCalibrationAsync(
                root,
                "manual-qa-candidate-project",
                "--estimate", estimate,
                "--policy", policy,
                "--expected-policy-digest", digest);

            Assert.Equal(0, first.ExitCode);
            Assert.Equal(string.Empty, first.StandardError);
            Assert.Equal(first.StandardOutput, second.StandardOutput);
            EstimateReport candidate = ContractJson.Deserialize<EstimateReport>(first.StandardOutput);
            WorkItem[] manualQa =
            [
                .. candidate.WorkItems.Where(item =>
                    item.Category == EffortCategory.ManualValidationDebuggingAndHardening),
            ];
            Assert.NotEmpty(manualQa);
            Assert.All(manualQa, item => Assert.Single(item.DependencyIds));
            Assert.Empty(ContractValidation.Validate(candidate));
        }
        finally
        {
            File.Delete(estimate);
        }
    }

    [Fact]
    public async Task ManualQaReviewFreezeCliReproducesTheFrozenPacketSet()
    {
        string root = FindRepositoryRoot();
        string checkpoint = Path.Combine(
            root,
            "calibration",
            "corpora",
            "public-readiness",
            "2.0.0");
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"eh-manual-qa-review-cli-{Guid.NewGuid():N}");
        string packets = Path.Combine(temporary, "packets");
        string manifest = Path.Combine(temporary, "manifest.json");
        try
        {
            string policy = Path.Combine(checkpoint, "manual-qa-review-policy.json");
            ProcessResult result = await RunCalibrationAsync(
                root,
                "manual-qa-review-freeze",
                "--corpus", Path.Combine(
                    root,
                    "calibration",
                    "corpora",
                    "public-readiness",
                    "0.3.0.development-corpus.json"),
                "--policy", policy,
                "--expected-policy-digest", Digest(await File.ReadAllTextAsync(policy)),
                "--packets", packets,
                "--manifest", manifest);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
            ManualQaReviewManifest frozen = ContractJson.Deserialize<ManualQaReviewManifest>(
                await File.ReadAllTextAsync(manifest));
            Assert.Equal(15, frozen.RecordCount);
            Assert.Equal(955, frozen.TargetCount);
            Assert.Equal(15, Directory.GetFiles(packets, "*.json").Length);
            Assert.Equal(
                await File.ReadAllTextAsync(Path.Combine(
                    checkpoint,
                    "manual-qa-review-manifest.json")),
                await File.ReadAllTextAsync(manifest));
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, recursive: true);
            }
        }
    }

    private static Task<ProcessResult> RunCliAsync(string root, params string[] arguments) =>
        RunAsync(root, "src/EffortHours.Cli", "efforthours.dll", arguments);

    private static Task<ProcessResult> RunCalibrationAsync(
        string root,
        params string[] arguments) => RunAsync(
            root,
            "tools/EffortHours.RepositoryCalibration",
            "EffortHours.RepositoryCalibration.dll",
            arguments);

    private static async Task<ProcessResult> RunAsync(
        string root,
        string project,
        string assemblyName,
        IReadOnlyList<string> arguments)
    {
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine the test build configuration.");
        string assembly = Path.Combine(
            root,
            project,
            "bin",
            configuration,
            "net10.0",
            assemblyName);
        Assert.True(File.Exists(assembly), $"Assembly was not built: {assembly}");
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.ArgumentList.Add(assembly);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the projection process.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        return new ProcessResult(
            process.ExitCode,
            Normalize(await stdout),
            Normalize(await stderr));
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    private static string Digest(string content)
    {
        string normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant()}";
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
