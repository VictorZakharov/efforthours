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
            ValidateRange(target.Hours, $"{path}.hours", errors);
            if (target.Hours.Expected <= 0m)
            {
                errors.Add($"{path}.hours.expected must be positive.");
            }

            if (target.Hours.Expected is < 0.5m or > 8m &&
                string.IsNullOrWhiteSpace(target.SizeException))
            {
                errors.Add($"{path} falls outside 0.5-to-8 expected hours and requires sizeException.");
            }

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
