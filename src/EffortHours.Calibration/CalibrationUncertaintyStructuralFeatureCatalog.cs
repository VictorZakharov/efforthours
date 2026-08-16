using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class CalibrationUncertaintyStructuralFeatureIds
{
    public const string CallableSizeP50 = "shape.local-callable-size-p50";
    public const string CallableSizeP90 = "shape.local-callable-size-p90";
    public const string CallableSizeMaximum = "shape.local-callable-size-maximum";
    public const string OversizedCallableShare = "shape.local-oversized-callable-share";
    public const string DecisionComplexityP50 = "shape.local-decision-complexity-p50";
    public const string DecisionComplexityP90 = "shape.local-decision-complexity-p90";
    public const string DecisionComplexityMaximum =
        "shape.local-decision-complexity-maximum";
    public const string HighDecisionComplexityShare =
        "shape.local-high-decision-complexity-share";
    public const string NestingDepthP50 = "shape.local-nesting-depth-p50";
    public const string NestingDepthP90 = "shape.local-nesting-depth-p90";
    public const string NestingDepthMaximum = "shape.local-nesting-depth-maximum";
    public const string DeepNestingShare = "shape.local-deep-nesting-share";
    public const string CallableMeasurementCoverage =
        "analysis.callable-structural-measurement-coverage";
    public const string AnalyzerAmbiguityConcentration =
        "analysis.structural-analyzer-ambiguity-concentration";
}

public static class CalibrationUncertaintyStructuralFeatureCatalog
{
    public const string Version = CalibrationUncertaintyVersions.StructuralFeatureContractV1;
    public const string ProjectorVersion = CalibrationUncertaintyVersions.StructuralProjectorV1;

    private const string MaximumRule =
        "Maximum value across contributing local project/package source-structure scopes";
    private const string MinimumRule =
        "Minimum value across contributing local project/package source-structure scopes";

    public static CalibrationUncertaintyFeatureContract Current { get; } = new()
    {
        Version = Version,
        EffectiveDate = "2026-08-16",
        LabelIndependent = true,
        IntervalPolicy = CalibrationUncertaintyFeatureCatalog.Current.IntervalPolicy,
        Features =
        [
            Feature(
                CalibrationUncertaintyStructuralFeatureIds.CallableSizeP50,
                CalibrationUncertaintyFeatureValueKind.Count,
                StructuralEvidenceMeasurementNames.CallableSizeP50,
                $"Nearest-rank p50 callable non-comment syntax-token count. {MaximumRule}."),
            Feature(
                CalibrationUncertaintyStructuralFeatureIds.CallableSizeP90,
                CalibrationUncertaintyFeatureValueKind.Count,
                StructuralEvidenceMeasurementNames.CallableSizeP90,
                $"Nearest-rank p90 callable non-comment syntax-token count. {MaximumRule}."),
            Feature(
                CalibrationUncertaintyStructuralFeatureIds.CallableSizeMaximum,
                CalibrationUncertaintyFeatureValueKind.Count,
                StructuralEvidenceMeasurementNames.CallableSizeMaximum,
                $"Maximum callable non-comment syntax-token count. {MaximumRule}."),
            Feature(
                CalibrationUncertaintyStructuralFeatureIds.OversizedCallableShare,
                CalibrationUncertaintyFeatureValueKind.Ratio,
                StructuralEvidenceMeasurementNames.OversizedCallableShare,
                $"Share of measured callables above {StructuralEvidenceThresholds.OversizedCallableTokens} syntax tokens. {MaximumRule}."),
            Feature(
                CalibrationUncertaintyStructuralFeatureIds.DecisionComplexityP50,
                CalibrationUncertaintyFeatureValueKind.Count,
                StructuralEvidenceMeasurementNames.DecisionComplexityP50,
                $"Nearest-rank p50 bounded decision-complexity points per callable. {MaximumRule}."),
            Feature(
                CalibrationUncertaintyStructuralFeatureIds.DecisionComplexityP90,
                CalibrationUncertaintyFeatureValueKind.Count,
                StructuralEvidenceMeasurementNames.DecisionComplexityP90,
                $"Nearest-rank p90 bounded decision-complexity points per callable. {MaximumRule}."),
            Feature(
                CalibrationUncertaintyStructuralFeatureIds.DecisionComplexityMaximum,
                CalibrationUncertaintyFeatureValueKind.Count,
                StructuralEvidenceMeasurementNames.DecisionComplexityMaximum,
                $"Maximum bounded decision-complexity points for one callable. {MaximumRule}."),
            Feature(
                CalibrationUncertaintyStructuralFeatureIds.HighDecisionComplexityShare,
                CalibrationUncertaintyFeatureValueKind.Ratio,
                StructuralEvidenceMeasurementNames.HighDecisionComplexityShare,
                $"Share of measured callables above {StructuralEvidenceThresholds.HighDecisionComplexity} decision-complexity points. {MaximumRule}."),
            Feature(
                CalibrationUncertaintyStructuralFeatureIds.NestingDepthP50,
                CalibrationUncertaintyFeatureValueKind.Count,
                StructuralEvidenceMeasurementNames.NestingDepthP50,
                $"Nearest-rank p50 bounded control-nesting depth per callable. {MaximumRule}."),
            Feature(
                CalibrationUncertaintyStructuralFeatureIds.NestingDepthP90,
                CalibrationUncertaintyFeatureValueKind.Count,
                StructuralEvidenceMeasurementNames.NestingDepthP90,
                $"Nearest-rank p90 bounded control-nesting depth per callable. {MaximumRule}."),
            Feature(
                CalibrationUncertaintyStructuralFeatureIds.NestingDepthMaximum,
                CalibrationUncertaintyFeatureValueKind.Count,
                StructuralEvidenceMeasurementNames.NestingDepthMaximum,
                $"Maximum bounded control-nesting depth for one callable. {MaximumRule}."),
            Feature(
                CalibrationUncertaintyStructuralFeatureIds.DeepNestingShare,
                CalibrationUncertaintyFeatureValueKind.Ratio,
                StructuralEvidenceMeasurementNames.DeepNestingShare,
                $"Share of measured callables above {StructuralEvidenceThresholds.DeepNestingLevels} nesting levels. {MaximumRule}."),
            Feature(
                CalibrationUncertaintyStructuralFeatureIds.CallableMeasurementCoverage,
                CalibrationUncertaintyFeatureValueKind.Ratio,
                StructuralEvidenceMeasurementNames.CallableMeasurementCoverage,
                $"Parser-backed callable samples divided by statically detected callables. {MinimumRule}."),
            Feature(
                CalibrationUncertaintyStructuralFeatureIds.AnalyzerAmbiguityConcentration,
                CalibrationUncertaintyFeatureValueKind.Ratio,
                StructuralEvidenceMeasurementNames.AnalyzerAmbiguityConcentration,
                $"Share of maintained source files without complete parser-backed structural measurement. {MaximumRule}."),
        ],
        DeferredCandidates =
        [
            Deferred(
                "graph.local-fan-in-distribution",
                "Local project/module fan-in distributions require a separate graph contract."),
            Deferred(
                "graph.local-fan-out-distribution",
                "Local project/module fan-out distributions require a separate graph contract."),
            Deferred(
                "graph.dependency-cycle-concentration",
                "Local dependency-cycle membership and concentration require a separate graph contract."),
            Deferred(
                "shape.local-public-interface-concentration",
                "The prior aggregate interface ratio was rejected as a direct signal; local distributions remain unevaluated."),
        ],
    };

    internal static string MeasurementName(string featureId) => featureId switch
    {
        CalibrationUncertaintyStructuralFeatureIds.CallableSizeP50 =>
            StructuralEvidenceMeasurementNames.CallableSizeP50,
        CalibrationUncertaintyStructuralFeatureIds.CallableSizeP90 =>
            StructuralEvidenceMeasurementNames.CallableSizeP90,
        CalibrationUncertaintyStructuralFeatureIds.CallableSizeMaximum =>
            StructuralEvidenceMeasurementNames.CallableSizeMaximum,
        CalibrationUncertaintyStructuralFeatureIds.OversizedCallableShare =>
            StructuralEvidenceMeasurementNames.OversizedCallableShare,
        CalibrationUncertaintyStructuralFeatureIds.DecisionComplexityP50 =>
            StructuralEvidenceMeasurementNames.DecisionComplexityP50,
        CalibrationUncertaintyStructuralFeatureIds.DecisionComplexityP90 =>
            StructuralEvidenceMeasurementNames.DecisionComplexityP90,
        CalibrationUncertaintyStructuralFeatureIds.DecisionComplexityMaximum =>
            StructuralEvidenceMeasurementNames.DecisionComplexityMaximum,
        CalibrationUncertaintyStructuralFeatureIds.HighDecisionComplexityShare =>
            StructuralEvidenceMeasurementNames.HighDecisionComplexityShare,
        CalibrationUncertaintyStructuralFeatureIds.NestingDepthP50 =>
            StructuralEvidenceMeasurementNames.NestingDepthP50,
        CalibrationUncertaintyStructuralFeatureIds.NestingDepthP90 =>
            StructuralEvidenceMeasurementNames.NestingDepthP90,
        CalibrationUncertaintyStructuralFeatureIds.NestingDepthMaximum =>
            StructuralEvidenceMeasurementNames.NestingDepthMaximum,
        CalibrationUncertaintyStructuralFeatureIds.DeepNestingShare =>
            StructuralEvidenceMeasurementNames.DeepNestingShare,
        CalibrationUncertaintyStructuralFeatureIds.CallableMeasurementCoverage =>
            StructuralEvidenceMeasurementNames.CallableMeasurementCoverage,
        CalibrationUncertaintyStructuralFeatureIds.AnalyzerAmbiguityConcentration =>
            StructuralEvidenceMeasurementNames.AnalyzerAmbiguityConcentration,
        _ => throw new ArgumentOutOfRangeException(nameof(featureId), featureId, null),
    };

    internal static bool SelectMinimum(string featureId) =>
        featureId == CalibrationUncertaintyStructuralFeatureIds.CallableMeasurementCoverage;

    private static CalibrationUncertaintyFeatureDefinition Feature(
        string id,
        CalibrationUncertaintyFeatureValueKind kind,
        string measurement,
        string description) => new()
        {
            Id = id,
            Stage = CalibrationUncertaintyFeatureStage.AvailableOffline,
            ValueKind = kind,
            Monotonicity = CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly,
            OfflineSource =
                $"repositoryEvidence.facts[source-structure,{StructuralEvidenceVersions.CallableMetricsV1Tag}].measurements.{measurement}",
            Description = description,
        };

    private static CalibrationUncertaintyFeatureDefinition Deferred(
        string id,
        string description) => new()
        {
            Id = id,
            Stage = CalibrationUncertaintyFeatureStage.DeferredEvidence,
            ValueKind = CalibrationUncertaintyFeatureValueKind.Distribution,
            Monotonicity = CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly,
            OfflineSource = $"unavailable-in-{StructuralEvidenceVersions.CallableMetricsV1}",
            Description = description,
        };
}
