using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class ChangeCalibrationIdentity
{
    public const string IdentityVersion = "change-final-delta/1.0.0";

    public static ChangeCalibrationReference CreateReference(
        ChangeEstimateReport estimate,
        string caseId,
        IReadOnlyList<string> coverageTags)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentNullException.ThrowIfNull(coverageTags);

        string finalDeltaDigest = ComputeFinalDeltaDigest(
            estimate.Evidence.BaseEvidenceDigest,
            estimate.Evidence.HeadEvidenceDigest);
        return new ChangeCalibrationReference
        {
            Id = caseId,
            SelectionKind = estimate.Selection.Kind,
            BaseObjectId = estimate.Selection.Base.ObjectId,
            HeadObjectId = estimate.Selection.Head.ObjectId,
            BaseEvidenceDigest = estimate.Evidence.BaseEvidenceDigest,
            HeadEvidenceDigest = estimate.Evidence.HeadEvidenceDigest,
            FinalDeltaDigest = finalDeltaDigest,
            CoverageTags = [.. coverageTags
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
        };
    }

    public static string ComputeFinalDeltaDigest(ChangeEstimateReport estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        return ComputeFinalDeltaDigest(
            estimate.Evidence.BaseEvidenceDigest,
            estimate.Evidence.HeadEvidenceDigest);
    }

    public static string ComputeFinalDeltaDigest(
        string baseEvidenceDigest,
        string headEvidenceDigest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseEvidenceDigest);
        ArgumentException.ThrowIfNullOrWhiteSpace(headEvidenceDigest);

        string identity = string.Join(
            '\n',
            IdentityVersion,
            baseEvidenceDigest,
            headEvidenceDigest);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return $"sha256:{Convert.ToHexString(digest).ToLowerInvariant()}";
    }
}
