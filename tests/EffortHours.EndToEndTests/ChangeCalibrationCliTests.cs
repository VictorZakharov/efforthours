using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EffortHours.Analysis;
using EffortHours.Calibration;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed class ChangeCalibrationCliTests
{
    [Fact]
    public async Task RepositoryScaffoldRedirectsChangeEstimateToDedicatedWorkflow()
    {
        ChangeEstimateReport estimate = await CreateEstimateAsync();
        using TemporaryDirectory directory = new();
        string estimatePath = directory.Write("change-estimate.json", ContractJson.Serialize(estimate));

        ProcessResult scaffold = await RunCliAsync(
            "calibration",
            "scaffold",
            estimatePath,
            "--compact");

        Assert.Equal(3, scaffold.ExitCode);
        Assert.Equal(string.Empty, scaffold.StandardOutput);
        Assert.Contains("eh calibration change-scaffold", scaffold.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("All values fail", scaffold.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangeCalibrationCommandsRoundTripExplicitFilesOffline()
    {
        ChangeEstimateReport estimate = await CreateEstimateAsync();
        using TemporaryDirectory directory = new();
        string estimatePath = directory.Write("change-estimate.json", ContractJson.Serialize(estimate));

        ProcessResult scaffold = await RunCliAsync(
            "calibration",
            "change-scaffold",
            estimatePath,
            "--repository-family",
            "repository:e2e-change-family",
            "--case",
            "change:e2e-code-and-test",
            "--tag",
            "production",
            "--tag",
            "tests",
            "--blind",
            "--compact");

        Assert.Equal(0, scaffold.ExitCode);
        Assert.Equal(string.Empty, scaffold.StandardError);
        CalibrationAuthoringPacket packet = ContractJson.Deserialize<CalibrationAuthoringPacket>(
            scaffold.StandardOutput);
        Assert.NotNull(packet.Change);
        Assert.Null(packet.Candidate.TotalHours);
        Assert.All(packet.Targets, target => Assert.Null(target.Candidate.Hours));

        CalibrationReviewPlan plan = CreatePlan(packet, estimate);
        string planPath = directory.Write("review-plan.json", ContractJson.Serialize(plan));
        ProcessResult compile = await RunCliAsync(
            "calibration",
            "change-compile",
            planPath,
            estimatePath,
            "--compact");

        Assert.Equal(0, compile.ExitCode);
        Assert.Equal(string.Empty, compile.StandardError);
        CalibrationCorpus corpus = ContractJson.Deserialize<CalibrationCorpus>(compile.StandardOutput);
        Assert.NotNull(Assert.Single(corpus.Records).Change);
        string corpusPath = directory.Write("corpus.json", compile.StandardOutput);

        ProcessResult validate = await RunCliAsync(
            "calibration",
            "validate",
            corpusPath,
            "--compact");
        ProcessResult evaluate = await RunCliAsync(
            "calibration",
            "change-evaluate",
            corpusPath,
            estimatePath,
            "--partition",
            "development",
            "--compact");

        Assert.Equal(0, validate.ExitCode);
        Assert.Equal(string.Empty, validate.StandardError);
        Assert.Equal(0, evaluate.ExitCode);
        Assert.Equal(string.Empty, evaluate.StandardError);
        using JsonDocument evaluation = JsonDocument.Parse(evaluate.StandardOutput);
        Assert.Equal(
            "change-calibration-evaluator/0.1.0",
            evaluation.RootElement.GetProperty("evaluatorVersion").GetString());
        Assert.Equal(
            0m,
            evaluation.RootElement
                .GetProperty("repositoryTotals")
                .GetProperty("expected")
                .GetProperty("weightedAbsolutePercentageError")
                .GetDecimal());
    }

    private static CalibrationReviewPlan CreatePlan(
        CalibrationAuthoringPacket packet,
        ChangeEstimateReport estimate) => new()
        {
            CompilerVersion = ChangeCalibrationReviewCompiler.CompilerVersion,
            Id = "e2e-change-corpus",
            Version = "0.1.0",
            Description = "Process-level Change calibration fixture.",
            Rubric = packet.Rubric,
            Records =
            [
                new CalibrationReviewPlanRecord
                {
                    Id = "record:e2e-change",
                    Repository = packet.Repository,
                    Change = packet.Change,
                    Profile = packet.Profile,
                    BaselineId = packet.BaselineId,
                    Partition = CalibrationPartition.Development,
                    SourceEstimatorVersion = estimate.EstimatorVersion,
                    SourceEstimateDigest = CalibrationDigest.Compute(estimate),
                    Source = new CalibrationSourceProvenance
                    {
                        DataClassification = CalibrationDataClassification.Synthetic,
                        SourceReference = "eh://e2e/change-calibration",
                        Revision = $"{estimate.Selection.Base.ObjectId}..{estimate.Selection.Head.ObjectId}",
                        LicenseExpression = "MIT",
                        RedistributionAllowed = true,
                    },
                    Review = new CalibrationReviewProvenance
                    {
                        Status = CalibrationReviewStatus.TeacherEstimate,
                        CompletedOn = new DateOnly(2026, 8, 6),
                        Reviewers =
                        [
                            new CalibrationReviewer
                            {
                                Id = "host-ai:e2e-teacher",
                                Kind = CalibrationReviewerKind.HostAi,
                                Role = CalibrationReviewerRole.Teacher,
                                ModelId = "test-model",
                                ModelVersion = "test-version",
                            },
                        ],
                    },
                    Capabilities = [.. estimate.WorkItems
                        .GroupBy(item => CalibrationAuthoring.GetSourceCapabilityId(item.Id))
                        .OrderBy(group => group.Key, StringComparer.Ordinal)
                        .Select(group => new CalibrationCapabilityReviewDecision
                        {
                            SourceCapabilityId = group.Key,
                            Rationale = "Process fixture teacher decision.",
                            Targets = [.. group
                                .OrderBy(item => item.Id, StringComparer.Ordinal)
                                .Select(item => new CalibrationReviewTargetDecision
                                {
                                    Hours = item.Hours,
                                    UncertaintyReasons = item.UncertaintyReasons,
                                })],
                        })],
                },
            ],
        };

    private static async Task<ChangeEstimateReport> CreateEstimateAsync()
    {
        MemorySnapshot before = new(("src/status.ts", "export const status = 'ok';\n"));
        MemorySnapshot after = new(
            ("src/status.ts", "export const status = 'ready';\n"),
            ("test/status.test.ts", "test('status', () => expect(status).toBe('ready'));\n"));
        return await new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "e2e-change",
                Selection = new ChangeSelection
                {
                    Kind = ChangeSelectionKind.BaseHead,
                    Base = Reference("base", before.ObjectId),
                    Head = Reference("head", after.ObjectId),
                },
                OpenBaseAsync = before.Factory,
                OpenHeadAsync = after.Factory,
            },
            EstimationProfile.Implementation);
    }

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.Evidence,
    };

    private static async Task<ProcessResult> RunCliAsync(params string[] arguments)
    {
        string root = FindRepositoryRoot();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine test configuration.");
        string cli = Path.Combine(root, "src", "EffortHours.Cli", "bin", configuration, "net10.0", "efforthours.dll");
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.ArgumentList.Add(cli);
        foreach (string argument in arguments)
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
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EffortHours.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"efforthours-change-calibration-{Guid.NewGuid():N}");
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

    private sealed class MemorySnapshot : IChangeSnapshot
    {
        private readonly (string Path, string Content)[] _files;
        private readonly MemoryRepositoryFileSystem _repository = new();

        public MemorySnapshot(params (string Path, string Content)[] files)
        {
            _files = [.. files];
            foreach ((string path, string content) in _files)
            {
                _repository.WriteText(path, content);
            }

            Files = [.. _files.OrderBy(file => file.Path, StringComparer.Ordinal).Select(file =>
            {
                byte[] bytes = Encoding.UTF8.GetBytes(file.Content);
                return new ChangeSnapshotFile
                {
                    Path = file.Path,
                    ObjectId = Digest(bytes),
                    Length = bytes.Length,
                    Mode = "100644",
                };
            })];
            ObjectId = Digest(Encoding.UTF8.GetBytes(string.Join('\n', Files.Select(file => file.ObjectId))));
        }

        public string ObjectId { get; }

        public string RootPath => _repository.RootPath;

        public IRepositoryFileSystem FileSystem => _repository;

        public IReadOnlyList<ChangeSnapshotFile> Files { get; }

        public Task<IChangeSnapshot> Factory(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IChangeSnapshot>(new MemorySnapshot(_files));
        }

        public ValueTask<byte[]> ReadAllBytesAsync(
            string relativePath,
            CancellationToken cancellationToken = default) =>
            _repository.ReadAllBytesAsync(Path.Combine(RootPath, relativePath), cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static string Digest(byte[] bytes) =>
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
