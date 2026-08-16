using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class ManualQaCandidateProjectionRunner
{
    public const int MaximumWorkItems = 250_000;

    public static async Task RunAsync(
        ManualQaCandidateProjectionOptions options,
        TextWriter standardOutput,
        CancellationToken cancellationToken)
    {
        string estimateJson = await File.ReadAllTextAsync(options.EstimatePath, cancellationToken)
            .ConfigureAwait(false);
        string policyJson = await File.ReadAllTextAsync(options.PolicyPath, cancellationToken)
            .ConfigureAwait(false);
        ValidateArtifactDigest(policyJson, options.ExpectedPolicyDigest);

        EstimateReport estimate = ContractJson.Deserialize<EstimateReport>(estimateJson);
        ManualQaCandidatePolicy policy = ContractJson.Deserialize<ManualQaCandidatePolicy>(policyJson);
        EstimateReport candidate = Project(estimate, policy, cancellationToken);
        string document = ContractJson.SerializeDocument(candidate);
        if (options.OutputPath is null)
        {
            await standardOutput.WriteAsync(document).ConfigureAwait(false);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(options.OutputPath)!);
        await File.WriteAllTextAsync(options.OutputPath, document, cancellationToken)
            .ConfigureAwait(false);
    }

    internal static EstimateReport Project(
        EstimateReport estimate,
        ManualQaCandidatePolicy policy,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> errors = ContractValidation.Validate(estimate);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                $"Manual-QA candidate source estimate is invalid: {string.Join("; ", errors)}");
        }

        ManualQaCandidateTransformer.ValidatePolicy(policy);
        int retained = estimate.WorkItems.Count(item =>
            item.Category != EffortCategory.ManualValidationDebuggingAndHardening);
        int derived = estimate.WorkItems.Count(item =>
            ManualQaCandidateTransformer.EligibleCategories.Contains(item.Category) &&
            item.Hours.Expected > 0m);
        if (estimate.WorkItems.Count > MaximumWorkItems || retained + derived > MaximumWorkItems)
        {
            throw new InvalidDataException(
                "Manual-QA candidate input or projected output exceeds the bounded work-item limit.");
        }

        EstimateReport candidate = ManualQaCandidateTransformer.Transform(
            estimate,
            policy,
            cancellationToken);
        IReadOnlyList<string> candidateErrors = ContractValidation.Validate(candidate);
        if (candidateErrors.Count > 0)
        {
            throw new InvalidDataException(
                $"Manual-QA candidate projection is invalid: {string.Join("; ", candidateErrors)}");
        }

        return candidate;
    }

    internal static void ValidateArtifactDigest(string policyJson, string expectedDigest)
    {
        string actual = JsonArtifactDigest.Compute(policyJson);
        if (!string.Equals(actual, expectedDigest, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Manual-QA candidate policy digest differs: expected {expectedDigest}; actual {actual}.");
        }
    }
}
