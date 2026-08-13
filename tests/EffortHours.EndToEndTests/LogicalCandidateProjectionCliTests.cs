using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

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
