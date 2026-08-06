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

        byte[] bytes = Encoding.UTF8.GetBytes(ContractJson.SerializeCompact(estimate));
        return $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
    }
}
