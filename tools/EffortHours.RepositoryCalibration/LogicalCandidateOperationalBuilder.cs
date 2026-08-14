using System.Text.Json;
using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class LogicalCandidateOperationalBuilder
{
    public const string PreflightVersion = "repository-candidate-operational-preflight/0.1.0";
    public const string PolicyVersion = "repository-model-admission/1.0.0";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task RunAsync(
        LogicalCandidateOperationalOptions options,
        CancellationToken cancellationToken)
    {
        string planJson = await ReadAsync(options.SamplingPlanPath, cancellationToken);
        string corpusJson = await ReadAsync(options.CorpusPath, cancellationToken);
        string seedJson = await ReadAsync(options.SeedEvaluationPath, cancellationToken);
        string modelJson = await ReadAsync(options.ModelPath, cancellationToken);
        string numericalJson = await ReadAsync(options.NumericalPreflightPath, cancellationToken);
        SamplingPlan plan = JsonSerializer.Deserialize<SamplingPlan>(planJson, JsonOptions)
            ?? throw new InvalidDataException("Logical-candidate sampling plan is empty.");
        CalibrationCorpus corpus = ContractJson.Deserialize<CalibrationCorpus>(corpusJson);
        CalibrationEvaluationReport seed =
            ContractJson.Deserialize<CalibrationEvaluationReport>(seedJson);
        LogicalCandidateModel model = ContractJson.Deserialize<LogicalCandidateModel>(modelJson);
        CandidatePreflightReport numerical =
            ContractJson.Deserialize<CandidatePreflightReport>(numericalJson);
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs =
            await LogicalCandidateDevelopmentLoader.LoadAsync(
                options.OutputDirectory,
                cancellationToken).ConfigureAwait(false);
        ValidateBoundary(
            plan,
            corpus,
            seed,
            model,
            numerical,
            modelJson,
            options.SourceCommit,
            inputs);

        EstimateReport[] seeds = [.. inputs.Select(input => input.Estimate)];
        EstimateReport[] candidates =
        [
            .. inputs.Select(input => LogicalCandidateTransformer.Transform(
                input.Estimate,
                input.Evidence,
                model,
                cancellationToken)),
        ];
        CalibrationEvaluationReport reproducedSeed = CalibrationEvaluator.Evaluate(
            corpus,
            seeds,
            CalibrationPartition.Development);
        if (ContractJson.Serialize(reproducedSeed) != ContractJson.Serialize(seed))
        {
            throw new InvalidDataException(
                "Operational preflight could not reproduce the frozen seed evaluation.");
        }

        CalibrationEvaluationReport candidate = CalibrationEvaluator.Evaluate(
            corpus,
            candidates,
            CalibrationPartition.Development);
        string modelDigest = JsonArtifactDigest.Compute(modelJson);
        IReadOnlyList<CandidatePreflightGate> assessed =
            CandidateOperationalGateAssessment.Build(
                plan,
                corpus,
                inputs,
                candidates,
                seed,
                candidate,
                model,
                modelJson,
                modelDigest);
        IReadOnlyList<CandidatePreflightGate> gates = BuildOperationalGates(assessed);
        bool rejected = gates.Any(gate => gate.Status == "failed");
        CandidatePreflightReport report = numerical with
        {
            PreflightVersion = PreflightVersion,
            Status = rejected
                ? "rejected-development-operational-preflight"
                : "development-operational-gates-passed-measured-preflight-pending",
            Inputs = numerical.Inputs with
            {
                NumericalPreflight = new CandidatePreflightArtifactReference
                {
                    Id = numerical.Candidate.Id,
                    Version = numerical.PreflightVersion,
                    Digest = JsonArtifactDigest.Compute(numericalJson),
                },
            },
            Candidate = numerical.Candidate with
            {
                Status = rejected
                    ? "rejected-development-operational-preflight"
                    : "operationally-eligible-measurement-pending",
                Configuration = numerical.Candidate.Configuration with
                {
                    ImplementationCommit = options.SourceCommit,
                },
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
                Rationale = rejected
                    ? "The exact candidate failed at least one development operational gate. " +
                      "Later mutation, cross-platform, resource, package, and scanner gates were " +
                      "not run; validation and test holdouts remain closed."
                    : "Every evaluated development operational gate passed, but later measured " +
                      "gates remain, so candidate freeze and holdout access stay blocked.",
                NextBoundary = rejected
                    ? $"Retire {model.CandidateId}. Any changed challenger requires a new " +
                      "candidate identity and a complete development-only preflight before holdout access."
                    : "Run every remaining measured operational gate against this exact candidate.",
            },
        };
        Directory.CreateDirectory(Path.GetDirectoryName(options.ReportPath)!);
        await File.WriteAllTextAsync(
            options.ReportPath,
            ContractJson.SerializeDocument(report),
            cancellationToken).ConfigureAwait(false);
    }

    private static List<CandidatePreflightGate> BuildOperationalGates(
        IReadOnlyList<CandidatePreflightGate> assessed)
    {
        Dictionary<string, CandidatePreflightGate> index = assessed.ToDictionary(
            gate => gate.Id,
            StringComparer.Ordinal);
        string[] ids =
        [
            "ecosystem-stratum-agreement",
            "material-category-agreement",
            "shape-and-size-slice-regression",
            "public-mutation-suite",
            "cross-platform-determinism",
            "schema-lineage-and-saved-explanation",
            "offline-safety-ood-and-tamper",
            "median-latency-overhead",
            "slowest-latency-overhead",
            "peak-working-set-overhead",
            "installed-package-increase",
            "scanner-thresholds-and-target-fingerprints",
        ];
        bool failed = false;
        List<CandidatePreflightGate> gates = [];
        foreach (string id in ids)
        {
            if (index.TryGetValue(id, out CandidatePreflightGate? gate))
            {
                gates.Add(gate);
                failed |= gate.Status == "failed";
                continue;
            }

            gates.Add(new CandidatePreflightGate
            {
                Id = id,
                Status = "not-evaluated",
                Passed = false,
                Requirement = "Required by repository-model-admission/1.0.0 before validation selection.",
                Rationale = failed
                    ? "An earlier development operational gate failed, so this later gate was not run and fails closed."
                    : "This measured operational gate has not yet been run and fails closed.",
            });
        }

        return gates;
    }

    private static void ValidateBoundary(
        SamplingPlan plan,
        CalibrationCorpus corpus,
        CalibrationEvaluationReport seed,
        LogicalCandidateModel model,
        CandidatePreflightReport numerical,
        string modelJson,
        string sourceCommit,
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs)
    {
        if (plan.SamplingPlanVersion != "repository-sampling-plan/1.0.0" ||
            corpus.Id != "efforthours-public-readiness-development" ||
            corpus.Version != "0.3.0" ||
            seed.Partition != CalibrationPartition.Development ||
            inputs.Count != 15 ||
            numerical.PreflightVersion != "repository-candidate-preflight/0.2.0" ||
            numerical.Status != "development-numerical-gates-passed-operational-preflight-pending" ||
            numerical.Candidate.Id != model.CandidateId ||
            numerical.Inputs.CandidateModel?.Digest != JsonArtifactDigest.Compute(modelJson) ||
            numerical.Candidate.Gates.Count(gate => gate.Status == "passed") != 16 ||
            numerical.Candidate.Gates.Count(gate => gate.Status == "not-evaluated") != 12 ||
            numerical.Decision.CandidateManifestFrozen ||
            numerical.Decision.ValidationAuthorized ||
            !LogicalCandidateModelOptions.IsCommit(sourceCommit))
        {
            throw new InvalidDataException(
                "Operational preflight inputs differ from the frozen development-only boundary.");
        }
    }

    private static Task<string> ReadAsync(string path, CancellationToken cancellationToken) =>
        File.ReadAllTextAsync(path, cancellationToken);
}
