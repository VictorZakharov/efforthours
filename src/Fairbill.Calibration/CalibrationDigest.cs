using System.Security.Cryptography;
using System.Text;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Calibration;

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

    private static string ComputeCanonical<T>(T value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(ContractJson.SerializeCompact(value));
        return $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
    }
}
