using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Reporting;

public static class EstimateProjector
{
    public const int MaximumReviewScopes = 20;
    public const int MaximumReviewCapabilities = 12;

    private const int MaterialCapabilityCount = 6;
    private const int UncertainCapabilityCount = 6;
    private const decimal LowConfidenceThreshold = 0.75m;

    public static EstimateViewReport Project(EstimateReport report, EstimateViewKind view)
    {
        ArgumentNullException.ThrowIfNull(report);
        EnsureValid(report);

        CapabilityGroup[] capabilityGroups =
        [
            .. BuildCapabilityGroups(report.WorkItems, rateCard: report.RateCard)
                .OrderBy(group => group.Entry.Category)
                .ThenBy(group => group.Entry.Scope, StringComparer.Ordinal)
                .ThenBy(group => group.Entry.Id, StringComparer.Ordinal),
        ];
        CapabilityViewEntry[] gapCapabilities =
        [
            .. BuildCapabilityGroups(report.ProfessionalizationGap, rateCard: null)
                .Select(group => group.Entry)
                .OrderBy(entry => entry.Category)
                .ThenBy(entry => entry.Scope, StringComparer.Ordinal)
                .ThenBy(entry => entry.Id, StringComparer.Ordinal),
        ];
        CategoryViewEntry[] allCategories = BuildCategories(report, capabilityGroups);
        ScopeViewEntry[] allScopes = BuildScopes(report, capabilityGroups);
        CapabilityViewEntry[] allCapabilities =
        [.. capabilityGroups.Select(group => group.Entry)];
        CapabilityViewEntry[] reviewQueue = view == EstimateViewKind.Review
            ? BuildReviewQueue(allCapabilities)
            : [];

        CategoryViewEntry[] categories = view is EstimateViewKind.Category or EstimateViewKind.Review
            ? allCategories
            : [];
        ScopeViewEntry[] scopes = view switch
        {
            EstimateViewKind.Scope => allScopes,
            EstimateViewKind.Review =>
            [
                .. allScopes
                    .OrderByDescending(entry => entry.Hours.Expected)
                    .ThenBy(entry => entry.Scope, StringComparer.Ordinal)
                    .Take(MaximumReviewScopes),
            ],
            _ => [],
        };
        CapabilityViewEntry[] capabilities = view == EstimateViewKind.WorkItem
            ? allCapabilities
            : [];

        HashSet<string> includedScopeIds = scopes
            .Select(entry => entry.Scope)
            .ToHashSet(StringComparer.Ordinal);
        ScopeViewEntry[] omittedScopes =
        [.. allScopes.Where(entry => !includedScopeIds.Contains(entry.Scope))];
        HashSet<string> includedCapabilityIds = capabilities
            .Concat(reviewQueue)
            .Select(entry => entry.Id)
            .ToHashSet(StringComparer.Ordinal);
        CapabilityViewEntry[] omittedCapabilities =
        [.. allCapabilities.Where(entry => !includedCapabilityIds.Contains(entry.Id))];

        EstimateViewReport projection = new()
        {
            SourceEstimateSchemaVersion = report.SchemaVersion,
            View = view,
            EstimatorVersion = report.EstimatorVersion,
            Repository = report.Repository,
            Profile = report.Profile,
            Baseline = report.Baseline,
            TotalEffort = report.TotalEffort,
            RateCard = report.RateCard,
            TotalCost = report.TotalCost,
            Counts = new EstimateViewCounts
            {
                RepresentedWorkItems = report.WorkItems.Count,
                CapabilityGroups = allCapabilities.Length,
                Scopes = allScopes.Length,
                ProfessionalizationGapItems = report.ProfessionalizationGap.Count,
            },
            Categories = categories,
            Scopes = scopes,
            Capabilities = capabilities,
            ReviewQueue = reviewQueue,
            ProfessionalizationGap = gapCapabilities,
            Omissions = new ProjectionOmissions
            {
                ScopeCount = omittedScopes.Length,
                ScopeExpectedHours = omittedScopes.Sum(entry => entry.Hours.Expected),
                CapabilityCount = omittedCapabilities.Length,
                CapabilityExpectedHours = omittedCapabilities.Sum(entry => entry.Hours.Expected),
            },
            Diagnostics = report.Diagnostics,
            Assumptions = report.Assumptions,
            Verification = report.Verification,
        };

        IReadOnlyList<string> errors = ContractValidation.Validate(projection);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "The estimate projection is invalid: " + string.Join(" ", errors));
        }

        return projection;
    }

    public static EstimateExplanation Explain(
        EstimateReport report,
        RepositoryEvidence evidence,
        string requestedId)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedId);
        EnsureValid(report);
        EnsureValid(evidence);
        if (!RepositoriesMatch(report.Repository, evidence.Repository))
        {
            throw new ArgumentException(
                "The evidence repository does not match the estimate repository.",
                nameof(evidence));
        }

        WorkItem[] allItems =
        [.. report.WorkItems.Concat(report.ProfessionalizationGap)];
        WorkItem? exact = allItems.SingleOrDefault(item => item.Id == requestedId);
        WorkItem[] capabilityMatches =
        [
            .. allItems
                .Where(item => GetCapabilityId(item.Id) == requestedId)
                .OrderBy(item => item.Id, StringComparer.Ordinal),
        ];
        if (exact is not null && capabilityMatches.Any(item => item.Id != exact.Id))
        {
            throw new ArgumentException(
                $"ID '{requestedId}' is ambiguous: it identifies both a work item and a capability group.",
                nameof(requestedId));
        }

        ExplanationMatchKind matchKind;
        WorkItem[] matchedItems;
        if (exact is not null)
        {
            matchKind = ExplanationMatchKind.WorkItem;
            matchedItems = [exact];
        }
        else
        {
            matchedItems = capabilityMatches;
            if (matchedItems.Length == 0)
            {
                throw new KeyNotFoundException(
                    $"No work item or capability has ID '{requestedId}'.");
            }

            matchKind = ExplanationMatchKind.Capability;
        }

        CapabilityGroup capability = BuildCapabilityGroups(matchedItems, rateCard: null).Single();
        Dictionary<string, EvidenceFact> factsById = evidence.Facts
            .ToDictionary(fact => fact.Id, StringComparer.Ordinal);
        string[] referencedIds =
        [
            .. matchedItems
                .SelectMany(item => item.EvidenceIds)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        EvidenceFact[] facts =
        [
            .. referencedIds
                .Where(factsById.ContainsKey)
                .Select(id => factsById[id]),
        ];
        string[] missingIds =
        [.. referencedIds.Where(id => !factsById.ContainsKey(id))];

        EstimateExplanation explanation = new()
        {
            EvidenceSchemaVersion = evidence.SchemaVersion,
            EstimatorVersion = report.EstimatorVersion,
            Repository = report.Repository,
            Profile = report.Profile,
            RequestedId = requestedId,
            MatchKind = matchKind,
            Capability = capability.Entry,
            WorkItems = matchedItems,
            EvidenceFacts = facts,
            MissingEvidenceIds = missingIds,
            Estimators =
            [
                .. matchedItems
                    .Select(item => item.Estimator)
                    .Distinct()
                    .OrderBy(estimator => estimator.Id, StringComparer.Ordinal)
                    .ThenBy(estimator => estimator.Version, StringComparer.Ordinal),
            ],
            Assumptions = Normalize(matchedItems.SelectMany(item => item.Assumptions)),
            Exclusions = Normalize(matchedItems.SelectMany(item => item.Exclusions)),
            CorrelationGroups = Normalize(
                matchedItems
                    .Select(item => item.CorrelationGroup)
                    .OfType<string>()),
            UncertaintyReasons = Normalize(
                matchedItems.SelectMany(item => item.UncertaintyReasons)),
            Diagnostics = report.Diagnostics,
            Verification = report.Verification,
        };

        IReadOnlyList<string> errors = ContractValidation.Validate(explanation);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "The estimate explanation is invalid: " + string.Join(" ", errors));
        }

        return explanation;
    }

    public static string GetCapabilityId(string workItemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workItemId);

        int marker = workItemId.LastIndexOf(":part-", StringComparison.Ordinal);
        if (marker <= 0 ||
            !int.TryParse(
                workItemId.AsSpan(marker + ":part-".Length),
                out int partNumber) ||
            partNumber <= 0)
        {
            return workItemId;
        }

        return workItemId[..marker];
    }

    private static CapabilityGroup[] BuildCapabilityGroups(
        IEnumerable<WorkItem> items,
        RateCard? rateCard)
    {
        return
        [
            .. items
                .GroupBy(item => GetCapabilityId(item.Id), StringComparer.Ordinal)
                .Select(group => BuildCapabilityGroup(group.Key, [.. group], rateCard)),
        ];
    }

    private static CapabilityGroup BuildCapabilityGroup(
        string id,
        WorkItem[] items,
        RateCard? rateCard)
    {
        WorkItem first = items[0];
        string title = BaseTitle(first.Title, items.Length);
        if (items.Any(item =>
            item.Category != first.Category ||
            item.Scope != first.Scope ||
            BaseTitle(item.Title, items.Length) != title ||
            item.Estimator != first.Estimator ||
            item.CorrelationGroup != first.CorrelationGroup ||
            !item.Profiles.SequenceEqual(first.Profiles)))
        {
            throw new InvalidOperationException(
                $"Capability group '{id}' contains inconsistent work-item lineage.");
        }

        EffortRange hours = ContractValidation.Sum(items.Select(item => item.Hours));
        CapabilityViewEntry entry = new()
        {
            Id = id,
            Category = first.Category,
            Title = title,
            Scope = first.Scope,
            Hours = hours,
            Cost = CalculateCost(hours, rateCard),
            Confidence = items.Min(item => item.Confidence),
            WorkItemCount = items.Length,
            EvidenceCount = items
                .SelectMany(item => item.EvidenceIds)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            UncertaintyReasons = Normalize(
                items.SelectMany(item => item.UncertaintyReasons)),
        };
        return new CapabilityGroup(entry, items);
    }

    private static CategoryViewEntry[] BuildCategories(
        EstimateReport report,
        IReadOnlyList<CapabilityGroup> capabilities)
    {
        return
        [
            .. report.Categories
                .OrderBy(category => category.Category)
                .Select(category =>
                {
                    WorkItem[] items =
                    [.. report.WorkItems.Where(item => item.Category == category.Category)];
                    return new CategoryViewEntry
                    {
                        Category = category.Category,
                        Hours = category.Hours,
                        Cost = CalculateCost(category.Hours, report.RateCard),
                        WorkItemCount = items.Length,
                        CapabilityCount = capabilities.Count(
                            capability => capability.Entry.Category == category.Category),
                        Confidence = WeightedConfidence(items),
                    };
                }),
        ];
    }

    private static ScopeViewEntry[] BuildScopes(
        EstimateReport report,
        IReadOnlyList<CapabilityGroup> capabilities)
    {
        return
        [
            .. report.WorkItems
                .GroupBy(item => item.Scope, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group =>
                {
                    WorkItem[] items = [.. group];
                    EffortRange hours = ContractValidation.Sum(items.Select(item => item.Hours));
                    return new ScopeViewEntry
                    {
                        Scope = group.Key,
                        Hours = hours,
                        Cost = CalculateCost(hours, report.RateCard),
                        WorkItemCount = items.Length,
                        CapabilityCount = capabilities.Count(
                            capability => capability.Entry.Scope == group.Key),
                        Confidence = WeightedConfidence(items),
                    };
                }),
        ];
    }

    private static CapabilityViewEntry[] BuildReviewQueue(
        IReadOnlyList<CapabilityViewEntry> capabilities)
    {
        Dictionary<string, CapabilityViewEntry> selected = new(StringComparer.Ordinal);
        List<string> order = [];

        foreach (CapabilityViewEntry capability in capabilities
            .OrderByDescending(entry => entry.Hours.Expected)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .Take(MaterialCapabilityCount))
        {
            AddReviewCandidate(selected, order, capability, "material-effort");
        }

        foreach (CapabilityViewEntry capability in capabilities
            .Where(entry =>
                entry.Confidence < LowConfidenceThreshold ||
                entry.UncertaintyReasons.Count > 0)
            .OrderBy(entry => entry.Confidence)
            .ThenByDescending(entry => entry.Hours.Expected)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .Take(UncertainCapabilityCount))
        {
            if (capability.Confidence < LowConfidenceThreshold)
            {
                AddReviewCandidate(selected, order, capability, "low-confidence");
            }

            if (capability.UncertaintyReasons.Count > 0)
            {
                AddReviewCandidate(selected, order, capability, "explicit-uncertainty");
            }
        }

        return
        [
            .. order
                .Take(MaximumReviewCapabilities)
                .Select(id => selected[id]),
        ];
    }

    private static void AddReviewCandidate(
        Dictionary<string, CapabilityViewEntry> selected,
        List<string> order,
        CapabilityViewEntry capability,
        string reason)
    {
        if (!selected.TryGetValue(capability.Id, out CapabilityViewEntry? existing))
        {
            order.Add(capability.Id);
            existing = capability;
        }

        selected[capability.Id] = existing with
        {
            ReviewReasons = Normalize([.. existing.ReviewReasons, reason]),
        };
    }

    private static decimal WeightedConfidence(IReadOnlyList<WorkItem> items)
    {
        decimal expected = items.Sum(item => item.Hours.Expected);
        if (expected == 0m)
        {
            return 0m;
        }

        decimal weighted = items.Sum(item => item.Hours.Expected * item.Confidence);
        return decimal.Round(weighted / expected, 2, MidpointRounding.AwayFromZero);
    }

    private static CostRange? CalculateCost(EffortRange hours, RateCard? rateCard) =>
        rateCard is null
            ? null
            : new CostRange
            {
                Low = Money(hours.Low * rateCard.HourlyRate),
                Expected = Money(hours.Expected * rateCard.HourlyRate),
                High = Money(hours.High * rateCard.HourlyRate),
                Currency = rateCard.Currency,
            };

    private static string BaseTitle(string title, int groupSize)
    {
        if (groupSize <= 1 || !title.EndsWith(')'))
        {
            return title;
        }

        int marker = title.LastIndexOf(" (part ", StringComparison.Ordinal);
        return marker > 0 ? title[..marker] : title;
    }

    private static string[] Normalize(IEnumerable<string> values) =>
    [
        .. values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    private static decimal Money(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static void EnsureValid(EstimateReport report)
    {
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The estimate report is invalid: " + string.Join(" ", errors),
                nameof(report));
        }
    }

    private static void EnsureValid(RepositoryEvidence evidence)
    {
        IReadOnlyList<string> errors = ContractValidation.Validate(evidence);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The repository evidence is invalid: " + string.Join(" ", errors),
                nameof(evidence));
        }
    }

    private static bool RepositoriesMatch(
        RepositoryDescriptor estimate,
        RepositoryDescriptor evidence) =>
        estimate.Name == evidence.Name &&
        estimate.Scope == evidence.Scope &&
        estimate.SourceDigest == evidence.SourceDigest &&
        estimate.Ecosystems.SequenceEqual(evidence.Ecosystems, StringComparer.Ordinal);

    private sealed record CapabilityGroup(
        CapabilityViewEntry Entry,
        IReadOnlyList<WorkItem> Items);
}
