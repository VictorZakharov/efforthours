using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class CalibrationDigest
{
    public static string Compute(EstimateReport estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);

        return ComputeCanonical(estimate);
    }

    public static string Compute(ChangeEstimateReport estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);

        return ComputeCanonical(estimate);
    }

    public static string Compute(CalibrationCorpus corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);

        return ComputeCanonical(corpus);
    }

    public static string Compute(RepositoryEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        return ComputeCanonical(evidence);
    }

    public static string Compute(CalibrationUncertaintyFeatureContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        return ComputeCanonical(contract);
    }

    public static string Compute(CalibrationUncertaintyFeatureReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return ComputeCanonical(report);
    }

    public static string Compute(CalibrationUncertaintyStructuralFeatureReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return ComputeCanonical(report);
    }

    public static string Compute(CalibrationUncertaintyGraphFeatureReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return ComputeCanonical(report);
    }

    public static string Compute(CalibrationUncertaintyStructuralEvaluationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return ComputeCanonical(policy);
    }

    public static string Compute(CalibrationUncertaintyStructuralEvaluationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return ComputeCanonical(report);
    }

    public static string Compute(CalibrationUncertaintySupportPopulation population)
    {
        ArgumentNullException.ThrowIfNull(population);

        return ComputeCanonical(population);
    }

    public static string Compute(CalibrationUncertaintySupportProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return ComputeCanonical(profile);
    }

    public static string Compute(CalibrationUncertaintyEvaluationReport evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        return ComputeCanonical(evaluation);
    }

    public static string Compute(CalibrationUncertaintySupportEvaluationReport evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        return ComputeCanonical(evaluation);
    }

    internal static string ComputeSequence(IEnumerable<string> values) =>
        ComputeCanonical(values.ToArray());

    internal static string ComputeStringSet(IEnumerable<string> values) =>
        ComputeCanonical(values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());

    private static string ComputeCanonical<T>(T value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(ContractJson.SerializeCompact(value));
        return $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
    }
}
