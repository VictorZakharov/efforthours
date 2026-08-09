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
    public const string EstimatorVersion = "change-seed/0.1.0";

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

            if (expectedDifference > 0m)
            {
                EffortRange marginal = PositiveDifference(baseHours, headHours);
                if (touched.Length > 0 && baseCapability is not null)
                {
                    EffortRange modification = ModificationRange(
                        source.Category,
                        touched.Sum(path => path.EditRegions),
                        Math.Max(baseHours.Expected, headHours.Expected));
                    hours = MaxExpected(marginal, modification);
                }
                else
                {
                    hours = marginal;
                }

                rule = "capability-marginal";
                verb = baseCapability is null ? "Add" : "Expand";
                reason = "Positive base-to-head marginal effort from the existing repository capability model, " +
                    "bounded by touched final-change regions when existing behavior was also modified.";
            }
            else if (expectedDifference < 0m && touched.Length > 0)
            {
                hours = RemovalRange(Math.Abs(expectedDifference));
                rule = "capability-removal";
                verb = "Remove or simplify";
                reason = "Bounded removal and simplification work derived from capability context; deleted volume " +
                    "is not treated as negative effort or a direct labor multiplier.";
            }
            else if (touched.Length > 0)
            {
                hours = ModificationRange(
                    source.Category,
                    touched.Sum(path => path.EditRegions),
                    Math.Max(baseHours.Expected, headHours.Expected));
                rule = "capability-modification";
                verb = "Modify";
                reason = "The final artifact changed inside an existing evidence-backed capability even though " +
                    "its repository-level structural quantity did not increase.";
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

            usedEvidenceIds.UnionWith(evidenceIds);
            items.AddRange(CreateItems(
                selection,
                rule,
                capabilityId,
                source.Category,
                $"{verb} {source.Title}",
                source.Scope,
                source.Complexity,
                hours,
                Math.Min(source.Confidence, 0.82m),
                reason,
                evidenceIds,
                profile,
                touched.Sum(path => path.EditRegions),
                source.UncertaintyReasons));
        }

        foreach (ChangePathEvidence path in represented.Where(path => !usedEvidenceIds.Contains(path.Id)))
        {
            EffortCategory category = FallbackCategory(path);
            EffortRange hours = FallbackRange(path, category);
            items.AddRange(CreateItems(
                selection,
                "maintained-artifact-fallback",
                path.Id,
                category,
                FallbackTitle(path),
                path.Path,
                ComplexityLevel.Moderate,
                hours,
                0.58m,
                "No more specific positive capability delta explained this maintained final change; " +
                    "a bounded edit-region fallback preserves visible effort without using physical lines as value.",
                [path.Id],
                profile,
                path.EditRegions,
                ["The current analyzer could not map this artifact change to a more specific capability."]));
            usedEvidenceIds.Add(path.Id);
        }

        if (represented.Length > 0)
        {
            string[] allEvidenceIds = [.. represented.Select(path => path.Id).Order(StringComparer.Ordinal)];
            int capabilityCount = items
                .Select(item => item.Category)
                .Distinct()
                .Count();
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
