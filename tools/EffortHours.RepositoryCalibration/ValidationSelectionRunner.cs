using System.Text;
using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class ValidationSelectionRunner
{
    public static async Task RunAsync(
        ValidationSelectionOptions options,
        CancellationToken cancellationToken)
    {
        VerifiedValidationSelection verified = await ValidationSelectionVerifier.VerifyAsync(
            options,
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, CalibrationRecord> records = verified.Corpus.Records.ToDictionary(
            record => record.Repository.SourceDigest,
            StringComparer.Ordinal);
        Dictionary<string, SamplingFamily> families = verified.Plan.Families
            .Where(family => family.Partition == "validation")
            .ToDictionary(family => family.Id, StringComparer.Ordinal);

        List<ProjectedValidationEstimate> projections = [];
        foreach (LogicalCandidateDevelopmentInput input in verified.Inputs
                     .OrderBy(item => records[item.Estimate.Repository.SourceDigest!].Id,
                         StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CalibrationRecord record = records[input.Estimate.Repository.SourceDigest!];
            SamplingFamily family = families[record.Repository.Id];
            LogicalCandidateProjectionResult result = LogicalCandidateProjectionRunner.Project(
                input.Estimate,
                input.Evidence,
                verified.Model,
                family.PrimaryStratum,
                cancellationToken);
            if (result.Diagnostic is not null ||
                result.Estimate.EstimatorVersion != verified.Model.EstimatorVersion)
            {
                throw new InvalidDataException(
                    $"Frozen candidate did not project validation record '{record.Id}'.");
            }

            string document = ContractJson.SerializeDocument(result.Estimate);
            projections.Add(new ProjectedValidationEstimate(
                record,
                input,
                result.Estimate,
                document,
                CalibrationDigest.Compute(result.Estimate),
                JsonArtifactDigest.Compute(document)));
        }

        EstimateReport[] seeds = [.. verified.Inputs.Select(input => input.Estimate)];
        EstimateReport[] candidates = [.. projections.Select(projection => projection.Estimate)];
        CalibrationEvaluationReport seedEvaluation = CalibrationEvaluator.Evaluate(
            verified.Corpus,
            seeds,
            CalibrationPartition.Validation);
        CalibrationEvaluationReport candidateEvaluation = CalibrationEvaluator.Evaluate(
            verified.Corpus,
            candidates,
            CalibrationPartition.Validation);
        string seedEvaluationDocument = ContractJson.SerializeDocument(seedEvaluation);
        string candidateEvaluationDocument = ContractJson.SerializeDocument(candidateEvaluation);
        string seedEvaluationDigest = JsonArtifactDigest.Compute(seedEvaluationDocument);
        string candidateEvaluationDigest = JsonArtifactDigest.Compute(candidateEvaluationDocument);

        CandidatePreflightAssessment numerical = CandidatePreflightAssessment.Build(
            verified.Corpus,
            candidates,
            seedEvaluation,
            candidateEvaluation,
            CalibrationPartition.Validation);
        CandidatePreflightGate[] numericalGates = [.. numerical.Gates.Take(16)];
        CandidatePreflightGate[] validationOperational =
        [
            .. CandidateOperationalGateAssessment.Build(
                verified.Plan,
                verified.Corpus,
                verified.Inputs,
                candidates,
                seedEvaluation,
                candidateEvaluation,
                verified.Model,
                verified.ModelJson,
                verified.ModelDigest,
                CalibrationPartition.Validation),
        ];
        CandidatePreflightGate[] validationGates = [.. numericalGates, .. validationOperational];
        IReadOnlyList<CandidatePreflightGate> boundaryGates =
            ValidationSelectionAssessment.BuildBoundaryGates(verified);
        bool eligible = boundaryGates.Concat(validationGates)
            .Concat(verified.FrozenOperationalGates)
            .All(gate => gate.Passed && gate.Status == "passed");
        string[] rejectionReasons = eligible
            ? []
            : [.. boundaryGates.Concat(validationGates).Concat(verified.FrozenOperationalGates)
                .Where(gate => !gate.Passed || gate.Status != "passed")
                .Select(gate => $"{gate.Id}: {gate.Rationale}")];

        ValidationSelectionReport report = BuildReport(
            options,
            verified,
            projections,
            seedEvaluation,
            seedEvaluationDigest,
            candidateEvaluationDigest,
            numerical.Metrics,
            boundaryGates,
            validationGates,
            eligible,
            rejectionReasons);
        string reportDocument = ContractJson.SerializeDocument(report);
        ValidationSelectionReport roundTrip =
            ContractJson.Deserialize<ValidationSelectionReport>(reportDocument);
        if (ContractJson.SerializeDocument(roundTrip) != reportDocument)
        {
            throw new InvalidDataException("Validation selection record is not canonical-round-trip stable.");
        }

        await WriteOutputsAsync(
            options,
            projections,
            seedEvaluationDocument,
            candidateEvaluationDocument,
            cancellationToken).ConfigureAwait(false);
        await WriteNewTextAsync(options.DecisionPath, reportDocument, cancellationToken)
            .ConfigureAwait(false);
    }

    private static ValidationSelectionReport BuildReport(
        ValidationSelectionOptions options,
        VerifiedValidationSelection verified,
        IReadOnlyList<ProjectedValidationEstimate> projections,
        CalibrationEvaluationReport seedEvaluation,
        string seedEvaluationDigest,
        string candidateEvaluationDigest,
        CandidatePreflightMetrics metrics,
        IReadOnlyList<CandidatePreflightGate> boundaryGates,
        IReadOnlyList<CandidatePreflightGate> validationGates,
        bool eligible,
        IReadOnlyList<string> rejectionReasons)
    {
        ValidationSelectionArtifact seedArtifact = ValidationSelectionAssessment.Artifact(
            "efforthours-public-readiness-validation-seed",
            seedEvaluation.EvaluatorVersion,
            seedEvaluationDigest);
        ValidationSelectionArtifact candidateArtifact = ValidationSelectionAssessment.Artifact(
            "efforthours-public-readiness-validation-logical-capability-0.3.0",
            seedEvaluation.EvaluatorVersion,
            candidateEvaluationDigest);
        return new ValidationSelectionReport
        {
            Status = eligible
                ? "validation-selected-candidate-test-sealed"
                : "validation-rejected-all-challengers-test-sealed",
            EvaluationImplementationCommit = options.SourceCommit,
            Inputs = new ValidationSelectionInputs
            {
                SamplingPlan = ValidationSelectionAssessment.Artifact(
                    verified.Plan.Id,
                    verified.Plan.Version,
                    verified.PlanDigest),
                Opening = ValidationSelectionAssessment.Artifact(
                    verified.Opening.Id,
                    verified.Opening.Version,
                    verified.OpeningDigest),
                Corpus = ValidationSelectionAssessment.Artifact(
                    verified.Corpus.Id,
                    verified.Corpus.Version,
                    verified.CorpusDigest),
                CandidateManifest = ValidationSelectionAssessment.Artifact(
                    "efforthours-public-readiness-candidate-freeze",
                    "1.2.0",
                    verified.CandidateManifestDigest),
                CandidateModel = ValidationSelectionAssessment.Artifact(
                    verified.Model.Id,
                    verified.Model.ModelVersion,
                    verified.ModelDigest),
                SeedEvaluation = seedArtifact,
                CandidateEvaluation = candidateArtifact,
                Projections = [.. projections.Select(projection => new ValidationProjectionArtifact
                {
                    RecordId = projection.Record.Id,
                    RepositoryId = projection.Record.Repository.Id,
                    SourceDigest = projection.Record.Repository.SourceDigest,
                    SeedEstimateDigest = projection.Input.EstimateDigest,
                    CandidateEstimateDigest = projection.EstimateDigest,
                    CandidateArtifactDigest = projection.ArtifactDigest,
                })],
            },
            Boundary = new ValidationSelectionBoundary
            {
                ValidationAccess =
                    "exact seed and sole frozen-challenger outputs generated and evaluated once",
                ValidationFamilyCount = verified.Corpus.Records.Count,
                ValidationTargetCount = verified.Corpus.Records.Sum(record => record.Targets.Count),
                TestAccess = "sealed; no source, label, seed, or challenger output accessed",
                TestSourceAccessed = false,
                TestLabelsAuthored = false,
                TestCandidateOutputsGenerated = false,
            },
            Baseline = ValidationSelectionAssessment.BuildBaseline(
                seedEvaluation,
                seedEvaluationDigest),
            Challengers =
            [
                new ValidationSelectionCandidate
                {
                    Ordinal = verified.CandidateOrdinal,
                    Id = verified.Model.CandidateId,
                    CandidateKind = verified.CandidateKind,
                    EstimatorVersion = verified.Model.EstimatorVersion,
                    Eligible = eligible,
                    Metrics = metrics,
                    BoundaryGates = boundaryGates,
                    ValidationGates = validationGates,
                    FrozenOperationalGates = verified.FrozenOperationalGates,
                    RejectionReasons = rejectionReasons,
                },
            ],
            SelectionRule = new ValidationSelectionRule
            {
                PrimaryMetric = "repository-total expected WAPE",
                Direction = "lowest",
                AbsoluteWapeTieTolerance = verified.WapeTieTolerance,
                TieBreakers = verified.TieBreakers,
            },
            Decision = new ValidationSelectionDecision
            {
                Status = eligible ? "selected" : "rejected-all",
                SelectedCandidateId = eligible ? verified.Model.CandidateId : null,
                TestAuthorized = eligible,
                CandidateAdmitted = false,
                ShippedEstimatorVersion = "seed-rules/0.4.0",
                Rationale = eligible
                    ? "The sole frozen challenger passed every validation and previously frozen operational gate. It is selected for the one-time sealed test; selection is not admission."
                    : "The sole frozen challenger failed at least one required gate. No challenger is selected, test remains sealed, and the seed remains shipped.",
                NextBoundary = eligible
                    ? "Commit this immutable selection record before authoring, revealing, or evaluating the sealed test labels. Then evaluate only this selected candidate and seed exactly once."
                    : "Close this attempt without test disclosure. Any successor requires a new candidate identity, manifest, and fresh sealed holdout boundary.",
            },
        };
    }

    private static async Task WriteOutputsAsync(
        ValidationSelectionOptions options,
        IReadOnlyList<ProjectedValidationEstimate> projections,
        string seedEvaluation,
        string candidateEvaluation,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.CandidateOutputDirectory);
        foreach (ProjectedValidationEstimate projection in projections)
        {
            string directory = Path.Combine(
                options.CandidateOutputDirectory,
                Slug(projection.Record.Repository.Name));
            Directory.CreateDirectory(directory);
            await WriteNewTextAsync(
                Path.Combine(directory, "candidate-estimate.json"),
                projection.Document,
                cancellationToken).ConfigureAwait(false);
        }

        await WriteNewTextAsync(options.SeedEvaluationPath, seedEvaluation, cancellationToken)
            .ConfigureAwait(false);
        await WriteNewTextAsync(
            options.CandidateEvaluationPath,
            candidateEvaluation,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteNewTextAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            65_536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using StreamWriter writer = new(stream, new UTF8Encoding(false));
        await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private static string Slug(string value)
    {
        string slug = new([.. value.ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')]);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }

    private sealed record ProjectedValidationEstimate(
        CalibrationRecord Record,
        LogicalCandidateDevelopmentInput Input,
        EstimateReport Estimate,
        string Document,
        string EstimateDigest,
        string ArtifactDigest);
}
