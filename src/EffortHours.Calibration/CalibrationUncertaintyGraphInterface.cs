using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationUncertaintyGraphInterface
{
    public static IReadOnlyDictionary<string, GraphInterfaceMeasurement> Measure(
        IReadOnlyList<EvidenceFact> facts,
        IReadOnlyList<GraphNodeSeed> seeds)
    {
        Dictionary<string, GraphNodeSeed> scopeIndex = seeds.ToDictionary(
            seed => ScopeKey(seed.Ecosystem, seed.Scope),
            StringComparer.Ordinal);
        Dictionary<string, List<EvidenceFact>> structures = new(StringComparer.Ordinal);
        foreach (EvidenceFact fact in facts)
        {
            string? ecosystem = StructureEcosystem(fact);
            if (ecosystem is null ||
                !scopeIndex.TryGetValue(ScopeKey(ecosystem, fact.Scope), out GraphNodeSeed? node))
            {
                continue;
            }

            if (!structures.TryGetValue(node.Id, out List<EvidenceFact>? values))
            {
                values = [];
                structures.Add(node.Id, values);
            }

            values.Add(fact);
        }

        Dictionary<string, GraphInterfaceMeasurement> result = new(StringComparer.Ordinal);
        foreach (GraphNodeSeed seed in seeds)
        {
            result.Add(seed.Id, MeasureNode(seed, structures.GetValueOrDefault(seed.Id)));
        }

        return result;
    }

    private static GraphInterfaceMeasurement MeasureNode(
        GraphNodeSeed seed,
        IReadOnlyList<EvidenceFact>? values)
    {
        if (values is null)
        {
            return GraphInterfaceMeasurement.NotApplicable("no-source-structure-evidence");
        }

        string[] required = seed.Ecosystem == "dotnet"
            ? ["types", "methods", "public-types", "public-methods"]
            : ["functions", "methods", "classes", "interfaces", "type-aliases",
                "enums", "exports"];
        if (values.Any(fact => required.Any(name => !HasCountMeasurement(fact, name))))
        {
            return GraphInterfaceMeasurement.Unavailable(
                "incompatible-source-structure-evidence",
                values.Select(fact => fact.Id));
        }

        string[] denominatorNames = seed.Ecosystem == "dotnet"
            ? ["types", "methods"]
            : ["functions", "methods", "classes", "interfaces", "type-aliases", "enums"];
        string[] numeratorNames = seed.Ecosystem == "dotnet"
            ? ["public-types", "public-methods"]
            : ["exports"];
        decimal denominator = values.Sum(fact => SumMeasurements(fact, denominatorNames));
        if (denominator == 0m)
        {
            return GraphInterfaceMeasurement.NotApplicable(
                "no-declaration-structure-measurement",
                values.Select(fact => fact.Id));
        }

        decimal numerator = values.Sum(fact => SumMeasurements(fact, numeratorNames));
        return GraphInterfaceMeasurement.Available(
            Round6(decimal.Min(1m, numerator / denominator)),
            values.Select(fact => fact.Id));
    }

    private static bool HasCountMeasurement(EvidenceFact fact, string name)
    {
        EvidenceMeasurement[] matches =
        [
            .. fact.Measurements.Where(measurement => measurement.Name == name),
        ];
        return matches.Length == 1 &&
               matches[0].Value >= 0m &&
               decimal.Truncate(matches[0].Value) == matches[0].Value;
    }

    private static decimal SumMeasurements(EvidenceFact fact, IReadOnlyList<string> names) =>
        fact.Measurements
            .Where(measurement => names.Contains(measurement.Name, StringComparer.Ordinal))
            .Sum(measurement => measurement.Value);

    private static string? StructureEcosystem(EvidenceFact fact) =>
        fact.Kind != EvidenceKinds.SourceStructure
            ? null
            : fact.Id.StartsWith("dotnet:source-structure:", StringComparison.Ordinal)
            ? "dotnet"
            : fact.Id.StartsWith("javascript:source-structure:", StringComparison.Ordinal)
                ? "javascript"
                : null;

    private static string ScopeKey(string ecosystem, string scope) => $"{ecosystem}\n{scope}";

    private static decimal Round6(decimal value) =>
        decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}

internal sealed record GraphInterfaceMeasurement(
    CalibrationUncertaintyFeatureAvailability Availability,
    decimal? Value,
    string? ReasonCode,
    IReadOnlyList<string> EvidenceIds)
{
    public static GraphInterfaceMeasurement Available(
        decimal value,
        IEnumerable<string> evidenceIds) =>
        new(
            CalibrationUncertaintyFeatureAvailability.Available,
            value,
            null,
            OrderIds(evidenceIds));

    public static GraphInterfaceMeasurement NotApplicable(
        string reason,
        IEnumerable<string>? evidenceIds = null) =>
        new(
            CalibrationUncertaintyFeatureAvailability.NotApplicable,
            null,
            reason,
            OrderIds(evidenceIds ?? []));

    public static GraphInterfaceMeasurement Unavailable(
        string reason,
        IEnumerable<string> evidenceIds) =>
        new(
            CalibrationUncertaintyFeatureAvailability.Unavailable,
            null,
            reason,
            OrderIds(evidenceIds));

    private static string[] OrderIds(IEnumerable<string> values) =>
        [.. values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
