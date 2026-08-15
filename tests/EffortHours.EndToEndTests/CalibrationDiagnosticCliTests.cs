using System.Diagnostics;
using System.Text;
using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed class CalibrationDiagnosticCliTests
{
    [Fact]
    public async Task UncertaintyFeaturesProjectsDigestMatchedSavedArtifactsOffline()
    {
        string root = FindRepositoryRoot();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "evidence",
            "minimal.repository-evidence.json");
        RepositoryEvidence source = ContractJson.Deserialize<RepositoryEvidence>(
            await File.ReadAllTextAsync(fixturePath));
        RepositoryEvidence evidence = source with
        {
            Repository = source.Repository with
            {
                SourceDigest = "sha256:" + new string('b', 64),
            },
        };
        using TemporaryDirectory temporary = new();
        string evidencePath = temporary.Write("evidence.json", ContractJson.Serialize(evidence));
        ProcessResult estimateResult = await RunCliAsync(
            root,
            "estimate",
            evidencePath,
            "--no-rate",
            "--compact");
        Assert.Equal(0, estimateResult.ExitCode);
        string estimatePath = temporary.Write("estimate.json", estimateResult.StandardOutput);

        ProcessResult result = await RunCliAsync(
            root,
            "calibration",
            "uncertainty-features",
            estimatePath,
            evidencePath,
            "--compact");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        CalibrationUncertaintyFeatureReport report =
            ContractJson.Deserialize<CalibrationUncertaintyFeatureReport>(result.StandardOutput);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintyFeatures,
            result.StandardOutput);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(report));
        Assert.Equal(evidence.Repository.SourceDigest, report.RepositorySourceDigest);
        Assert.Equal("repository-uncertainty-features/1.0.0", report.FeatureContract.Version);
        Assert.NotEmpty(report.WorkItems);
    }

    [Fact]
    public async Task DiagnoseWritesSchemaValidExactlyReconciledJsonOffline()
    {
        string root = FindRepositoryRoot();
        string estimatePath = Path.Combine(
            root,
            "calibration",
            "mutations",
            "public-synthetic",
            "estimates",
            "seed-rules-0.4.0",
            "terraform-resource-type.estimate.json");
        EstimateReport estimate = ContractJson.Deserialize<EstimateReport>(
            await File.ReadAllTextAsync(estimatePath));
        CalibrationCorpus corpus = CreateCorpus(estimate);
        using TemporaryDirectory temporary = new();
        string corpusPath = temporary.Write("corpus.json", ContractJson.Serialize(corpus));

        ProcessResult result = await RunCliAsync(
            root,
            "calibration",
            "diagnose",
            corpusPath,
            estimatePath,
            "--partition",
            "development",
            "--compact");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        CalibrationDiagnosticReport report =
            ContractJson.Deserialize<CalibrationDiagnosticReport>(result.StandardOutput);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationDiagnostic,
            result.StandardOutput);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(report));
        Assert.Equal(estimate.WorkItems.Count, Assert.Single(report.Repositories).Components.Count);
        Assert.Equal(0m, report.Summary.Range.ExpectedAbsoluteErrorHours);
        Assert.Equal(0m, report.Summary.RepositoryReconciliationDelta.Expected);
    }

    [Fact]
    public async Task UncertaintyEvaluateWritesDevelopmentOnlyRepositoryHeldOutReport()
    {
        string root = FindRepositoryRoot();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "evidence",
            "minimal.repository-evidence.json");
        RepositoryEvidence source = ContractJson.Deserialize<RepositoryEvidence>(
            await File.ReadAllTextAsync(fixturePath));
        using TemporaryDirectory temporary = new();
        List<string> featurePaths = [];
        List<CalibrationUncertaintyFeatureReport> featureReports = [];
        for (int index = 0; index < 3; index++)
        {
            RepositoryEvidence evidence = source with
            {
                Repository = source.Repository with
                {
                    SourceDigest = "sha256:" + new string((char)('d' + index), 64),
                },
            };
            string evidencePath = temporary.Write(
                $"evidence-{index}.json",
                ContractJson.Serialize(evidence));
            ProcessResult estimate = await RunCliAsync(
                root,
                "estimate",
                evidencePath,
                "--no-rate",
                "--compact");
            Assert.Equal(0, estimate.ExitCode);
            string estimatePath = temporary.Write($"estimate-{index}.json", estimate.StandardOutput);
            ProcessResult projection = await RunCliAsync(
                root,
                "calibration",
                "uncertainty-features",
                estimatePath,
                evidencePath,
                "--compact");
            Assert.Equal(0, projection.ExitCode);
            string featurePath = temporary.Write($"features-{index}.json", projection.StandardOutput);
            featurePaths.Add(featurePath);
            featureReports.Add(
                ContractJson.Deserialize<CalibrationUncertaintyFeatureReport>(
                    projection.StandardOutput));
        }

        CalibrationCorpus corpus = CreateUncertaintyCorpus(featureReports);
        string corpusPath = temporary.Write("corpus.json", ContractJson.Serialize(corpus));
        List<string> arguments = ["calibration", "uncertainty-evaluate", corpusPath];
        arguments.AddRange(featurePaths);
        arguments.Add("--compact");

        ProcessResult result = await RunCliAsync(root, [.. arguments]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        CalibrationUncertaintyEvaluationReport report =
            ContractJson.Deserialize<CalibrationUncertaintyEvaluationReport>(
                result.StandardOutput);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintyEvaluation,
            result.StandardOutput);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(report));
        Assert.Equal(3, report.Summary.RepositoryCount);
        Assert.Equal(CalibrationPartition.Development, report.Partition);
        Assert.True(report.Protocol.RepositoryIsolated);
        Assert.False(report.Protocol.FitsProductionModel);
    }

    private static CalibrationCorpus CreateCorpus(EstimateReport estimate) => new()
    {
        Id = "diagnostic-cli-fixture",
        Version = "1.0.0",
        Description = "Process-level calibration diagnostic fixture.",
        Rubric = new CalibrationRubricReference
        {
            Id = "ehe-work-item",
            Version = "1.1.0",
        },
        Records =
        [
            new CalibrationRecord
            {
                Id = "record:diagnostic-cli",
                Repository = new CalibrationRepositoryReference
                {
                    Id = "repository:diagnostic-cli",
                    Name = estimate.Repository.Name,
                    SourceDigest = estimate.Repository.SourceDigest!,
                },
                Profile = estimate.Profile,
                BaselineId = estimate.Baseline.Id,
                Partition = CalibrationPartition.Development,
                SourceEstimatorVersion = estimate.EstimatorVersion,
                SourceEstimateDigest = CalibrationDigest.Compute(estimate),
                Source = new CalibrationSourceProvenance
                {
                    DataClassification = CalibrationDataClassification.Synthetic,
                    SourceReference = "eh://e2e/calibration-diagnostic",
                    Revision = "1",
                    LicenseExpression = "MIT",
                    RedistributionAllowed = true,
                },
                Review = new CalibrationReviewProvenance
                {
                    Status = CalibrationReviewStatus.TeacherEstimate,
                    CompletedOn = new DateOnly(2026, 8, 15),
                    Reviewers =
                    [
                        new CalibrationReviewer
                        {
                            Id = "host-ai:e2e-diagnostic-teacher",
                            Kind = CalibrationReviewerKind.HostAi,
                            Role = CalibrationReviewerRole.Teacher,
                            ModelId = "test-model",
                            ModelVersion = "test-version",
                        },
                    ],
                },
                Targets = [.. estimate.WorkItems
                    .OrderBy(item => item.Id, StringComparer.Ordinal)
                    .Select(item => new CalibrationTarget
                    {
                        Id = $"target:{item.Id}",
                        Category = item.Category,
                        Title = item.Title,
                        Scope = item.Scope,
                        SourceWorkItemIds = [item.Id],
                        EvidenceIds = item.EvidenceIds,
                        Hours = item.Hours,
                        Rationale = "The process fixture accepts the source work-item range.",
                        UncertaintyReasons = item.UncertaintyReasons,
                    })],
            },
        ],
    };

    private static CalibrationCorpus CreateUncertaintyCorpus(
        IReadOnlyList<CalibrationUncertaintyFeatureReport> reports) => new()
        {
            Id = "uncertainty-evaluation-cli-fixture",
            Version = "1.0.0",
            Description = "Process-level uncertainty feature evaluation fixture.",
            Rubric = new CalibrationRubricReference
            {
                Id = "ehe-work-item",
                Version = "1.1.0",
            },
            Records = [.. reports.Select((report, index) => new CalibrationRecord
        {
            Id = $"record:uncertainty-cli-{index}",
            Repository = new CalibrationRepositoryReference
            {
                Id = $"repository:uncertainty-cli-{index}",
                Name = $"Synthetic uncertainty CLI repository {index}",
                SourceDigest = report.RepositorySourceDigest,
            },
            Profile = report.Profile,
            BaselineId = report.BaselineId,
            Partition = CalibrationPartition.Development,
            SourceEstimatorVersion = report.EstimatorVersion,
            SourceEstimateDigest = report.EstimateDigest,
            Source = new CalibrationSourceProvenance
            {
                DataClassification = CalibrationDataClassification.Synthetic,
                SourceReference = $"eh://e2e/uncertainty-evaluation/{index}",
                Revision = "1",
                LicenseExpression = "MIT",
                RedistributionAllowed = true,
            },
            Review = new CalibrationReviewProvenance
            {
                Status = CalibrationReviewStatus.TeacherEstimate,
                CompletedOn = new DateOnly(2026, 8, 15),
                Reviewers =
                [
                    new CalibrationReviewer
                    {
                        Id = "host-ai:e2e-uncertainty-teacher",
                        Kind = CalibrationReviewerKind.HostAi,
                        Role = CalibrationReviewerRole.Teacher,
                        ModelId = "test-model",
                        ModelVersion = "test-version",
                    },
                ],
            },
            Targets = [.. report.WorkItems.Select(item => new CalibrationTarget
            {
                Id = $"target:{item.WorkItemId}",
                Category = item.Category,
                Title = "Synthetic uncertainty CLI target",
                Scope = "synthetic-cli-fixture",
                SourceWorkItemIds = [item.WorkItemId],
                EvidenceIds = item.ResolvedEvidenceIds.Count > 0
                    ? item.ResolvedEvidenceIds
                    : ["evidence:synthetic"],
                Hours = item.SourceRange,
                Rationale = "The process fixture accepts the projected source range.",
                SizeException = "Synthetic fixture retains the projected source partition.",
            })],
        })],
        };

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
            RootPath = Path.Combine(
                Path.GetTempPath(),
                $"efforthours-calibration-diagnostic-{Guid.NewGuid():N}");
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
