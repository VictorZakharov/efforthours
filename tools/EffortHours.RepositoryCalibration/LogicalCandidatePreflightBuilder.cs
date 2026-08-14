using System.Text.Json;
using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class LogicalCandidatePreflightBuilder
{
    public const string PreflightVersion = "repository-candidate-preflight/0.2.0";
    public const string PolicyVersion = "repository-model-admission/1.0.0";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task RunAsync(
        LogicalCandidatePreflightOptions options,
        CancellationToken cancellationToken)
    {
        string planJson = await File.ReadAllTextAsync(options.SamplingPlanPath, cancellationToken)
            .ConfigureAwait(false);
        string corpusJson = await File.ReadAllTextAsync(options.CorpusPath, cancellationToken)
            .ConfigureAwait(false);
        string seedJson = await File.ReadAllTextAsync(options.SeedEvaluationPath, cancellationToken)
            .ConfigureAwait(false);
        string modelJson = await File.ReadAllTextAsync(options.ModelPath, cancellationToken)
            .ConfigureAwait(false);
        SamplingPlan plan = JsonSerializer.Deserialize<SamplingPlan>(planJson, JsonOptions)
            ?? throw new InvalidDataException("Logical-candidate sampling plan is empty.");
        CalibrationCorpus corpus = ContractJson.Deserialize<CalibrationCorpus>(corpusJson);
        CalibrationEvaluationReport seed =
            ContractJson.Deserialize<CalibrationEvaluationReport>(seedJson);
        LogicalCandidateModel model = ContractJson.Deserialize<LogicalCandidateModel>(modelJson);
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs =
            await LogicalCandidateDevelopmentLoader.LoadAsync(
                options.OutputDirectory,
                cancellationToken).ConfigureAwait(false);
        ValidateInputs(
            plan,
            corpus,
            JsonArtifactDigest.Compute(corpusJson),
            seed,
            inputs,
            model,
            options.SourceCommit);

        EstimateReport[] estimates = [.. inputs.Select(input => input.Estimate)];
        CalibrationEvaluationReport reproduced = CalibrationEvaluator.Evaluate(
            corpus,
            estimates,
            CalibrationPartition.Development);
        if (ContractJson.Serialize(reproduced) != ContractJson.Serialize(seed))
        {
            throw new InvalidDataException(
                "Logical-candidate preflight could not reproduce the seed development evaluation.");
        }

        EstimateReport[] candidates =
        [
            .. inputs.Select(input => LogicalCandidateTransformer.Transform(
                input.Estimate,
                input.Evidence,
                model)),
        ];
        foreach (EstimateReport candidate in candidates)
        {
            IReadOnlyList<string> errors = ContractValidation.Validate(candidate);
            if (errors.Count > 0)
            {
                throw new InvalidDataException(
                    $"Logical candidate transformation is invalid: {string.Join("; ", errors)}");
            }
        }

        CalibrationEvaluationReport candidateEvaluation = CalibrationEvaluator.Evaluate(
            corpus,
            candidates,
            CalibrationPartition.Development);
        CandidatePreflightAssessment assessment = CandidatePreflightAssessment.Build(
            corpus,
            candidates,
            seed,
            candidateEvaluation);
        bool numericalPass = assessment.Gates
            .Where(gate => gate.Status != "not-evaluated")
            .All(gate => gate.Passed);
        if (numericalPass)
        {
            assessment = assessment with
            {
                Gates =
                [
                    .. assessment.Gates.Select(gate => gate.Status == "not-evaluated"
                        ? gate with
                        {
                            Rationale =
                                "This required operational preflight gate has not yet been run, " +
                                "so candidate freeze and holdout access remain blocked.",
                        }
                        : gate),
                ],
            };
        }

        CandidatePreflightReport report = BuildReport(
            plan,
            JsonArtifactDigest.Compute(planJson),
            corpus,
            JsonArtifactDigest.Compute(corpusJson),
            seed,
            JsonArtifactDigest.Compute(seedJson),
            model,
            JsonArtifactDigest.Compute(modelJson),
            inputs,
            assessment,
            options.SourceCommit,
            numericalPass);
        Directory.CreateDirectory(Path.GetDirectoryName(options.ReportPath)!);
        await File.WriteAllTextAsync(
            options.ReportPath,
            ContractJson.SerializeDocument(report),
            cancellationToken).ConfigureAwait(false);
    }

    private static CandidatePreflightReport BuildReport(
        SamplingPlan plan,
        string planDigest,
        CalibrationCorpus corpus,
        string corpusDigest,
        CalibrationEvaluationReport seed,
        string seedDigest,
        LogicalCandidateModel model,
        string modelDigest,
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs,
        CandidatePreflightAssessment assessment,
        string sourceCommit,
        bool numericalPass)
    {
        string[] failed =
        [
            .. assessment.Gates.Where(gate => gate.Status == "failed")
                .Select(gate => $"{gate.Id}: {gate.Rationale}"),
        ];
        return new CandidatePreflightReport
        {
            PreflightVersion = PreflightVersion,
            PolicyVersion = PolicyVersion,
            Status = numericalPass
                ? "development-numerical-gates-passed-operational-preflight-pending"
                : "rejected-before-candidate-freeze",
            Partition = "development",
            Inputs = new CandidatePreflightInputs
            {
                SamplingPlan = Reference(plan.Id, plan.Version, planDigest),
                Corpus = Reference(corpus.Id, corpus.Version, corpusDigest),
                SeedEvaluation = Reference(seed.CorpusId, seed.CorpusVersion, seedDigest),
                CandidateModel = Reference(model.Id, model.ModelVersion, modelDigest),
                DevelopmentEstimates = BuildSourceReferences(corpus, inputs),
            },
            Candidate = new CandidatePreflightDesign
            {
                Id = model.CandidateId,
                Status = numericalPass
                    ? "numerically-eligible-operational-preflight-pending"
                    : "rejected-development-preflight",
                Configuration = BuildConfiguration(model, sourceCommit),
                DevelopmentMetrics = assessment.Metrics,
                Gates = assessment.Gates,
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
                Rationale = numericalPass
                    ? "The fitted logical-capability table clears every development numerical gate. " +
                      "Operational, safety, determinism, resource, mutation, and explanation gates " +
                      "remain deliberately unrun, so candidate freeze and holdout access still fail closed."
                    : "At least one required development numerical gate failed, so the candidate cannot advance.",
                NextBoundary = numericalPass
                    ? "Run every remaining operational preflight gate against this exact model and " +
                      "implementation. Freeze the finite candidate manifest only if all pass; do not " +
                      "author or inspect validation or test labels before that decision."
                    : "Improve the candidate using development evidence only, then run a new exact preflight.",
            },
        };
    }

    private static CandidatePreflightConfiguration BuildConfiguration(
        LogicalCandidateModel model,
        string sourceCommit) => new()
        {
            Kind = "transparent-fitted-table",
            ImplementationCommit = sourceCommit,
            BaselineEstimatorVersion = model.BaselineEstimatorVersion,
            FeatureContractVersion = model.FeatureContractVersion,
            Features =
            [
                "canonical-evidence-measurements",
                "work-item-kind",
                "normalized-scope-role",
                "logical-expected-size-band",
                "candidate-expected-size-band",
                "bounded-work-item-kind-factor-ceiling",
            ],
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
            DiscountFactor = 0.25m,
            Range = new CandidatePreflightRangeConfiguration
            {
                Features = model.Range.Features,
                LowerQuantile = model.Range.LowerQuantile,
                UpperQuantile = model.Range.UpperQuantile,
                MinimumExactGroupSamples = model.Range.MinimumExactGroupSamples,
                SparseGroupFallback = model.Range.SparseGroupFallback,
            },
            Dependencies = [".NET 10", "EffortHours.Contracts", "EffortHours.Calibration"],
            LicenseExpression = model.LicenseExpression,
            RuntimeBoundary =
                "Offline deterministic projection of a saved implementation-profile seed estimate " +
                "and its digest-matched canonical evidence bundle; no source body, repository read, " +
                "target execution, network, Git history, provider, or external process.",
            FallbackEstimatorVersion = model.BaselineEstimatorVersion,
            ExplanationForm =
                "Every work item retains its stable ID and records the logical capability point, " +
                "scope-role factor, frozen point group/factor (including any work-item-kind ceiling), " +
                "frozen range group/factors, and proportional allocation; unknown groups retain " +
                "the complete seed capability.",
        };

    private static IReadOnlyList<CandidatePreflightSourceEstimate> BuildSourceReferences(
        CalibrationCorpus corpus,
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs)
    {
        Dictionary<CandidateKey, LogicalCandidateDevelopmentInput> index = inputs.ToDictionary(
            input => Key(input.Estimate));
        return
        [
            .. corpus.Records.OrderBy(record => record.Id, StringComparer.Ordinal).Select(record =>
            {
                LogicalCandidateDevelopmentInput input = index[new CandidateKey(
                    record.Repository.SourceDigest,
                    record.Profile,
                    record.BaselineId)];
                return new CandidatePreflightSourceEstimate
                {
                    RecordId = record.Id,
                    RepositoryId = record.Repository.Id,
                    SourceDigest = record.Repository.SourceDigest,
                    EstimateDigest = input.EstimateDigest,
                    EvidenceDigest = input.EvidenceDigest,
                };
            }),
        ];
    }

    private static void ValidateInputs(
        SamplingPlan plan,
        CalibrationCorpus corpus,
        string corpusDigest,
        CalibrationEvaluationReport seed,
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs,
        LogicalCandidateModel model,
        string sourceCommit)
    {
        LogicalCandidateTransformer.ValidateModel(model);
        if (plan.SamplingPlanVersion != "repository-sampling-plan/1.0.0" ||
            corpus.Id != "efforthours-public-readiness-development" ||
            corpus.Version != "0.3.0" ||
            seed.Partition != CalibrationPartition.Development ||
            seed.RecordCount != 15 ||
            inputs.Count != 15 ||
            model.Training.CorpusId != corpus.Id ||
            model.Training.CorpusVersion != corpus.Version ||
            model.Training.CorpusDigest != corpusDigest ||
            model.Training.ImplementationCommit != sourceCommit ||
            model.Training.RepositoryCount != 15 ||
            model.Training.TargetCount != 2030 ||
            model.Training.Sources.Count != inputs.Count)
        {
            throw new InvalidDataException(
                "Logical-candidate preflight inputs differ from the frozen development boundary or model.");
        }

        LogicalCandidateModel reproduced = LogicalCandidateModelFitter.Fit(
            corpus,
            corpusDigest,
            inputs,
            sourceCommit);
        if (ContractJson.Serialize(reproduced) != ContractJson.Serialize(model))
        {
            throw new InvalidDataException(
                "Logical-candidate model does not reproduce from the frozen development inputs.");
        }

        Dictionary<string, LogicalCandidateTrainingSource> modelSources = model.Training.Sources
            .ToDictionary(source => source.RecordId, StringComparer.Ordinal);
        Dictionary<CandidateKey, LogicalCandidateDevelopmentInput> inputIndex = inputs.ToDictionary(
            input => Key(input.Estimate));
        foreach (CalibrationRecord record in corpus.Records)
        {
            LogicalCandidateDevelopmentInput input = inputIndex[new CandidateKey(
                record.Repository.SourceDigest,
                record.Profile,
                record.BaselineId)];
            LogicalCandidateTrainingSource source = modelSources[record.Id];
            if (input.EstimateDigest != record.SourceEstimateDigest ||
                source.RepositoryId != record.Repository.Id ||
                source.SourceDigest != record.Repository.SourceDigest ||
                source.EstimateDigest != input.EstimateDigest ||
                source.EvidenceDigest != input.EvidenceDigest)
            {
                throw new InvalidDataException(
                    $"Logical-candidate source lineage differs for '{record.Id}'.");
            }
        }
    }

    private static CandidatePreflightArtifactReference Reference(
        string id,
        string version,
        string digest) => new()
        {
            Id = id,
            Version = version,
            Digest = digest,
        };

    private static CandidateKey Key(EstimateReport estimate) => new(
        estimate.Repository.SourceDigest
            ?? throw new InvalidDataException("A development estimate source digest is missing."),
        estimate.Profile,
        estimate.Baseline.Id);

    private readonly record struct CandidateKey(
        string SourceDigest,
        EstimationProfile Profile,
        string BaselineId);
}
