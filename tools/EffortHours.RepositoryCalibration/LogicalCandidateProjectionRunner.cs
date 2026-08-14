using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class LogicalCandidateProjectionRunner
{
    public const int MaximumWorkItems = 250_000;
    public const int MaximumEvidenceFacts = 1_000_000;
    public const long MaximumEvidenceReferences = 4_000_000;

    private static readonly HashSet<string> SupportedStrata = new(StringComparer.Ordinal)
    {
        "dotnet",
        "javascript-typescript",
        "mixed-dotnet-javascript-typescript",
    };

    public static async Task RunAsync(
        LogicalCandidateProjectionOptions options,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        string estimateJson = await File.ReadAllTextAsync(options.EstimatePath, cancellationToken)
            .ConfigureAwait(false);
        string evidenceJson = await File.ReadAllTextAsync(options.EvidencePath, cancellationToken)
            .ConfigureAwait(false);
        string modelJson = await File.ReadAllTextAsync(options.ModelPath, cancellationToken)
            .ConfigureAwait(false);
        ValidateArtifactDigest(modelJson, options.ExpectedModelDigest);

        EstimateReport estimate = ContractJson.Deserialize<EstimateReport>(estimateJson);
        RepositoryEvidence evidence = ContractJson.Deserialize<RepositoryEvidence>(evidenceJson);
        LogicalCandidateModel model = ContractJson.Deserialize<LogicalCandidateModel>(modelJson);
        ValidateContracts(estimate, evidence);
        LogicalCandidateTransformer.ValidateModel(model);

        LogicalCandidateProjectionResult result = Project(
            estimate,
            evidence,
            model,
            options.PrimaryStratum,
            cancellationToken);
        if (result.Diagnostic is not null)
        {
            await standardError.WriteLineAsync(result.Diagnostic).ConfigureAwait(false);
        }

        await standardOutput.WriteAsync(ContractJson.SerializeDocument(result.Estimate))
            .ConfigureAwait(false);
    }

    internal static LogicalCandidateProjectionResult Project(
        EstimateReport estimate,
        RepositoryEvidence evidence,
        LogicalCandidateModel model,
        string primaryStratum,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogicalCandidateTransformer.ValidateModel(model);
        ValidateBounds(estimate, evidence);
        if (!SupportedStrata.Contains(primaryStratum))
        {
            return new LogicalCandidateProjectionResult(
                estimate,
                $"Candidate '{model.CandidateId}' does not support primary stratum " +
                $"'{primaryStratum}'; retained the complete '{model.BaselineEstimatorVersion}' seed fallback.");
        }

        EstimateReport candidate = LogicalCandidateTransformer.Transform(
            estimate,
            evidence,
            model,
            cancellationToken);
        IReadOnlyList<string> errors = ContractValidation.Validate(candidate);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                $"Logical candidate projection is invalid: {string.Join("; ", errors)}");
        }

        return new LogicalCandidateProjectionResult(candidate, null);
    }

    internal static void ValidateArtifactDigest(string modelJson, string expectedDigest)
    {
        string actual = JsonArtifactDigest.Compute(modelJson);
        if (!string.Equals(actual, expectedDigest, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Logical-candidate model digest differs: expected {expectedDigest}; actual {actual}.");
        }
    }

    private static void ValidateContracts(
        EstimateReport estimate,
        RepositoryEvidence evidence)
    {
        IReadOnlyList<string> estimateErrors = ContractValidation.Validate(estimate);
        IReadOnlyList<string> evidenceErrors = ContractValidation.Validate(evidence);
        if (estimateErrors.Count > 0 || evidenceErrors.Count > 0)
        {
            throw new InvalidDataException(
                "Logical-candidate saved input is invalid: " +
                string.Join("; ", estimateErrors.Concat(evidenceErrors)));
        }

        if (!string.Equals(
                estimate.Repository.SourceDigest,
                evidence.Repository.SourceDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Logical-candidate estimate and evidence source digests differ.");
        }
    }

    private static void ValidateBounds(
        EstimateReport estimate,
        RepositoryEvidence evidence)
    {
        long references = estimate.WorkItems.Sum(item => (long)item.EvidenceIds.Count);
        if (estimate.WorkItems.Count > MaximumWorkItems ||
            evidence.Facts.Count > MaximumEvidenceFacts ||
            references > MaximumEvidenceReferences)
        {
            throw new InvalidDataException(
                "Logical-candidate saved input exceeds the bounded work-item, fact, or evidence-reference limit.");
        }
    }
}

internal sealed record LogicalCandidateProjectionResult(
    EstimateReport Estimate,
    string? Diagnostic);
