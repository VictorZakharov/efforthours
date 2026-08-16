using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(CalibrationUncertaintyGraphFeatureReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "calibration uncertainty graph report", errors);
        if (report.ProjectorVersion != CalibrationUncertaintyVersions.GraphProjectorV1)
        {
            errors.Add($"Unsupported uncertainty graph projector '{report.ProjectorVersion}'.");
        }

        RequireDigest(report.FeatureContractDigest, "featureContractDigest", errors);
        if (report.FeatureContractDigest !=
            CalibrationUncertaintyVersions.GraphFeatureContractDigestV1)
        {
            errors.Add("The uncertainty graph report does not pin the canonical v1 contract.");
        }

        RequireDigest(report.EstimateDigest, "estimateDigest", errors);
        RequireDigest(report.EvidenceDigest, "evidenceDigest", errors);
        RequireDigest(report.RepositorySourceDigest, "repositorySourceDigest", errors);
        RequireText(report.EstimatorVersion, "estimatorVersion", errors);
        RequireText(report.BaselineId, "baselineId", errors);
        RequireUniqueText(report.Ecosystems, "ecosystems", errors);
        ValidateGraphFeatureContract(report.FeatureContract, errors);
        ValidateGraphNodesAndEdges(report, errors);
        ValidateGraphFeatures(report, errors);
        ValidateGraphWorkItems(report, errors);
        ValidateGraphSummary(report, errors);
        return errors;
    }

    private static void ValidateGraphFeatureContract(
        CalibrationUncertaintyFeatureContract contract,
        List<string> errors)
    {
        if (contract.Version != CalibrationUncertaintyVersions.GraphFeatureContractV1)
        {
            errors.Add($"Unsupported uncertainty graph contract '{contract.Version}'.");
        }

        RequireText(contract.EffectiveDate, "featureContract.effectiveDate", errors);
        if (!DateOnly.TryParseExact(
                contract.EffectiveDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            errors.Add("featureContract.effectiveDate must use yyyy-MM-dd.");
        }

        if (!contract.LabelIndependent)
        {
            errors.Add("The uncertainty graph contract must be label-independent.");
        }

        ValidateUncertaintyPolicy(contract.IntervalPolicy, errors);
        if (contract.Features.Count == 0)
        {
            errors.Add("The uncertainty graph contract must contain offline features.");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (CalibrationUncertaintyFeatureDefinition feature in contract.Features)
        {
            ValidateUncertaintyFeatureDefinition(feature, "feature", errors);
            if (feature.Stage != CalibrationUncertaintyFeatureStage.AvailableOffline ||
                feature.Monotonicity != CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly)
            {
                errors.Add(
                    $"Graph feature '{feature.Id}' must be available offline and diagnostic-only.");
            }

            if (feature.ValueKind is not CalibrationUncertaintyFeatureValueKind.Count and not
                CalibrationUncertaintyFeatureValueKind.Ratio)
            {
                errors.Add($"Graph feature '{feature.Id}' must be a scalar count or ratio.");
            }

            if (!ids.Add(feature.Id))
            {
                errors.Add($"Uncertainty graph feature ID '{feature.Id}' is duplicated.");
            }
        }

        foreach (CalibrationUncertaintyFeatureDefinition feature in contract.DeferredCandidates)
        {
            ValidateUncertaintyFeatureDefinition(feature, "deferredCandidate", errors);
            if (!ids.Add(feature.Id))
            {
                errors.Add($"Uncertainty graph feature ID '{feature.Id}' is duplicated.");
            }
        }
    }

    private static void ValidateGraphNodesAndEdges(
        CalibrationUncertaintyGraphFeatureReport report,
        List<string> errors)
    {
        HashSet<string> nodeIds = new(StringComparer.Ordinal);
        foreach (CalibrationUncertaintyGraphNode node in report.Nodes)
        {
            string path = $"node[{node.NodeId}]";
            RequireText(node.NodeId, "node.id", errors);
            if (!nodeIds.Add(node.NodeId))
            {
                errors.Add($"Uncertainty graph node ID '{node.NodeId}' is duplicated.");
            }

            if (node.Ecosystem is not "dotnet" and not "javascript")
            {
                errors.Add($"{path}.ecosystem must be dotnet or javascript.");
            }

            if (node.FanIn < 0 || node.FanOut < 0 || node.CyclicComponentSize < 0)
            {
                errors.Add($"{path} graph counts cannot be negative.");
            }

            if (node.Cyclic != (node.CyclicComponentSize > 0) ||
                node.CyclicComponentSize > report.Nodes.Count)
            {
                errors.Add($"{path} cyclic membership and component size are inconsistent.");
            }

            decimal expectedShare = report.Nodes.Count == 0
                ? 0m
                : RoundGraph((decimal)node.CyclicComponentSize / report.Nodes.Count);
            if (node.CyclicComponentNodeShare != expectedShare)
            {
                errors.Add($"{path}.cyclicComponentNodeShare does not reconcile.");
            }

            RequireUniqueText(
                node.PublicInterfaceEvidenceIds,
                $"{path}.publicInterfaceEvidenceIds",
                errors);
            ValidateGraphInterface(node, path, errors);
        }

        HashSet<string> edgeKeys = new(StringComparer.Ordinal);
        Dictionary<string, int> fanIn = nodeIds.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        Dictionary<string, int> fanOut = nodeIds.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        foreach (CalibrationUncertaintyGraphEdge edge in report.Edges)
        {
            string key = $"{edge.SourceNodeId}\n{edge.TargetNodeId}";
            RequireText(edge.SourceNodeId, "edge.sourceNodeId", errors);
            RequireText(edge.TargetNodeId, "edge.targetNodeId", errors);
            RequireUniqueText(edge.EvidenceIds, $"edge[{key}].evidenceIds", errors);
            if (edge.EvidenceIds.Count == 0)
            {
                errors.Add($"edge[{key}].evidenceIds cannot be empty.");
            }

            if (!edgeKeys.Add(key))
            {
                errors.Add($"Uncertainty graph edge '{key}' is duplicated.");
            }

            if (!nodeIds.Contains(edge.SourceNodeId) || !nodeIds.Contains(edge.TargetNodeId))
            {
                errors.Add($"Uncertainty graph edge '{key}' references an unknown node.");
                continue;
            }

            fanOut[edge.SourceNodeId]++;
            fanIn[edge.TargetNodeId]++;
        }

        foreach (CalibrationUncertaintyGraphNode node in report.Nodes)
        {
            if (node.FanIn != fanIn[node.NodeId] || node.FanOut != fanOut[node.NodeId])
            {
                errors.Add($"node[{node.NodeId}] fan-in/fan-out does not reconcile to edges.");
            }
        }
    }

    private static void ValidateGraphInterface(
        CalibrationUncertaintyGraphNode node,
        string path,
        List<string> errors)
    {
        if (node.PublicInterfaceAvailability == CalibrationUncertaintyFeatureAvailability.Available)
        {
            if (node.PublicInterfaceConcentration is null or < 0m or > 1m)
            {
                errors.Add($"{path}.publicInterfaceConcentration must be a ratio when available.");
            }

            if (node.PublicInterfaceReasonCode is not null ||
                node.PublicInterfaceEvidenceIds.Count == 0)
            {
                errors.Add($"{path} available interface evidence needs IDs and no reason code.");
            }
        }
        else
        {
            if (node.PublicInterfaceConcentration is not null)
            {
                errors.Add($"{path}.publicInterfaceConcentration must be omitted when unavailable.");
            }

            RequireText(node.PublicInterfaceReasonCode, $"{path}.publicInterfaceReasonCode", errors);
        }
    }

    private static void ValidateGraphFeatures(
        CalibrationUncertaintyGraphFeatureReport report,
        List<string> errors)
    {
        string[] expected = [.. report.FeatureContract.Features.Select(feature => feature.Id)];
        string[] actual = [.. report.Features.Select(feature => feature.FeatureId)];
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            errors.Add("Uncertainty graph features must match feature-contract order exactly.");
        }

        HashSet<string> allowedEvidenceIds =
        [
            .. report.Nodes.Select(node => node.NodeId),
            .. report.Nodes.SelectMany(node => node.PublicInterfaceEvidenceIds),
            .. report.Edges.SelectMany(edge => edge.EvidenceIds),
        ];
        foreach ((CalibrationUncertaintyFeatureValue value, int index) in
                 report.Features.Select((value, index) => (value, index)))
        {
            CalibrationUncertaintyFeatureDefinition? definition =
                index < report.FeatureContract.Features.Count
                    ? report.FeatureContract.Features[index]
                    : null;
            ValidateUncertaintyFeatureValue(
                value,
                definition,
                allowedEvidenceIds,
                "repositoryGraph",
                errors);
        }
    }

    private static void ValidateGraphWorkItems(
        CalibrationUncertaintyGraphFeatureReport report,
        List<string> errors)
    {
        HashSet<string> nodeIds = report.Nodes.Select(node => node.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> workItemIds = new(StringComparer.Ordinal);
        foreach (CalibrationUncertaintyGraphWorkItemMapping item in report.WorkItems)
        {
            string path = $"workItem[{item.WorkItemId}]";
            RequireText(item.WorkItemId, "workItem.id", errors);
            if (!workItemIds.Add(item.WorkItemId))
            {
                errors.Add($"Uncertainty graph work-item ID '{item.WorkItemId}' is duplicated.");
            }

            ValidateRange(item.SourceRange, $"{path}.sourceRange", errors);
            if (item.ExpectedHours != item.SourceRange.Expected)
            {
                errors.Add($"{path}.expectedHours must equal sourceRange.expected.");
            }

            if (item.ParentId is not null)
            {
                RequireText(item.ParentId, $"{path}.parentId", errors);
            }

            if (item.CorrelationGroup is not null)
            {
                RequireText(item.CorrelationGroup, $"{path}.correlationGroup", errors);
            }

            RequireUniqueText(item.ResolvedEvidenceIds, $"{path}.resolvedEvidenceIds", errors);
            RequireUniqueText(item.UnresolvedEvidenceIds, $"{path}.unresolvedEvidenceIds", errors);
            RequireUniqueText(item.NodeIds, $"{path}.nodeIds", errors);
            if (item.ResolvedEvidenceIds.Intersect(
                    item.UnresolvedEvidenceIds,
                    StringComparer.Ordinal).Any())
            {
                errors.Add($"{path} cannot resolve and leave unresolved the same evidence ID.");
            }

            if (item.NodeIds.Any(id => !nodeIds.Contains(id)))
            {
                errors.Add($"{path}.nodeIds must reference graph nodes in the same report.");
            }
        }
    }

    private static void ValidateGraphSummary(
        CalibrationUncertaintyGraphFeatureReport report,
        List<string> errors)
    {
        CalibrationUncertaintyGraphFeatureSummary summary = report.Summary;
        if (summary.FeatureCount != report.FeatureContract.Features.Count ||
            summary.NodeCount != report.Nodes.Count ||
            summary.EdgeCount != report.Edges.Count ||
            summary.ResolvedLocalReferenceFactCount != report.Edges.Sum(edge =>
                edge.EvidenceIds.Count) ||
            summary.CandidateReferenceFactCount < summary.ResolvedLocalReferenceFactCount ||
            summary.CyclicNodeCount != report.Nodes.Count(node => node.Cyclic) ||
            summary.PublicInterfaceAvailableNodeCount != CountGraphInterface(
                report,
                CalibrationUncertaintyFeatureAvailability.Available) ||
            summary.PublicInterfaceNotApplicableNodeCount != CountGraphInterface(
                report,
                CalibrationUncertaintyFeatureAvailability.NotApplicable) ||
            summary.PublicInterfaceUnavailableNodeCount != CountGraphInterface(
                report,
                CalibrationUncertaintyFeatureAvailability.Unavailable) ||
            summary.WorkItemCount != report.WorkItems.Count ||
            summary.MappedWorkItemCount != report.WorkItems.Count(item => item.NodeIds.Count > 0) ||
            summary.UnmappedWorkItemCount != report.WorkItems.Count(item => item.NodeIds.Count == 0) ||
            summary.ResolvedEvidenceReferenceCount != report.WorkItems.Sum(item =>
                item.ResolvedEvidenceIds.Count) ||
            summary.UnresolvedEvidenceReferenceCount != report.WorkItems.Sum(item =>
                item.UnresolvedEvidenceIds.Count))
        {
            errors.Add("Uncertainty graph summary does not reconcile to its report.");
        }
    }

    private static int CountGraphInterface(
        CalibrationUncertaintyGraphFeatureReport report,
        CalibrationUncertaintyFeatureAvailability availability) =>
        report.Nodes.Count(node => node.PublicInterfaceAvailability == availability);

    private static decimal RoundGraph(decimal value) =>
        decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}
