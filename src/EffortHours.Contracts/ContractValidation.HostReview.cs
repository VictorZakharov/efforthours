using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(HostReviewPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        List<string> errors = [];
        RequireVersion(packet.SchemaVersion, "host-review packet", errors);
        RequireHostReviewProtocol(packet.ProtocolVersion, errors);
        ValidateDigest(packet.InputDigest, "inputDigest", errors);
        RequireText(packet.EstimatorVersion, "estimatorVersion", errors);
        ValidateRepository(packet.Repository, errors);
        ValidateRange(packet.TotalEffort, "totalEffort", errors);
        ValidateModelIdentity(packet.ReviewerModel, errors);
        ValidateStructuralLimits(packet.StructuralLimits, errors);
        RequireText(packet.ReviewObjective, "reviewObjective", errors);
        RequireText(packet.StatusDisclaimer, "statusDisclaimer", errors);
        ValidateTextSet(packet.CallerResponsibilities, "callerResponsibilities", errors);
        ValidateTextSet(packet.Assumptions, "assumptions", errors);

        if (!packet.LocalBaselineComplete)
        {
            errors.Add("A host-review packet must preserve a complete local baseline.");
        }

        if (packet.PricingIncluded)
        {
            errors.Add("A host-review packet must not include pricing.");
        }

        if (packet.Candidates.Count > HostReviewProtocol.MaximumPacketCapabilities)
        {
            errors.Add($"A host-review packet cannot contain more than {HostReviewProtocol.MaximumPacketCapabilities} candidates.");
        }

        if (packet.EvidenceFacts.Count > HostReviewProtocol.MaximumPacketEvidenceFacts)
        {
            errors.Add($"A host-review packet cannot contain more than {HostReviewProtocol.MaximumPacketEvidenceFacts} evidence facts.");
        }

        HashSet<string> candidateIds = new(StringComparer.Ordinal);
        HashSet<string> referencedEvidenceIds = new(StringComparer.Ordinal);
        foreach (HostReviewCandidate candidate in packet.Candidates)
        {
            ValidateCandidate(candidate, requireReviewReason: true, errors);
            if (!candidateIds.Add(candidate.Capability.Id))
            {
                errors.Add($"Host-review candidate '{candidate.Capability.Id}' is duplicated.");
            }

            referencedEvidenceIds.UnionWith(candidate.EvidenceIds);
        }

        HashSet<string> suppliedEvidenceIds = new(StringComparer.Ordinal);
        foreach (EvidenceFact fact in packet.EvidenceFacts)
        {
            ValidateEvidenceFact(fact, errors);
            if (!suppliedEvidenceIds.Add(fact.Id))
            {
                errors.Add($"Host-review evidence fact '{fact.Id}' is duplicated.");
            }
        }

        ValidateTextSet(packet.MissingEvidenceIds, "missingEvidenceIds", errors);
        HashSet<string> missingEvidenceIds = packet.MissingEvidenceIds.ToHashSet(StringComparer.Ordinal);
        if (suppliedEvidenceIds.Overlaps(missingEvidenceIds))
        {
            errors.Add("A host-review evidence ID cannot be both supplied and missing.");
        }

        if (suppliedEvidenceIds.Any(id => !referencedEvidenceIds.Contains(id)) ||
            missingEvidenceIds.Any(id => !referencedEvidenceIds.Contains(id)))
        {
            errors.Add("Host-review packet evidence contains an ID not referenced by a candidate.");
        }

        ValidatePacketOmissions(packet.Omissions, errors);
        int availableReferencedCount = referencedEvidenceIds.Count - missingEvidenceIds.Count;
        if (packet.EvidenceFacts.Count + packet.Omissions.EvidenceFactCount != availableReferencedCount)
        {
            errors.Add("Host-review packet evidence omission counts are inconsistent.");
        }

        if (packet.Omissions.MissingEvidenceFactCount != missingEvidenceIds.Count)
        {
            errors.Add("Host-review packet missing-evidence count is inconsistent.");
        }

        HashSet<HostReviewQueryKind> queryKinds = [.. packet.AvailableQueries];
        if (packet.AvailableQueries.Count != queryKinds.Count ||
            !queryKinds.SetEquals(Enum.GetValues<HostReviewQueryKind>()))
        {
            errors.Add("Host-review packet availableQueries must contain every query kind exactly once.");
        }

        ValidateEstimators(packet.Estimators, "estimators", errors);
        HashSet<EstimatorReference> packetEstimators = [.. packet.Estimators];
        if (packet.Candidates.Any(candidate =>
            candidate.Estimators.Any(estimator => !packetEstimators.Contains(estimator))))
        {
            errors.Add("A host-review candidate references an estimator absent from the packet estimator set.");
        }

        ValidatePacketCategories(packet, errors);
        return errors;
    }

    public static IReadOnlyList<string> Validate(HostReviewQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        List<string> errors = [];
        ValidateQuery(query, errors);
        return errors;
    }

    public static IReadOnlyList<string> Validate(HostReviewQueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        List<string> errors = [];
        RequireVersion(result.SchemaVersion, "host-review query result", errors);
        RequireHostReviewProtocol(result.ProtocolVersion, errors);
        ValidateDigest(result.InputDigest, "inputDigest", errors);
        ValidateRepository(result.Repository, errors);
        ValidateQuery(result.Query, errors);
        RequireText(result.DisclosureNotice, "disclosureNotice", errors);
        if (!string.Equals(result.InputDigest, result.Query.InputDigest, StringComparison.Ordinal))
        {
            errors.Add("The query result input digest does not match its query.");
        }

        foreach (HostReviewCandidate candidate in result.Capabilities)
        {
            ValidateCandidate(candidate, requireReviewReason: false, errors);
        }

        if (result.Capabilities.Select(candidate => candidate.Capability.Id).Distinct(StringComparer.Ordinal).Count() !=
            result.Capabilities.Count)
        {
            errors.Add("Host-review query capabilities must have unique IDs.");
        }

        foreach (EvidenceFact fact in result.EvidenceFacts)
        {
            ValidateEvidenceFact(fact, errors);
        }

        if (result.EvidenceFacts.Select(fact => fact.Id).Distinct(StringComparer.Ordinal).Count() !=
            result.EvidenceFacts.Count)
        {
            errors.Add("Host-review query evidence facts must have unique IDs.");
        }

        ValidateQueryOmissions(result.Omissions, errors);
        ValidateQueryResultShape(result, errors);
        return errors;
    }

    public static IReadOnlyList<string> Validate(HostReviewAdjustmentLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        List<string> errors = [];
        RequireVersion(ledger.SchemaVersion, "host-review adjustment ledger", errors);
        RequireHostReviewProtocol(ledger.ProtocolVersion, errors);
        ValidateDigest(ledger.InputDigest, "inputDigest", errors);
        ValidateModelIdentity(ledger.ReviewerModel, errors);
        ValidateTextSet(ledger.Notes, "notes", errors);

        HashSet<string> targetIds = new(StringComparer.Ordinal);
        foreach (HostReviewAdjustment adjustment in ledger.Adjustments)
        {
            RequireText(adjustment.TargetId, "adjustment.targetId", errors);
            RequireText(adjustment.Reason, $"adjustment[{adjustment.TargetId}].reason", errors);
            ValidateRange(
                adjustment.OriginalHours,
                $"adjustment[{adjustment.TargetId}].originalHours",
                errors);
            ValidateTextSet(
                adjustment.EvidenceIds,
                $"adjustment[{adjustment.TargetId}].evidenceIds",
                errors);
            if (adjustment.EvidenceIds.Count == 0)
            {
                errors.Add($"Host-review adjustment '{adjustment.TargetId}' must cite evidence.");
            }

            if (!targetIds.Add(adjustment.TargetId))
            {
                errors.Add($"Host-review adjustment target '{adjustment.TargetId}' is duplicated.");
            }

            if (adjustment.Decision == HostReviewDecision.Affirm && adjustment.Replacement is not null)
            {
                errors.Add($"Affirm adjustment '{adjustment.TargetId}' must not contain a replacement.");
            }
            else if (adjustment.Decision == HostReviewDecision.Replace && adjustment.Replacement is null)
            {
                errors.Add($"Replace adjustment '{adjustment.TargetId}' requires a replacement.");
            }

            if (adjustment.Replacement is not null)
            {
                ValidateReplacement(adjustment.TargetId, adjustment.Replacement, errors);
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(HostReviewValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "host-review validation report", errors);
        RequireHostReviewProtocol(report.ProtocolVersion, errors);
        ValidateDigest(report.InputDigest, "inputDigest", errors);
        if (report.AdjustmentCount < 0)
        {
            errors.Add("Host-review validation adjustmentCount cannot be negative.");
        }

        if (report.AdjustmentsApplied)
        {
            errors.Add("The initial host-review protocol cannot apply adjustments.");
        }

        ValidateTextSet(report.Errors, "errors", errors);
        ValidateTextSet(report.Warnings, "warnings", errors);
        if (report.IsValid != (report.Errors.Count == 0))
        {
            errors.Add("Host-review validation isValid is inconsistent with its errors.");
        }

        return errors;
    }

    private static void ValidateCandidate(
        HostReviewCandidate candidate,
        bool requireReviewReason,
        List<string> errors)
    {
        ValidateCapability(candidate.Capability, rateCard: null, requireCost: false, errors);
        if (candidate.Capability.Cost is not null)
        {
            errors.Add($"Host-review capability '{candidate.Capability.Id}' must not include pricing.");
        }

        ValidateTextSet(candidate.EstimationReasons, "candidate.estimationReasons", errors);
        ValidateTextSet(candidate.EvidenceIds, "candidate.evidenceIds", errors);
        ValidateTextSet(candidate.Assumptions, "candidate.assumptions", errors);
        ValidateTextSet(candidate.Exclusions, "candidate.exclusions", errors);
        ValidateTextSet(candidate.CorrelationGroups, "candidate.correlationGroups", errors);
        ValidateTextSet(candidate.UncertaintyReasons, "candidate.uncertaintyReasons", errors);
        ValidateEstimators(candidate.Estimators, "candidate.estimators", errors);

        if (candidate.EstimationReasons.Count == 0)
        {
            errors.Add($"Host-review candidate '{candidate.Capability.Id}' must record estimator reasoning.");
        }

        if (candidate.EvidenceIds.Count == 0)
        {
            errors.Add($"Host-review candidate '{candidate.Capability.Id}' must reference evidence.");
        }

        if (candidate.Capability.EvidenceCount != candidate.EvidenceIds.Count)
        {
            errors.Add($"Host-review candidate '{candidate.Capability.Id}' has an inconsistent evidence count.");
        }

        HashSet<string> uncertaintyReasons = new(
            candidate.UncertaintyReasons,
            StringComparer.Ordinal);
        if (!uncertaintyReasons.SetEquals(candidate.Capability.UncertaintyReasons))
        {
            errors.Add($"Host-review candidate '{candidate.Capability.Id}' has inconsistent uncertainty reasons.");
        }

        if (requireReviewReason && candidate.Capability.ReviewReasons.Count == 0)
        {
            errors.Add($"Host-review candidate '{candidate.Capability.Id}' must record why it was selected.");
        }
    }

    private static void ValidateQuery(HostReviewQuery query, List<string> errors)
    {
        RequireText(query.Selector, "query.selector", errors);
        RequireText(query.Reason, "query.reason", errors);
        ValidateDigest(query.InputDigest, "query.inputDigest", errors);

        switch (query.Kind)
        {
            case HostReviewQueryKind.Scope:
                if (query.Offset is null or < 0)
                {
                    errors.Add("A scope query requires a non-negative offset.");
                }

                if (query.Limit is null or <= 0 or > HostReviewProtocol.MaximumScopeCapabilities)
                {
                    errors.Add($"A scope query limit must be between 1 and {HostReviewProtocol.MaximumScopeCapabilities}.");
                }

                if (query.StartLine is not null || query.LineCount is not null)
                {
                    errors.Add("A scope query cannot specify source line options.");
                }

                break;

            case HostReviewQueryKind.SelectedSource:
                if (query.StartLine is null or <= 0)
                {
                    errors.Add("A selected-source query requires a positive start line.");
                }

                if (query.LineCount is null or <= 0 or > HostReviewProtocol.MaximumSourceLines)
                {
                    errors.Add($"A selected-source line count must be between 1 and {HostReviewProtocol.MaximumSourceLines}.");
                }

                if (query.Offset is not null || query.Limit is not null)
                {
                    errors.Add("A selected-source query cannot specify pagination options.");
                }

                break;

            default:
                if (query.Offset is not null || query.Limit is not null ||
                    query.StartLine is not null || query.LineCount is not null)
                {
                    errors.Add($"A {query.Kind} query cannot specify pagination or source line options.");
                }

                break;
        }
    }

}
