using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;
using EffortHours.Review;

namespace EffortHours.Tests;

internal static class HostReviewMeasurementTestData
{
    public static (RepositoryEvidence Evidence, EstimateReport Report, HostReviewPacket Packet) Create()
    {
        RepositoryEvidence evidence = TestRepositoryEvidence.Create();
        EstimateReport report = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Implementation,
            rateCard: null);
        HostReviewPacket packet = HostReviewPacketBuilder.Build(report, evidence);
        return (evidence, report, packet);
    }

    public static HostReviewAdjustmentLedger Ledger(
        HostReviewPacket packet,
        decimal firstCandidateFactor = 1m,
        EffortCategory? replacementCategory = null,
        string model = "test-reviewer")
    {
        HostReviewAdjustment[] adjustments =
        [
            .. packet.Candidates.Select((candidate, index) =>
                index == 0 && firstCandidateFactor != 1m
                    ? Replacement(candidate, firstCandidateFactor, replacementCategory)
                    : Affirm(candidate)),
        ];
        return new HostReviewAdjustmentLedger
        {
            InputDigest = packet.InputDigest,
            ReviewerModel = new HostReviewModelIdentity
            {
                IsAvailable = true,
                Provider = "test-provider",
                Model = model,
                Version = "test-version",
            },
            Adjustments = adjustments,
            Notes = [],
        };
    }

    public static HostReviewMeasurement Measurement(
        HostReviewPacket packet,
        HostReviewAdjustmentLedger ledger,
        HostReviewContextMode context,
        string subject = "subject-a",
        string session = "session-a",
        IReadOnlyList<HostReviewQueryPayloadInput>? queries = null,
        long? elapsedMilliseconds = null,
        long? inputTokens = null,
        long? outputTokens = null,
        decimal? cost = null,
        string currency = "USD",
        long additionalBytes = 0,
        long additionalCharacters = 0,
        bool inputComplete = false,
        bool independent = false)
    {
        string packetJson = ContractJson.SerializeCompact(packet);
        string ledgerJson = ContractJson.SerializeCompact(ledger);
        return HostReviewMeasurementBuilder.Build(
            packet,
            packetJson,
            ledger,
            ledgerJson,
            new HostReviewMeasurementOptions
            {
                SubjectId = subject,
                SessionId = session,
                Context = context,
                Queries = queries ?? [],
                ElapsedMilliseconds = elapsedMilliseconds,
                ElapsedBasis = elapsedMilliseconds.HasValue ? "test stopwatch" : null,
                ProviderInputTokens = inputTokens,
                ProviderOutputTokens = outputTokens,
                TokenBasis = inputTokens.HasValue ? "provider response" : null,
                CostAmount = cost,
                CostCurrency = cost.HasValue ? currency : null,
                CostBasis = cost.HasValue ? "provider invoice" : null,
                AdditionalInputBytes = additionalBytes,
                AdditionalInputCharacters = additionalCharacters,
                AdditionalInputBasis = additionalBytes > 0 || additionalCharacters > 0
                    ? "test context inventory"
                    : null,
                AdditionalInputSizeReported = additionalBytes > 0 || additionalCharacters > 0,
                ObservedInputComplete = inputComplete,
                IndependentOfPairedReview = independent,
            });
    }

    public static HostReviewQuery Query(HostReviewPacket packet, HostReviewCandidate candidate) =>
        new()
        {
            Kind = HostReviewQueryKind.Capability,
            Selector = candidate.Capability.Id,
            Reason = "Resolve a test uncertainty.",
            Profile = packet.Profile,
            InputDigest = packet.InputDigest,
        };

    private static HostReviewAdjustment Affirm(HostReviewCandidate candidate) => new()
    {
        TargetId = candidate.Capability.Id,
        Decision = HostReviewDecision.Affirm,
        OriginalHours = candidate.Capability.Hours,
        EvidenceIds = [candidate.EvidenceIds[0]],
        Reason = "The packet supports the baseline range.",
    };

    private static HostReviewAdjustment Replacement(
        HostReviewCandidate candidate,
        decimal factor,
        EffortCategory? replacementCategory) => new()
        {
            TargetId = candidate.Capability.Id,
            Decision = HostReviewDecision.Replace,
            OriginalHours = candidate.Capability.Hours,
            Replacement = new HostReviewReplacement
            {
                Category = replacementCategory ?? candidate.Capability.Category,
                Hours = Scale(candidate.Capability.Hours, factor),
                Confidence = candidate.Capability.Confidence,
                Assumptions = candidate.Assumptions,
                Exclusions = candidate.Exclusions,
                UncertaintyReasons = candidate.UncertaintyReasons,
            },
            EvidenceIds = [candidate.EvidenceIds[0]],
            Reason = "The review replaces the range for a deterministic test.",
        };

    private static EffortRange Scale(EffortRange range, decimal factor) => new()
    {
        Low = decimal.Round(range.Low * factor, 4),
        Expected = decimal.Round(range.Expected * factor, 4),
        High = decimal.Round(range.High * factor, 4),
    };
}
