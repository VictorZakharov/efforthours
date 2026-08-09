using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(CalibrationAuthoringPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        List<string> errors = [];
        RequireVersion(packet.SchemaVersion, "calibration authoring packet", errors);
        RequireText(packet.AuthoringVersion, "authoringVersion", errors);
        RequireText(packet.Warning, "warning", errors);
        RequireText(packet.Rubric.Id, "rubric.id", errors);
        RequireText(packet.Rubric.Version, "rubric.version", errors);
        RequireText(packet.Repository.Id, "repository.id", errors);
        RequireText(packet.Repository.Name, "repository.name", errors);
        RequireDigest(packet.Repository.SourceDigest, "repository.sourceDigest", errors);
        ValidateChangeCalibrationReference(
            packet.Change,
            packet.Repository,
            packet.Rubric.Id,
            "packet",
            errors);
        RequireText(packet.BaselineId, "baselineId", errors);
        RequireText(packet.Candidate.EstimatorVersion, "candidate.estimatorVersion", errors);
        RequireDigest(packet.Candidate.EstimateDigest, "candidate.estimateDigest", errors);

        if (packet.CandidateVisibility == CalibrationCandidateVisibility.Reference)
        {
            if (packet.Candidate.TotalHours is null)
            {
                errors.Add("Reference authoring packets require candidate.totalHours.");
            }

            bool zeroChange = packet.Change is not null &&
                packet.Candidate.TotalHours is { Low: 0m, Expected: 0m, High: 0m };
            if (packet.Candidate.Categories.Count == 0 && !zeroChange)
            {
                errors.Add("Reference authoring packets require candidate.categories.");
            }
        }
        else
        {
            if (packet.Candidate.TotalHours is not null || packet.Candidate.Categories.Count > 0)
            {
                errors.Add("Blind authoring packets must hide candidate totals and categories.");
            }
        }

        if (packet.Candidate.TotalHours is not null)
        {
            ValidateRange(packet.Candidate.TotalHours, "candidate.totalHours", errors);
        }

        HashSet<EffortCategory> categories = [];
        foreach (CategoryEstimate category in packet.Candidate.Categories)
        {
            if (!categories.Add(category.Category))
            {
                errors.Add($"Candidate category '{category.Category}' is duplicated.");
            }

            ValidateRange(category.Hours, $"candidate.category[{category.Category}]", errors);
        }

        if (packet.Targets.Count == 0 && packet.Change is null)
        {
            errors.Add("The calibration authoring packet must contain at least one target.");
        }

        HashSet<string> targetIds = new(StringComparer.Ordinal);
        HashSet<string> sourceWorkItemIds = new(StringComparer.Ordinal);
        foreach (CalibrationAuthoringTarget target in packet.Targets)
        {
            string path = $"target[{target.Id}]";
            RequireText(target.Id, "target.id", errors);
            RequireText(target.SourceCapabilityId, $"{path}.sourceCapabilityId", errors);
            RequireText(target.Title, $"{path}.title", errors);
            RequireText(target.Scope, $"{path}.scope", errors);
            RequireText(target.Candidate.Reason, $"{path}.candidate.reason", errors);

            if (!targetIds.Add(target.Id))
            {
                errors.Add($"Calibration authoring target ID '{target.Id}' is duplicated.");
            }

            if (target.SourceWorkItemIds.Count == 0)
            {
                errors.Add($"{path}.sourceWorkItemIds must contain at least one ID.");
            }

            RequireUniqueText(target.SourceWorkItemIds, $"{path}.sourceWorkItemIds", errors);
            foreach (string sourceWorkItemId in target.SourceWorkItemIds)
            {
                if (!sourceWorkItemIds.Add(sourceWorkItemId))
                {
                    errors.Add(
                        $"Source work-item ID '{sourceWorkItemId}' is mapped to more than one authoring target.");
                }
            }

            if (target.EvidenceIds.Count == 0)
            {
                errors.Add($"{path}.evidenceIds must contain at least one ID.");
            }

            RequireUniqueText(target.EvidenceIds, $"{path}.evidenceIds", errors);
            RequireUniqueText(target.Assumptions, $"{path}.assumptions", errors);
            RequireUniqueText(target.Exclusions, $"{path}.exclusions", errors);
            RequireUniqueText(target.UncertaintyReasons, $"{path}.uncertaintyReasons", errors);

            if (packet.CandidateVisibility == CalibrationCandidateVisibility.Reference)
            {
                if (target.Candidate.Hours is null || target.Candidate.Confidence is null)
                {
                    errors.Add($"{path} requires candidate hours and confidence in reference mode.");
                }
            }
            else if (target.Candidate.Hours is not null || target.Candidate.Confidence is not null)
            {
                errors.Add($"{path} must hide candidate hours and confidence in blind mode.");
            }

            if (target.Candidate.Hours is not null)
            {
                ValidateRange(target.Candidate.Hours, $"{path}.candidate.hours", errors);
            }

            if (target.Candidate.Confidence is < 0m or > 1m)
            {
                errors.Add($"{path}.candidate.confidence must be between zero and one.");
            }

            if (target.Review.Hours is null)
            {
                if (!string.IsNullOrWhiteSpace(target.Review.Rationale) ||
                    !string.IsNullOrWhiteSpace(target.Review.SizeException))
                {
                    errors.Add($"{path}.review cannot contain rationale or sizeException without hours.");
                }

                continue;
            }

            RequireText(target.Review.Rationale, $"{path}.review.rationale", errors);
            ValidateReviewedRange(
                target.Review.Hours,
                $"{path}.review.hours",
                target.Review.SizeException,
                $"{path}.review.sizeException",
                errors);
        }

        RequireUniqueText(
            packet.ProfessionalizationGapWorkItemIds,
            "professionalizationGapWorkItemIds",
            errors);
        RequireUniqueText(packet.Instructions, "instructions", errors);
        if (packet.Instructions.Count == 0)
        {
            errors.Add("Calibration authoring instructions must not be empty.");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(CalibrationReviewPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        List<string> errors = [];
        RequireVersion(plan.SchemaVersion, "calibration review plan", errors);
        RequireText(plan.CompilerVersion, "plan.compilerVersion", errors);
        RequireText(plan.Id, "plan.id", errors);
        RequireText(plan.Version, "plan.version", errors);
        RequireText(plan.Description, "plan.description", errors);
        RequireText(plan.Rubric.Id, "plan.rubric.id", errors);
        RequireText(plan.Rubric.Version, "plan.rubric.version", errors);

        if (plan.Records.Count == 0)
        {
            errors.Add("The calibration review plan must contain at least one record.");
        }

        HashSet<string> recordIds = new(StringComparer.Ordinal);
        Dictionary<string, CalibrationPartition> repositoryPartitions = new(StringComparer.Ordinal);
        HashSet<(string SourceDigest, EstimationProfile Profile, string BaselineId)> matchKeys = [];

        foreach (CalibrationReviewPlanRecord record in plan.Records)
        {
            string path = $"record[{record.Id}]";
            RequireText(record.Id, "record.id", errors);
            if (!recordIds.Add(record.Id))
            {
                errors.Add($"Calibration review-plan record ID '{record.Id}' is duplicated.");
            }

            RequireText(record.Repository.Id, $"{path}.repository.id", errors);
            RequireText(record.Repository.Name, $"{path}.repository.name", errors);
            RequireDigest(record.Repository.SourceDigest, $"{path}.repository.sourceDigest", errors);
            ValidateChangeCalibrationReference(
                record.Change,
                record.Repository,
                plan.Rubric.Id,
                path,
                errors);
            RequireText(record.BaselineId, $"{path}.baselineId", errors);
            RequireText(record.SourceEstimatorVersion, $"{path}.sourceEstimatorVersion", errors);
            RequireDigest(record.SourceEstimateDigest, $"{path}.sourceEstimateDigest", errors);

            if (repositoryPartitions.TryGetValue(
                    record.Repository.Id,
                    out CalibrationPartition existingPartition) &&
                existingPartition != record.Partition)
            {
                errors.Add(
                    $"Repository '{record.Repository.Id}' occurs in both " +
                    $"'{existingPartition}' and '{record.Partition}' partitions.");
            }
            else
            {
                repositoryPartitions[record.Repository.Id] = record.Partition;
            }

            if (!matchKeys.Add((record.Repository.SourceDigest, record.Profile, record.BaselineId)))
            {
                errors.Add(
                    $"More than one review-plan record matches source digest " +
                    $"'{record.Repository.SourceDigest}', profile '{record.Profile}', and " +
                    $"baseline '{record.BaselineId}'.");
            }

            ValidateCalibrationSource(record.Source, path, errors);
            ValidateCalibrationReview(record.Review, path, errors);

            if (record.Capabilities.Count == 0 && record.Change is null)
            {
                errors.Add($"{path}.capabilities must contain at least one decision.");
            }

            HashSet<string> capabilityIds = new(StringComparer.Ordinal);
            foreach (CalibrationCapabilityReviewDecision capability in record.Capabilities)
            {
                string capabilityPath = $"{path}.capability[{capability.SourceCapabilityId}]";
                RequireText(capability.SourceCapabilityId, $"{path}.capability.sourceCapabilityId", errors);
                RequireText(capability.Rationale, $"{capabilityPath}.rationale", errors);
                if (!capabilityIds.Add(capability.SourceCapabilityId))
                {
                    errors.Add(
                        $"Capability decision '{capability.SourceCapabilityId}' is duplicated in '{record.Id}'.");
                }

                if (capability.Targets.Count == 0)
                {
                    errors.Add($"{capabilityPath}.targets must contain at least one reviewed target.");
                }

                for (int index = 0; index < capability.Targets.Count; index++)
                {
                    CalibrationReviewTargetDecision target = capability.Targets[index];
                    string targetPath = $"{capabilityPath}.target[{index}]";
                    ValidateReviewedRange(
                        target.Hours,
                        $"{targetPath}.hours",
                        target.SizeException,
                        $"{targetPath}.sizeException",
                        errors);

                    RequireUniqueText(
                        target.UncertaintyReasons,
                        $"{targetPath}.uncertaintyReasons",
                        errors);
                }
            }
        }

        ValidateCalibrationSubjectConsistency(
            plan.Rubric.Id,
            plan.Records.Select(record => record.Change),
            "review plan",
            errors);

        return errors;
    }

}
