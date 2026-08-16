using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationUncertaintyGraphProjectionBuilder
{
    public static CalibrationUncertaintyGraphProjection Build(RepositoryEvidence evidence)
    {
        List<string> errors = [];
        GraphNodeSeed[] seeds = BuildNodeSeeds(evidence.Facts, errors);
        Dictionary<string, GraphNodeSeed> endpointIndex = seeds.ToDictionary(
            seed => EndpointKey(seed.Ecosystem, seed.Endpoint),
            StringComparer.Ordinal);
        Dictionary<string, GraphNodeSeed> scopeIndex = seeds.ToDictionary(
            seed => EndpointKey(seed.Ecosystem, seed.Scope),
            StringComparer.Ordinal);
        (CalibrationUncertaintyGraphEdge[] Edges, int CandidateFacts, int ResolvedFacts) =
            BuildEdges(evidence.Facts, scopeIndex, endpointIndex, errors);
        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors.Distinct(StringComparer.Ordinal));
        }

        Dictionary<string, HashSet<string>> outgoing = seeds.ToDictionary(
            seed => seed.Id,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> incoming = seeds.ToDictionary(
            seed => seed.Id,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        foreach (CalibrationUncertaintyGraphEdge edge in Edges)
        {
            outgoing[edge.SourceNodeId].Add(edge.TargetNodeId);
            incoming[edge.TargetNodeId].Add(edge.SourceNodeId);
        }

        IReadOnlyDictionary<string, int> cyclicComponents =
            CalibrationUncertaintyGraphComponents.FindCyclicComponents(outgoing);
        IReadOnlyDictionary<string, GraphInterfaceMeasurement> interfaces =
            CalibrationUncertaintyGraphInterface.Measure(evidence.Facts, seeds);
        CalibrationUncertaintyGraphNode[] nodes =
        [
            .. seeds.OrderBy(seed => seed.Id, StringComparer.Ordinal).Select(seed =>
            {
                int componentSize = cyclicComponents.GetValueOrDefault(seed.Id);
                GraphInterfaceMeasurement measurement = interfaces[seed.Id];
                return new CalibrationUncertaintyGraphNode
                {
                    NodeId = seed.Id,
                    Ecosystem = seed.Ecosystem,
                    FanIn = incoming[seed.Id].Count,
                    FanOut = outgoing[seed.Id].Count,
                    Cyclic = componentSize > 0,
                    CyclicComponentSize = componentSize,
                    CyclicComponentNodeShare = seeds.Length == 0
                        ? 0m
                        : Round6((decimal)componentSize / seeds.Length),
                    PublicInterfaceAvailability = measurement.Availability,
                    PublicInterfaceConcentration = measurement.Value,
                    PublicInterfaceReasonCode = measurement.ReasonCode,
                    PublicInterfaceEvidenceIds = measurement.EvidenceIds,
                };
            }),
        ];
        Dictionary<string, GraphNodeSeed[]> byScope = seeds
            .GroupBy(seed => seed.Scope, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(seed => seed.Id, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        return new CalibrationUncertaintyGraphProjection(
            nodes,
            Edges,
            byScope,
            CandidateFacts,
            ResolvedFacts);
    }

    private static GraphNodeSeed[] BuildNodeSeeds(
        IReadOnlyList<EvidenceFact> facts,
        List<string> errors)
    {
        List<GraphNodeSeed> seeds = [];
        foreach (EvidenceFact fact in facts.OrderBy(fact => fact.Id, StringComparer.Ordinal))
        {
            string? ecosystem = null;
            string? endpoint = null;
            if (fact.Kind == EvidenceKinds.DotNetProject &&
                fact.Id.StartsWith("dotnet:project:", StringComparison.Ordinal))
            {
                ecosystem = "dotnet";
                endpoint = fact.Scope;
            }
            else if (fact.Kind == EvidenceKinds.JavaScriptPackage &&
                     fact.Id.StartsWith("javascript:package:", StringComparison.Ordinal))
            {
                ecosystem = "javascript";
                endpoint = fact.Locations.Count == 1 ? fact.Locations[0].Path : null;
                if (endpoint is null)
                {
                    errors.Add(
                        $"JavaScript graph node '{fact.Id}' must have exactly one manifest location.");
                    continue;
                }
            }

            if (ecosystem is not null && endpoint is not null)
            {
                seeds.Add(new GraphNodeSeed(fact.Id, ecosystem, fact.Scope, endpoint, fact));
            }
        }

        foreach (IGrouping<string, GraphNodeSeed> group in seeds.GroupBy(
                     seed => EndpointKey(seed.Ecosystem, seed.Endpoint),
                     StringComparer.Ordinal))
        {
            if (group.Count() > 1)
            {
                errors.Add(
                    $"Graph endpoint '{group.Key}' is declared by multiple node facts.");
            }
        }

        return [.. seeds];
    }

    private static (CalibrationUncertaintyGraphEdge[] Edges, int CandidateFacts,
        int ResolvedFacts) BuildEdges(
        IReadOnlyList<EvidenceFact> facts,
        Dictionary<string, GraphNodeSeed> scopeIndex,
        Dictionary<string, GraphNodeSeed> endpointIndex,
        List<string> errors)
    {
        Dictionary<string, EdgeSeed> edges = new(StringComparer.Ordinal);
        int candidateFacts = 0;
        int resolvedFacts = 0;
        foreach (EvidenceFact fact in facts.OrderBy(fact => fact.Id, StringComparer.Ordinal))
        {
            string? ecosystem = ReferenceEcosystem(fact);
            if (ecosystem is null)
            {
                continue;
            }

            candidateFacts++;
            if (!fact.Tags.Contains("reference:resolved", StringComparer.Ordinal))
            {
                continue;
            }

            string[] targets = [.. fact.Tags
                .Where(tag => tag.StartsWith("target:", StringComparison.Ordinal))
                .Select(tag => tag[7..])];
            if (targets.Length != 1)
            {
                errors.Add(
                    $"Resolved graph reference '{fact.Id}' must identify one source and target.");
                continue;
            }

            string sourceKey = EndpointKey(ecosystem, fact.Scope);
            string targetKey = EndpointKey(ecosystem, targets[0]);
            if (!scopeIndex.TryGetValue(sourceKey, out GraphNodeSeed? source) ||
                !endpointIndex.TryGetValue(targetKey, out GraphNodeSeed? target))
            {
                continue;
            }

            resolvedFacts++;
            string edgeKey = $"{source.Id}\n{target.Id}";
            if (!edges.TryGetValue(edgeKey, out EdgeSeed? edge))
            {
                edge = new EdgeSeed(source.Id, target.Id);
                edges.Add(edgeKey, edge);
            }

            edge.EvidenceIds.Add(fact.Id);
        }

        CalibrationUncertaintyGraphEdge[] result =
        [
            .. edges.Values
                .OrderBy(edge => edge.Source, StringComparer.Ordinal)
                .ThenBy(edge => edge.Target, StringComparer.Ordinal)
                .Select(edge => new CalibrationUncertaintyGraphEdge
                {
                    SourceNodeId = edge.Source,
                    TargetNodeId = edge.Target,
                    EvidenceIds = [.. edge.EvidenceIds.Order(StringComparer.Ordinal)],
                }),
        ];
        return (result, candidateFacts, resolvedFacts);
    }

    private static string? ReferenceEcosystem(EvidenceFact fact) =>
        fact.Kind != EvidenceKinds.ProjectReference
            ? null
            : fact.Id.StartsWith("dotnet:project-reference:", StringComparison.Ordinal)
            ? "dotnet"
            : fact.Id.StartsWith("javascript:project-reference:", StringComparison.Ordinal)
                ? "javascript"
                : null;

    private static string EndpointKey(string ecosystem, string endpoint) =>
        $"{ecosystem}\n{endpoint}";

    private static decimal Round6(decimal value) =>
        decimal.Round(value, 6, MidpointRounding.AwayFromZero);

    private sealed class EdgeSeed(string source, string target)
    {
        public string Source { get; } = source;

        public string Target { get; } = target;

        public HashSet<string> EvidenceIds { get; } = new(StringComparer.Ordinal);
    }

}
