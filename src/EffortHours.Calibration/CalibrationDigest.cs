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

    internal static string ComputeStringSet(IEnumerable<string> values) =>
        ComputeCanonical(values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());

    private static string ComputeCanonical<T>(T value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(ContractJson.SerializeCompact(value));
        return $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
    }
}
