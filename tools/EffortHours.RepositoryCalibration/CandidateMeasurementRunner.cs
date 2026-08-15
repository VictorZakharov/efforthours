using System.Runtime.InteropServices;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class CandidateMeasurementRunner
{
    public const string MeasurementVersion = "repository-candidate-platform-measurement/1.0.0";
    public const string ExpectedCandidateId = "logical-capability/0.3.0";
    public const string ExpectedModelVersion = "repository-logical-capability-model/0.3.0";
    public const string ExpectedEstimatorVersion =
        "candidate-logical-capability/0.3.0+seed-rules/0.4.0";
    public const string ExpectedFeatureContractVersion = "logical-capability-features/1.3.0";
    public const string ExpectedCandidateImplementationCommit =
        "7e5451c807cef3ce22bedd1bc374ab519882b21c";
    public const string ExpectedModelDigest =
        "sha256:492e10b2f427a471a8edcf4f7e3f19d65b098e4822e25c552956c9ce992fa1ea";
    public const string ExpectedNumericalDigest =
        "sha256:c5614f11513619b7293f454d58cda87efdc192e181ef3551270b1d3bc2a9ba97";
    public const string ExpectedOperationalDigest =
        "sha256:eb091d5de07399a169dcef4bc9a08f9feb129563dd61d4101eed6c40cab7ddc8";
    public const string ExpectedMutationSuiteDigest =
        "sha256:ab7d3ad79a33cba14d3837211433f94965cd98d7def64e08434a4820f77386c7";

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
            ContractJson.SerializeDocument(report),
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
            model.CandidateId != ExpectedCandidateId ||
            model.ModelVersion != ExpectedModelVersion ||
            model.EstimatorVersion != ExpectedEstimatorVersion ||
            model.FeatureContractVersion != ExpectedFeatureContractVersion ||
            model.Training.ImplementationCommit != ExpectedCandidateImplementationCommit ||
            operational.PreflightVersion != LogicalCandidateOperationalBuilder.PreflightVersion ||
            operational.Status != "development-operational-gates-passed-measured-preflight-pending" ||
            operational.Candidate.Id != model.CandidateId ||
            operational.Inputs.CandidateModel?.Digest != ExpectedModelDigest ||
            operational.Inputs.NumericalPreflight?.Digest != ExpectedNumericalDigest ||
            operational.Candidate.Gates.Count(gate => gate.Status == "passed") != 5 ||
            operational.Candidate.Gates.Count(gate => gate.Status == "not-evaluated") != 7 ||
            operational.Decision.CandidateManifestFrozen ||
            operational.Decision.ValidationAuthorized)
        {
            throw new InvalidDataException(
                "Measured preflight inputs differ from the exact frozen logical-capability v0.3 boundary.");
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
