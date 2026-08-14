using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EffortHours.RepositoryCalibration;

namespace EffortHours.EndToEndTests;

public sealed class CanonicalJsonOutputTests
{
    private const string ModelRelativePath =
        "calibration/corpora/public-readiness/0.7.0.logical-capability-model.json";

    [Fact]
    public async Task RepositoryJsonUsesSameBytesForStdoutSavedReportAndReprojection()
    {
        string root = FindRepositoryRoot();
        string evidence = Fixture("minimal-dotnet.repository-evidence.json");
        string savedPath = Path.Combine(Path.GetTempPath(), $"eh-canonical-{Guid.NewGuid():N}.json");
        try
        {
            ProcessResult direct = await RunCliAsync(root,
                "estimate", evidence, "--no-rate", "--format", "json");
            ProcessResult saved = await RunCliAsync(root,
                "estimate", evidence, "--no-rate", "--format", "json", "--output", savedPath);
            ProcessResult reprojected = await RunCliAsync(root,
                "report", savedPath, "--view", "full", "--format", "json");

            Assert.Equal(0, direct.ExitCode);
            Assert.Equal(0, saved.ExitCode);
            Assert.Equal(0, reprojected.ExitCode);
            Assert.Empty(direct.StandardError);
            Assert.Empty(saved.StandardError);
            Assert.Empty(reprojected.StandardError);
            Assert.Empty(saved.StandardOutput);
            AssertCanonicalJsonDocument(direct.StandardOutput);
            AssertCanonicalJsonDocument(reprojected.StandardOutput);

            byte[] savedBytes = await File.ReadAllBytesAsync(savedPath);
            Assert.Equal(direct.StandardOutput, savedBytes);
            Assert.Equal(direct.StandardOutput, reprojected.StandardOutput);
        }
        finally
        {
            File.Delete(savedPath);
        }
    }

    [Fact]
    public async Task CandidateBenchmarkProjectionHasPinnedCanonicalBytesForEveryMeasuredShape()
    {
        string root = FindRepositoryRoot();
        string workspace = Path.Combine(Path.GetTempPath(), $"eh-canonical-shapes-{Guid.NewGuid():N}");
        try
        {
            IReadOnlyList<CandidateMeasurementInput> inputs =
                await CandidateMeasurementInputBuilder.BuildAsync(
                    Fixture("minimal-dotnet.repository-evidence.json"),
                    workspace,
                    CancellationToken.None);
            string model = Path.Combine(root, ModelRelativePath);
            string calibrationAssembly = BuiltAssembly(
                root,
                "tools/EffortHours.RepositoryCalibration",
                "EffortHours.RepositoryCalibration.dll");
            List<string> actual = [];
            foreach (CandidateMeasurementInput input in inputs)
            {
                ProcessResult firstSeed = await RunCalibrationAsync(root,
                    "candidate-benchmark-project", "--evidence", input.Path, "--mode", "seed");
                ProcessResult firstCandidate = await RunCalibrationAsync(root,
                    "candidate-benchmark-project",
                    "--evidence", input.Path,
                    "--mode", "candidate",
                    "--model", model,
                    "--expected-model-digest", CandidateMeasurementRunner.ExpectedModelDigest);
                CandidateProjectionProcessResult secondSeed =
                    await CandidateMeasurementProcess.RunProjectionAsync(
                        calibrationAssembly,
                        input,
                        candidate: false,
                        modelPath: model,
                        modelDigest: CandidateMeasurementRunner.ExpectedModelDigest,
                        sequence: 2,
                        cancellationToken: CancellationToken.None);
                CandidateProjectionProcessResult secondCandidate =
                    await CandidateMeasurementProcess.RunProjectionAsync(
                        calibrationAssembly,
                        input,
                        candidate: true,
                        modelPath: model,
                        modelDigest: CandidateMeasurementRunner.ExpectedModelDigest,
                        sequence: 2,
                        cancellationToken: CancellationToken.None);

                AssertSuccessfulCanonical(firstSeed);
                AssertSuccessfulCanonical(firstCandidate);
                string seedDigest = Digest(firstSeed.StandardOutput);
                string candidateDigest = Digest(firstCandidate.StandardOutput);
                Assert.Equal(seedDigest, secondSeed.Run.OutputDigest);
                Assert.Equal(seedDigest, secondSeed.Run.LfNormalizedOutputDigest);
                Assert.Equal(candidateDigest, secondCandidate.Run.OutputDigest);
                Assert.Equal(candidateDigest, secondCandidate.Run.LfNormalizedOutputDigest);
                actual.Add($"{input.Id}:seed={seedDigest}");
                actual.Add($"{input.Id}:candidate={candidateDigest}");
            }

            Assert.Equal(
                [
                    "small:seed=sha256:40a1a711323772b3f4856c33ff62a4a646e6d01898097802bdd05a9ec247f6c5",
                    "small:candidate=sha256:5e2af841f5c255501f4a516f563a3b6a386245b164fd99e5396ea93874a90aa4",
                    "medium:seed=sha256:f3e86e2aa9706b3f852d53c1e8b0dfa0614b943959bb3b93a51d603d5fe4e291",
                    "medium:candidate=sha256:b43f0943c43b2ddcf8524fc0e9f48a2da90a70f3dde47bc18ba5a627cea33c6b",
                    "large:seed=sha256:76cfe5844cc462a98bcad1d54b19890e6ee3478de7e469f54ebf3989275d4c3c",
                    "large:candidate=sha256:a0385b34400575c356f107e38650719528728a8e860fcb8c586e7fa394ef91a8",
                ],
                actual);
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, recursive: true);
            }
        }
    }

    private static void AssertSuccessfulCanonical(ProcessResult result)
    {
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardError);
        AssertCanonicalJsonDocument(result.StandardOutput);
    }

    private static void AssertCanonicalJsonDocument(byte[] value)
    {
        Assert.NotEmpty(value);
        Assert.DoesNotContain((byte)'\r', value);
        Assert.Equal((byte)'\n', value[^1]);
        Assert.True(value.Length == 1 || value[^2] != (byte)'\n');
        Assert.False(value.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        _ = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(value);
        using JsonDocument document = JsonDocument.Parse(value);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }

    private static string Digest(byte[] value) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant()}";

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "evidence", name);

    private static Task<ProcessResult> RunCliAsync(string root, params string[] arguments) =>
        RunAsync(root, "src/EffortHours.Cli", "efforthours.dll", arguments);

    private static Task<ProcessResult> RunCalibrationAsync(string root, params string[] arguments) =>
        RunAsync(
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
        string assembly = BuiltAssembly(root, project, assemblyName);
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
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
            ?? throw new InvalidOperationException("Could not start the canonical-output process.");
        Task<byte[]> stdout = ReadToEndAsync(process.StandardOutput.BaseStream);
        Task<byte[]> stderr = ReadToEndAsync(process.StandardError.BaseStream);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private static string BuiltAssembly(string root, string project, string assemblyName)
    {
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine the test build configuration.");
        string assembly = Path.Combine(root, project, "bin", configuration, "net10.0", assemblyName);
        Assert.True(File.Exists(assembly), $"Assembly was not built: {assembly}");
        return assembly;
    }

    private static async Task<byte[]> ReadToEndAsync(Stream stream)
    {
        using MemoryStream buffer = new();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record ProcessResult(int ExitCode, byte[] StandardOutput, byte[] StandardError);
}
