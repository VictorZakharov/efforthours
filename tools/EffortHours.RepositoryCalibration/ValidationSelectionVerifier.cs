using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal sealed record VerifiedValidationSelection
{
    public required SamplingPlan Plan { get; init; }

    public required ValidationOpeningManifest Opening { get; init; }

    public required CalibrationCorpus Corpus { get; init; }

    public required LogicalCandidateModel Model { get; init; }

    public required string ModelJson { get; init; }

    public required string PlanDigest { get; init; }

    public required string OpeningDigest { get; init; }

    public required string CorpusDigest { get; init; }

    public required string CandidateManifestDigest { get; init; }

    public required string ModelDigest { get; init; }

    public required int CandidateOrdinal { get; init; }

    public required string CandidateKind { get; init; }

    public required decimal WapeTieTolerance { get; init; }

    public IReadOnlyList<string> TieBreakers { get; init; } = [];

    public IReadOnlyList<LogicalCandidateDevelopmentInput> Inputs { get; init; } = [];

    public IReadOnlyList<CandidatePreflightGate> FrozenOperationalGates { get; init; } = [];
}

internal static class ValidationSelectionVerifier
{
    public const string ExpectedCorpusDigest =
        "sha256:dad8ba8c4af5162522bc6fbfc7ee1733eb0ace0f6cdd3b185de98ebb531c3be7";
    public const string ExpectedModelDigest =
        "sha256:492e10b2f427a471a8edcf4f7e3f19d65b098e4822e25c552956c9ce992fa1ea";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static async Task<VerifiedValidationSelection> VerifyAsync(
        ValidationSelectionOptions options,
        CancellationToken cancellationToken)
    {
        ValidatePaths(options);
        await ValidateCheckoutAsync(options, cancellationToken).ConfigureAwait(false);
        string planDigest = await ValidationBoundaryVerifier.RequireDigestAsync(
            options.SamplingPlanPath,
            ValidationBoundaryVerifier.ExpectedPlanDigest,
            cancellationToken).ConfigureAwait(false);
        string openingDigest = await ValidationBoundaryVerifier.RequireDigestAsync(
            options.OpeningPath,
            ValidationReviewPlanBuilder.ExpectedOpeningDigest,
            cancellationToken).ConfigureAwait(false);
        string corpusDigest = await ValidationBoundaryVerifier.RequireDigestAsync(
            options.CorpusPath,
            ExpectedCorpusDigest,
            cancellationToken).ConfigureAwait(false);
        string candidateManifestDigest = await ValidationBoundaryVerifier.RequireDigestAsync(
            options.CandidateManifestPath,
            ValidationBoundaryVerifier.ExpectedCandidateManifestDigest,
            cancellationToken).ConfigureAwait(false);
        string modelDigest = await ValidationBoundaryVerifier.RequireDigestAsync(
            options.ModelPath,
            ExpectedModelDigest,
            cancellationToken).ConfigureAwait(false);

        string manifestJson = await File.ReadAllTextAsync(
            options.CandidateManifestPath,
            cancellationToken).ConfigureAwait(false);
        using JsonDocument manifestDocument = JsonDocument.Parse(manifestJson);
        JsonElement manifest = manifestDocument.RootElement;
        await ValidationCandidateVerifier.ValidateAsync(
            manifest,
            options.RepositoryRoot,
            cancellationToken).ConfigureAwait(false);

        SamplingPlan plan = Read<SamplingPlan>(await File.ReadAllTextAsync(
            options.SamplingPlanPath,
            cancellationToken).ConfigureAwait(false));
        ValidationOpeningManifest opening = Read<ValidationOpeningManifest>(
            await File.ReadAllTextAsync(options.OpeningPath, cancellationToken).ConfigureAwait(false));
        CalibrationCorpus corpus = ContractJson.Deserialize<CalibrationCorpus>(
            await File.ReadAllTextAsync(options.CorpusPath, cancellationToken).ConfigureAwait(false));
        string modelJson = await File.ReadAllTextAsync(options.ModelPath, cancellationToken)
            .ConfigureAwait(false);
        LogicalCandidateModel model = ContractJson.Deserialize<LogicalCandidateModel>(modelJson);
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs =
            await LogicalCandidateDevelopmentLoader.LoadAsync(
                options.SeedOutputDirectory,
                cancellationToken).ConfigureAwait(false);

        JsonElement challenger = manifest.GetProperty("challengers").EnumerateArray().Single();
        JsonElement selectionRule = manifest.GetProperty("validationSelectionRule");
        ValidateBoundary(options, plan, opening, corpus, model, challenger, inputs);
        IReadOnlyList<CandidatePreflightGate> operational = await ReadOperationalGatesAsync(
            options.RepositoryRoot,
            manifest,
            model,
            cancellationToken).ConfigureAwait(false);

        return new VerifiedValidationSelection
        {
            Plan = plan,
            Opening = opening,
            Corpus = corpus,
            Model = model,
            ModelJson = modelJson,
            PlanDigest = planDigest,
            OpeningDigest = openingDigest,
            CorpusDigest = corpusDigest,
            CandidateManifestDigest = candidateManifestDigest,
            ModelDigest = modelDigest,
            CandidateOrdinal = challenger.GetProperty("ordinal").GetInt32(),
            CandidateKind = String(challenger, "candidateKind"),
            WapeTieTolerance = selectionRule.GetProperty("absoluteWapeTieTolerance").GetDecimal(),
            TieBreakers = [.. selectionRule.GetProperty("tieBreakers").EnumerateArray()
                .OrderBy(item => item.GetProperty("ordinal").GetInt32())
                .Select(item => String(item, "criterion"))],
            Inputs = inputs,
            FrozenOperationalGates = operational,
        };
    }

    private static void ValidatePaths(ValidationSelectionOptions options)
    {
        string root = Path.GetFullPath(options.RepositoryRoot);
        if (Path.GetPathRoot(root) == root || !File.Exists(Path.Combine(root, "EffortHours.slnx")))
        {
            throw new InvalidDataException("Repository root is not the EffortHours checkout.");
        }

        string[] inputs =
        [
            options.SamplingPlanPath,
            options.OpeningPath,
            options.CorpusPath,
            options.CandidateManifestPath,
            options.ModelPath,
        ];
        foreach (string path in inputs)
        {
            ValidationBoundaryVerifier.RequireContainedPath(root, path);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Frozen validation-selection input was not found.", path);
            }
        }

        ValidationBoundaryVerifier.RequireContainedPath(root, options.SeedOutputDirectory);
        if (!Directory.Exists(options.SeedOutputDirectory))
        {
            throw new DirectoryNotFoundException("Validation seed output directory was not found.");
        }

        string[] fresh =
        [
            options.CandidateOutputDirectory,
            options.SeedEvaluationPath,
            options.CandidateEvaluationPath,
            options.DecisionPath,
        ];
        foreach (string path in fresh)
        {
            ValidationBoundaryVerifier.RequireContainedPath(root, path);
            if (File.Exists(path) || Directory.Exists(path))
            {
                throw new InvalidDataException(
                    "Validation selection is one-shot; every candidate, evaluation, and decision output must be fresh.");
            }
        }

        bool overlap = fresh.SelectMany((left, index) => fresh.Skip(index + 1)
                .Select(right => (left, right)))
            .Any(pair => IsSameOrNested(pair.left, pair.right) ||
                IsSameOrNested(pair.right, pair.left));
        if (overlap ||
            IsSameOrNested(options.CandidateOutputDirectory, options.SeedOutputDirectory))
        {
            throw new InvalidDataException("Validation selection output paths overlap.");
        }
    }

    private static async Task ValidateCheckoutAsync(
        ValidationSelectionOptions options,
        CancellationToken cancellationToken)
    {
        string head = (await ExternalProcess.RunAsync(
            "git",
            ["-C", options.RepositoryRoot, "rev-parse", "HEAD"],
            cancellationToken).ConfigureAwait(false)).Trim();
        if (!string.Equals(head, options.SourceCommit, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Validation evaluator source commit does not match the checked-out commit.");
        }
    }

    private static void ValidateBoundary(
        ValidationSelectionOptions options,
        SamplingPlan plan,
        ValidationOpeningManifest opening,
        CalibrationCorpus corpus,
        LogicalCandidateModel model,
        JsonElement challenger,
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs)
    {
        SamplingFamily[] families = [.. plan.Families
            .Where(item => item.Partition == "validation")
            .OrderBy(item => item.Id, StringComparer.Ordinal)];
        if (plan.Id != "efforthours-public-readiness" ||
            plan.Version != "0.1.0" ||
            opening.Status != "strict-blind-validation-packets-generated-candidate-values-unavailable" ||
            opening.Families.Count != 9 ||
            opening.Failures.Count != 0 ||
            opening.ContaminatedFamilies.Count != 0 ||
            corpus.Id != "efforthours-public-readiness-validation" ||
            corpus.Version != "1.3.0" ||
            corpus.Records.Count != 9 ||
            corpus.Records.Sum(record => record.Targets.Count) != 2_747 ||
            corpus.Records.Any(record =>
                record.Partition != CalibrationPartition.Validation ||
                record.Review.Status < CalibrationReviewStatus.TeacherEstimate ||
                record.Review.Reviewers.Count == 0) ||
            model.CandidateId != String(challenger, "id") ||
            model.EstimatorVersion != String(challenger, "estimatorVersion") ||
            model.BaselineEstimatorVersion != "seed-rules/0.4.0" ||
            inputs.Count != 9)
        {
            throw new InvalidDataException("Validation selection boundary identity or review maturity changed.");
        }

        string expectedModelPath = ValidationBoundaryVerifier.RequireContainedPath(
            options.RepositoryRoot,
            String(challenger.GetProperty("artifact"), "path"));
        if (!PathComparer().Equals(expectedModelPath, options.ModelPath))
        {
            throw new InvalidDataException("Validation model path differs from the frozen challenger artifact.");
        }

        Dictionary<string, ReproductionFamily> opened = opening.Families.ToDictionary(
            family => family.Id,
            StringComparer.Ordinal);
        Dictionary<string, LogicalCandidateDevelopmentInput> inputBySource = inputs.ToDictionary(
            input => input.Estimate.Repository.SourceDigest!,
            StringComparer.Ordinal);
        foreach (CalibrationRecord record in corpus.Records)
        {
            SamplingFamily family = families.Single(item => item.Id == record.Repository.Id);
            ReproductionFamily reproduction = opened[record.Repository.Id];
            DevelopmentAnalysis analysis = reproduction.Analysis
                ?? throw new InvalidDataException($"Opening analysis is absent for '{record.Repository.Id}'.");
            LogicalCandidateDevelopmentInput input = inputBySource[record.Repository.SourceDigest];
            if (family.PrimaryStratum != reproduction.PrimaryStratum ||
                family.SourceSnapshot.CommitSha != record.Source.Revision ||
                record.SourceEstimatorVersion != "seed-rules/0.4.0" ||
                record.SourceEstimateDigest != analysis.EstimateDigest ||
                input.EstimateDigest != analysis.EstimateDigest ||
                JsonArtifactDigest.Compute(input.EstimateJson) != analysis.EstimateArtifactSha256 ||
                input.EvidenceDigest != analysis.EvidenceArtifactSha256 ||
                input.Evidence.Repository.SourceDigest != record.Repository.SourceDigest ||
                input.Estimate.Repository.SourceDigest != record.Repository.SourceDigest ||
                input.Estimate.EstimatorVersion != "seed-rules/0.4.0" ||
                input.Estimate.Profile != record.Profile ||
                input.Estimate.Baseline.Id != record.BaselineId)
            {
                throw new InvalidDataException(
                    $"Validation source, evidence, estimate, or review lineage changed for '{record.Id}'.");
            }
        }
    }

    private static async Task<IReadOnlyList<CandidatePreflightGate>> ReadOperationalGatesAsync(
        string repositoryRoot,
        JsonElement manifest,
        LogicalCandidateModel model,
        CancellationToken cancellationToken)
    {
        JsonElement reference = manifest.GetProperty("developmentBoundary")
            .GetProperty("aggregateOperationalPreflight");
        string path = ValidationBoundaryVerifier.RequireContainedPath(
            repositoryRoot,
            String(reference, "path"));
        string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        CandidatePreflightReport report = ContractJson.Deserialize<CandidatePreflightReport>(json);
        string[] required =
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
        CandidatePreflightGate[] gates = [.. report.Candidate.Gates
            .Where(gate => required.Contains(gate.Id, StringComparer.Ordinal))];
        if (report.Candidate.Id != model.CandidateId ||
            gates.Length != required.Length ||
            gates.Any(gate => !gate.Passed || gate.Status != "passed") ||
            !gates.Select(gate => gate.Id).SequenceEqual(required, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Frozen operational eligibility is not a complete 12-gate pass.");
        }

        return gates;
    }

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(
        json,
        JsonOptions)
        ?? throw new InvalidDataException($"Validation artifact '{typeof(T).Name}' is empty.");

    private static string String(JsonElement element, string property) =>
        element.GetProperty(property).GetString()
        ?? throw new InvalidDataException($"Manifest property '{property}' is null.");

    private static bool IsSameOrNested(string candidate, string parent)
    {
        string childPath = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar);
        string parentPath = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar);
        string prefix = parentPath + Path.DirectorySeparatorChar;
        return PathComparer().Equals(childPath, parentPath) ||
            childPath.StartsWith(prefix, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }

    private static StringComparer PathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
