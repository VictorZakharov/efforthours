using Fairbill.Contracts.V1;

namespace Fairbill.Contracts;

public static class ContractValidation
{
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

        if ((report.RateCard is null) != (report.TotalCost is null))
        {
            errors.Add("Rate card and total cost must either both be present or both be absent.");
        }

        if (report.RateCard is not null)
        {
            ValidateRateCard(report.RateCard, errors);
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

        if (rateCard.MarketRange is not null &&
            (rateCard.MarketRange.Low < 0m ||
             rateCard.MarketRange.Low > rateCard.MarketRange.Expected ||
             rateCard.MarketRange.Expected > rateCard.MarketRange.High))
        {
            errors.Add($"Rate card '{rateCard.Id}' market range is invalid.");
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
}
