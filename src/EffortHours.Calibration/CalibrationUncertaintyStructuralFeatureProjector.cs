using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class CalibrationUncertaintyStructuralFeatureProjector
{
    public static CalibrationUncertaintyStructuralFeatureReport Project(
        EstimateReport estimate,
        RepositoryEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        ArgumentNullException.ThrowIfNull(evidence);
        CalibrationUncertaintyInputValidator.Validate(estimate, evidence);

        CalibrationUncertaintyFeatureContract contract =
            CalibrationUncertaintyStructuralFeatureCatalog.Current;
        Dictionary<string, EvidenceFact> facts = evidence.Facts.ToDictionary(
            fact => fact.Id,
            StringComparer.Ordinal);
        IReadOnlyDictionary<string, CalibrationUncertaintyStructuralFact> structuralFacts =
            CalibrationUncertaintyStructuralFact.Parse(evidence.Facts);
        CalibrationUncertaintyStructuralWorkItemFeatures[] workItems =
        [
            .. estimate.WorkItems
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => ProjectWorkItem(item, facts, structuralFacts, contract)),
        ];

        return new CalibrationUncertaintyStructuralFeatureReport
        {
            ProjectorVersion = CalibrationUncertaintyStructuralFeatureCatalog.ProjectorVersion,
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
            Summary = BuildSummary(workItems, contract.Features.Count),
            WorkItems = workItems,
        };
    }

    private static CalibrationUncertaintyStructuralWorkItemFeatures ProjectWorkItem(
        WorkItem item,
        Dictionary<string, EvidenceFact> factIndex,
        IReadOnlyDictionary<string, CalibrationUncertaintyStructuralFact> structuralIndex,
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
            .. requestedIds.Where(factIndex.ContainsKey).Select(id => factIndex[id]),
        ];
        string[] unresolved = [.. requestedIds.Where(id => !factIndex.ContainsKey(id))];
        EvidenceFact[] sourceFacts =
        [
            .. resolved.Where(fact => fact.Kind == EvidenceKinds.SourceStructure),
        ];
        CalibrationUncertaintyStructuralFact[] compatible =
        [
            .. sourceFacts
                .Where(fact => structuralIndex.ContainsKey(fact.Id))
                .Select(fact => structuralIndex[fact.Id]),
        ];
        string[] incompatible =
        [
            .. sourceFacts
                .Where(fact => !structuralIndex.ContainsKey(fact.Id))
                .Select(fact => fact.Id)
                .Order(StringComparer.Ordinal),
        ];
        CalibrationUncertaintyStructuralCoverageStatus status = CoverageStatus(
            sourceFacts,
            compatible,
            incompatible,
            unresolved);

        return new CalibrationUncertaintyStructuralWorkItemFeatures
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
            ParentId = item.ParentId,
            CorrelationGroup = item.CorrelationGroup,
            CoverageStatus = status,
            ResolvedEvidenceIds = [.. resolved.Select(fact => fact.Id)],
            UnresolvedEvidenceIds = unresolved,
            StructuralEvidenceIds = [.. compatible.Select(value => value.Fact.Id)],
            IncompatibleStructuralEvidenceIds = incompatible,
            Features =
            [
                .. contract.Features.Select(definition => Extract(
                    definition,
                    sourceFacts,
                    compatible,
                    incompatible,
                    unresolved)),
            ],
        };
    }

    private static CalibrationUncertaintyFeatureValue Extract(
        CalibrationUncertaintyFeatureDefinition definition,
        EvidenceFact[] sourceFacts,
        CalibrationUncertaintyStructuralFact[] compatible,
        string[] incompatible,
        string[] unresolved)
    {
        if (unresolved.Length > 0)
        {
            return Unavailable(definition.Id, "unresolved-supporting-evidence", unresolved);
        }

        if (sourceFacts.Length == 0)
        {
            return NotApplicable(definition.Id, "no-source-structure-evidence");
        }

        if (incompatible.Length > 0)
        {
            return Unavailable(
                definition.Id,
                "structural-evidence-contract-unavailable",
                incompatible);
        }

        int detected = compatible.Sum(value => value.DetectedCallables);
        if (detected == 0 && definition.Id !=
            CalibrationUncertaintyStructuralFeatureIds.AnalyzerAmbiguityConcentration)
        {
            return NotApplicable(definition.Id, "no-detected-callables");
        }

        string measurementName =
            CalibrationUncertaintyStructuralFeatureCatalog.MeasurementName(definition.Id);
        CalibrationUncertaintyStructuralFact[] candidates =
        [
            .. compatible.Where(value =>
                value.FeatureMeasurements.ContainsKey(measurementName)),
        ];
        if (candidates.Length == 0 ||
            (IsDistribution(definition.Id) && compatible.Any(value =>
                value.DetectedCallables > 0 && value.MeasuredCallables == 0)))
        {
            return Unavailable(
                definition.Id,
                "no-parser-backed-callable-samples",
                compatible.Select(value => value.Fact.Id));
        }

        bool minimum = CalibrationUncertaintyStructuralFeatureCatalog.SelectMinimum(
            definition.Id);
        decimal selected = minimum
            ? candidates.Min(value => value.FeatureMeasurements[measurementName])
            : candidates.Max(value => value.FeatureMeasurements[measurementName]);
        return Available(
            definition.Id,
            selected,
            candidates.Select(value => value.Fact.Id));
    }

    private static CalibrationUncertaintyStructuralCoverageStatus CoverageStatus(
        EvidenceFact[] sourceFacts,
        CalibrationUncertaintyStructuralFact[] compatible,
        string[] incompatible,
        string[] unresolved)
    {
        if (unresolved.Length > 0 || incompatible.Length > 0)
        {
            return CalibrationUncertaintyStructuralCoverageStatus.Unavailable;
        }

        if (sourceFacts.Length == 0 || compatible.Sum(value => value.DetectedCallables) == 0)
        {
            return CalibrationUncertaintyStructuralCoverageStatus.NotApplicable;
        }

        if (compatible.Sum(value => value.MeasuredCallables) == 0)
        {
            return CalibrationUncertaintyStructuralCoverageStatus.Unavailable;
        }

        return compatible.All(value => value.MeasuredCallables == value.DetectedCallables &&
                value.ParserBackedFiles == value.SourceFiles)
            ? CalibrationUncertaintyStructuralCoverageStatus.Complete
            : CalibrationUncertaintyStructuralCoverageStatus.Partial;
    }

    private static CalibrationUncertaintyStructuralFeatureSummary BuildSummary(
        CalibrationUncertaintyStructuralWorkItemFeatures[] workItems,
        int featureCount) => new()
        {
            WorkItemCount = workItems.Length,
            FeatureCount = featureCount,
            CompleteWorkItemCount = CountStatus(
                workItems,
                CalibrationUncertaintyStructuralCoverageStatus.Complete),
            PartialWorkItemCount = CountStatus(
                workItems,
                CalibrationUncertaintyStructuralCoverageStatus.Partial),
            NotApplicableWorkItemCount = CountStatus(
                workItems,
                CalibrationUncertaintyStructuralCoverageStatus.NotApplicable),
            UnavailableWorkItemCount = CountStatus(
                workItems,
                CalibrationUncertaintyStructuralCoverageStatus.Unavailable),
            ResolvedEvidenceReferenceCount = workItems.Sum(item => item.ResolvedEvidenceIds.Count),
            UnresolvedEvidenceReferenceCount = workItems.Sum(item => item.UnresolvedEvidenceIds.Count),
            StructuralEvidenceReferenceCount = workItems.Sum(item => item.StructuralEvidenceIds.Count),
            IncompatibleStructuralEvidenceReferenceCount = workItems.Sum(item =>
                item.IncompatibleStructuralEvidenceIds.Count),
        };

    private static int CountStatus(
        IEnumerable<CalibrationUncertaintyStructuralWorkItemFeatures> workItems,
        CalibrationUncertaintyStructuralCoverageStatus status) =>
        workItems.Count(item => item.CoverageStatus == status);

    private static bool IsDistribution(string featureId) => featureId is not
        CalibrationUncertaintyStructuralFeatureIds.CallableMeasurementCoverage and not
        CalibrationUncertaintyStructuralFeatureIds.AnalyzerAmbiguityConcentration;

    private static CalibrationUncertaintyFeatureValue Available(
        string id,
        decimal value,
        IEnumerable<string> evidenceIds) => new()
        {
            FeatureId = id,
            Availability = CalibrationUncertaintyFeatureAvailability.Available,
            Value = value,
            EvidenceIds = OrderIds(evidenceIds),
        };

    private static CalibrationUncertaintyFeatureValue NotApplicable(
        string id,
        string reason) => new()
        {
            FeatureId = id,
            Availability = CalibrationUncertaintyFeatureAvailability.NotApplicable,
            ReasonCode = reason,
        };

    private static CalibrationUncertaintyFeatureValue Unavailable(
        string id,
        string reason,
        IEnumerable<string> evidenceIds) => new()
        {
            FeatureId = id,
            Availability = CalibrationUncertaintyFeatureAvailability.Unavailable,
            ReasonCode = reason,
            EvidenceIds = OrderIds(evidenceIds),
        };

    private static string[] OrderIds(IEnumerable<string> values) =>
        [.. values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
