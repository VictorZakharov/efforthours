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

    [Fact]
    public async Task EvaluatesGraphReportsThroughTheCli()
    {
        string root = FindRepositoryRoot();
        using TemporaryRepository temporary = new();
        List<string> featurePaths = [];
        List<CalibrationUncertaintyGraphFeatureReport> reports = [];
        for (int index = 0; index < 3; index++)
        {
            string repositoryPath = temporary.CreateDirectory($"repository-{index}");
            temporary.Write($"repository-{index}/A/A.csproj", Project("../B/B.csproj"));
            temporary.Write($"repository-{index}/B/B.csproj", Project("../A/A.csproj"));
            temporary.Write(
                $"repository-{index}/A/A.cs",
                $"public sealed class A{index} {{ public void Run() {{ }} }}\n");
            temporary.Write(
                $"repository-{index}/B/B.cs",
                $"public sealed class B{index} {{ public void Run() {{ }} }}\n");
            string evidencePath = Path.Combine(temporary.RootPath, $"evidence-{index}.json");
            string estimatePath = Path.Combine(temporary.RootPath, $"estimate-{index}.json");
            string featurePath = Path.Combine(temporary.RootPath, $"features-{index}.json");

            Assert.Equal(0, (await RunCliAsync(
                root,
                "scan",
                repositoryPath,
                "--output",
                evidencePath)).ExitCode);
            Assert.Equal(0, (await RunCliAsync(
                root,
                "estimate",
                evidencePath,
                "--no-rate",
                "--compact",
                "--output",
                estimatePath)).ExitCode);
            ProcessResult projection = await RunCliAsync(
                root,
                "calibration",
                "uncertainty-graph",
                estimatePath,
                evidencePath,
                "--compact",
                "--output",
                featurePath);
            Assert.Equal(0, projection.ExitCode);
            featurePaths.Add(featurePath);
            reports.Add(ContractJson.Deserialize<CalibrationUncertaintyGraphFeatureReport>(
                await File.ReadAllTextAsync(featurePath)));
        }

        string corpusPath = temporary.Write(
            "corpus.json",
            ContractJson.Serialize(CreateCorpus(reports)));
        List<string> arguments =
            ["calibration", "uncertainty-graph-evaluate", corpusPath, .. featurePaths];
        arguments.Add("--compact");

        ProcessResult result = await RunCliAsync(root, [.. arguments]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        CalibrationUncertaintyGraphEvaluationReport evaluation =
            ContractJson.Deserialize<CalibrationUncertaintyGraphEvaluationReport>(
                result.StandardOutput);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintyGraphEvaluation,
            result.StandardOutput);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(evaluation));
        Assert.Equal(3, evaluation.Summary.RepositoryCount);
        Assert.Equal(14, evaluation.Features.Count);
        Assert.False(evaluation.Protocol.FitsProductionModel);
    }

    private static CalibrationCorpus CreateCorpus(
        IReadOnlyList<CalibrationUncertaintyGraphFeatureReport> reports) => new()
        {
            Id = "graph-uncertainty-cli-fixture",
            Version = "1.0.0",
            Description = "Process-level graph uncertainty evaluation fixture.",
            Rubric = new CalibrationRubricReference
            {
                Id = "ehe-work-item",
                Version = "1.1.0",
            },
            Records = [.. reports.Select((report, index) => new CalibrationRecord
            {
                Id = $"record:graph-uncertainty-cli-{index}",
                Repository = new CalibrationRepositoryReference
                {
                    Id = $"repository:graph-uncertainty-cli-{index}",
                    Name = $"Graph uncertainty CLI fixture {index}",
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
                    SourceReference = $"eh://tests/graph-uncertainty-cli/{index}",
                    Revision = "1",
                    LicenseExpression = "MIT",
                    RedistributionAllowed = true,
                },
                Review = new CalibrationReviewProvenance
                {
                    Status = CalibrationReviewStatus.TeacherEstimate,
                    CompletedOn = new DateOnly(2026, 8, 16),
                    Reviewers =
                    [
                        new CalibrationReviewer
                        {
                            Id = "host-ai:graph-uncertainty-cli",
                            Kind = CalibrationReviewerKind.HostAi,
                            Role = CalibrationReviewerRole.Teacher,
                            ModelId = "synthetic-test-model",
                            ModelVersion = "1",
                        },
                    ],
                },
                Targets = [.. report.WorkItems.Select(item => new CalibrationTarget
                {
                    Id = $"target:{item.WorkItemId}",
                    Category = item.Category,
                    Title = "Graph uncertainty CLI target",
                    Scope = "synthetic-fixture",
                    SourceWorkItemIds = [item.WorkItemId],
                    EvidenceIds = item.ResolvedEvidenceIds.Count > 0
                        ? item.ResolvedEvidenceIds
                        : ["evidence:synthetic"],
                    Hours = Symmetric(item.ExpectedHours + 0.5m + index),
                    Rationale = "Synthetic process-level reviewed range.",
                    SizeException = "Synthetic process-level fixture.",
                })],
            })],
        };

    private static EffortRange Symmetric(decimal expected)
    {
        decimal halfWidth = decimal.Min(1m, expected);
        return new EffortRange
        {
            Low = expected - halfWidth,
            Expected = expected,
            High = expected + halfWidth,
        };
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

        public string CreateDirectory(string name)
        {
            string path = Path.Combine(RootPath, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public string Write(string name, string content)
        {
            string path = Path.Combine(RootPath, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        public void Dispose() => Directory.Delete(RootPath, recursive: true);
    }
}
