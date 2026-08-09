using EffortHours.Contracts.V1;

namespace EffortHours.Review;

internal static class HostReviewCandidateFactory
{
    public static HostReviewCandidate Create(
        EstimateExplanation explanation,
        IReadOnlyList<string>? reviewReasons = null)
    {
        ArgumentNullException.ThrowIfNull(explanation);

        CapabilityViewEntry capability = explanation.Capability with
        {
            Cost = null,
            ReviewReasons = Normalize(reviewReasons ?? explanation.Capability.ReviewReasons),
        };
        return new HostReviewCandidate
        {
            Capability = capability,
            EstimationReasons = Normalize(explanation.WorkItems.Select(item => item.Reason)),
            EvidenceIds = Normalize(
                explanation.WorkItems.SelectMany(item => item.EvidenceIds)),
            Estimators =
            [
                .. explanation.Estimators
                    .Distinct()
                    .OrderBy(estimator => estimator.Id, StringComparer.Ordinal)
                    .ThenBy(estimator => estimator.Version, StringComparer.Ordinal)
                    .ThenBy(estimator => estimator.Kind),
            ],
            Assumptions = Normalize(explanation.Assumptions),
            Exclusions = Normalize(explanation.Exclusions),
            CorrelationGroups = Normalize(explanation.CorrelationGroups),
            UncertaintyReasons = Normalize(explanation.UncertaintyReasons),
        };
    }

    private static string[] Normalize(IEnumerable<string> values) =>
    [
        .. values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];
}
