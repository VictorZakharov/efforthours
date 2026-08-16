using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(ManualQaReviewPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        List<string> errors = [];
        RequireVersion(policy.SchemaVersion, "manual-QA review policy", errors);
        RequireText(policy.PolicyVersion, "policyVersion", errors);
        RequireText(policy.Id, "id", errors);
        RequireText(policy.AuthoringVersion, "authoringVersion", errors);
        RequireText(policy.LicenseExpression, "licenseExpression", errors);
        RequireText(policy.Maturity, "maturity", errors);
        ValidateRubric(policy.Rubric, "rubric", errors);
        ValidateCorpusReference(policy.SourceCorpus, "sourceCorpus", errors);
        RequireText(policy.BaselineId, "baselineId", errors);
        RequireUniqueText(policy.HiddenInputs, "hiddenInputs", errors);
        RequireUniqueText(policy.RequiredReviewPractices, "requiredReviewPractices", errors);
        RequireUniqueText(policy.Limitations, "limitations", errors);

        if (policy.Partition != CalibrationPartition.Development)
        {
            errors.Add("Manual-QA review policy partition must be development.");
        }

        if (policy.Profile != EstimationProfile.Implementation)
        {
            errors.Add("Manual-QA review policy profile must be implementation.");
        }

        if (policy.CandidateVisibility != CalibrationCandidateVisibility.Blind)
        {
            errors.Add("Manual-QA review policy must keep candidate visibility blind.");
        }

        if (!policy.EligibleCategories.SequenceEqual(EligibleCodingEffortVersions.Categories))
        {
            errors.Add(
                $"Manual-QA review policy eligibleCategories must equal '{EligibleCodingEffortVersions.V1}'.");
        }

        if (policy.ExpectedRecordCount <= 0)
        {
            errors.Add("expectedRecordCount must be positive.");
        }

        if (policy.ExpectedTargetCount <= 0)
        {
            errors.Add("expectedTargetCount must be positive.");
        }

        if (policy.HiddenInputs.Count == 0)
        {
            errors.Add("hiddenInputs must describe the candidate-blind boundary.");
        }

        if (policy.RequiredReviewPractices.Count == 0)
        {
            errors.Add("requiredReviewPractices must not be empty.");
        }

        if (policy.Limitations.Count == 0)
        {
            errors.Add("limitations must not be empty.");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(ManualQaReviewPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        List<string> errors = [];
        RequireVersion(packet.SchemaVersion, "manual-QA review packet", errors);
        RequireText(packet.AuthoringVersion, "authoringVersion", errors);
        RequireText(packet.Warning, "warning", errors);
        ValidatePolicyReference(packet.Policy, "policy", errors);
        ValidateCorpusReference(packet.SourceCorpus, "sourceCorpus", errors);
        ValidateRubric(packet.Rubric, "rubric", errors);
        RequireText(packet.SourceRecordId, "sourceRecordId", errors);
        ValidateRepository(packet.Repository, "repository", errors);
        ValidateManualQaSource(packet.Source, errors);
        RequireText(packet.BaselineId, "baselineId", errors);
        RequireUniqueText(packet.Instructions, "instructions", errors);

        if (packet.Status != CalibrationAuthoringStatus.Unreviewed)
        {
            errors.Add("Manual-QA review packet status must be unreviewed.");
        }

        if (packet.CandidateVisibility != CalibrationCandidateVisibility.Blind)
        {
            errors.Add("Manual-QA review packet must keep candidate visibility blind.");
        }

        if (packet.Partition != CalibrationPartition.Development)
        {
            errors.Add("Manual-QA review packet partition must be development.");
        }

        if (packet.Profile != EstimationProfile.Implementation)
        {
            errors.Add("Manual-QA review packet profile must be implementation.");
        }

        if (packet.Targets.Count == 0)
        {
            errors.Add("Manual-QA review packet must contain at least one target.");
        }

        HashSet<string> targetIds = new(StringComparer.Ordinal);
        Dictionary<string, string> groupScopes = new(StringComparer.Ordinal);
        Dictionary<string, string> scopeGroups = new(StringComparer.Ordinal);
        foreach (ManualQaReviewTarget target in packet.Targets)
        {
            string path = $"target[{target.SourceTargetId}]";
            RequireText(target.SourceTargetId, "target.sourceTargetId", errors);
            RequireDigest(target.SourceLineageDigest, $"{path}.sourceLineageDigest", errors);
            RequireText(target.Title, $"{path}.title", errors);
            RequireText(target.Scope, $"{path}.scope", errors);
            RequireText(target.OverlapGroupId, $"{path}.overlapGroupId", errors);
            RequireUniqueText(target.EvidenceIds, $"{path}.evidenceIds", errors);

            if (!targetIds.Add(target.SourceTargetId))
            {
                errors.Add($"Manual-QA source target '{target.SourceTargetId}' is duplicated.");
            }

            if (!EligibleCodingEffortVersions.Categories.Contains(target.SourceCategory))
            {
                errors.Add($"{path}.sourceCategory is outside '{EligibleCodingEffortVersions.V1}'.");
            }

            if (target.EvidenceIds.Count == 0)
            {
                errors.Add($"{path}.evidenceIds must contain at least one ID.");
            }

            if (groupScopes.TryGetValue(target.OverlapGroupId, out string? existingScope) &&
                !string.Equals(existingScope, target.Scope, StringComparison.Ordinal))
            {
                errors.Add(
                    $"Overlap group '{target.OverlapGroupId}' contains more than one exact scope.");
            }
            else
            {
                groupScopes[target.OverlapGroupId] = target.Scope;
            }

            if (scopeGroups.TryGetValue(target.Scope, out string? existingGroup) &&
                !string.Equals(existingGroup, target.OverlapGroupId, StringComparison.Ordinal))
            {
                errors.Add($"Scope '{target.Scope}' appears in more than one overlap group.");
            }
            else
            {
                scopeGroups[target.Scope] = target.OverlapGroupId;
            }
        }

        if (packet.Instructions.Count == 0)
        {
            errors.Add("Manual-QA review packet instructions must not be empty.");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(ManualQaReviewManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        List<string> errors = [];
        RequireVersion(manifest.SchemaVersion, "manual-QA review manifest", errors);
        RequireText(manifest.ManifestVersion, "manifestVersion", errors);
        RequireText(manifest.AuthoringVersion, "authoringVersion", errors);
        ValidatePolicyReference(manifest.Policy, "policy", errors);
        ValidateCorpusReference(manifest.SourceCorpus, "sourceCorpus", errors);
        ValidateRubric(manifest.Rubric, "rubric", errors);
        RequireUniqueText(manifest.Instructions, "instructions", errors);

        if (manifest.CandidateVisibility != CalibrationCandidateVisibility.Blind)
        {
            errors.Add("Manual-QA review manifest must keep candidate visibility blind.");
        }

        if (manifest.Partition != CalibrationPartition.Development)
        {
            errors.Add("Manual-QA review manifest partition must be development.");
        }

        if (manifest.RecordCount <= 0 || manifest.RecordCount != manifest.Packets.Count)
        {
            errors.Add("recordCount must be positive and equal packets.Count.");
        }

        if (manifest.TargetCount <= 0 ||
            manifest.TargetCount != manifest.Packets.Sum(packet => packet.TargetCount))
        {
            errors.Add("targetCount must be positive and equal the packet target sum.");
        }

        HashSet<string> recordIds = new(StringComparer.Ordinal);
        HashSet<string> repositoryIds = new(StringComparer.Ordinal);
        HashSet<string> fileNames = new(StringComparer.Ordinal);
        foreach (ManualQaReviewManifestPacket packet in manifest.Packets)
        {
            string path = $"packet[{packet.SourceRecordId}]";
            RequireText(packet.SourceRecordId, "packet.sourceRecordId", errors);
            RequireText(packet.RepositoryId, $"{path}.repositoryId", errors);
            RequireDigest(packet.RepositorySourceDigest, $"{path}.repositorySourceDigest", errors);
            RequireText(packet.FileName, $"{path}.fileName", errors);
            RequireDigest(packet.PacketDigest, $"{path}.packetDigest", errors);
            RequireDigest(packet.LineageDigest, $"{path}.lineageDigest", errors);

            if (!recordIds.Add(packet.SourceRecordId))
            {
                errors.Add($"Manifest source record '{packet.SourceRecordId}' is duplicated.");
            }

            if (!repositoryIds.Add(packet.RepositoryId))
            {
                errors.Add($"Manifest repository '{packet.RepositoryId}' is duplicated.");
            }

            if (!fileNames.Add(packet.FileName))
            {
                errors.Add($"Manifest fileName '{packet.FileName}' is duplicated.");
            }

            if (packet.FileName.Contains('/') ||
                packet.FileName.Contains('\\') ||
                packet.FileName.Contains("..", StringComparison.Ordinal))
            {
                errors.Add($"{path}.fileName must be a local leaf name.");
            }

            if (packet.TargetCount <= 0)
            {
                errors.Add($"{path}.targetCount must be positive.");
            }
        }

        if (manifest.Instructions.Count == 0)
        {
            errors.Add("Manual-QA review manifest instructions must not be empty.");
        }

        return errors;
    }

    private static void ValidatePolicyReference(
        ManualQaReviewPolicyReference reference,
        string path,
        List<string> errors)
    {
        RequireText(reference.Id, $"{path}.id", errors);
        RequireText(reference.Version, $"{path}.version", errors);
        RequireDigest(reference.Digest, $"{path}.digest", errors);
    }

    private static void ValidateRubric(
        CalibrationRubricReference rubric,
        string path,
        List<string> errors)
    {
        RequireText(rubric.Id, $"{path}.id", errors);
        RequireText(rubric.Version, $"{path}.version", errors);
    }

    private static void ValidateRepository(
        CalibrationRepositoryReference repository,
        string path,
        List<string> errors)
    {
        RequireText(repository.Id, $"{path}.id", errors);
        RequireText(repository.Name, $"{path}.name", errors);
        RequireDigest(repository.SourceDigest, $"{path}.sourceDigest", errors);
    }

    private static void ValidateManualQaSource(
        ManualQaReviewSourceReference source,
        List<string> errors)
    {
        if (source.DataClassification != CalibrationDataClassification.PublicRedistributable)
        {
            errors.Add("Manual-QA public review packet source must be public-redistributable.");
        }

        RequireText(source.SourceReference, "source.sourceReference", errors);
        RequireText(source.Revision, "source.revision", errors);
        RequireText(source.LicenseExpression, "source.licenseExpression", errors);
        if (!source.RedistributionAllowed)
        {
            errors.Add("Manual-QA public review packet source must allow redistribution.");
        }
    }
}
