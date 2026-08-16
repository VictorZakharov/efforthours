using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationUncertaintyInputValidator
{
    public static void Validate(EstimateReport estimate, RepositoryEvidence evidence)
    {
        List<string> errors =
        [
            .. ContractValidation.Validate(estimate),
            .. ContractValidation.Validate(evidence),
        ];
        if (!IsCanonicalSha256(estimate.Repository.SourceDigest))
        {
            errors.Add(
                "estimate.repository.sourceDigest must be a canonical lowercase SHA-256 digest " +
                "for uncertainty feature projection.");
        }

        if (!IsCanonicalSha256(evidence.Repository.SourceDigest))
        {
            errors.Add(
                "evidence.repository.sourceDigest must be a canonical lowercase SHA-256 digest " +
                "for uncertainty feature projection.");
        }

        if (!string.Equals(
                estimate.Repository.SourceDigest,
                evidence.Repository.SourceDigest,
                StringComparison.Ordinal))
        {
            errors.Add("Estimate and evidence repository source digests do not match.");
        }

        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors.Distinct(StringComparer.Ordinal));
        }
    }

    private static bool IsCanonicalSha256(string? value)
    {
        if (value is null ||
            value.Length != 71 ||
            !value.StartsWith("sha256:", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (char character in value.AsSpan(7))
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }
}
