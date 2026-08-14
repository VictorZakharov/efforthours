using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class CandidateMeasurementAggregator
{
    public const string MeasurementVersion =
        "repository-candidate-measured-operational-preflight/1.0.0";
    public const string PreflightVersion = "repository-candidate-operational-preflight/0.2.0";

    private static readonly string[] Platforms = ["linux", "macos", "windows"];
    private static readonly string[] Shapes = ["large", "medium", "small"];

    public static async Task<bool> RunAsync(
        CandidateMeasurementAggregateOptions options,
        CancellationToken cancellationToken)
    {
        string operationalJson = await ReadAsync(options.OperationalPreflightPath, cancellationToken);
        string modelJson = await ReadAsync(options.ModelPath, cancellationToken);
        string suiteJson = await ReadAsync(options.MutationSuitePath, cancellationToken);
        string mutationJson = await ReadAsync(options.MutationReportPath, cancellationToken);
        CandidatePreflightReport operational =
            ContractJson.Deserialize<CandidatePreflightReport>(operationalJson);
        LogicalCandidateModel model = ContractJson.Deserialize<LogicalCandidateModel>(modelJson);
        CalibrationMutationSuite suite = ContractJson.Deserialize<CalibrationMutationSuite>(suiteJson);
        CalibrationMutationReport mutationReport =
            ContractJson.Deserialize<CalibrationMutationReport>(mutationJson);
        List<CandidatePlatformMeasurement> platforms = [];
        foreach (string platform in Platforms)
        {
            string path = Path.Combine(options.MeasurementDirectory, $"{platform}.json");
            platforms.Add(ContractJson.Deserialize<CandidatePlatformMeasurement>(
                await ReadAsync(path, cancellationToken)));
        }

        string operationalDigest = JsonArtifactDigest.Compute(operationalJson);
        string modelDigest = JsonArtifactDigest.Compute(modelJson);
        string suiteDigest = JsonArtifactDigest.Compute(suiteJson);
        string mutationDigest = JsonArtifactDigest.Compute(mutationJson);
        ValidateInputs(
            operational,
            model,
            suite,
            mutationReport,
            platforms,
            operationalDigest,
            modelDigest,
            suiteDigest,
            mutationDigest);

        CandidateCrossPlatformMeasurement crossPlatform = BuildCrossPlatform(platforms);
        CandidateMutationMeasurement mutations = platforms.Single(
            item => item.Mutations is not null).Mutations!;
        CandidatePackageMeasurement package = platforms.Single(
            item => item.Package is not null).Package!;
        IReadOnlyList<CandidatePreflightGate> measuredGates =
            CandidateMeasuredGateAssessment.Build(platforms, crossPlatform, mutations, package);
        bool passed = measuredGates.All(gate => gate.Passed);
        CandidateMeasuredOperationalReport measurement = new()
        {
            MeasurementVersion = MeasurementVersion,
            PolicyVersion = LogicalCandidateOperationalBuilder.PolicyVersion,
            Status = passed ? "all-measured-gates-passed" : "measured-gate-failure",
            OperationalPreflight = Reference(
                operational.Candidate.Id,
                operational.PreflightVersion,
                operationalDigest),
            CandidateModel = Reference(model.Id, model.ModelVersion, modelDigest),
            MutationSuite = Reference(suite.Id, suite.Version, suiteDigest),
            MutationReport = Reference(
                mutationReport.SuiteId,
                mutationReport.EvaluatorVersion,
                mutationDigest),
            Platforms = platforms,
            CrossPlatform = crossPlatform,
            Mutations = mutations,
            Package = package,
            Gates = measuredGates,
            Limitations =
            [
                "Latency and memory are paired engineering measurements on GitHub-hosted runners, not universal product guarantees.",
                "Saved-evidence shapes are deterministic synthetic scaling inputs; they do not establish population accuracy.",
                "The installed candidate layout conservatively stages the complete calibration assembly plus the exact model artifact; an admitted product integration may be smaller.",
                "No cross-platform scanner latency or memory threshold is frozen; the scanner gate checks its applicable process, offline, and unchanged-target boundaries.",
            ],
        };
        string measurementJson = ContractJson.SerializeDocument(measurement);
        Directory.CreateDirectory(Path.GetDirectoryName(options.MeasurementReportPath)!);
        await File.WriteAllTextAsync(
            options.MeasurementReportPath,
            measurementJson,
            cancellationToken).ConfigureAwait(false);

        IReadOnlyList<CandidatePreflightGate> gates = MergeGates(
            operational.Candidate.Gates,
            measuredGates);
        CandidatePreflightReport preflight = operational with
        {
            PreflightVersion = PreflightVersion,
            Status = passed
                ? "development-all-operational-gates-passed-candidate-freeze-pending"
                : "rejected-measured-operational-preflight",
            Inputs = operational.Inputs with
            {
                MeasuredOperationalPreflight = Reference(
                    operational.Candidate.Id,
                    MeasurementVersion,
                    JsonArtifactDigest.Compute(measurementJson)),
            },
            Candidate = operational.Candidate with
            {
                Status = passed
                    ? "operationally-eligible-candidate-freeze-pending"
                    : "rejected-measured-operational-preflight",
                Gates = gates,
                RejectionReasons =
                [
                    .. gates.Where(gate => gate.Status == "failed")
                        .Select(gate => $"{gate.Id}: {gate.Rationale}"),
                ],
            },
            Decision = new CandidatePreflightDecision
            {
                Status = "no-candidate-frozen",
                CandidateManifestFrozen = false,
                ValidationAuthorized = false,
                Rationale = passed
                    ? "Every numerical, development-computable operational, and measured operational gate passes. A separate finite manifest and selection-rule freeze is still required before validation access."
                    : "At least one measured operational gate failed, so the exact candidate is retired and holdouts remain closed.",
                NextBoundary = passed
                    ? "Freeze the exact finite candidate manifest, resource record, and precommitted validation selection rule without retuning; keep validation and test unopened until that freeze."
                    : $"Retire {model.CandidateId}; any changed challenger requires a new identity and complete development-only preflight.",
            },
        };
        Directory.CreateDirectory(Path.GetDirectoryName(options.PreflightReportPath)!);
        await File.WriteAllTextAsync(
            options.PreflightReportPath,
            ContractJson.SerializeDocument(preflight),
            cancellationToken).ConfigureAwait(false);
        return passed;
    }

    private static void ValidateInputs(
        CandidatePreflightReport operational,
        LogicalCandidateModel model,
        CalibrationMutationSuite suite,
        CalibrationMutationReport mutationReport,
        IReadOnlyList<CandidatePlatformMeasurement> platforms,
        string operationalDigest,
        string modelDigest,
        string suiteDigest,
        string mutationDigest)
    {
        LogicalCandidateTransformer.ValidateModel(model);
        IReadOnlyList<string> suiteErrors = ContractValidation.Validate(suite);
        IReadOnlyList<string> reportErrors = ContractValidation.Validate(mutationReport);
        if (suiteErrors.Count > 0 || reportErrors.Count > 0)
        {
            throw new InvalidDataException(
                "Measured mutation contracts are invalid: " +
                string.Join("; ", suiteErrors.Concat(reportErrors)));
        }

        if (operationalDigest != CandidateMeasurementRunner.ExpectedOperationalDigest ||
            operational.PreflightVersion != LogicalCandidateOperationalBuilder.PreflightVersion ||
            operational.Status != "development-operational-gates-passed-measured-preflight-pending" ||
            operational.Candidate.Id != model.CandidateId ||
            operational.Candidate.Gates.Count(gate => gate.Status == "passed") != 5 ||
            operational.Candidate.Gates.Count(gate => gate.Status == "not-evaluated") != 7 ||
            operational.Decision.CandidateManifestFrozen ||
            operational.Decision.ValidationAuthorized ||
            modelDigest != CandidateMeasurementRunner.ExpectedModelDigest ||
            suite.Id != "efforthours-public-synthetic-mutations" ||
            suite.Version != "0.8.0" ||
            mutationReport.SuiteId != suite.Id ||
            mutationReport.SuiteVersion != suite.Version ||
            mutationReport.CaseCount != suite.Cases.Count ||
            mutationReport.AssertionCount != suite.Assertions.Count ||
            platforms.Select(item => item.Environment.Platform).Order(StringComparer.Ordinal)
                .SequenceEqual(Platforms, StringComparer.Ordinal) is false ||
            platforms.Count(item => item.Mutations is not null) != 1 ||
            platforms.Single(item => item.Mutations is not null).Environment.Platform != "linux" ||
            platforms.Count(item => item.Package is not null) != 1 ||
            platforms.Single(item => item.Package is not null).Environment.Platform != "linux")
        {
            throw new InvalidDataException(
                "Measured operational inputs differ from the exact frozen artifact and platform boundary.");
        }

        CandidateMutationMeasurement mutation = platforms.Single(
            item => item.Mutations is not null).Mutations!;
        string[] commits = [.. platforms.Select(item => item.MeasurementImplementationCommit)
            .Distinct(StringComparer.Ordinal)];
        string[] runIds = [.. platforms.Select(item => item.RunId).Distinct(StringComparer.Ordinal)];
        int[] attempts = [.. platforms.Select(item => item.RunAttempt).Distinct()];
        if (commits.Length != 1 || !LogicalCandidateModelOptions.IsCommit(commits[0]) ||
            runIds.Length != 1 || attempts.Length != 1 ||
            mutation.SuiteDigest != suiteDigest || mutation.ReportDigest != mutationDigest ||
            mutation.CaseCount != mutationReport.CaseCount ||
            mutation.CandidateAppliedCaseCount + mutation.SeedFallbackCaseCount !=
                mutation.CaseCount ||
            mutation.AssertionCount != mutationReport.AssertionCount ||
            mutation.PassedCount != mutationReport.PassedCount ||
            mutation.FailedCount != mutationReport.FailedCount ||
            mutation.Passed != mutationReport.AllPassed ||
            mutationReport.Cases.Any(item =>
                item.CandidateEstimatorVersion is not null &&
                item.CandidateEstimatorVersion != model.EstimatorVersion &&
                item.CandidateEstimatorVersion != model.BaselineEstimatorVersion) ||
            mutation.CandidateAppliedCaseCount != mutationReport.Cases.Count(
                item => item.CandidateEstimatorVersion == model.EstimatorVersion) ||
            mutation.SeedFallbackCaseCount != mutationReport.Cases.Count(
                item => item.CandidateEstimatorVersion == model.BaselineEstimatorVersion))
        {
            throw new InvalidDataException(
                "Measured platform, workflow, or mutation lineage does not reconcile.");
        }

        foreach (CandidatePlatformMeasurement platform in platforms)
        {
            string[] shapeIds = [.. platform.Shapes.Select(item => item.Id)
                .Order(StringComparer.Ordinal)];
            bool validShapes = shapeIds.SequenceEqual(Shapes, StringComparer.Ordinal) &&
                platform.Shapes.All(shape =>
                    shape.SeedRuns.Count == platform.FreshProcessRunsPerShape &&
                    shape.CandidateRuns.Count == platform.FreshProcessRunsPerShape &&
                    shape.SeedWorkItemCount == shape.CandidateWorkItemCount &&
                    shape.EvidenceFactCount > 0 &&
                    shape.Summary == CandidateResourceGateAssessment.Build(
                        shape.SeedRuns,
                        shape.CandidateRuns));
            if (platform.MeasurementVersion != CandidateMeasurementRunner.MeasurementVersion ||
                platform.PolicyVersion != LogicalCandidateOperationalBuilder.PolicyVersion ||
                platform.CandidateId != model.CandidateId ||
                platform.ModelVersion != model.ModelVersion ||
                platform.ModelDigest != modelDigest ||
                platform.CandidateImplementationCommit != model.Training.ImplementationCommit ||
                platform.OperationalPreflightDigest != operationalDigest ||
                platform.FreshProcessRunsPerShape < 5 || !validShapes)
            {
                throw new InvalidDataException(
                    $"Platform measurement '{platform.Environment.Platform}' has invalid identity or shape coverage.");
            }
        }
    }

    private static CandidateCrossPlatformMeasurement BuildCrossPlatform(
        List<CandidatePlatformMeasurement> platforms)
    {
        bool evidence = Shapes.All(shape => platforms
            .Select(platform => platform.Shapes.Single(item => item.Id == shape).EvidenceDigest)
            .Distinct(StringComparer.Ordinal).Count() == 1);
        bool repeated = platforms.SelectMany(platform => platform.Shapes).All(shape =>
            shape.SeedRuns.Select(run => run.OutputDigest).Distinct(StringComparer.Ordinal).Count() == 1 &&
            shape.CandidateRuns.Select(run => run.OutputDigest).Distinct(StringComparer.Ordinal).Count() == 1);
        bool seed = Shapes.All(shape => platforms
            .Select(platform => platform.Shapes.Single(item => item.Id == shape)
                .SeedRuns[0].OutputDigest)
            .Distinct(StringComparer.Ordinal).Count() == 1);
        bool candidate = Shapes.All(shape => platforms
            .Select(platform => platform.Shapes.Single(item => item.Id == shape)
                .CandidateRuns[0].OutputDigest)
            .Distinct(StringComparer.Ordinal).Count() == 1);
        bool normalizedSeed = Shapes.All(shape => platforms
            .Select(platform => platform.Shapes.Single(item => item.Id == shape)
                .SeedRuns[0].LfNormalizedOutputDigest)
            .Distinct(StringComparer.Ordinal).Count() == 1);
        bool normalizedCandidate = Shapes.All(shape => platforms
            .Select(platform => platform.Shapes.Single(item => item.Id == shape)
                .CandidateRuns[0].LfNormalizedOutputDigest)
            .Distinct(StringComparer.Ordinal).Count() == 1);
        return new CandidateCrossPlatformMeasurement
        {
            PlatformCount = platforms.Count,
            ShapeCount = Shapes.Length,
            EvidenceDigestsIdentical = evidence,
            SeedOutputsIdentical = seed,
            CandidateOutputsIdentical = candidate,
            LfNormalizedSeedOutputsIdentical = normalizedSeed,
            LfNormalizedCandidateOutputsIdentical = normalizedCandidate,
            RepeatedOutputsIdentical = repeated,
            Passed = evidence && candidate && repeated,
        };
    }

    internal static IReadOnlyList<CandidatePreflightGate> MergeGates(
        IReadOnlyList<CandidatePreflightGate> operational,
        IReadOnlyList<CandidatePreflightGate> measured)
    {
        Dictionary<string, CandidatePreflightGate> index = operational.ToDictionary(
            gate => gate.Id,
            gate => gate,
            StringComparer.Ordinal);
        foreach (CandidatePreflightGate gate in measured)
        {
            index[gate.Id] = gate;
        }

        string[] ids =
        [
            "ecosystem-stratum-agreement", "material-category-agreement",
            "shape-and-size-slice-regression", "public-mutation-suite",
            "cross-platform-determinism", "schema-lineage-and-saved-explanation",
            "offline-safety-ood-and-tamper", "median-latency-overhead",
            "slowest-latency-overhead", "peak-working-set-overhead",
            "installed-package-increase", "scanner-thresholds-and-target-fingerprints",
        ];
        return [.. ids.Select(id => index[id])];
    }

    private static CandidatePreflightArtifactReference Reference(
        string id,
        string version,
        string digest) => new() { Id = id, Version = version, Digest = digest };

    private static Task<string> ReadAsync(string path, CancellationToken cancellationToken) =>
        File.ReadAllTextAsync(path, cancellationToken);
}
