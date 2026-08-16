using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal sealed class CalibrationUncertaintyGraphProjection
{
    private readonly IReadOnlyDictionary<string, GraphNodeSeed[]> _nodesByScope;

    public CalibrationUncertaintyGraphProjection(
        IReadOnlyList<CalibrationUncertaintyGraphNode> nodes,
        IReadOnlyList<CalibrationUncertaintyGraphEdge> edges,
        IReadOnlyDictionary<string, GraphNodeSeed[]> nodesByScope,
        int candidateReferenceFactCount,
        int resolvedLocalReferenceFactCount)
    {
        Nodes = nodes;
        Edges = edges;
        _nodesByScope = nodesByScope;
        CandidateReferenceFactCount = candidateReferenceFactCount;
        ResolvedLocalReferenceFactCount = resolvedLocalReferenceFactCount;
    }

    public IReadOnlyList<CalibrationUncertaintyGraphNode> Nodes { get; }

    public IReadOnlyList<CalibrationUncertaintyGraphEdge> Edges { get; }

    public int CandidateReferenceFactCount { get; }

    public int ResolvedLocalReferenceFactCount { get; }

    public IReadOnlyList<string> ResolveNodeIds(EvidenceFact fact)
    {
        if (Nodes.Any(node => node.NodeId == fact.Id))
        {
            return [fact.Id];
        }

        string? ecosystem = GraphEcosystem(fact.Id);
        if (ecosystem is null)
        {
            return [];
        }

        if (!_nodesByScope.TryGetValue(fact.Scope, out GraphNodeSeed[]? candidates))
        {
            return [];
        }

        return [.. candidates
            .Where(candidate => candidate.Ecosystem == ecosystem)
            .Select(candidate => candidate.Id)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }

    internal static string? GraphEcosystem(string factId) =>
        factId.StartsWith("dotnet:", StringComparison.Ordinal)
            ? "dotnet"
            : factId.StartsWith("javascript:", StringComparison.Ordinal)
                ? "javascript"
                : null;
}

internal sealed record GraphNodeSeed(
    string Id,
    string Ecosystem,
    string Scope,
    string Endpoint,
    EvidenceFact Fact);
