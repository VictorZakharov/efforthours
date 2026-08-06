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
