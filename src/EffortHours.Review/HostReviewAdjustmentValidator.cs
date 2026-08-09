using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Review;

public static class HostReviewAdjustmentValidator
{
    public static HostReviewValidationReport Validate(
        HostReviewPacket packet,
        HostReviewAdjustmentLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(ledger);

        List<string> errors =
        [
            .. ContractValidation.Validate(packet)
                .Select(error => $"Packet: {error}"),
            .. ContractValidation.Validate(ledger)
                .Select(error => $"Adjustment: {error}"),
        ];

        if (packet.ProtocolVersion != ledger.ProtocolVersion)
        {
            errors.Add("The adjustment protocol version does not match the packet.");
        }

        if (packet.InputDigest != ledger.InputDigest)
        {
            errors.Add("The adjustment input digest does not match the packet.");
        }

        if (packet.ReviewerModel.IsAvailable && packet.ReviewerModel != ledger.ReviewerModel)
        {
            errors.Add("The adjustment reviewer-model identity does not match the identity recorded in the packet.");
        }

        Dictionary<string, HostReviewCandidate> candidates = packet.Candidates
            .GroupBy(candidate => candidate.Capability.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (HostReviewAdjustment adjustment in ledger.Adjustments)
        {
            if (!candidates.TryGetValue(adjustment.TargetId, out HostReviewCandidate? candidate))
            {
                errors.Add(
                    $"Adjustment target '{adjustment.TargetId}' is not a candidate in the packet.");
                continue;
            }

            if (adjustment.OriginalHours != candidate.Capability.Hours)
            {
                errors.Add(
                    $"Adjustment target '{adjustment.TargetId}' does not repeat its exact baseline range.");
            }

            HashSet<string> candidateEvidenceIds = candidate.EvidenceIds
                .ToHashSet(StringComparer.Ordinal);
            foreach (string evidenceId in adjustment.EvidenceIds)
            {
                if (!candidateEvidenceIds.Contains(evidenceId))
                {
                    errors.Add(
                        $"Adjustment target '{adjustment.TargetId}' cites unrelated evidence '{evidenceId}'.");
                }
            }
        }

        string[] normalizedErrors =
        [
            .. errors
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        return new HostReviewValidationReport
        {
            ProtocolVersion = packet.ProtocolVersion,
            InputDigest = packet.InputDigest,
            IsValid = normalizedErrors.Length == 0,
            AdjustmentCount = ledger.Adjustments.Count,
            AdjustmentsApplied = false,
            Errors = normalizedErrors,
            Warnings =
            [
                "Validation checks identity, shape, and lineage; it does not establish correctness or calibration.",
                "No adjustment was applied to the local estimate.",
            ],
        };
    }
}
