using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class CalibrationUncertaintyGraphFeatureProjector
{
    public static CalibrationUncertaintyGraphFeatureReport Project(
        EstimateReport estimate,
        RepositoryEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        ArgumentNullException.ThrowIfNull(evidence);
        CalibrationUncertaintyInputValidator.Validate(estimate, evidence);

        CalibrationUncertaintyGraphProjection graph =
            CalibrationUncertaintyGraphProjectionBuilder.Build(evidence);
        CalibrationUncertaintyFeatureContract contract =
            CalibrationUncertaintyGraphFeatureCatalog.Current;
        Dictionary<string, EvidenceFact> facts = evidence.Facts.ToDictionary(
            fact => fact.Id,
            StringComparer.Ordinal);
        CalibrationUncertaintyGraphWorkItemMapping[] workItems =
        [
            .. estimate.WorkItems
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => MapWorkItem(item, facts, graph)),
        ];
        CalibrationUncertaintyFeatureValue[] features = BuildFeatures(graph, contract);

        return new CalibrationUncertaintyGraphFeatureReport
        {
            ProjectorVersion = CalibrationUncertaintyGraphFeatureCatalog.ProjectorVersion,
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
            Summary = BuildSummary(graph, workItems, features.Length),
            Features = features,
            Nodes = graph.Nodes,
            Edges = graph.Edges,
            WorkItems = workItems,
        };
    }

    private static CalibrationUncertaintyGraphWorkItemMapping MapWorkItem(
        WorkItem item,
        Dictionary<string, EvidenceFact> facts,
        CalibrationUncertaintyGraphProjection graph)
    {
        string[] requested =
        [
            .. item.EvidenceIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
        ];
        EvidenceFact[] resolved =
        [
            .. requested.Where(facts.ContainsKey).Select(id => facts[id]),
        ];
        return new CalibrationUncertaintyGraphWorkItemMapping
        {
            WorkItemId = item.Id,
            Category = item.Category,
            SourceComplexity = item.Complexity,
            ExpectedHours = item.Hours.Expected,
            SourceRange = item.Hours,
            ParentId = item.ParentId,
            CorrelationGroup = item.CorrelationGroup,
            ResolvedEvidenceIds = [.. resolved.Select(fact => fact.Id)],
            UnresolvedEvidenceIds = [.. requested.Where(id => !facts.ContainsKey(id))],
            NodeIds =
            [
                .. resolved
                    .SelectMany(graph.ResolveNodeIds)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
            ],
        };
    }

    private static CalibrationUncertaintyFeatureValue[] BuildFeatures(
        CalibrationUncertaintyGraphProjection graph,
        CalibrationUncertaintyFeatureContract contract)
    {
        int[] fanIn = [.. graph.Nodes.Select(node => node.FanIn)];
        int[] fanOut = [.. graph.Nodes.Select(node => node.FanOut)];
        decimal[] publicInterfaces =
        [
            .. graph.Nodes
                .Where(node => node.PublicInterfaceAvailability ==
                    CalibrationUncertaintyFeatureAvailability.Available)
                .Select(node => node.PublicInterfaceConcentration!.Value),
        ];
        string[] graphEvidence =
        [
            .. graph.Nodes.Select(node => node.NodeId)
                .Concat(graph.Edges.SelectMany(edge => edge.EvidenceIds))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        string[] interfaceEvidence =
        [
            .. graph.Nodes.SelectMany(node => node.PublicInterfaceEvidenceIds)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        bool interfaceUnavailable = graph.Nodes.Any(node =>
            node.PublicInterfaceAvailability ==
            CalibrationUncertaintyFeatureAvailability.Unavailable);

        return
        [
            .. contract.Features.Select(definition => definition.Id switch
            {
                CalibrationUncertaintyGraphFeatureIds.FanInP50 =>
                    Degree(definition.Id, fanIn, 0.50m, graphEvidence),
                CalibrationUncertaintyGraphFeatureIds.FanInP90 =>
                    Degree(definition.Id, fanIn, 0.90m, graphEvidence),
                CalibrationUncertaintyGraphFeatureIds.FanInMaximum =>
                    DegreeMaximum(definition.Id, fanIn, graphEvidence),
                CalibrationUncertaintyGraphFeatureIds.HighFanInShare =>
                    DegreeShare(definition.Id, fanIn, graphEvidence),
                CalibrationUncertaintyGraphFeatureIds.FanOutP50 =>
                    Degree(definition.Id, fanOut, 0.50m, graphEvidence),
                CalibrationUncertaintyGraphFeatureIds.FanOutP90 =>
                    Degree(definition.Id, fanOut, 0.90m, graphEvidence),
                CalibrationUncertaintyGraphFeatureIds.FanOutMaximum =>
                    DegreeMaximum(definition.Id, fanOut, graphEvidence),
                CalibrationUncertaintyGraphFeatureIds.HighFanOutShare =>
                    DegreeShare(definition.Id, fanOut, graphEvidence),
                CalibrationUncertaintyGraphFeatureIds.CyclicNodeShare =>
                    GraphRatio(
                        definition.Id,
                        graph,
                        graph.Nodes.Count(node => node.Cyclic),
                        graphEvidence),
                CalibrationUncertaintyGraphFeatureIds.LargestCyclicComponentShare =>
                    GraphRatio(
                        definition.Id,
                        graph,
                        graph.Nodes.Count == 0
                            ? 0
                            : graph.Nodes.Max(node => node.CyclicComponentSize),
                        graphEvidence),
                CalibrationUncertaintyGraphFeatureIds.PublicInterfaceP50 =>
                    InterfacePercentile(
                        definition.Id,
                        publicInterfaces,
                        0.50m,
                        interfaceUnavailable,
                        interfaceEvidence),
                CalibrationUncertaintyGraphFeatureIds.PublicInterfaceP90 =>
                    InterfacePercentile(
                        definition.Id,
                        publicInterfaces,
                        0.90m,
                        interfaceUnavailable,
                        interfaceEvidence),
                CalibrationUncertaintyGraphFeatureIds.PublicInterfaceMaximum =>
                    InterfaceMaximum(
                        definition.Id,
                        publicInterfaces,
                        interfaceUnavailable,
                        interfaceEvidence),
                CalibrationUncertaintyGraphFeatureIds.HighPublicInterfaceShare =>
                    InterfaceShare(
                        definition.Id,
                        publicInterfaces,
                        interfaceUnavailable,
                        interfaceEvidence),
                _ => Unavailable(definition.Id, "feature-extractor-not-implemented", []),
            }),
        ];
    }

    private static CalibrationUncertaintyFeatureValue Degree(
        string id,
        int[] values,
        decimal quantile,
        string[] evidenceIds) => values.Length == 0
        ? NotApplicable(id, "no-supported-project-or-package-nodes")
        : Available(id, NearestRank(values, quantile), evidenceIds);

    private static CalibrationUncertaintyFeatureValue DegreeMaximum(
        string id,
        int[] values,
        string[] evidenceIds) => values.Length == 0
        ? NotApplicable(id, "no-supported-project-or-package-nodes")
        : Available(id, values.Max(), evidenceIds);

    private static CalibrationUncertaintyFeatureValue DegreeShare(
        string id,
        int[] values,
        string[] evidenceIds) => values.Length == 0
        ? NotApplicable(id, "no-supported-project-or-package-nodes")
        : Available(
            id,
            Round6((decimal)values.Count(value => value >
                CalibrationUncertaintyGraphFeatureCatalog.HighFanDegreeThreshold) / values.Length),
            evidenceIds);

    private static CalibrationUncertaintyFeatureValue GraphRatio(
        string id,
        CalibrationUncertaintyGraphProjection graph,
        int numerator,
        string[] evidenceIds) => graph.Nodes.Count == 0
        ? NotApplicable(id, "no-supported-project-or-package-nodes")
        : Available(id, Round6((decimal)numerator / graph.Nodes.Count), evidenceIds);

    private static CalibrationUncertaintyFeatureValue InterfacePercentile(
        string id,
        decimal[] values,
        decimal quantile,
        bool unavailable,
        string[] evidenceIds) => Interface(
            id,
            values,
            unavailable,
            evidenceIds,
            candidates => NearestRank(candidates, quantile));

    private static CalibrationUncertaintyFeatureValue InterfaceMaximum(
        string id,
        decimal[] values,
        bool unavailable,
        string[] evidenceIds) => Interface(
            id,
            values,
            unavailable,
            evidenceIds,
            candidates => candidates.Max());

    private static CalibrationUncertaintyFeatureValue InterfaceShare(
        string id,
        decimal[] values,
        bool unavailable,
        string[] evidenceIds) => Interface(
            id,
            values,
            unavailable,
            evidenceIds,
            candidates => Round6((decimal)candidates.Count(value => value >
                CalibrationUncertaintyGraphFeatureCatalog.HighPublicInterfaceThreshold) /
                candidates.Length));

    private static CalibrationUncertaintyFeatureValue Interface(
        string id,
        decimal[] values,
        bool unavailable,
        string[] evidenceIds,
        Func<decimal[], decimal> measure)
    {
        if (unavailable)
        {
            return Unavailable(id, "incompatible-source-structure-evidence", evidenceIds);
        }

        return values.Length == 0
            ? NotApplicable(id, "no-local-public-interface-measurements", evidenceIds)
            : Available(id, measure(values), evidenceIds);
    }

    private static decimal NearestRank(int[] values, decimal quantile) =>
        NearestRank([.. values.Select(value => (decimal)value)], quantile);

    private static decimal NearestRank(decimal[] values, decimal quantile)
    {
        decimal[] sorted = [.. values.Order()];
        int rank = (int)decimal.Ceiling(quantile * sorted.Length);
        return sorted[Math.Max(0, rank - 1)];
    }

    private static CalibrationUncertaintyGraphFeatureSummary BuildSummary(
        CalibrationUncertaintyGraphProjection graph,
        CalibrationUncertaintyGraphWorkItemMapping[] workItems,
        int featureCount) => new()
        {
            FeatureCount = featureCount,
            NodeCount = graph.Nodes.Count,
            EdgeCount = graph.Edges.Count,
            CandidateReferenceFactCount = graph.CandidateReferenceFactCount,
            ResolvedLocalReferenceFactCount = graph.ResolvedLocalReferenceFactCount,
            CyclicNodeCount = graph.Nodes.Count(node => node.Cyclic),
            PublicInterfaceAvailableNodeCount = CountInterface(
                graph,
                CalibrationUncertaintyFeatureAvailability.Available),
            PublicInterfaceNotApplicableNodeCount = CountInterface(
                graph,
                CalibrationUncertaintyFeatureAvailability.NotApplicable),
            PublicInterfaceUnavailableNodeCount = CountInterface(
                graph,
                CalibrationUncertaintyFeatureAvailability.Unavailable),
            WorkItemCount = workItems.Length,
            MappedWorkItemCount = workItems.Count(item => item.NodeIds.Count > 0),
            UnmappedWorkItemCount = workItems.Count(item => item.NodeIds.Count == 0),
            ResolvedEvidenceReferenceCount = workItems.Sum(item => item.ResolvedEvidenceIds.Count),
            UnresolvedEvidenceReferenceCount = workItems.Sum(item =>
                item.UnresolvedEvidenceIds.Count),
        };

    private static int CountInterface(
        CalibrationUncertaintyGraphProjection graph,
        CalibrationUncertaintyFeatureAvailability availability) =>
        graph.Nodes.Count(node => node.PublicInterfaceAvailability == availability);

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
        string reason,
        IEnumerable<string>? evidenceIds = null) => new()
        {
            FeatureId = id,
            Availability = CalibrationUncertaintyFeatureAvailability.NotApplicable,
            ReasonCode = reason,
            EvidenceIds = OrderIds(evidenceIds ?? []),
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

    private static decimal Round6(decimal value) =>
        decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}
