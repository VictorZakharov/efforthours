using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class LogicalCandidateDevelopmentLoader
{
    public static async Task<IReadOnlyList<LogicalCandidateDevelopmentInput>> LoadAsync(
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Logical-candidate output directory was not found: {outputDirectory}");
        }

        List<LogicalCandidateDevelopmentInput> inputs = [];
        foreach (string estimatePath in Directory.EnumerateFiles(
                     outputDirectory,
                     "source-estimate.json",
                     SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            string directory = Path.GetDirectoryName(estimatePath)!;
            string evidencePath = Path.Combine(directory, "repository-evidence.json");
            if (!File.Exists(evidencePath))
            {
                throw new FileNotFoundException(
                    $"Repository evidence paired with '{estimatePath}' was not found.",
                    evidencePath);
            }

            string estimateJson = await File.ReadAllTextAsync(estimatePath, cancellationToken)
                .ConfigureAwait(false);
            string evidenceJson = await File.ReadAllTextAsync(evidencePath, cancellationToken)
                .ConfigureAwait(false);
            EstimateReport estimate = ContractJson.Deserialize<EstimateReport>(estimateJson);
            RepositoryEvidence evidence = ContractJson.Deserialize<RepositoryEvidence>(evidenceJson);
            inputs.Add(new LogicalCandidateDevelopmentInput
            {
                DirectoryPath = directory,
                EstimateJson = estimateJson,
                EvidenceJson = evidenceJson,
                Estimate = estimate,
                Evidence = evidence,
                EstimateDigest = CalibrationDigest.Compute(estimate),
                EvidenceDigest = JsonArtifactDigest.Compute(evidenceJson),
            });
        }

        return inputs;
    }
}
