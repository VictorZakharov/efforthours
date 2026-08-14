using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.RepositoryCalibration;

internal static class CandidateBenchmarkProjectionRunner
{
    public static async Task RunAsync(
        CandidateBenchmarkProjectionOptions options,
        TextWriter standardOutput,
        CancellationToken cancellationToken)
    {
        string evidenceJson = await File.ReadAllTextAsync(
                options.EvidencePath,
                cancellationToken)
            .ConfigureAwait(false);
        RepositoryEvidence evidence = ContractJson.Deserialize<RepositoryEvidence>(evidenceJson);
        IReadOnlyList<string> evidenceErrors = ContractValidation.Validate(evidence);
        if (evidenceErrors.Count > 0)
        {
            throw new InvalidDataException(
                $"Candidate benchmark evidence is invalid: {string.Join("; ", evidenceErrors)}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        EstimateReport seed = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Implementation);
        EstimateReport output = seed;
        if (options.ApplyCandidate)
        {
            string modelJson = await File.ReadAllTextAsync(
                    options.ModelPath!,
                    cancellationToken)
                .ConfigureAwait(false);
            LogicalCandidateProjectionRunner.ValidateArtifactDigest(
                modelJson,
                options.ExpectedModelDigest!);
            LogicalCandidateModel model = ContractJson.Deserialize<LogicalCandidateModel>(modelJson);
            output = LogicalCandidateProjectionRunner.Project(
                seed,
                evidence,
                model,
                "dotnet",
                cancellationToken).Estimate;
        }

        await standardOutput.WriteAsync(ContractJson.SerializeDocument(output)).ConfigureAwait(false);
    }
}
