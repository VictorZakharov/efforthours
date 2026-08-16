using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static partial class CalibrationUncertaintyFeatureProjector
{
    private static readonly string[] NonMaterialOfflineSuffixes =
    [
        ":not-executed",
        ":not-performed",
        ":not-verified",
        ":not-proven",
        ":not-invoked",
        ":not-emitted",
    ];

    public static CalibrationUncertaintyFeatureReport Project(
        EstimateReport estimate,
        RepositoryEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        ArgumentNullException.ThrowIfNull(evidence);
        CalibrationUncertaintyInputValidator.Validate(estimate, evidence);

        CalibrationUncertaintyFeatureContract contract =
            CalibrationUncertaintyFeatureCatalog.Current;
        Dictionary<string, EvidenceFact> facts = evidence.Facts.ToDictionary(
            fact => fact.Id,
            StringComparer.Ordinal);
        CalibrationUncertaintyWorkItemFeatures[] workItems =
        [
            .. estimate.WorkItems
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => ProjectWorkItem(item, facts, contract)),
        ];
        HashSet<string> widthDriverIds =
        [
            .. contract.Features
                .Where(feature => feature.Monotonicity !=
                    CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly)
                .Select(feature => feature.Id),
        ];

        return new CalibrationUncertaintyFeatureReport
        {
            ProjectorVersion = CalibrationUncertaintyFeatureCatalog.ProjectorVersion,
            FeatureContract = contract,
            FeatureContractDigest = CalibrationDigest.Compute(contract),
            EstimateDigest = CalibrationDigest.Compute(estimate),
            EvidenceDigest = CalibrationDigest.Compute(evidence),
            RepositorySourceDigest = estimate.Repository.SourceDigest!,
            Ecosystems =
            [
                .. evidence.Repository.Ecosystems
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
            ],
            EstimatorVersion = estimate.EstimatorVersion,
            Profile = estimate.Profile,
            BaselineId = estimate.Baseline.Id,
            Summary = BuildSummary(workItems, contract, widthDriverIds),
            WorkItems = workItems,
        };
    }

    private static CalibrationUncertaintyWorkItemFeatures ProjectWorkItem(
        WorkItem item,
        Dictionary<string, EvidenceFact> facts,
        CalibrationUncertaintyFeatureContract contract)
    {
        string[] requestedIds =
        [
            .. item.EvidenceIds
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        EvidenceFact[] resolved =
        [
            .. requestedIds
                .Where(facts.ContainsKey)
                .Select(id => facts[id]),
        ];
        string[] unresolved = [.. requestedIds.Where(id => !facts.ContainsKey(id))];
        CalibrationUncertaintyFeatureValue[] values =
        [
            .. contract.Features.Select(definition =>
                Extract(definition, item, resolved, unresolved)),
        ];

        return new CalibrationUncertaintyWorkItemFeatures
        {
            WorkItemId = item.Id,
            Category = item.Category,
            SourceComplexity = item.Complexity,
            Ecosystems =
            [
                .. resolved
                    .SelectMany(fact => fact.Tags)
                    .Where(tag => tag.StartsWith("ecosystem:", StringComparison.Ordinal))
                    .Select(tag => tag[10..])
                    .Where(value => value.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
            ],
            ExpectedHours = item.Hours.Expected,
            SourceRange = item.Hours,
            SourceRangePolicyCompliant =
                CalibrationUncertaintyIntervalRules.IsPolicyCompliant(item.Hours),
            ParentId = item.ParentId,
            CorrelationGroup = item.CorrelationGroup,
            ResolvedEvidenceIds = [.. resolved.Select(fact => fact.Id)],
            UnresolvedEvidenceIds = unresolved,
            Features = values,
        };
    }

    private static CalibrationUncertaintyFeatureValue Extract(
        CalibrationUncertaintyFeatureDefinition definition,
        WorkItem item,
        EvidenceFact[] facts,
        string[] unresolved)
    {
        bool incomplete = unresolved.Length > 0;
        return definition.Id switch
        {
            CalibrationUncertaintyFeatureIds.SourceConfidence =>
                Available(definition.Id, item.Confidence),
            CalibrationUncertaintyFeatureIds.InferredFactShare =>
                ExtractInferredShare(definition.Id, facts, incomplete),
            CalibrationUncertaintyFeatureIds.ParserRisk =>
                ExtractParserRisk(definition.Id, facts, incomplete),
            CalibrationUncertaintyFeatureIds.ExplicitUncertaintyCount =>
                Available(
                    definition.Id,
                    item.UncertaintyReasons.Distinct(StringComparer.Ordinal).Count()),
            CalibrationUncertaintyFeatureIds.MaterialUnresolvedCount =>
                ExtractMaterialUnresolved(definition.Id, item, facts, unresolved),
            CalibrationUncertaintyFeatureIds.NonMaterialOfflineLimitationCount =>
                ExtractFactCount(
                    definition.Id,
                    facts,
                    incomplete,
                    fact => fact.Tags.Any(IsNonMaterialOfflineLimitation)),
            CalibrationUncertaintyFeatureIds.DynamicBoundaryCount =>
                ExtractFactCount(
                    definition.Id,
                    facts,
                    incomplete,
                    HasDynamicBoundary),
            CalibrationUncertaintyFeatureIds.UnsupportedBoundaryCount =>
                ExtractFactCount(
                    definition.Id,
                    facts,
                    incomplete,
                    HasUnsupportedBoundary),
            CalibrationUncertaintyFeatureIds.ResolvedFactCount =>
                Available(definition.Id, facts.Length, facts.Select(fact => fact.Id)),
            CalibrationUncertaintyFeatureIds.AggregateBranchDensity =>
                ExtractBranchDensity(definition.Id, facts, incomplete),
            CalibrationUncertaintyFeatureIds.AggregatePublicInterfaceConcentration =>
                ExtractPublicInterfaceConcentration(definition.Id, facts, incomplete),
            _ => Unavailable(definition.Id, "feature-extractor-not-implemented"),
        };
    }

    private static CalibrationUncertaintyFeatureValue ExtractInferredShare(
        string id,
        EvidenceFact[] facts,
        bool incomplete)
    {
        if (incomplete)
        {
            return Unavailable(id, "unresolved-supporting-evidence", facts.Select(fact => fact.Id));
        }

        if (facts.Length == 0)
        {
            return NotApplicable(id, "no-resolved-supporting-evidence");
        }

        decimal inferred = facts.Count(fact =>
            fact.Provenance.SourceKind == EvidenceSourceKind.Inferred);
        return Available(id, Round6(inferred / facts.Length), facts.Select(fact => fact.Id));
    }

    private static CalibrationUncertaintyFeatureValue ExtractParserRisk(
        string id,
        EvidenceFact[] facts,
        bool incomplete)
    {
        EvidenceFact[] parserFacts = [.. facts.Where(fact => ParserRisk(fact) is not null)];
        if (incomplete)
        {
            return Unavailable(id, "unresolved-supporting-evidence", parserFacts.Select(fact => fact.Id));
        }

        return parserFacts.Length == 0
            ? NotApplicable(id, "no-parser-signal")
            : Available(
                id,
                parserFacts.Max(fact => ParserRisk(fact)!.Value),
                parserFacts.Select(fact => fact.Id));
    }

    private static CalibrationUncertaintyFeatureValue ExtractMaterialUnresolved(
        string id,
        WorkItem item,
        EvidenceFact[] facts,
        string[] unresolved)
    {
        EvidenceFact[] materialFacts =
        [
            .. facts.Where(fact => fact.Tags.Any(tag =>
                tag == CalibrationUncertaintyFeatureCatalog.MaterialAccessGapTag ||
                tag.StartsWith(
                    CalibrationUncertaintyFeatureCatalog.MaterialAccessGapTag + ":",
                    StringComparison.Ordinal))),
        ];
        int evidenceLess = item.EvidenceIds.Count == 0 ? 1 : 0;
        return Available(
            id,
            evidenceLess + unresolved.Length + materialFacts.Length,
            unresolved.Concat(materialFacts.Select(fact => fact.Id)));
    }

    private static CalibrationUncertaintyFeatureValue ExtractFactCount(
        string id,
        EvidenceFact[] facts,
        bool incomplete,
        Func<EvidenceFact, bool> predicate)
    {
        EvidenceFact[] matching = [.. facts.Where(predicate)];
        return incomplete
            ? Unavailable(id, "unresolved-supporting-evidence", matching.Select(fact => fact.Id))
            : Available(id, matching.Length, matching.Select(fact => fact.Id));
    }

    private static CalibrationUncertaintyFeatureValue ExtractBranchDensity(
        string id,
        EvidenceFact[] facts,
        bool incomplete)
    {
        EvidenceFact[] contributing = [.. facts.Where(fact => fact.Measurements.Any(measurement =>
            measurement.Name is "branch-points" or "functions" or "methods"))];
        if (incomplete)
        {
            return Unavailable(id, "unresolved-supporting-evidence", contributing.Select(fact => fact.Id));
        }

        decimal branches = SumMeasurements(contributing, "branch-points");
        decimal callables = SumMeasurements(contributing, "functions", "methods");
        return callables == 0m
            ? NotApplicable(id, "no-callable-structure-measurement")
            : Available(id, Round6(branches / callables), contributing.Select(fact => fact.Id));
    }

    private static CalibrationUncertaintyFeatureValue ExtractPublicInterfaceConcentration(
        string id,
        EvidenceFact[] facts,
        bool incomplete)
    {
        string[] publicNames = ["public-types", "public-methods", "public-symbols", "exports"];
        string[] declarationNames =
        [
            "types", "methods", "functions", "classes", "interfaces", "type-aliases", "enums",
        ];
        EvidenceFact[] contributing = [.. facts.Where(fact => fact.Measurements.Any(measurement =>
            publicNames.Contains(measurement.Name, StringComparer.Ordinal) ||
            declarationNames.Contains(measurement.Name, StringComparer.Ordinal)))];
        if (incomplete)
        {
            return Unavailable(id, "unresolved-supporting-evidence", contributing.Select(fact => fact.Id));
        }

        decimal declarations = SumMeasurements(contributing, declarationNames);
        if (declarations == 0m)
        {
            return NotApplicable(id, "no-declaration-structure-measurement");
        }

        decimal publicDeclarations = SumMeasurements(contributing, publicNames);
        return Available(
            id,
            Round6(decimal.Min(1m, publicDeclarations / declarations)),
            contributing.Select(fact => fact.Id));
    }

}
