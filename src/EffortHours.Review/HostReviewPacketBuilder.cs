using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Review;

public static class HostReviewPacketBuilder
{
    public static HostReviewPacket Build(
        EstimateReport report,
        RepositoryEvidence evidence,
        HostReviewModelIdentity? reviewerModel = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(evidence);

        cancellationToken.ThrowIfCancellationRequested();
        string inputDigest = HostReviewInputDigest.Compute(
            report,
            evidence,
            cancellationToken);
        EstimateViewReport review = EstimateProjector.Project(report, EstimateViewKind.Review);
        List<HostReviewCandidate> candidates = [];
        foreach (CapabilityViewEntry entry in review.ReviewQueue)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EstimateExplanation explanation = EstimateProjector.Explain(
                report,
                evidence,
                entry.Id);
            candidates.Add(HostReviewCandidateFactory.Create(
                explanation,
                entry.ReviewReasons));
        }

        Dictionary<string, EvidenceFact> factsById = evidence.Facts
            .ToDictionary(fact => fact.Id, StringComparer.Ordinal);
        string[] referencedEvidenceIds =
        [
            .. candidates
                .SelectMany(candidate => candidate.EvidenceIds)
                .Distinct(StringComparer.Ordinal),
        ];
        string[] availableEvidenceIds =
        [.. referencedEvidenceIds.Where(factsById.ContainsKey)];
        string[] includedEvidenceIds =
        [.. availableEvidenceIds.Take(HostReviewProtocol.MaximumPacketEvidenceFacts)];
        string[] missingEvidenceIds =
        [
            .. referencedEvidenceIds
                .Where(id => !factsById.ContainsKey(id))
                .Order(StringComparer.Ordinal),
        ];

        HostReviewPacket packet = new()
        {
            InputDigest = inputDigest,
            EstimatorVersion = report.EstimatorVersion,
            Repository = report.Repository,
            Profile = report.Profile,
            Baseline = report.Baseline,
            TotalEffort = report.TotalEffort,
            Categories =
            [
                .. review.Categories.Select(category => category with { Cost = null }),
            ],
            Estimators = BuildEstimators(report),
            Candidates = candidates,
            EvidenceFacts = [.. includedEvidenceIds.Select(id => factsById[id])],
            MissingEvidenceIds = missingEvidenceIds,
            Omissions = new HostReviewPacketOmissions
            {
                CapabilityCount = review.Omissions.CapabilityCount,
                CapabilityExpectedHours = review.Omissions.CapabilityExpectedHours,
                EvidenceFactCount = availableEvidenceIds.Length - includedEvidenceIds.Length,
                MissingEvidenceFactCount = missingEvidenceIds.Length,
            },
            ReviewerModel = reviewerModel ?? new HostReviewModelIdentity
            {
                IsAvailable = false,
            },
            AvailableQueries = Enum.GetValues<HostReviewQueryKind>(),
            StructuralLimits = new HostReviewStructuralLimits
            {
                PacketCapabilities = HostReviewProtocol.MaximumPacketCapabilities,
                PacketEvidenceFacts = HostReviewProtocol.MaximumPacketEvidenceFacts,
                QueryEvidenceFacts = HostReviewProtocol.MaximumQueryEvidenceFacts,
                ScopeCapabilities = HostReviewProtocol.MaximumScopeCapabilities,
                SourceLines = HostReviewProtocol.MaximumSourceLines,
                SourceCharacters = HostReviewProtocol.MaximumSourceCharacters,
                SourceBytes = HostReviewProtocol.MaximumSourceBytes,
            },
            Diagnostics = report.Diagnostics,
            Assumptions = report.Assumptions,
            Verification = report.Verification,
            ReviewObjective =
                "Review only consequential uncertainty in the selected capabilities. " +
                "Estimate counterfactual non-AI replacement effort, not actual historical labor.",
            LocalBaselineComplete = true,
            PricingIncluded = false,
            CallerResponsibilities =
            [
                "Choose and authorize any AI provider used by the surrounding session.",
                "Decide whether repository metadata or explicitly requested source may be disclosed.",
                "Apply the provider's privacy, retention, and contractual requirements.",
                "Treat proposed adjustments as review input until separately admitted and applied.",
            ],
            StatusDisclaimer =
                "The bundled local estimator and this host-review protocol are experimental and uncalibrated.",
        };

        IReadOnlyList<string> errors = ContractValidation.Validate(packet);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "The host-review packet is invalid: " + string.Join(" ", errors));
        }

        return packet;
    }

    private static EstimatorReference[] BuildEstimators(EstimateReport report) =>
    [
        .. report.WorkItems
            .Concat(report.ProfessionalizationGap)
            .Select(item => item.Estimator)
            .Distinct()
            .OrderBy(estimator => estimator.Id, StringComparer.Ordinal)
            .ThenBy(estimator => estimator.Version, StringComparer.Ordinal)
            .ThenBy(estimator => estimator.Kind),
    ];
}
