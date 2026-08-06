using Fairbill.Contracts.V1;

namespace Fairbill.Contracts;

public static class ContractValidation
{
    public static IReadOnlyList<string> Validate(RateCard rateCard)
    {
        ArgumentNullException.ThrowIfNull(rateCard);

        List<string> errors = [];
        ValidateRateCard(rateCard, errors);
        return errors;
    }

    public static IReadOnlyList<string> Validate(RepositoryEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        List<string> errors = [];
        RequireVersion(evidence.SchemaVersion, "repository evidence", errors);
        RequireText(evidence.Repository.Name, "repository.name", errors);
        RequireText(evidence.Repository.Scope, "repository.scope", errors);

        HashSet<string> factIds = new(StringComparer.Ordinal);
        foreach (EvidenceFact fact in evidence.Facts)
        {
            RequireText(fact.Id, "fact.id", errors);
            RequireText(fact.Kind, $"fact[{fact.Id}].kind", errors);
            RequireText(fact.Scope, $"fact[{fact.Id}].scope", errors);
            RequireText(fact.Summary, $"fact[{fact.Id}].summary", errors);

            if (!factIds.Add(fact.Id))
            {
                errors.Add($"Evidence fact ID '{fact.Id}' is duplicated.");
            }

            foreach (EvidenceLocation location in fact.Locations)
            {
                RequireText(location.Path, $"fact[{fact.Id}].location.path", errors);
                if (location.Line is <= 0)
                {
                    errors.Add($"Evidence fact '{fact.Id}' has a non-positive line number.");
                }
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(EstimateReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "estimate report", errors);
        RequireVersion(report.EvidenceSchemaVersion, "estimate evidence", errors);
        RequireText(report.EstimatorVersion, "estimatorVersion", errors);
        ValidateRange(report.TotalEffort, "totalEffort", errors);

        HashSet<string> itemIds = new(StringComparer.Ordinal);
        foreach (WorkItem item in report.WorkItems.Concat(report.ProfessionalizationGap))
        {
            RequireVersion(item.SchemaVersion, $"work item '{item.Id}'", errors);
            RequireText(item.Id, "workItem.id", errors);
            RequireText(item.Title, $"workItem[{item.Id}].title", errors);
            RequireText(item.Scope, $"workItem[{item.Id}].scope", errors);
            RequireText(item.Reason, $"workItem[{item.Id}].reason", errors);
            ValidateRange(item.Hours, $"workItem[{item.Id}].hours", errors);

            if (item.Quantity <= 0m)
            {
                errors.Add($"Work item '{item.Id}' quantity must be positive.");
            }

            if (item.Confidence is < 0m or > 1m)
            {
                errors.Add($"Work item '{item.Id}' confidence must be between 0 and 1.");
            }

            if (!itemIds.Add(item.Id))
            {
                errors.Add($"Work item ID '{item.Id}' is duplicated.");
            }
        }

        EffortRange itemTotal = Sum(report.WorkItems.Select(item => item.Hours));
        if (itemTotal != report.TotalEffort)
        {
            errors.Add("The total effort does not equal the sum of represented work items.");
        }

        EffortRange categoryTotal = Sum(report.Categories.Select(category => category.Hours));
        if (categoryTotal != report.TotalEffort)
        {
            errors.Add("The total effort does not equal the sum of category estimates.");
        }

        ValidateRateAndCost(
            report.RateCard,
            report.TotalCost,
            report.TotalEffort,
            "totalCost",
            errors);

        return errors;
    }

    public static IReadOnlyList<string> Validate(EstimateViewReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "estimate view", errors);
        RequireVersion(report.SourceEstimateSchemaVersion, "source estimate", errors);
        RequireText(report.EstimatorVersion, "estimatorVersion", errors);
        RequireText(report.Repository.Name, "repository.name", errors);
        RequireText(report.Repository.Scope, "repository.scope", errors);
        RequireText(
            report.ProfessionalizationGapTreatment,
            "professionalizationGapTreatment",
            errors);
        ValidateRange(report.TotalEffort, "totalEffort", errors);
        ValidateRateAndCost(
            report.RateCard,
            report.TotalCost,
            report.TotalEffort,
            "totalCost",
            errors);

        if (report.Counts.RepresentedWorkItems < 0 ||
            report.Counts.CapabilityGroups < 0 ||
            report.Counts.Scopes < 0 ||
            report.Counts.ProfessionalizationGapItems < 0)
        {
            errors.Add("Estimate-view counts cannot be negative.");
        }

        if (report.Omissions.ScopeCount < 0 ||
            report.Omissions.ScopeExpectedHours < 0m ||
            report.Omissions.CapabilityCount < 0 ||
            report.Omissions.CapabilityExpectedHours < 0m)
        {
            errors.Add("Estimate-view omission values cannot be negative.");
        }

        foreach (CategoryViewEntry entry in report.Categories)
        {
            ValidateRange(entry.Hours, $"category[{entry.Category}].hours", errors);
            ValidateAggregate(
                entry.WorkItemCount,
                entry.CapabilityCount,
                entry.Confidence,
                $"category[{entry.Category}]",
                errors);
            ValidateProjectedCost(
                entry.Cost,
                report.RateCard,
                entry.Hours,
                requireCost: true,
                $"category[{entry.Category}]",
                errors);
        }

        foreach (ScopeViewEntry entry in report.Scopes)
        {
            RequireText(entry.Scope, "scope.scope", errors);
            ValidateRange(entry.Hours, $"scope[{entry.Scope}].hours", errors);
            ValidateAggregate(
                entry.WorkItemCount,
                entry.CapabilityCount,
                entry.Confidence,
                $"scope[{entry.Scope}]",
                errors);
            ValidateProjectedCost(
                entry.Cost,
                report.RateCard,
                entry.Hours,
                requireCost: true,
                $"scope[{entry.Scope}]",
                errors);
        }

        HashSet<string> capabilityIds = new(StringComparer.Ordinal);
        foreach (CapabilityViewEntry entry in report.Capabilities.Concat(report.ReviewQueue))
        {
            ValidateCapability(entry, report.RateCard, requireCost: true, errors);
            if (!capabilityIds.Add(entry.Id))
            {
                errors.Add($"Capability ID '{entry.Id}' is repeated in the projection.");
            }
        }

        foreach (CapabilityViewEntry entry in report.ProfessionalizationGap)
        {
            ValidateCapability(entry, rateCard: null, requireCost: false, errors);
            if (!capabilityIds.Add(entry.Id))
            {
                errors.Add($"Capability ID '{entry.Id}' is repeated in the projection.");
            }
        }

        ValidateViewShape(report, errors);
        return errors;
    }

    public static IReadOnlyList<string> Validate(EstimateExplanation explanation)
    {
        ArgumentNullException.ThrowIfNull(explanation);

        List<string> errors = [];
        RequireVersion(explanation.SchemaVersion, "estimate explanation", errors);
        RequireVersion(explanation.EvidenceSchemaVersion, "explanation evidence", errors);
        RequireText(explanation.EstimatorVersion, "estimatorVersion", errors);
        RequireText(explanation.Repository.Name, "repository.name", errors);
        RequireText(explanation.Repository.Scope, "repository.scope", errors);
        RequireText(explanation.RequestedId, "requestedId", errors);
        ValidateCapability(explanation.Capability, rateCard: null, requireCost: false, errors);

        if (explanation.WorkItems.Count == 0)
        {
            errors.Add("An estimate explanation must contain at least one work item.");
        }

        HashSet<string> itemIds = new(StringComparer.Ordinal);
        HashSet<string> referencedEvidenceIds = new(StringComparer.Ordinal);
        foreach (WorkItem item in explanation.WorkItems)
        {
            RequireVersion(item.SchemaVersion, $"work item '{item.Id}'", errors);
            RequireText(item.Id, "workItem.id", errors);
            RequireText(item.Title, $"workItem[{item.Id}].title", errors);
            RequireText(item.Scope, $"workItem[{item.Id}].scope", errors);
            RequireText(item.Reason, $"workItem[{item.Id}].reason", errors);
            ValidateRange(item.Hours, $"workItem[{item.Id}].hours", errors);
            if (!itemIds.Add(item.Id))
            {
                errors.Add($"Work item ID '{item.Id}' is duplicated in the explanation.");
            }

            foreach (string evidenceId in item.EvidenceIds)
            {
                referencedEvidenceIds.Add(evidenceId);
            }
        }

        if (Sum(explanation.WorkItems.Select(item => item.Hours)) != explanation.Capability.Hours)
        {
            errors.Add("Explanation capability effort does not equal its work-item effort.");
        }

        if (explanation.Capability.WorkItemCount != explanation.WorkItems.Count)
        {
            errors.Add("Explanation capability work-item count is inconsistent.");
        }

        HashSet<string> suppliedEvidenceIds = new(StringComparer.Ordinal);
        foreach (EvidenceFact fact in explanation.EvidenceFacts)
        {
            RequireText(fact.Id, "evidenceFact.id", errors);
            if (!suppliedEvidenceIds.Add(fact.Id))
            {
                errors.Add($"Evidence fact ID '{fact.Id}' is duplicated in the explanation.");
            }
        }

        HashSet<string> missingEvidenceIds = explanation.MissingEvidenceIds
            .ToHashSet(StringComparer.Ordinal);
        if (suppliedEvidenceIds.Overlaps(missingEvidenceIds))
        {
            errors.Add("An evidence ID cannot be both supplied and missing.");
        }

        foreach (string evidenceId in referencedEvidenceIds)
        {
            if (!suppliedEvidenceIds.Contains(evidenceId) && !missingEvidenceIds.Contains(evidenceId))
            {
                errors.Add($"Referenced evidence ID '{evidenceId}' is not accounted for.");
            }
        }

        if (suppliedEvidenceIds.Any(id => !referencedEvidenceIds.Contains(id)) ||
            missingEvidenceIds.Any(id => !referencedEvidenceIds.Contains(id)))
        {
            errors.Add("Explanation evidence contains an ID not referenced by its work items.");
        }

        if (explanation.Capability.EvidenceCount != referencedEvidenceIds.Count)
        {
            errors.Add("Explanation capability evidence count is inconsistent.");
        }

        if (explanation.Estimators.Count == 0)
        {
            errors.Add("An estimate explanation must identify at least one estimator.");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(RepositoryScanCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);

        List<string> errors = [];
        RequireVersion(cache.SchemaVersion, "repository scan cache", errors);
        RequireText(cache.AnalyzerVersion, "repositoryScanCache.analyzerVersion", errors);
        RequireText(cache.RepositoryKey, "repositoryScanCache.repositoryKey", errors);

        HashSet<string> paths = new(StringComparer.Ordinal);
        foreach (RepositoryScanCacheEntry entry in cache.Files)
        {
            RequireText(entry.Path, "repositoryScanCache.file.path", errors);
            RequireText(entry.Sha256, $"repositoryScanCache.file[{entry.Path}].sha256", errors);
            RequireText(entry.Role, $"repositoryScanCache.file[{entry.Path}].role", errors);
            if (entry.Length < 0 || entry.Bytes < 0 || entry.Lines < 0)
            {
                errors.Add($"Repository scan cache entry '{entry.Path}' has a negative measurement.");
            }

            if (!paths.Add(entry.Path))
            {
                errors.Add($"Repository scan cache path '{entry.Path}' is duplicated.");
            }
        }

        return errors;
    }

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
        RequireText(packet.BaselineId, "baselineId", errors);
        RequireText(packet.Candidate.EstimatorVersion, "candidate.estimatorVersion", errors);
        RequireDigest(packet.Candidate.EstimateDigest, "candidate.estimateDigest", errors);

        if (packet.CandidateVisibility == CalibrationCandidateVisibility.Reference)
        {
            if (packet.Candidate.TotalHours is null)
            {
                errors.Add("Reference authoring packets require candidate.totalHours.");
            }

            if (packet.Candidate.Categories.Count == 0)
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

        if (packet.Targets.Count == 0)
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

            if (record.Capabilities.Count == 0)
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

        return errors;
    }

    public static IReadOnlyList<string> Validate(CalibrationCorpusReviewPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        List<string> errors = [];
        RequireVersion(packet.SchemaVersion, "calibration corpus review packet", errors);
        RequireText(packet.AuthoringVersion, "authoringVersion", errors);
        RequireText(packet.Warning, "warning", errors);
        ValidateCorpusReference(packet.SourceCorpus, "sourceCorpus", errors);
        RequireText(packet.Rubric.Id, "rubric.id", errors);
        RequireText(packet.Rubric.Version, "rubric.version", errors);

        if (packet.Records.Count == 0)
        {
            errors.Add("The calibration corpus review packet must contain at least one record.");
        }

        HashSet<string> recordIds = new(StringComparer.Ordinal);
        foreach (CalibrationCorpusReviewRecord record in packet.Records)
        {
            string recordPath = $"record[{record.SourceRecordId}]";
            RequireText(record.SourceRecordId, "record.sourceRecordId", errors);
            if (!recordIds.Add(record.SourceRecordId))
            {
                errors.Add($"Corpus review record '{record.SourceRecordId}' is duplicated.");
            }

            RequireText(record.Repository.Id, $"{recordPath}.repository.id", errors);
            RequireText(record.Repository.Name, $"{recordPath}.repository.name", errors);
            RequireDigest(record.Repository.SourceDigest, $"{recordPath}.repository.sourceDigest", errors);
            RequireText(record.BaselineId, $"{recordPath}.baselineId", errors);

            if (packet.CandidateVisibility == CalibrationCandidateVisibility.Reference)
            {
                if (record.CandidateTotalHours is null)
                {
                    errors.Add($"{recordPath} requires candidateTotalHours in reference mode.");
                }
            }
            else if (record.CandidateTotalHours is not null)
            {
                errors.Add($"{recordPath} must hide candidateTotalHours in blind mode.");
            }

            if (record.CandidateTotalHours is not null)
            {
                ValidateRange(record.CandidateTotalHours, $"{recordPath}.candidateTotalHours", errors);
            }

            if (record.Targets.Count == 0)
            {
                errors.Add($"{recordPath}.targets must contain at least one target.");
            }

            HashSet<string> targetIds = new(StringComparer.Ordinal);
            foreach (CalibrationCorpusReviewTarget target in record.Targets)
            {
                string targetPath = $"{recordPath}.target[{target.SourceTargetId}]";
                RequireText(target.SourceTargetId, $"{recordPath}.target.sourceTargetId", errors);
                RequireText(target.Title, $"{targetPath}.title", errors);
                RequireText(target.Scope, $"{targetPath}.scope", errors);
                if (!targetIds.Add(target.SourceTargetId))
                {
                    errors.Add($"Source target '{target.SourceTargetId}' is duplicated in '{record.SourceRecordId}'.");
                }

                if (target.SourceWorkItemIds.Count == 0)
                {
                    errors.Add($"{targetPath}.sourceWorkItemIds must contain at least one ID.");
                }

                if (target.EvidenceIds.Count == 0)
                {
                    errors.Add($"{targetPath}.evidenceIds must contain at least one ID.");
                }

                RequireUniqueText(target.SourceWorkItemIds, $"{targetPath}.sourceWorkItemIds", errors);
                RequireUniqueText(target.EvidenceIds, $"{targetPath}.evidenceIds", errors);

                if (packet.CandidateVisibility == CalibrationCandidateVisibility.Reference)
                {
                    if (target.Candidate.Hours is null)
                    {
                        errors.Add($"{targetPath} requires candidate hours in reference mode.");
                    }

                    RequireText(target.Candidate.Rationale, $"{targetPath}.candidate.rationale", errors);
                }
                else if (target.Candidate.Hours is not null ||
                         target.Candidate.Rationale is not null ||
                         target.Candidate.UncertaintyReasons.Count > 0 ||
                         target.Candidate.SizeException is not null)
                {
                    errors.Add($"{targetPath} must hide prior target judgments in blind mode.");
                }

                if (target.Candidate.Hours is not null)
                {
                    ValidateRange(target.Candidate.Hours, $"{targetPath}.candidate.hours", errors);
                }

                RequireUniqueText(
                    target.Candidate.UncertaintyReasons,
                    $"{targetPath}.candidate.uncertaintyReasons",
                    errors);

                if (target.Review.Action is not null ||
                    target.Review.Hours is not null ||
                    target.Review.Rationale is not null ||
                    target.Review.UncertaintyReasons.Count > 0 ||
                    target.Review.SizeException is not null)
                {
                    errors.Add($"{targetPath}.review must remain empty in an unreviewed packet.");
                }
            }
        }

        RequireUniqueText(packet.Instructions, "instructions", errors);
        if (packet.Instructions.Count == 0)
        {
            errors.Add("Calibration corpus review instructions must not be empty.");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(CalibrationCorpusReviewPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        List<string> errors = [];
        RequireVersion(plan.SchemaVersion, "calibration corpus review plan", errors);
        RequireText(plan.CompilerVersion, "plan.compilerVersion", errors);
        ValidateCorpusReference(plan.SourceCorpus, "plan.sourceCorpus", errors);
        RequireText(plan.Id, "plan.id", errors);
        RequireText(plan.Version, "plan.version", errors);
        RequireText(plan.Description, "plan.description", errors);

        if (plan.Records.Count == 0)
        {
            errors.Add("The calibration corpus review plan must contain at least one record.");
        }

        HashSet<string> recordIds = new(StringComparer.Ordinal);
        foreach (CalibrationCorpusReviewPlanRecord record in plan.Records)
        {
            string recordPath = $"record[{record.SourceRecordId}]";
            RequireText(record.SourceRecordId, "record.sourceRecordId", errors);
            if (!recordIds.Add(record.SourceRecordId))
            {
                errors.Add($"Corpus review-plan record '{record.SourceRecordId}' is duplicated.");
            }

            if (record.ResultStatus == CalibrationReviewStatus.TeacherEstimate)
            {
                errors.Add($"{recordPath}.resultStatus must advance to reviewed or adjudicated.");
            }

            if (record.CompletedOn == default)
            {
                errors.Add($"{recordPath}.completedOn is required.");
            }

            RequireText(record.Notes, $"{recordPath}.notes", errors);
            if (record.Reviewers.Count == 0)
            {
                errors.Add($"{recordPath}.reviewers must contain at least one subsequent reviewer.");
            }

            HashSet<string> reviewerIds = new(StringComparer.Ordinal);
            foreach (CalibrationReviewer reviewer in record.Reviewers)
            {
                RequireText(reviewer.Id, $"{recordPath}.reviewer.id", errors);
                if (!reviewerIds.Add(reviewer.Id))
                {
                    errors.Add($"Reviewer '{reviewer.Id}' is duplicated in {recordPath}.");
                }

                if (reviewer.Role == CalibrationReviewerRole.Teacher)
                {
                    errors.Add($"{recordPath} may add only reviewer or adjudicator roles.");
                }

                if (reviewer.Kind == CalibrationReviewerKind.HostAi)
                {
                    RequireText(reviewer.ModelId, $"{recordPath}.reviewer[{reviewer.Id}].modelId", errors);
                    RequireText(
                        reviewer.ModelVersion,
                        $"{recordPath}.reviewer[{reviewer.Id}].modelVersion",
                        errors);
                }
            }

            if (record.ResultStatus == CalibrationReviewStatus.Reviewed &&
                !record.Reviewers.Any(reviewer => reviewer.Role == CalibrationReviewerRole.Reviewer))
            {
                errors.Add($"{recordPath} requires a reviewer role to produce reviewed labels.");
            }

            if (record.ResultStatus == CalibrationReviewStatus.Adjudicated &&
                !record.Reviewers.Any(reviewer => reviewer.Role == CalibrationReviewerRole.Adjudicator))
            {
                errors.Add($"{recordPath} requires an adjudicator role to produce adjudicated labels.");
            }

            if (record.Targets.Count == 0)
            {
                errors.Add($"{recordPath}.targets must contain at least one decision.");
            }

            HashSet<string> targetIds = new(StringComparer.Ordinal);
            foreach (CalibrationCorpusReviewTargetDecision target in record.Targets)
            {
                string targetPath = $"{recordPath}.target[{target.SourceTargetId}]";
                RequireText(target.SourceTargetId, $"{recordPath}.target.sourceTargetId", errors);
                if (!targetIds.Add(target.SourceTargetId))
                {
                    errors.Add($"Source target '{target.SourceTargetId}' is duplicated in {recordPath}.");
                }

                RequireUniqueText(
                    target.UncertaintyReasons,
                    $"{targetPath}.uncertaintyReasons",
                    errors);
                if (target.Action == CalibrationCorpusReviewAction.Accept)
                {
                    if (target.Hours is not null ||
                        target.Rationale is not null ||
                        target.UncertaintyReasons.Count > 0 ||
                        target.SizeException is not null)
                    {
                        errors.Add($"{targetPath} accepts the source target and cannot contain replacement values.");
                    }

                    continue;
                }

                if (target.Hours is null)
                {
                    errors.Add($"{targetPath}.hours is required for a replacement decision.");
                    continue;
                }

                RequireText(target.Rationale, $"{targetPath}.rationale", errors);
                ValidateReviewedRange(
                    target.Hours,
                    $"{targetPath}.hours",
                    target.SizeException,
                    $"{targetPath}.sizeException",
                    errors);
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(CalibrationMutationSuite suite)
    {
        ArgumentNullException.ThrowIfNull(suite);

        List<string> errors = [];
        RequireVersion(suite.SchemaVersion, "calibration mutation suite", errors);
        RequireText(suite.MetricVersion, "metricVersion", errors);
        RequireText(suite.Id, "suite.id", errors);
        RequireText(suite.Version, "suite.version", errors);
        RequireText(suite.Description, "suite.description", errors);

        if (suite.Cases.Count < 2)
        {
            errors.Add("A calibration mutation suite must contain at least two cases.");
        }

        HashSet<string> caseIds = new(StringComparer.Ordinal);
        HashSet<(string SourceDigest, EstimationProfile Profile, string BaselineId)> caseKeys = [];
        foreach (CalibrationMutationCase mutationCase in suite.Cases)
        {
            string path = $"case[{mutationCase.Id}]";
            RequireText(mutationCase.Id, "case.id", errors);
            RequireText(mutationCase.Description, $"{path}.description", errors);
            RequireDigest(mutationCase.SourceDigest, $"{path}.sourceDigest", errors);
            RequireText(mutationCase.BaselineId, $"{path}.baselineId", errors);
            if (!caseIds.Add(mutationCase.Id))
            {
                errors.Add($"Mutation case '{mutationCase.Id}' is duplicated.");
            }

            if (!caseKeys.Add((mutationCase.SourceDigest, mutationCase.Profile, mutationCase.BaselineId)))
            {
                errors.Add($"More than one mutation case selects the candidate for '{mutationCase.SourceDigest}'.");
            }
        }

        if (suite.Assertions.Count == 0)
        {
            errors.Add("A calibration mutation suite must contain at least one assertion.");
        }

        HashSet<string> assertionIds = new(StringComparer.Ordinal);
        foreach (CalibrationMutationAssertion assertion in suite.Assertions)
        {
            string path = $"assertion[{assertion.Id}]";
            RequireText(assertion.Id, "assertion.id", errors);
            RequireText(assertion.Family, $"{path}.family", errors);
            RequireText(assertion.SubjectCaseId, $"{path}.subjectCaseId", errors);
            RequireText(assertion.ReferenceCaseId, $"{path}.referenceCaseId", errors);
            RequireText(assertion.Rationale, $"{path}.rationale", errors);
            if (!assertionIds.Add(assertion.Id))
            {
                errors.Add($"Mutation assertion '{assertion.Id}' is duplicated.");
            }

            if (!caseIds.Contains(assertion.SubjectCaseId))
            {
                errors.Add($"{path} references unknown subject case '{assertion.SubjectCaseId}'.");
            }

            if (!caseIds.Contains(assertion.ReferenceCaseId))
            {
                errors.Add($"{path} references unknown reference case '{assertion.ReferenceCaseId}'.");
            }

            if (string.Equals(assertion.SubjectCaseId, assertion.ReferenceCaseId, StringComparison.Ordinal))
            {
                errors.Add($"{path} must compare two different cases.");
            }

            ValidateMutationScope(assertion.Scope, assertion.Category, path, errors);
            if (assertion.MinimumDifferenceHours is null && assertion.MaximumDifferenceHours is null)
            {
                errors.Add($"{path} requires at least one difference bound.");
            }

            if (assertion.MinimumDifferenceHours is not null &&
                assertion.MaximumDifferenceHours is not null &&
                assertion.MinimumDifferenceHours > assertion.MaximumDifferenceHours)
            {
                errors.Add($"{path}.minimumDifferenceHours cannot exceed maximumDifferenceHours.");
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(CalibrationMutationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "calibration mutation report", errors);
        RequireText(report.EvaluatorVersion, "evaluatorVersion", errors);
        RequireText(report.MetricVersion, "metricVersion", errors);
        RequireText(report.SuiteId, "suiteId", errors);
        RequireText(report.SuiteVersion, "suiteVersion", errors);
        RequireUniqueText(report.CandidateEstimatorVersions, "candidateEstimatorVersions", errors);
        if (report.CandidateEstimatorVersions.Count == 0)
        {
            errors.Add("A mutation report requires at least one candidate estimator version.");
        }

        if (report.CaseCount != report.Cases.Count || report.CaseCount < 2)
        {
            errors.Add("Mutation report caseCount must equal the case result count and be at least two.");
        }

        HashSet<string> caseIds = new(StringComparer.Ordinal);
        foreach (CalibrationMutationCaseResult mutationCase in report.Cases)
        {
            string path = $"case[{mutationCase.CaseId}]";
            RequireText(mutationCase.CaseId, "case.caseId", errors);
            RequireText(mutationCase.CandidateEstimatorVersion, $"{path}.candidateEstimatorVersion", errors);
            RequireDigest(mutationCase.CandidateEstimateDigest, $"{path}.candidateEstimateDigest", errors);
            ValidateRange(mutationCase.TotalHours, $"{path}.totalHours", errors);
            if (!caseIds.Add(mutationCase.CaseId))
            {
                errors.Add($"Mutation case result '{mutationCase.CaseId}' is duplicated.");
            }
        }

        if (report.AssertionCount != report.Assertions.Count || report.AssertionCount < 1)
        {
            errors.Add("Mutation report assertionCount must equal the assertion result count and be positive.");
        }

        HashSet<string> assertionIds = new(StringComparer.Ordinal);
        foreach (CalibrationMutationAssertionResult assertion in report.Assertions)
        {
            string path = $"assertion[{assertion.Id}]";
            RequireText(assertion.Id, "assertion.id", errors);
            RequireText(assertion.Family, $"{path}.family", errors);
            RequireText(assertion.SubjectCaseId, $"{path}.subjectCaseId", errors);
            RequireText(assertion.ReferenceCaseId, $"{path}.referenceCaseId", errors);
            RequireText(assertion.Rationale, $"{path}.rationale", errors);
            if (!assertionIds.Add(assertion.Id))
            {
                errors.Add($"Mutation assertion result '{assertion.Id}' is duplicated.");
            }

            if (!caseIds.Contains(assertion.SubjectCaseId) || !caseIds.Contains(assertion.ReferenceCaseId))
            {
                errors.Add($"{path} references a case not present in the report.");
            }

            ValidateMutationScope(assertion.Scope, assertion.Category, path, errors);
            if (assertion.MinimumDifferenceHours is null && assertion.MaximumDifferenceHours is null)
            {
                errors.Add($"{path} requires at least one difference bound.");
            }

            if (assertion.MinimumDifferenceHours is not null &&
                assertion.MaximumDifferenceHours is not null &&
                assertion.MinimumDifferenceHours > assertion.MaximumDifferenceHours)
            {
                errors.Add($"{path}.minimumDifferenceHours cannot exceed maximumDifferenceHours.");
            }

            if (assertion.SubjectHours < 0m || assertion.ReferenceHours < 0m)
            {
                errors.Add($"{path} contains negative compared effort.");
            }

            decimal difference = assertion.SubjectHours - assertion.ReferenceHours;
            if (assertion.DifferenceHours != difference)
            {
                errors.Add($"{path}.differenceHours does not equal subjectHours minus referenceHours.");
            }

            bool expectedPass =
                (assertion.MinimumDifferenceHours is null || difference >= assertion.MinimumDifferenceHours) &&
                (assertion.MaximumDifferenceHours is null || difference <= assertion.MaximumDifferenceHours);
            if (assertion.Passed != expectedPass)
            {
                errors.Add($"{path}.passed does not agree with its difference bounds.");
            }
        }

        int passed = report.Assertions.Count(assertion => assertion.Passed);
        int failed = report.Assertions.Count - passed;
        if (report.PassedCount != passed ||
            report.FailedCount != failed ||
            report.AllPassed != (failed == 0) ||
            report.IgnoredCandidateCount < 0)
        {
            errors.Add("Mutation report pass/fail or ignored-candidate summary is inconsistent.");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(CalibrationCorpus corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);

        List<string> errors = [];
        RequireVersion(corpus.SchemaVersion, "calibration corpus", errors);
        RequireText(corpus.Id, "corpus.id", errors);
        RequireText(corpus.Version, "corpus.version", errors);
        RequireText(corpus.Description, "corpus.description", errors);
        RequireText(corpus.Rubric.Id, "corpus.rubric.id", errors);
        RequireText(corpus.Rubric.Version, "corpus.rubric.version", errors);

        if (corpus.Records.Count == 0)
        {
            errors.Add("The calibration corpus must contain at least one record.");
        }

        HashSet<string> recordIds = new(StringComparer.Ordinal);
        Dictionary<string, CalibrationPartition> repositoryPartitions = new(StringComparer.Ordinal);
        Dictionary<string, (string RepositoryId, CalibrationPartition Partition)> digestOwners =
            new(StringComparer.Ordinal);
        HashSet<(string SourceDigest, EstimationProfile Profile, string BaselineId)> matchKeys = [];

        foreach (CalibrationRecord record in corpus.Records)
        {
            string path = $"record[{record.Id}]";
            RequireText(record.Id, "record.id", errors);
            if (!recordIds.Add(record.Id))
            {
                errors.Add($"Calibration record ID '{record.Id}' is duplicated.");
            }

            RequireText(record.Repository.Id, $"{path}.repository.id", errors);
            RequireText(record.Repository.Name, $"{path}.repository.name", errors);
            RequireDigest(record.Repository.SourceDigest, $"{path}.repository.sourceDigest", errors);
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

            if (digestOwners.TryGetValue(
                    record.Repository.SourceDigest,
                    out (string RepositoryId, CalibrationPartition Partition) owner) &&
                (!string.Equals(owner.RepositoryId, record.Repository.Id, StringComparison.Ordinal) ||
                 owner.Partition != record.Partition))
            {
                errors.Add(
                    $"Source digest '{record.Repository.SourceDigest}' is assigned to conflicting " +
                    "repository identities or partitions.");
            }
            else
            {
                digestOwners[record.Repository.SourceDigest] =
                    (record.Repository.Id, record.Partition);
            }

            if (!matchKeys.Add((record.Repository.SourceDigest, record.Profile, record.BaselineId)))
            {
                errors.Add(
                    $"More than one calibration record matches source digest " +
                    $"'{record.Repository.SourceDigest}', profile '{record.Profile}', and " +
                    $"baseline '{record.BaselineId}'.");
            }

            ValidateCalibrationSource(record.Source, path, errors);
            ValidateCalibrationReview(record.Review, path, errors);
            ValidateCalibrationTargets(record, path, errors);
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(CalibrationValidationSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        List<string> errors = [];
        RequireVersion(summary.SchemaVersion, "calibration validation summary", errors);
        RequireText(summary.CorpusId, "corpusId", errors);
        RequireText(summary.CorpusVersion, "corpusVersion", errors);
        if (!summary.Valid)
        {
            errors.Add("A successful calibration validation summary must be valid.");
        }

        if (summary.RecordCount <= 0 || summary.RepositoryCount <= 0)
        {
            errors.Add("Calibration validation counts must be positive.");
        }

        HashSet<CalibrationPartition> partitions = [];
        foreach (CalibrationPartitionSummary partition in summary.Partitions)
        {
            if (!partitions.Add(partition.Partition))
            {
                errors.Add($"Partition summary '{partition.Partition}' is duplicated.");
            }

            if (partition.RecordCount <= 0 || partition.RepositoryCount <= 0)
            {
                errors.Add($"Partition '{partition.Partition}' counts must be positive.");
            }
        }

        if (summary.Partitions.Sum(partition => partition.RecordCount) != summary.RecordCount)
        {
            errors.Add("Partition record counts do not equal the validation record count.");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(CalibrationEvaluationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "calibration evaluation report", errors);
        RequireText(report.EvaluatorVersion, "evaluatorVersion", errors);
        RequireText(report.MetricVersion, "metricVersion", errors);
        RequireText(report.CorpusId, "corpusId", errors);
        RequireText(report.CorpusVersion, "corpusVersion", errors);
        if (report.RecordCount <= 0 || report.RepositoryCount <= 0)
        {
            errors.Add("Calibration evaluation record and repository counts must be positive.");
        }

        if (report.IgnoredCandidateCount < 0)
        {
            errors.Add("ignoredCandidateCount cannot be negative.");
        }

        RequireUniqueText(report.CandidateEstimatorVersions, "candidateEstimatorVersions", errors);
        if (report.CandidateEstimatorVersions.Count == 0)
        {
            errors.Add("At least one candidate estimator version is required.");
        }

        ValidateCalibrationMetrics(report.RepositoryTotals, "repositoryTotals", errors);
        ValidateCalibrationMetrics(report.WorkItems, "workItems", errors);

        HashSet<EffortCategory> categories = [];
        foreach (CalibrationCategoryMetrics category in report.Categories)
        {
            if (!categories.Add(category.Category))
            {
                errors.Add($"Calibration category '{category.Category}' is duplicated.");
            }

            ValidateCalibrationMetrics(category.Metrics, $"category[{category.Category}]", errors);
        }

        if (report.Repositories.Count != report.RecordCount)
        {
            errors.Add("Repository evaluation count does not equal recordCount.");
        }

        HashSet<string> recordIds = new(StringComparer.Ordinal);
        foreach (CalibrationRepositoryEvaluation repository in report.Repositories)
        {
            RequireText(repository.RecordId, "repository.recordId", errors);
            RequireText(repository.RepositoryId, $"repository[{repository.RecordId}].repositoryId", errors);
            RequireDigest(repository.SourceDigest, $"repository[{repository.RecordId}].sourceDigest", errors);
            RequireText(repository.BaselineId, $"repository[{repository.RecordId}].baselineId", errors);
            RequireText(
                repository.CandidateEstimatorVersion,
                $"repository[{repository.RecordId}].candidateEstimatorVersion",
                errors);
            RequireDigest(
                repository.CandidateEstimateDigest,
                $"repository[{repository.RecordId}].candidateEstimateDigest",
                errors);
            ValidateRange(repository.ReviewedTotal, $"repository[{repository.RecordId}].reviewedTotal", errors);
            ValidateRange(repository.CandidateTotal, $"repository[{repository.RecordId}].candidateTotal", errors);

            if (!recordIds.Add(repository.RecordId))
            {
                errors.Add($"Repository evaluation record ID '{repository.RecordId}' is duplicated.");
            }

            if (repository.ExpectedAbsoluteErrorHours < 0m)
            {
                errors.Add($"Repository '{repository.RecordId}' absolute error cannot be negative.");
            }

            if (repository.TargetCount < 0 ||
                repository.MatchedTargetCount < 0 ||
                repository.MatchedTargetCount > repository.TargetCount ||
                repository.CandidateWorkItemCount < 0 ||
                repository.MatchedCandidateWorkItemCount < 0 ||
                repository.MatchedCandidateWorkItemCount > repository.CandidateWorkItemCount)
            {
                errors.Add($"Repository '{repository.RecordId}' contains invalid match counts.");
            }

            RequireUniqueText(repository.UnmatchedTargetIds, $"repository[{repository.RecordId}].unmatchedTargetIds", errors);
            RequireUniqueText(
                repository.UnmatchedCandidateWorkItemIds,
                $"repository[{repository.RecordId}].unmatchedCandidateWorkItemIds",
                errors);
            RequireUniqueText(
                repository.CategoryMismatchTargetIds,
                $"repository[{repository.RecordId}].categoryMismatchTargetIds",
                errors);
        }

        if (report.Repositories.Select(repository => repository.RepositoryId)
                .Distinct(StringComparer.Ordinal)
                .Count() != report.RepositoryCount)
        {
            errors.Add("Distinct repository evaluation count does not equal repositoryCount.");
        }

        ValidateMatchSummary(report.Match, errors);
        if (report.Match.TargetCount != report.Repositories.Sum(repository => repository.TargetCount) ||
            report.Match.MatchedTargetCount != report.Repositories.Sum(repository => repository.MatchedTargetCount) ||
            report.Match.CandidateWorkItemCount != report.Repositories.Sum(repository => repository.CandidateWorkItemCount) ||
            report.Match.MatchedCandidateWorkItemCount !=
                report.Repositories.Sum(repository => repository.MatchedCandidateWorkItemCount))
        {
            errors.Add("Aggregate match counts do not equal repository match counts.");
        }

        if (report.RepositoryTotals.Expected.SampleCount != report.RecordCount ||
            report.WorkItems.Expected.SampleCount != report.Match.MatchedTargetCount)
        {
            errors.Add("Metric sample counts do not match their evaluation populations.");
        }

        return errors;
    }

    public static EffortRange Sum(IEnumerable<EffortRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);

        decimal low = 0m;
        decimal expected = 0m;
        decimal high = 0m;

        foreach (EffortRange range in ranges)
        {
            low += range.Low;
            expected += range.Expected;
            high += range.High;
        }

        return new EffortRange { Low = low, Expected = expected, High = high };
    }

    private static void ValidateRateCard(RateCard rateCard, List<string> errors)
    {
        RequireVersion(rateCard.SchemaVersion, $"rate card '{rateCard.Id}'", errors);
        RequireText(rateCard.Id, "rateCard.id", errors);
        RequireText(rateCard.Name, "rateCard.name", errors);
        RequireText(rateCard.Currency, "rateCard.currency", errors);
        RequireText(rateCard.Methodology, "rateCard.methodology", errors);

        if (rateCard.HourlyRate < 0m)
        {
            errors.Add($"Rate card '{rateCard.Id}' hourly rate cannot be negative.");
        }

        if (string.IsNullOrEmpty(rateCard.Currency) ||
            rateCard.Currency.Length != 3 ||
            rateCard.Currency.Any(character => character is < 'A' or > 'Z'))
        {
            errors.Add($"Rate card '{rateCard.Id}' currency must be a three-letter uppercase code.");
        }

        if (rateCard.MarketRange is not null &&
            (rateCard.MarketRange.Low < 0m ||
             rateCard.MarketRange.Low > rateCard.MarketRange.Expected ||
             rateCard.MarketRange.Expected > rateCard.MarketRange.High))
        {
            errors.Add($"Rate card '{rateCard.Id}' market range is invalid.");
        }
    }

    private static void ValidateRateAndCost(
        RateCard? rateCard,
        CostRange? totalCost,
        EffortRange hours,
        string path,
        List<string> errors)
    {
        if ((rateCard is null) != (totalCost is null))
        {
            errors.Add("Rate card and total cost must either both be present or both be absent.");
        }

        if (rateCard is not null)
        {
            ValidateRateCard(rateCard, errors);
        }

        ValidateProjectedCost(totalCost, rateCard, hours, requireCost: true, path, errors);
    }

    private static void ValidateAggregate(
        int workItemCount,
        int capabilityCount,
        decimal confidence,
        string path,
        List<string> errors)
    {
        if (workItemCount <= 0 || capabilityCount <= 0)
        {
            errors.Add($"{path} must contain positive item and capability counts.");
        }

        if (confidence is < 0m or > 1m)
        {
            errors.Add($"{path}.confidence must be between 0 and 1.");
        }
    }

    private static void ValidateCapability(
        CapabilityViewEntry capability,
        RateCard? rateCard,
        bool requireCost,
        List<string> errors)
    {
        RequireText(capability.Id, "capability.id", errors);
        RequireText(capability.Title, $"capability[{capability.Id}].title", errors);
        RequireText(capability.Scope, $"capability[{capability.Id}].scope", errors);
        ValidateRange(capability.Hours, $"capability[{capability.Id}].hours", errors);
        ValidateAggregate(
            capability.WorkItemCount,
            capabilityCount: 1,
            capability.Confidence,
            $"capability[{capability.Id}]",
            errors);
        if (capability.EvidenceCount <= 0)
        {
            errors.Add($"Capability '{capability.Id}' must reference evidence.");
        }

        ValidateProjectedCost(
            capability.Cost,
            rateCard,
            capability.Hours,
            requireCost,
            $"capability[{capability.Id}]",
            errors);
    }

    private static void ValidateProjectedCost(
        CostRange? cost,
        RateCard? rateCard,
        EffortRange hours,
        bool requireCost,
        string path,
        List<string> errors)
    {
        if (requireCost && rateCard is not null && cost is null)
        {
            errors.Add($"{path}.cost is required when a rate card is present.");
        }

        if (cost is not null && rateCard is null)
        {
            errors.Add($"{path}.cost cannot exist without a rate card.");
        }

        if (cost is not null &&
            (cost.Low < 0m || cost.Low > cost.Expected || cost.Expected > cost.High))
        {
            errors.Add($"{path}.cost must satisfy 0 <= low <= expected <= high.");
        }

        if (cost is not null && rateCard is not null)
        {
            CostRange expected = new()
            {
                Low = RoundMoney(hours.Low * rateCard.HourlyRate),
                Expected = RoundMoney(hours.Expected * rateCard.HourlyRate),
                High = RoundMoney(hours.High * rateCard.HourlyRate),
                Currency = rateCard.Currency,
            };
            if (cost != expected)
            {
                errors.Add($"{path}.cost does not equal effort multiplied by the selected rate.");
            }
        }
    }

    private static void ValidateViewShape(EstimateViewReport report, List<string> errors)
    {
        bool categoriesExpected = report.View is EstimateViewKind.Category or EstimateViewKind.Review;
        bool scopesExpected = report.View is EstimateViewKind.Scope or EstimateViewKind.Review;
        bool capabilitiesExpected = report.View == EstimateViewKind.WorkItem;
        bool queueExpected = report.View == EstimateViewKind.Review;

        if (!categoriesExpected && report.Categories.Count > 0)
        {
            errors.Add($"View '{report.View}' must not include category rows.");
        }

        if (!scopesExpected && report.Scopes.Count > 0)
        {
            errors.Add($"View '{report.View}' must not include scope rows.");
        }

        if (!capabilitiesExpected && report.Capabilities.Count > 0)
        {
            errors.Add($"View '{report.View}' must not include capability rows.");
        }

        if (!queueExpected && report.ReviewQueue.Count > 0)
        {
            errors.Add($"View '{report.View}' must not include a review queue.");
        }

        if (report.ReviewQueue.Count > 12)
        {
            errors.Add("The review queue cannot contain more than 12 capabilities.");
        }

        if (categoriesExpected &&
            Sum(report.Categories.Select(category => category.Hours)) != report.TotalEffort)
        {
            errors.Add("Category projection totals do not equal total effort.");
        }

        if (report.View == EstimateViewKind.Scope &&
            Sum(report.Scopes.Select(scope => scope.Hours)) != report.TotalEffort)
        {
            errors.Add("Scope projection totals do not equal total effort.");
        }

        if (report.View == EstimateViewKind.WorkItem &&
            Sum(report.Capabilities.Select(capability => capability.Hours)) != report.TotalEffort)
        {
            errors.Add("Capability projection totals do not equal total effort.");
        }
    }

    private static void ValidateRange(EffortRange range, string path, List<string> errors)
    {
        if (range.Low < 0m || range.Low > range.Expected || range.Expected > range.High)
        {
            errors.Add($"{path} must satisfy 0 <= low <= expected <= high.");
        }
    }

    private static void ValidateReviewedRange(
        EffortRange range,
        string path,
        string? sizeException,
        string sizeExceptionPath,
        List<string> errors)
    {
        ValidateRange(range, path, errors);

        if (range.Expected == 0m)
        {
            if (range.Low != 0m || range.High != 0m)
            {
                errors.Add($"{path} uses zero reviewed EHE and must be exactly 0/0/0.");
            }

            RequireText(sizeException, sizeExceptionPath, errors);
            return;
        }

        if (range.Expected is < 0.5m or > 8m)
        {
            RequireText(sizeException, sizeExceptionPath, errors);
        }
    }

    private static void ValidateCalibrationSource(
        CalibrationSourceProvenance source,
        string recordPath,
        List<string> errors)
    {
        RequireText(source.SourceReference, $"{recordPath}.source.sourceReference", errors);
        RequireText(source.Revision, $"{recordPath}.source.revision", errors);
        RequireText(source.LicenseExpression, $"{recordPath}.source.licenseExpression", errors);

        if (source.DataClassification == CalibrationDataClassification.Private &&
            source.RedistributionAllowed)
        {
            errors.Add($"{recordPath} is classified private but permits redistribution.");
        }

        if (source.DataClassification != CalibrationDataClassification.Private &&
            !source.RedistributionAllowed)
        {
            errors.Add($"{recordPath} is public or synthetic but does not permit redistribution.");
        }
    }

    private static void ValidateCalibrationReview(
        CalibrationReviewProvenance review,
        string recordPath,
        List<string> errors)
    {
        if (review.CompletedOn == default)
        {
            errors.Add($"{recordPath}.review.completedOn is required.");
        }

        if (review.Reviewers.Count == 0)
        {
            errors.Add($"{recordPath}.review.reviewers must contain at least one reviewer.");
        }

        HashSet<(string Id, CalibrationReviewerRole Role)> reviewers = [];
        foreach (CalibrationReviewer reviewer in review.Reviewers)
        {
            RequireText(reviewer.Id, $"{recordPath}.review.reviewer.id", errors);
            if (!reviewers.Add((reviewer.Id, reviewer.Role)))
            {
                errors.Add(
                    $"Reviewer '{reviewer.Id}' has duplicate role '{reviewer.Role}' in {recordPath}.");
            }

            if (reviewer.Kind == CalibrationReviewerKind.HostAi)
            {
                RequireText(reviewer.ModelId, $"{recordPath}.review.reviewer[{reviewer.Id}].modelId", errors);
                RequireText(
                    reviewer.ModelVersion,
                    $"{recordPath}.review.reviewer[{reviewer.Id}].modelVersion",
                    errors);
            }
        }

        if (!review.Reviewers.Any(reviewer => reviewer.Role == CalibrationReviewerRole.Teacher))
        {
            errors.Add($"{recordPath}.review requires a '{CalibrationReviewerRole.Teacher}' role.");
        }

        if (review.Status is CalibrationReviewStatus.Reviewed or CalibrationReviewStatus.Adjudicated &&
            !review.Reviewers.Any(reviewer => reviewer.Role == CalibrationReviewerRole.Reviewer))
        {
            errors.Add(
                $"{recordPath}.review status '{review.Status}' requires a " +
                $"'{CalibrationReviewerRole.Reviewer}' role.");
        }

        if (review.Status == CalibrationReviewStatus.Adjudicated &&
            !review.Reviewers.Any(reviewer => reviewer.Role == CalibrationReviewerRole.Adjudicator))
        {
            errors.Add(
                $"{recordPath}.review status '{review.Status}' requires an " +
                $"'{CalibrationReviewerRole.Adjudicator}' role.");
        }
    }

    private static void ValidateCalibrationTargets(
        CalibrationRecord record,
        string recordPath,
        List<string> errors)
    {
        if (record.Targets.Count == 0)
        {
            errors.Add($"{recordPath}.targets must contain at least one target.");
        }

        HashSet<string> targetIds = new(StringComparer.Ordinal);
        HashSet<string> sourceWorkItemIds = new(StringComparer.Ordinal);
        foreach (CalibrationTarget target in record.Targets)
        {
            string path = $"{recordPath}.target[{target.Id}]";
            RequireText(target.Id, $"{recordPath}.target.id", errors);
            RequireText(target.Title, $"{path}.title", errors);
            RequireText(target.Scope, $"{path}.scope", errors);
            RequireText(target.Rationale, $"{path}.rationale", errors);
            ValidateReviewedRange(
                target.Hours,
                $"{path}.hours",
                target.SizeException,
                $"{path}.sizeException",
                errors);

            if (!targetIds.Add(target.Id))
            {
                errors.Add($"Calibration target ID '{target.Id}' is duplicated in record '{record.Id}'.");
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
                        $"Source work-item ID '{sourceWorkItemId}' is mapped to more than one target " +
                        $"in record '{record.Id}'.");
                }
            }

            if (target.EvidenceIds.Count == 0)
            {
                errors.Add($"{path}.evidenceIds must contain at least one ID.");
            }

            RequireUniqueText(target.EvidenceIds, $"{path}.evidenceIds", errors);
            RequireUniqueText(target.UncertaintyReasons, $"{path}.uncertaintyReasons", errors);
        }
    }

    private static void ValidateCalibrationMetrics(
        CalibrationRangeMetrics metrics,
        string path,
        List<string> errors)
    {
        ValidatePointMetrics(metrics.Low, $"{path}.low", errors);
        ValidatePointMetrics(metrics.Expected, $"{path}.expected", errors);
        ValidatePointMetrics(metrics.High, $"{path}.high", errors);

        CalibrationIntervalMetrics interval = metrics.Interval;
        if (interval.SampleCount < 0 ||
            interval.ReviewedExpectedCoveredCount < 0 ||
            interval.ReviewedExpectedCoveredCount > interval.SampleCount ||
            interval.ReviewedRangeFullyCoveredCount < 0 ||
            interval.ReviewedRangeFullyCoveredCount > interval.SampleCount ||
            interval.MeanCandidateWidthHours < 0m ||
            interval.MeanReviewedWidthHours < 0m)
        {
            errors.Add($"{path}.interval contains invalid counts or widths.");
        }

        ValidateUnitRatio(interval.ReviewedExpectedCoverage, $"{path}.interval.reviewedExpectedCoverage", errors);
        ValidateUnitRatio(
            interval.ReviewedRangeFullyCoveredRate,
            $"{path}.interval.reviewedRangeFullyCoveredRate",
            errors);
    }

    private static void ValidatePointMetrics(
        CalibrationPointMetrics metrics,
        string path,
        List<string> errors)
    {
        if (metrics.SampleCount < 0 ||
            metrics.ReviewedHours < 0m ||
            metrics.CandidateHours < 0m ||
            metrics.MeanAbsoluteErrorHours < 0m ||
            metrics.MedianAbsoluteErrorHours < 0m ||
            metrics.RootMeanSquaredErrorHours < 0m ||
            metrics.WeightedAbsolutePercentageError < 0m)
        {
            errors.Add($"{path} contains invalid counts, hours, or nonnegative error metrics.");
        }
    }

    private static void ValidateMatchSummary(CalibrationMatchSummary match, List<string> errors)
    {
        if (match.TargetCount < 0 ||
            match.MatchedTargetCount < 0 ||
            match.MatchedTargetCount > match.TargetCount ||
            match.SourceWorkItemReferenceCount < 0 ||
            match.MatchedSourceWorkItemReferenceCount < 0 ||
            match.MatchedSourceWorkItemReferenceCount > match.SourceWorkItemReferenceCount ||
            match.CandidateWorkItemCount < 0 ||
            match.MatchedCandidateWorkItemCount < 0 ||
            match.MatchedCandidateWorkItemCount > match.CandidateWorkItemCount)
        {
            errors.Add("Calibration aggregate match counts are invalid.");
        }

        ValidateUnitRatio(match.TargetMatchRate, "match.targetMatchRate", errors);
        ValidateUnitRatio(
            match.SourceWorkItemReferenceMatchRate,
            "match.sourceWorkItemReferenceMatchRate",
            errors);
        ValidateUnitRatio(match.CandidateWorkItemMatchRate, "match.candidateWorkItemMatchRate", errors);
    }

    private static void ValidateCorpusReference(
        CalibrationCorpusReference reference,
        string path,
        List<string> errors)
    {
        RequireText(reference.Id, $"{path}.id", errors);
        RequireText(reference.Version, $"{path}.version", errors);
        RequireDigest(reference.Digest, $"{path}.digest", errors);
    }

    private static void ValidateMutationScope(
        CalibrationMutationScope scope,
        EffortCategory? category,
        string path,
        List<string> errors)
    {
        if (scope == CalibrationMutationScope.Category && category is null)
        {
            errors.Add($"{path}.category is required for category scope.");
        }

        if (scope == CalibrationMutationScope.RepositoryTotal && category is not null)
        {
            errors.Add($"{path}.category must be omitted for repository-total scope.");
        }
    }

    private static void RequireDigest(string? value, string path, List<string> errors)
    {
        RequireText(value, path, errors);
        if (value is not null && !value.StartsWith("sha256:", StringComparison.Ordinal))
        {
            errors.Add($"{path} must start with 'sha256:'.");
        }
    }

    private static void RequireUniqueText(
        IReadOnlyList<string> values,
        string path,
        List<string> errors)
    {
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireText(value, path, errors);
            if (!unique.Add(value))
            {
                errors.Add($"{path} contains duplicate value '{value}'.");
            }
        }
    }

    private static void ValidateUnitRatio(decimal? value, string path, List<string> errors)
    {
        if (value is < 0m or > 1m)
        {
            errors.Add($"{path} must be between 0 and 1 when present.");
        }
    }

    private static void RequireVersion(string version, string subject, List<string> errors)
    {
        if (!string.Equals(version, ContractVersions.V1, StringComparison.Ordinal))
        {
            errors.Add($"The {subject} uses unsupported schema version '{version}'.");
        }
    }

    private static void RequireText(string? value, string path, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{path} is required.");
        }
    }

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
