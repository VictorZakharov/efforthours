using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class CalibrationUncertaintyGraphFeatureIds
{
    public const string FanInP50 = "graph.local-fan-in-p50";
    public const string FanInP90 = "graph.local-fan-in-p90";
    public const string FanInMaximum = "graph.local-fan-in-maximum";
    public const string HighFanInShare = "graph.local-high-fan-in-share";
    public const string FanOutP50 = "graph.local-fan-out-p50";
    public const string FanOutP90 = "graph.local-fan-out-p90";
    public const string FanOutMaximum = "graph.local-fan-out-maximum";
    public const string HighFanOutShare = "graph.local-high-fan-out-share";
    public const string CyclicNodeShare = "graph.local-cyclic-node-share";
    public const string LargestCyclicComponentShare =
        "graph.local-largest-cyclic-component-share";
    public const string PublicInterfaceP50 = "shape.local-public-interface-p50";
    public const string PublicInterfaceP90 = "shape.local-public-interface-p90";
    public const string PublicInterfaceMaximum = "shape.local-public-interface-maximum";
    public const string HighPublicInterfaceShare =
        "shape.local-high-public-interface-share";
}

public static class CalibrationUncertaintyGraphFeatureCatalog
{
    public const string Version = CalibrationUncertaintyVersions.GraphFeatureContractV1;
    public const string ProjectorVersion = CalibrationUncertaintyVersions.GraphProjectorV1;
    public const int HighFanDegreeThreshold = 3;
    public const decimal HighPublicInterfaceThreshold = 0.5m;

    private const string GraphSource =
        "repositoryEvidence.facts[dotnet-project|javascript-package|project-reference]";
    private const string InterfaceSource =
        "repositoryEvidence.facts[source-structure].measurements[public/declaration counts]";
    private const string DegreePopulation =
        "supported declared .NET projects and JavaScript packages, including zero-degree nodes; " +
        "resolved same-ecosystem local edges are deduplicated by source and target";

    public static CalibrationUncertaintyFeatureContract Current { get; } = new()
    {
        Version = Version,
        EffectiveDate = "2026-08-16",
        LabelIndependent = true,
        IntervalPolicy = CalibrationUncertaintyFeatureCatalog.Current.IntervalPolicy,
        Features =
        [
            Count(CalibrationUncertaintyGraphFeatureIds.FanInP50,
                $"Nearest-rank p50 local fan-in across {DegreePopulation}."),
            Count(CalibrationUncertaintyGraphFeatureIds.FanInP90,
                $"Nearest-rank p90 local fan-in across {DegreePopulation}."),
            Count(CalibrationUncertaintyGraphFeatureIds.FanInMaximum,
                $"Maximum local fan-in across {DegreePopulation}."),
            Ratio(CalibrationUncertaintyGraphFeatureIds.HighFanInShare, GraphSource,
                $"Share of supported nodes with local fan-in strictly above {HighFanDegreeThreshold}; " +
                "the threshold is the first value in the pre-existing 4-7 count bucket."),
            Count(CalibrationUncertaintyGraphFeatureIds.FanOutP50,
                $"Nearest-rank p50 local fan-out across {DegreePopulation}."),
            Count(CalibrationUncertaintyGraphFeatureIds.FanOutP90,
                $"Nearest-rank p90 local fan-out across {DegreePopulation}."),
            Count(CalibrationUncertaintyGraphFeatureIds.FanOutMaximum,
                $"Maximum local fan-out across {DegreePopulation}."),
            Ratio(CalibrationUncertaintyGraphFeatureIds.HighFanOutShare, GraphSource,
                $"Share of supported nodes with local fan-out strictly above {HighFanDegreeThreshold}; " +
                "the threshold is the first value in the pre-existing 4-7 count bucket."),
            Ratio(CalibrationUncertaintyGraphFeatureIds.CyclicNodeShare, GraphSource,
                "Share of supported nodes in a directed strongly connected component with more " +
                "than one node, or with a self-edge."),
            Ratio(CalibrationUncertaintyGraphFeatureIds.LargestCyclicComponentShare, GraphSource,
                "Largest cyclic strongly connected component divided by all supported graph nodes; " +
                "acyclic graphs produce zero."),
            Ratio(CalibrationUncertaintyGraphFeatureIds.PublicInterfaceP50, InterfaceSource,
                "Nearest-rank p50 local public-interface ratio across applicable scopes. .NET uses " +
                "public types plus methods over all types plus methods; JavaScript uses export " +
                "declarations over functions, methods, classes, interfaces, aliases, and enums. " +
                "Each ratio is capped at one; any incompatible supported scope makes the " +
                "repository distribution unavailable instead of silently using a partial sample."),
            Ratio(CalibrationUncertaintyGraphFeatureIds.PublicInterfaceP90, InterfaceSource,
                "Nearest-rank p90 of the same local public-interface ratio."),
            Ratio(CalibrationUncertaintyGraphFeatureIds.PublicInterfaceMaximum, InterfaceSource,
                "Maximum of the same local public-interface ratio."),
            Ratio(CalibrationUncertaintyGraphFeatureIds.HighPublicInterfaceShare, InterfaceSource,
                $"Share of applicable scopes whose public-interface ratio is strictly above " +
                $"{HighPublicInterfaceThreshold}; this means a majority-public declaration surface."),
        ],
        DeferredCandidates = [],
    };

    private static CalibrationUncertaintyFeatureDefinition Count(
        string id,
        string description) => Feature(
            id,
            CalibrationUncertaintyFeatureValueKind.Count,
            GraphSource,
            description);

    private static CalibrationUncertaintyFeatureDefinition Ratio(
        string id,
        string source,
        string description) => Feature(
            id,
            CalibrationUncertaintyFeatureValueKind.Ratio,
            source,
            description);

    private static CalibrationUncertaintyFeatureDefinition Feature(
        string id,
        CalibrationUncertaintyFeatureValueKind valueKind,
        string source,
        string description) => new()
        {
            Id = id,
            Stage = CalibrationUncertaintyFeatureStage.AvailableOffline,
            ValueKind = valueKind,
            Monotonicity = CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly,
            OfflineSource = source,
            Description = description,
        };
}
