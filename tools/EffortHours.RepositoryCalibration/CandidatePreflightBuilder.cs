using System.Text.Json;
using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class CandidatePreflightBuilder
{
    public const string PreflightVersion = "repository-candidate-preflight/0.1.0";
    public const string PolicyVersion = "repository-model-admission/1.0.0";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task RunAsync(
        CandidatePreflightOptions options,
        CancellationToken cancellationToken)
    {
        string planJson = await File.ReadAllTextAsync(options.SamplingPlanPath, cancellationToken)
            .ConfigureAwait(false);
        string corpusJson = await File.ReadAllTextAsync(options.CorpusPath, cancellationToken)
            .ConfigureAwait(false);
        string seedEvaluationJson = await File.ReadAllTextAsync(
            options.SeedEvaluationPath,
            cancellationToken).ConfigureAwait(false);
        SamplingPlan plan = JsonSerializer.Deserialize<SamplingPlan>(planJson, JsonOptions)
            ?? throw new InvalidDataException("Candidate-preflight sampling plan is empty.");
        CalibrationCorpus corpus = ContractJson.Deserialize<CalibrationCorpus>(corpusJson);
        CalibrationEvaluationReport seedEvaluation =
            ContractJson.Deserialize<CalibrationEvaluationReport>(seedEvaluationJson);
        EstimateReport[] estimates = await LoadEstimatesAsync(
            options.OutputDirectory,
            cancellationToken).ConfigureAwait(false);

        CalibrationEvaluationReport reproduced = CalibrationEvaluator.Evaluate(
            corpus,
            estimates,
            CalibrationPartition.Development);
        if (ContractJson.Serialize(reproduced) != ContractJson.Serialize(seedEvaluation))
        {
            throw new InvalidDataException(
                "Candidate preflight could not reproduce the supplied seed development evaluation.");
        }

        EstimateReport[] candidates = [.. estimates.Select(CandidatePreflightTransformer.Transform)];
        foreach (EstimateReport candidate in candidates)
        {
            IReadOnlyList<string> errors = ContractValidation.Validate(candidate);
            if (errors.Count > 0)
            {
                throw new InvalidDataException(
                    $"Candidate transformation is invalid: {string.Join("; ", errors)}");
            }
        }

        CalibrationEvaluationReport candidateEvaluation = CalibrationEvaluator.Evaluate(
            corpus,
            candidates,
            CalibrationPartition.Development);
        CandidatePreflightReport report = Build(
            plan,
            JsonArtifactDigest.Compute(planJson),
            corpus,
            JsonArtifactDigest.Compute(corpusJson),
            seedEvaluation,
            JsonArtifactDigest.Compute(seedEvaluationJson),
            estimates,
            candidates,
            candidateEvaluation,
            options.SourceCommit);
        string output = ContractJson.Serialize(report) + Environment.NewLine;
        Directory.CreateDirectory(Path.GetDirectoryName(options.ReportPath)!);
        await File.WriteAllTextAsync(options.ReportPath, output, cancellationToken).ConfigureAwait(false);
    }

    internal static CandidatePreflightReport Build(
        SamplingPlan plan,
        string planDigest,
        CalibrationCorpus corpus,
        string corpusDigest,
        CalibrationEvaluationReport seedEvaluation,
        string seedEvaluationDigest,
        IReadOnlyList<EstimateReport> estimates,
        IReadOnlyList<EstimateReport> candidates,
        CalibrationEvaluationReport candidateEvaluation,
        string sourceCommit)
    {
        ValidateBoundary(plan, corpus, seedEvaluation, estimates, candidates, candidateEvaluation);
        CandidatePreflightAssessment assessment = CandidatePreflightAssessment.Build(
            corpus,
            candidates,
            seedEvaluation,
            candidateEvaluation);
        CandidatePreflightMetrics metrics = assessment.Metrics;
        IReadOnlyList<CandidatePreflightGate> gates = assessment.Gates;
        string[] failed =
        [
            .. gates
                .Where(gate => !gate.Passed)
                .Select(gate => $"{gate.Id}: {gate.Rationale}"),
        ];

        return new CandidatePreflightReport
        {
            PreflightVersion = PreflightVersion,
            PolicyVersion = PolicyVersion,
            Status = "rejected-before-candidate-freeze",
            Partition = "development",
            Inputs = new CandidatePreflightInputs
            {
                SamplingPlan = new CandidatePreflightArtifactReference
                {
                    Id = plan.Id,
                    Version = plan.Version,
                    Digest = planDigest,
                },
                Corpus = new CandidatePreflightArtifactReference
                {
                    Id = corpus.Id,
                    Version = corpus.Version,
                    Digest = corpusDigest,
                },
                SeedEvaluation = new CandidatePreflightArtifactReference
                {
                    Id = seedEvaluation.CorpusId,
                    Version = seedEvaluation.CorpusVersion,
                    Digest = seedEvaluationDigest,
                },
                DevelopmentEstimates = BuildSourceReferences(corpus, estimates),
            },
            Candidate = new CandidatePreflightDesign
            {
                Id = CandidatePreflightTransformer.CandidateId,
                Status = "rejected-development-preflight",
                Configuration = BuildConfiguration(sourceCommit),
                DevelopmentMetrics = metrics,
                Gates = gates,
                RejectionReasons = failed,
            },
            Holdouts = new CandidatePreflightHoldoutBoundary
            {
                Validation = "withheld-not-run; labels not-authored",
                Test = "withheld-not-run; labels not-authored and body not present",
                CandidateOutputsGenerated = false,
                LabelsAuthored = false,
            },
            Decision = new CandidatePreflightDecision
            {
                Status = "no-candidate-frozen",
                CandidateManifestFrozen = false,
                ValidationAuthorized = false,
                Rationale =
                    "The best bounded transparent design fails development family consistency and " +
                    "matched-target coverage. Operational gates also remain deliberately unevaluated, " +
                    "so the policy fails closed.",
                NextBoundary =
                    "Improve general semantic marginality and capability allocation using development " +
                    "evidence only, then run a new preflight before freezing any candidate manifest.",
            },
        };
    }

    private static CandidatePreflightConfiguration BuildConfiguration(string sourceCommit) => new()
    {
        Kind = "transparent-rule",
        ImplementationCommit = sourceCommit,
        BaselineEstimatorVersion = CandidatePreflightTransformer.BaselineEstimatorVersion,
        FeatureContractVersion = CandidatePreflightTransformer.FeatureContractVersion,
        Features = ["work-item-kind", "normalized-scope-role"],
        ExactZeroRules =
        [
            "test semantic surfaces",
            "benchmark application entry points and source backbones",
            "generated/golden source backbones and support work",
        ],
        DiscountRules =
        [
            "test source backbones and application entry points",
            "benchmark semantic surfaces",
        ],
        DiscountFactor = CandidatePreflightTransformer.DiscountFactor,
        Range = new CandidatePreflightRangeConfiguration
        {
            LowFactor = CandidatePreflightTransformer.LowFactor,
            HighFactor = CandidatePreflightTransformer.HighFactor,
        },
        Dependencies = [".NET 10", "EffortHours.Contracts", "EffortHours.Calibration"],
        LicenseExpression = "MIT",
        RuntimeBoundary =
            "Offline deterministic transformation of a saved implementation-profile seed estimate; " +
            "no repository read, target execution, network, Git history, provider, or external process.",
        FallbackEstimatorVersion = CandidatePreflightTransformer.BaselineEstimatorVersion,
        ExplanationForm =
            "Every work item retains its stable ID and records its seed expected hours, exact point " +
            "factor, range factors, candidate identity, and bounded scope-role reason.",
    };

    private static IReadOnlyList<CandidatePreflightSourceEstimate> BuildSourceReferences(
        CalibrationCorpus corpus,
        IReadOnlyList<EstimateReport> estimates)
    {
        Dictionary<CandidateKey, EstimateReport> index = estimates.ToDictionary(candidate =>
            new CandidateKey(candidate.Repository.SourceDigest!, candidate.Profile, candidate.Baseline.Id));
        return
        [
            .. corpus.Records
                .Where(record => record.Partition == CalibrationPartition.Development)
                .OrderBy(record => record.Id, StringComparer.Ordinal)
                .Select(record =>
                {
                    EstimateReport estimate = index[new CandidateKey(
                        record.Repository.SourceDigest,
                        record.Profile,
                        record.BaselineId)];
                    return new CandidatePreflightSourceEstimate
                    {
                        RecordId = record.Id,
                        RepositoryId = record.Repository.Id,
                        SourceDigest = record.Repository.SourceDigest,
                        EstimateDigest = CalibrationDigest.Compute(estimate),
                    };
                }),
        ];
    }

    private static void ValidateBoundary(
        SamplingPlan plan,
        CalibrationCorpus corpus,
        CalibrationEvaluationReport seedEvaluation,
        IReadOnlyList<EstimateReport> estimates,
        IReadOnlyList<EstimateReport> candidates,
        CalibrationEvaluationReport candidateEvaluation)
    {
        if (plan.SamplingPlanVersion != "repository-sampling-plan/1.0.0" ||
            corpus.Id != "efforthours-public-readiness-development" ||
            corpus.Version != "0.3.0" ||
            seedEvaluation.Partition != CalibrationPartition.Development ||
            candidateEvaluation.Partition != CalibrationPartition.Development ||
            seedEvaluation.RecordCount != 15 ||
            candidateEvaluation.RecordCount != 15 ||
            estimates.Count != 15 ||
            candidates.Count != 15 ||
            corpus.Records.Any(record =>
                record.Partition != CalibrationPartition.Development ||
                record.SourceEstimatorVersion != CandidatePreflightTransformer.BaselineEstimatorVersion))
        {
            throw new InvalidDataException(
                "Candidate preflight requires the exact complete public-readiness 0.3.0 development boundary.");
        }
    }

    private static async Task<EstimateReport[]> LoadEstimatesAsync(
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Candidate-preflight output directory was not found: {outputDirectory}");
        }

        string[] paths =
        [
            .. Directory.EnumerateFiles(
                    outputDirectory,
                    "source-estimate.json",
                    SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal),
        ];
        List<EstimateReport> estimates = [];
        foreach (string path in paths)
        {
            estimates.Add(ContractJson.Deserialize<EstimateReport>(
                await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)));
        }

        return [.. estimates];
    }

    private readonly record struct CandidateKey(
        string SourceDigest,
        EstimationProfile Profile,
        string BaselineId);

}
