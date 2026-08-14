using System.Runtime.InteropServices;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class CandidateMeasurementRunner
{
    public const string MeasurementVersion = "repository-candidate-platform-measurement/1.0.0";
    public const string ExpectedModelDigest =
        "sha256:f53b5af09b5adf0d3efed5339e9309156f026b3378c6b69e979467b92524ae93";
    public const string ExpectedOperationalDigest =
        "sha256:609307d5a366b52c18118db2ef9f79e46d86565b5419aa767e0cbbf7f1fe8ec8";

    public static async Task RunAsync(
        CandidateMeasurementOptions options,
        CancellationToken cancellationToken)
    {
        ValidatePlatform(options.Platform);
        string modelJson = await File.ReadAllTextAsync(options.ModelPath, cancellationToken)
            .ConfigureAwait(false);
        string operationalJson = await File.ReadAllTextAsync(
                options.OperationalPreflightPath,
                cancellationToken)
            .ConfigureAwait(false);
        LogicalCandidateModel model = ContractJson.Deserialize<LogicalCandidateModel>(modelJson);
        CandidatePreflightReport operational =
            ContractJson.Deserialize<CandidatePreflightReport>(operationalJson);
        string modelDigest = JsonArtifactDigest.Compute(modelJson);
        string operationalDigest = JsonArtifactDigest.Compute(operationalJson);
        ValidateBoundary(model, operational, modelDigest, operationalDigest);

        Directory.CreateDirectory(options.WorkspacePath);
        IReadOnlyList<CandidateMeasurementInput> inputs =
            await CandidateMeasurementInputBuilder.BuildAsync(
                options.EvidenceTemplatePath,
                options.WorkspacePath,
                cancellationToken).ConfigureAwait(false);
        List<CandidateShapeMeasurement> shapes = [];
        string calibrationAssembly = typeof(Program).Assembly.Location;
        foreach (CandidateMeasurementInput input in inputs)
        {
            List<CandidateMeasurementRun> seedRuns = [];
            List<CandidateMeasurementRun> candidateRuns = [];
            int seedItems = 0;
            int candidateItems = 0;
            for (int sequence = 1; sequence <= options.RunsPerShape; sequence++)
            {
                bool candidateFirst = sequence % 2 == 0;
                CandidateProjectionProcessResult first = await RunProjectionAsync(candidateFirst)
                    .ConfigureAwait(false);
                CandidateProjectionProcessResult second = await RunProjectionAsync(!candidateFirst)
                    .ConfigureAwait(false);
                Add(first, candidateFirst);
                Add(second, !candidateFirst);

                async Task<CandidateProjectionProcessResult> RunProjectionAsync(bool candidate) =>
                    await CandidateMeasurementProcess.RunProjectionAsync(
                        calibrationAssembly,
                        input,
                        candidate,
                        options.ModelPath,
                        modelDigest,
                        sequence,
                        cancellationToken).ConfigureAwait(false);

                void Add(CandidateProjectionProcessResult result, bool candidate)
                {
                    if (candidate)
                    {
                        candidateRuns.Add(result.Run);
                        candidateItems = result.WorkItemCount;
                    }
                    else
                    {
                        seedRuns.Add(result.Run);
                        seedItems = result.WorkItemCount;
                    }
                }
            }

            shapes.Add(new CandidateShapeMeasurement
            {
                Id = input.Id,
                ModuleCopies = input.ModuleCopies,
                EvidenceFactCount = input.FactCount,
                EvidenceDigest = input.Digest,
                SeedWorkItemCount = seedItems,
                CandidateWorkItemCount = candidateItems,
                SeedRuns = seedRuns,
                CandidateRuns = candidateRuns,
                Summary = CandidateResourceGateAssessment.Build(seedRuns, candidateRuns),
            });
        }

        CandidateMutationMeasurement? mutations = options.MeasuresMutations
            ? await CandidateMutationMeasurementRunner.RunAsync(
                options,
                model,
                cancellationToken).ConfigureAwait(false)
            : null;
        CandidateScannerMeasurement scanner = await CandidateScannerMeasurementRunner.RunAsync(
            options.ScannerBenchmarkPath,
            cancellationToken).ConfigureAwait(false);
        CandidatePackageMeasurement? package = options.MeasuresPackage
            ? CandidatePackageMeasurementBuilder.Build(
                options.SeedInstallPath!,
                options.CandidateInstallPath!)
            : null;
        CandidatePlatformMeasurement report = new()
        {
            MeasurementVersion = MeasurementVersion,
            PolicyVersion = LogicalCandidateOperationalBuilder.PolicyVersion,
            CandidateId = model.CandidateId,
            ModelVersion = model.ModelVersion,
            ModelDigest = modelDigest,
            CandidateImplementationCommit = model.Training.ImplementationCommit,
            MeasurementImplementationCommit = options.MeasurementImplementationCommit,
            OperationalPreflightDigest = operationalDigest,
            RunId = options.RunId,
            RunAttempt = options.RunAttempt,
            Environment = new CandidateMeasurementEnvironment
            {
                Platform = options.Platform,
                OperatingSystem = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
                Framework = RuntimeInformation.FrameworkDescription,
                LogicalProcessors = Environment.ProcessorCount,
            },
            FreshProcessRunsPerShape = options.RunsPerShape,
            Shapes = shapes,
            Mutations = mutations,
            Scanner = scanner,
            Package = package,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(options.ReportPath)!);
        await File.WriteAllTextAsync(
            options.ReportPath,
            ContractJson.Serialize(report) + Environment.NewLine,
            cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateBoundary(
        LogicalCandidateModel model,
        CandidatePreflightReport operational,
        string modelDigest,
        string operationalDigest)
    {
        LogicalCandidateTransformer.ValidateModel(model);
        if (modelDigest != ExpectedModelDigest ||
            operationalDigest != ExpectedOperationalDigest ||
            operational.PreflightVersion != LogicalCandidateOperationalBuilder.PreflightVersion ||
            operational.Status != "development-operational-gates-passed-measured-preflight-pending" ||
            operational.Candidate.Id != model.CandidateId ||
            operational.Candidate.Gates.Count(gate => gate.Status == "passed") != 5 ||
            operational.Candidate.Gates.Count(gate => gate.Status == "not-evaluated") != 7 ||
            operational.Decision.CandidateManifestFrozen ||
            operational.Decision.ValidationAuthorized)
        {
            throw new InvalidDataException(
                "Measured preflight inputs differ from the exact frozen logical-capability v0.2 boundary.");
        }
    }

    private static void ValidatePlatform(string platform)
    {
        bool matches = platform switch
        {
            "windows" => OperatingSystem.IsWindows(),
            "linux" => OperatingSystem.IsLinux(),
            "macos" => OperatingSystem.IsMacOS(),
            _ => false,
        };
        if (!matches)
        {
            throw new InvalidDataException(
                $"Declared measurement platform '{platform}' differs from the current operating system.");
        }
    }
}
