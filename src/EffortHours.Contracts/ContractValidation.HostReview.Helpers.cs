using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateQueryResultShape(
        HostReviewQueryResult result,
        List<string> errors)
    {
        bool hasSource = result.SourceExcerpt is not null;
        if (result.ContainsSourceExcerpt != hasSource)
        {
            errors.Add("containsSourceExcerpt is inconsistent with sourceExcerpt.");
        }

        switch (result.Query.Kind)
        {
            case HostReviewQueryKind.Capability:
                if (result.Capabilities.Count != 1 || hasSource)
                {
                    errors.Add("A capability query result must contain one capability and no source excerpt.");
                }

                if (result.EvidenceFacts.Count > HostReviewProtocol.MaximumQueryEvidenceFacts)
                {
                    errors.Add("A capability query returned too many evidence facts.");
                }

                if (result.Capabilities.Count == 1)
                {
                    HashSet<string> evidenceIds =
                        [.. result.Capabilities[0].EvidenceIds];
                    if (result.EvidenceFacts.Any(fact => !evidenceIds.Contains(fact.Id)))
                    {
                        errors.Add("A capability query returned unrelated evidence.");
                    }
                }

                break;
            case HostReviewQueryKind.Evidence:
                if (result.Capabilities.Count != 0 || result.EvidenceFacts.Count != 1 || hasSource)
                {
                    errors.Add("An evidence query result must contain one evidence fact only.");
                }

                if (result.EvidenceFacts.Count == 1 &&
                    result.EvidenceFacts[0].Id != result.Query.Selector)
                {
                    errors.Add("The returned evidence fact does not match the query selector.");
                }

                break;
            case HostReviewQueryKind.Scope:
                if (result.EvidenceFacts.Count != 0 || hasSource ||
                    result.Capabilities.Count > (result.Query.Limit ?? 0))
                {
                    errors.Add("A scope query result has an invalid shape.");
                }

                if (result.Capabilities.Any(candidate =>
                    candidate.Capability.Scope != result.Query.Selector))
                {
                    errors.Add("A scope query returned a capability from another scope.");
                }

                break;
            case HostReviewQueryKind.SelectedSource:
                if (result.Capabilities.Count != 0 || result.EvidenceFacts.Count != 1 || !hasSource)
                {
                    errors.Add("A selected-source result must contain one file fact and one excerpt.");
                }

                if (result.SourceExcerpt is not null)
                {
                    ValidateSourceExcerpt(result.SourceExcerpt, errors);
                    if (result.SourceExcerpt.Path != result.Query.Selector)
                    {
                        errors.Add("The source excerpt path does not match the query selector.");
                    }

                    if (result.EvidenceFacts.Count == 1 &&
                        (result.EvidenceFacts[0].Id != result.SourceExcerpt.EvidenceId ||
                         result.EvidenceFacts[0].Kind != EvidenceKinds.File ||
                         !result.EvidenceFacts[0].Locations.Any(location =>
                             location.Path.Replace('\\', '/') == result.SourceExcerpt.Path)))
                    {
                        errors.Add("The source excerpt does not match its admitted file evidence.");
                    }
                }

                break;
        }
    }

    private static void ValidateSourceExcerpt(
        HostReviewSourceExcerpt excerpt,
        List<string> errors)
    {
        RequireText(excerpt.Path, "sourceExcerpt.path", errors);
        RequireText(excerpt.EvidenceId, "sourceExcerpt.evidenceId", errors);
        ValidateDigest(excerpt.FileDigest, "sourceExcerpt.fileDigest", errors);
        if (excerpt.TotalLines < 0 || excerpt.RequestedStartLine <= 0 ||
            excerpt.RequestedLineCount <= 0 ||
            excerpt.RequestedLineCount > HostReviewProtocol.MaximumSourceLines ||
            excerpt.Lines.Count > HostReviewProtocol.MaximumSourceLines)
        {
            errors.Add("Source excerpt counts are outside protocol limits.");
        }

        int characters = 0;
        int previousLine = excerpt.RequestedStartLine - 1;
        foreach (HostReviewSourceLine line in excerpt.Lines)
        {
            if (line.Line != previousLine + 1)
            {
                errors.Add("Source excerpt line numbers must be consecutive.");
                break;
            }

            previousLine = line.Line;
            characters += line.Text.Length;
        }

        if (characters > HostReviewProtocol.MaximumSourceCharacters)
        {
            errors.Add("Source excerpt exceeds the character safety ceiling.");
        }
    }

    private static void ValidateReplacement(
        string targetId,
        HostReviewReplacement replacement,
        List<string> errors)
    {
        ValidateRange(replacement.Hours, $"adjustment[{targetId}].replacement.hours", errors);
        if (replacement.Confidence is < 0m or > 1m)
        {
            errors.Add($"Adjustment '{targetId}' replacement confidence must be between 0 and 1.");
        }

        ValidateTextSet(replacement.Assumptions, $"adjustment[{targetId}].replacement.assumptions", errors);
        ValidateTextSet(replacement.Exclusions, $"adjustment[{targetId}].replacement.exclusions", errors);
        ValidateTextSet(
            replacement.UncertaintyReasons,
            $"adjustment[{targetId}].replacement.uncertaintyReasons",
            errors);
    }

    private static void ValidateModelIdentity(
        HostReviewModelIdentity identity,
        List<string> errors)
    {
        if (identity.IsAvailable)
        {
            RequireText(identity.Model, "reviewerModel.model", errors);
            if (identity.Provider is not null)
            {
                RequireText(identity.Provider, "reviewerModel.provider", errors);
            }

            if (identity.Version is not null)
            {
                RequireText(identity.Version, "reviewerModel.version", errors);
            }

            return;
        }

        if (identity.Provider is not null || identity.Model is not null || identity.Version is not null)
        {
            errors.Add("Unavailable reviewer-model identity cannot contain provider, model, or version values.");
        }
    }

    private static void ValidateStructuralLimits(
        HostReviewStructuralLimits limits,
        List<string> errors)
    {
        if (limits.PacketCapabilities != HostReviewProtocol.MaximumPacketCapabilities ||
            limits.PacketEvidenceFacts != HostReviewProtocol.MaximumPacketEvidenceFacts ||
            limits.QueryEvidenceFacts != HostReviewProtocol.MaximumQueryEvidenceFacts ||
            limits.ScopeCapabilities != HostReviewProtocol.MaximumScopeCapabilities ||
            limits.SourceLines != HostReviewProtocol.MaximumSourceLines ||
            limits.SourceCharacters != HostReviewProtocol.MaximumSourceCharacters ||
            limits.SourceBytes != HostReviewProtocol.MaximumSourceBytes)
        {
            errors.Add("Host-review structural limits do not match the protocol version.");
        }
    }

    private static void ValidatePacketOmissions(
        HostReviewPacketOmissions omissions,
        List<string> errors)
    {
        if (omissions.CapabilityCount < 0 || omissions.CapabilityExpectedHours < 0m ||
            omissions.EvidenceFactCount < 0 || omissions.MissingEvidenceFactCount < 0)
        {
            errors.Add("Host-review packet omissions cannot be negative.");
        }
    }

    private static void ValidateQueryOmissions(
        HostReviewQueryOmissions omissions,
        List<string> errors)
    {
        if (omissions.CapabilityCount < 0 || omissions.EvidenceFactCount < 0 ||
            omissions.SourceLineCount < 0 || omissions.SourceCharacterCount < 0)
        {
            errors.Add("Host-review query omissions cannot be negative.");
        }
    }

    private static void ValidatePacketCategories(
        HostReviewPacket packet,
        List<string> errors)
    {
        foreach (CategoryViewEntry category in packet.Categories)
        {
            ValidateRange(category.Hours, $"category[{category.Category}].hours", errors);
            ValidateAggregate(
                category.WorkItemCount,
                category.CapabilityCount,
                category.Confidence,
                $"category[{category.Category}]",
                errors);
            if (category.Cost is not null)
            {
                errors.Add($"Host-review category '{category.Category}' must not include pricing.");
            }
        }

        if (Sum(packet.Categories.Select(category => category.Hours)) != packet.TotalEffort)
        {
            errors.Add("Host-review packet category totals do not equal total effort.");
        }
    }

    private static void ValidateEstimators(
        IReadOnlyList<EstimatorReference> estimators,
        string path,
        List<string> errors)
    {
        HashSet<EstimatorReference> distinct = [];
        foreach (EstimatorReference estimator in estimators)
        {
            RequireText(estimator.Id, $"{path}.id", errors);
            RequireText(estimator.Version, $"{path}.version", errors);
            if (!distinct.Add(estimator))
            {
                errors.Add($"{path} contains a duplicate estimator reference.");
            }
        }
    }

    private static void ValidateEvidenceFact(EvidenceFact fact, List<string> errors)
    {
        RequireText(fact.Id, "evidenceFact.id", errors);
        RequireText(fact.Kind, $"evidenceFact[{fact.Id}].kind", errors);
        RequireText(fact.Scope, $"evidenceFact[{fact.Id}].scope", errors);
        RequireText(fact.Summary, $"evidenceFact[{fact.Id}].summary", errors);
        RequireText(fact.Provenance.Analyzer, $"evidenceFact[{fact.Id}].provenance.analyzer", errors);
        RequireText(fact.Provenance.AnalyzerVersion, $"evidenceFact[{fact.Id}].provenance.analyzerVersion", errors);
        RequireText(fact.Provenance.Method, $"evidenceFact[{fact.Id}].provenance.method", errors);
        foreach (EvidenceLocation location in fact.Locations)
        {
            RequireText(location.Path, $"evidenceFact[{fact.Id}].location.path", errors);
            if (location.Line is <= 0)
            {
                errors.Add($"Evidence fact '{fact.Id}' has a non-positive line number.");
            }
        }
    }

    private static void ValidateRepository(RepositoryDescriptor repository, List<string> errors)
    {
        RequireText(repository.Name, "repository.name", errors);
        RequireText(repository.Scope, "repository.scope", errors);
        ValidateTextSet(repository.Ecosystems, "repository.ecosystems", errors);
        if (repository.SourceDigest is not null)
        {
            RequireText(repository.SourceDigest, "repository.sourceDigest", errors);
        }
    }

    private static void ValidateTextSet(
        IReadOnlyList<string> values,
        string path,
        List<string> errors)
    {
        HashSet<string> distinct = new(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireText(value, path, errors);
            if (!distinct.Add(value))
            {
                errors.Add($"{path} contains a duplicate value '{value}'.");
            }
        }
    }

    private static void RequireHostReviewProtocol(string version, List<string> errors)
    {
        if (!string.Equals(version, HostReviewProtocolVersions.V1, StringComparison.Ordinal))
        {
            errors.Add($"The host-review document uses unsupported protocol version '{version}'.");
        }
    }

    private static void ValidateDigest(string? digest, string path, List<string> errors)
    {
        RequireText(digest, path, errors);
        if (string.IsNullOrWhiteSpace(digest))
        {
            return;
        }

        if (digest.Length != 71 || !digest.StartsWith("sha256:", StringComparison.Ordinal) ||
            digest[7..].Any(character => character is not (
                >= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            errors.Add($"{path} must be a lowercase SHA-256 digest prefixed with 'sha256:'.");
        }
    }
}
