using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed record ChangeWorkItemResult(
    IReadOnlyList<WorkItem> WorkItems,
    IReadOnlyList<CategoryEstimate> Categories,
    EffortRange TotalEffort);

internal static partial class ChangeWorkItemBuilder
{
    public const string EstimatorVersion = "change-seed/0.14.0";

    public static ChangeWorkItemResult Build(
        ChangeSelection selection,
        ChangeEvidence changeEvidence,
        RepositoryEvidence baseEvidence,
        RepositoryEvidence headEvidence,
        EstimateReport baseEstimate,
        EstimateReport headEstimate,
        EstimationProfile profile)
    {
        Dictionary<string, EvidenceFact> baseFacts = baseEvidence.Facts
            .ToDictionary(fact => fact.Id, StringComparer.Ordinal);
        Dictionary<string, EvidenceFact> headFacts = headEvidence.Facts
            .ToDictionary(fact => fact.Id, StringComparer.Ordinal);
        Dictionary<string, Capability> baseCapabilities = Capabilities(baseEstimate, baseFacts);
        Dictionary<string, Capability> headCapabilities = Capabilities(headEstimate, headFacts);
        ChangePathEvidence[] represented = [.. changeEvidence.Paths.Where(path => path.Represented)];
        HashSet<string> usedEvidenceIds = new(StringComparer.Ordinal);
        HashSet<EffortCategory> comprehensionCategories = [];
        List<WorkItem> items = [];

        foreach (string capabilityId in baseCapabilities.Keys
            .Union(headCapabilities.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            baseCapabilities.TryGetValue(capabilityId, out Capability? baseCapability);
            headCapabilities.TryGetValue(capabilityId, out Capability? headCapability);
            Capability source = headCapability ?? baseCapability!;
            ChangePathEvidence[] touched = [.. represented.Where(path =>
                Touches(source.Paths, path) ||
                (baseCapability is not null && Touches(baseCapability.Paths, path)))];
            EffortRange baseHours = baseCapability?.Hours ?? Zero();
            EffortRange headHours = headCapability?.Hours ?? Zero();
            decimal expectedDifference = headHours.Expected - baseHours.Expected;
            EffortRange? hours = null;
            string? rule = null;
            string? verb = null;
            string? reason = null;

            if (!SupportsCapabilityDelta(source.Category, baseCapability, headCapability))
            {
                continue;
            }

            if (expectedDifference > 0m)
            {
                EffortRange marginal = PositiveDifference(baseHours, headHours);
                ChangePathEvidence[] logicalCandidates = touched.Length > 0
                    ? touched
                    : represented;
                bool modifiesExistingArtifact = logicalCandidates.Any(path =>
                    path.Status == ChangePathStatus.Modified);
                if (logicalCandidates.Length > 0 &&
                    (baseCapability is not null || modifiesExistingArtifact))
                {
                    ChangePathEvidence[] logicalEvidence = DistinctLogicalPaths(logicalCandidates);
                    if (logicalEvidence.Length == 0)
                    {
                        continue;
                    }

                    touched = logicalEvidence;
                    hours = ModificationRange(source.Category, logicalEvidence);
                }
                else
                {
                    hours = marginal;
                }

                rule = "capability-marginal";
                verb = baseCapability is null ? "Add" : "Expand";
                reason = baseCapability is null && !modifiesExistingArtifact
                    ? "Positive base-to-head marginal effort for a distinct capability added by the final change."
                    : "The final change expands an existing artifact-backed capability. Repository work-item " +
                        "partitions provide context but share one evidence-derived logical marginal budget.";
            }
            else if (expectedDifference < 0m && touched.Length > 0)
            {
                hours = RemovalRange(Math.Abs(expectedDifference));
                rule = "capability-removal";
                verb = "Remove or simplify";
                reason = "Bounded removal and simplification work derived from capability context; deleted volume " +
                    "is not treated as negative effort or a direct labor multiplier.";
            }
            else if (touched.Length > 0 &&
                baseCapability is not null &&
                headCapability is not null &&
                HasMaterialCapabilityEvidenceChange(
                    baseCapability,
                    headCapability,
                    baseFacts,
                    headFacts,
                    touched))
            {
                touched = DistinctLogicalPaths(touched);
                if (touched.Length == 0)
                {
                    continue;
                }

                hours = ModificationRange(source.Category, touched);
                rule = "capability-modification";
                verb = "Modify";
                reason = "The final artifact materially changed the normalized evidence for an existing " +
                    "capability. Its distinct changed paths share one logical marginal modification budget.";
            }

            if (hours is null || hours.Expected <= 0m || rule is null || verb is null || reason is null)
            {
                continue;
            }

            string[] evidenceIds = touched.Length > 0
                ? [.. touched.Select(path => path.Id).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]
                : [.. represented.Select(path => path.Id).Order(StringComparer.Ordinal)];
            if (evidenceIds.Length == 0)
            {
                continue;
            }

            comprehensionCategories.Add(source.Category);
            CapabilityRolePartition[] partitions = PartitionCapabilityRoleBudget(
                source.Category,
                touched,
                evidenceIds,
                hours);
            foreach (CapabilityRolePartition partition in partitions)
            {
                bool primary = partition.Category == source.Category;
                string partitionTitle = primary
                    ? $"{verb} {source.Title}"
                    : $"{verb} {CategoryName(partition.Category)} support for {source.Title}";
                string partitionScope = primary || partition.Paths.Count != 1
                    ? source.Scope
                    : partition.Paths[0].Path;
                string partitionReason = partitions.Length == 1
                    ? reason
                    : reason + " The capability's unchanged logical budget is partitioned by explicit " +
                        "maintained-artifact role so production, tests, documentation, and delivery evidence " +
                        "remain disjoint without multiplying total effort.";
                usedEvidenceIds.UnionWith(partition.EvidenceIds);
                items.AddRange(CreateItems(
                    selection,
                    rule,
                    capabilityId,
                    partition.Category,
                    partitionTitle,
                    partitionScope,
                    source.Complexity,
                    partition.Hours,
                    Math.Min(source.Confidence, 0.82m),
                    partitionReason,
                    partition.EvidenceIds,
                    profile,
                    partition.Paths.Sum(path => path.EditRegions),
                    source.UncertaintyReasons));
            }
        }

        IGrouping<(EffortCategory Category, ChangePathStatus Status), ChangePathEvidence>[] fallbackGroups =
        [.. represented
            .Where(path => !usedEvidenceIds.Contains(path.Id))
            .GroupBy(path => (Category: FallbackCategory(path), path.Status))
            .OrderBy(group => group.Key.Category)
            .ThenBy(group => group.Key.Status)];
        foreach (IGrouping<(EffortCategory Category, ChangePathStatus Status), ChangePathEvidence> group in
            fallbackGroups)
        {
            ChangePathEvidence[] paths = [.. group.OrderBy(path => path.Path, StringComparer.Ordinal)];
            EffortCategory category = group.Key.Category;
            comprehensionCategories.Add(category);
            EffortRange hours = FallbackRange(paths, category);
            string[] evidenceIds = [.. paths.Select(path => path.Id).Order(StringComparer.Ordinal)];
            items.AddRange(CreateItems(
                selection,
                "maintained-artifact-fallback",
                string.Join(':', evidenceIds),
                category,
                FallbackTitle(paths, category),
                paths.Length == 1 ? paths[0].Path : ".",
                ComplexityLevel.Moderate,
                hours,
                0.58m,
                "No more specific material capability delta explained these maintained artifacts. Their edit " +
                    "regions share one category-and-status marginal budget rather than repeating a per-path minimum.",
                evidenceIds,
                profile,
                paths.Sum(path => path.EditRegions),
                ["The current analyzer could not map this artifact change to a more specific capability."]));
            usedEvidenceIds.UnionWith(evidenceIds);
        }

        if (represented.Length > 0)
        {
            string[] allEvidenceIds = [.. represented.Select(path => path.Id).Order(StringComparer.Ordinal)];
            int capabilityCount = comprehensionCategories.Count;
            decimal comprehensionExpected = Math.Min(
                4m,
                0.5m + (Math.Max(1, capabilityCount) - 1) * 0.25m);
            items.AddRange(CreateItems(
                selection,
                "change-comprehension",
                "change-comprehension",
                EffortCategory.SpecificationComprehensionAndDomainLearning,
                "Understand the bounded change specification",
                ".",
                ComplexityLevel.Moderate,
                RangeFromExpected(comprehensionExpected, 0.78m),
                0.78m,
                "Comprehend the final behavior and quality delta represented by the affected capability categories.",
                allEvidenceIds,
                profile,
                Math.Max(1, capabilityCount),
                []));

            decimal reviewExpected = MarginalPathHours(represented.Length, 0.5m, 0.25m, 0.1m, 8m);
            items.AddRange(CreateItems(
                selection,
                "change-review",
                "change-review",
                EffortCategory.SelfReviewAndSystemIntegration,
                "Review and integrate the completed change",
                ".",
                ComplexityLevel.Moderate,
                RangeFromExpected(reviewExpected, 0.8m),
                0.8m,
                "Review the coherent final delta and integrate its affected maintained artifacts; this is " +
                    "derived from final scope rather than commit count.",
                allEvidenceIds,
                profile,
                represented.Length,
                []));

            decimal validationExpected = RoundQuarter(MarginalPathHours(
                represented.Length,
                0.5m,
                0.2m,
                0.05m,
                6m));
            items.AddRange(CreateItems(
                selection,
                "change-validation",
                "change-validation",
                EffortCategory.ManualValidationDebuggingAndHardening,
                "Validate and harden the completed change",
                ".",
                ComplexityLevel.Moderate,
                RangeFromExpected(validationExpected, 0.68m),
                0.68m,
                "Perform bounded validation and debugging once across the coherent final delta; static " +
                    "repository-level validation capabilities are not repeated for every touched scope.",
                allEvidenceIds,
                profile,
                represented.Length,
                ["The immutable snapshots were not executed; validation effort is inferred from represented change surfaces."]));

            if (profile == EstimationProfile.Recreation)
            {
                decimal designExpected = Math.Min(4m, 0.5m + capabilityCount * 0.25m);
                items.AddRange(CreateItems(
                    selection,
                    "change-recreation-design",
                    "change-recreation-design",
                    EffortCategory.ArchitectureAndTechnicalDesign,
                    "Recover change-level design decisions",
                    ".",
                    ComplexityLevel.Moderate,
                    RangeFromExpected(designExpected, 0.68m),
                    0.68m,
                    "The recreation profile includes recovering bounded design choices embodied in the final delta.",
                    allEvidenceIds,
                    profile,
                    Math.Max(1, capabilityCount),
                    ["No explicit change specification was supplied to distinguish provided from inferred design inputs."]));
            }
        }

        WorkItem[] ordered = [.. items
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Scope, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)];
        CategoryEstimate[] categories = [.. ordered
            .GroupBy(item => item.Category)
            .OrderBy(group => group.Key)
            .Select(group => new CategoryEstimate
            {
                Category = group.Key,
                Hours = ContractValidation.Sum(group.Select(item => item.Hours)),
            })];
        return new ChangeWorkItemResult(
            ordered,
            categories,
            ContractValidation.Sum(ordered.Select(item => item.Hours)));
    }
}
