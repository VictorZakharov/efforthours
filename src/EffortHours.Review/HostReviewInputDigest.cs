using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Review;

public static class HostReviewInputDigest
{
    public static string Compute(
        EstimateReport report,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(evidence);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<string> reportErrors = ContractValidation.Validate(report);
        if (reportErrors.Count > 0)
        {
            throw new ArgumentException(
                "The host-review estimate is invalid: " + string.Join(" ", reportErrors),
                nameof(report));
        }

        IReadOnlyList<string> evidenceErrors = ContractValidation.Validate(evidence);
        if (evidenceErrors.Count > 0)
        {
            throw new ArgumentException(
                "The host-review evidence is invalid: " + string.Join(" ", evidenceErrors),
                nameof(evidence));
        }

        if (report.RateCard is not null || report.TotalCost is not null)
        {
            throw new ArgumentException(
                "Host-review identity requires a rate-free estimate.",
                nameof(report));
        }

        if (!RepositoriesMatch(report.Repository, evidence.Repository))
        {
            throw new ArgumentException(
                "The host-review evidence repository does not match the estimate repository.",
                nameof(evidence));
        }

        ReviewInputEnvelope envelope = new()
        {
            Estimate = report,
            Evidence = evidence,
        };
        cancellationToken.ThrowIfCancellationRequested();
        byte[] digest = SHA256.HashData(
            Encoding.UTF8.GetBytes(ContractJson.SerializeCompact(envelope)));
        cancellationToken.ThrowIfCancellationRequested();
        return $"sha256:{Convert.ToHexString(digest).ToLowerInvariant()}";
    }

    private static bool RepositoriesMatch(
        RepositoryDescriptor estimate,
        RepositoryDescriptor evidence) =>
        estimate.Name == evidence.Name &&
        estimate.Scope == evidence.Scope &&
        estimate.SourceDigest == evidence.SourceDigest &&
        estimate.Ecosystems.SequenceEqual(evidence.Ecosystems, StringComparer.Ordinal);

    private sealed record ReviewInputEnvelope
    {
        public required EstimateReport Estimate { get; init; }

        public required RepositoryEvidence Evidence { get; init; }
    }
}
