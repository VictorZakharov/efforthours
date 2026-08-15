using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class CalibrationUncertaintyFeatureIds
{
    public const string SourceConfidence = "model.source-confidence";
    public const string InferredFactShare = "evidence.inferred-fact-share";
    public const string ParserRisk = "analysis.parser-risk";
    public const string ExplicitUncertaintyCount = "analysis.explicit-uncertainty-count";
    public const string MaterialUnresolvedCount = "access.material-unresolved-count";
    public const string NonMaterialOfflineLimitationCount =
        "analysis.non-material-offline-limitation-count";
    public const string DynamicBoundaryCount = "analysis.dynamic-boundary-count";
    public const string UnsupportedBoundaryCount = "analysis.unsupported-boundary-count";
    public const string ResolvedFactCount = "evidence.resolved-fact-count";
    public const string AggregateBranchDensity = "shape.aggregate-branch-density";
    public const string AggregatePublicInterfaceConcentration =
        "shape.aggregate-public-interface-concentration";
}

public static class CalibrationUncertaintyFeatureCatalog
{
    public const string Version = CalibrationUncertaintyVersions.FeatureContractV1;
    public const string ProjectorVersion = CalibrationUncertaintyVersions.ProjectorV1;
    public const string IntervalPolicyVersion = CalibrationUncertaintyVersions.IntervalPolicyV1;
    public const string MaterialAccessGapTag = "uncertainty:material-access-gap";

    public static CalibrationUncertaintyFeatureContract Current { get; } = new()
    {
        Version = Version,
        EffectiveDate = "2026-08-15",
        LabelIndependent = true,
        IntervalPolicy = new CalibrationUncertaintyIntervalPolicy
        {
            Version = IntervalPolicyVersion,
            IntendedCoverageMetric = CalibrationUncertaintyCoverageMetric.ReviewedExpectedPoint,
            IntendedCoverageTarget = 0.80m,
            FormalProbabilityInterval = false,
            SymmetricAroundExpected = true,
            ZeroHourFloor = true,
            DirectionalContingenciesSeparate = true,
            MaterialUnresolvedFactsMustWiden = true,
            ComparableWeakerEvidenceMustNotNarrow = true,
            MissingValuesWidenAutomatically = false,
        },
        Features =
        [
            Available(
                CalibrationUncertaintyFeatureIds.SourceConfidence,
                CalibrationUncertaintyFeatureValueKind.Ratio,
                CalibrationUncertaintyFeatureMonotonicity.LowerMustNotNarrow,
                "estimate.workItems[].confidence",
                "Estimator source confidence. Lower confidence may widen but must not narrow a comparable interval."),
            Available(
                CalibrationUncertaintyFeatureIds.InferredFactShare,
                CalibrationUncertaintyFeatureValueKind.Ratio,
                CalibrationUncertaintyFeatureMonotonicity.HigherMustNotNarrow,
                "repositoryEvidence.facts[].provenance.sourceKind",
                "Share of resolved supporting facts whose provenance is inferred."),
            Available(
                CalibrationUncertaintyFeatureIds.ParserRisk,
                CalibrationUncertaintyFeatureValueKind.Ordinal,
                CalibrationUncertaintyFeatureMonotonicity.HigherMustNotNarrow,
                "repositoryEvidence.facts[].tags",
                "Worst parser risk among resolved supporting facts: parser-backed/high=0, bounded/medium=1, token-backed=2, low/fallback=3."),
            Available(
                CalibrationUncertaintyFeatureIds.ExplicitUncertaintyCount,
                CalibrationUncertaintyFeatureValueKind.Count,
                CalibrationUncertaintyFeatureMonotonicity.HigherMustNotNarrow,
                "estimate.workItems[].uncertaintyReasons",
                "Count of distinct explicit uncertainty reasons on the source work item."),
            Available(
                CalibrationUncertaintyFeatureIds.MaterialUnresolvedCount,
                CalibrationUncertaintyFeatureValueKind.Count,
                CalibrationUncertaintyFeatureMonotonicity.HigherMustWiden,
                "estimate.workItems[].evidenceIds + repositoryEvidence.facts[].tags",
                "Evidence-less work items, missing supporting fact references, and facts explicitly tagged as material unresolved access gaps."),
            Available(
                CalibrationUncertaintyFeatureIds.NonMaterialOfflineLimitationCount,
                CalibrationUncertaintyFeatureValueKind.Count,
                CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly,
                "repositoryEvidence.facts[].tags",
                "Static-analysis non-execution or non-verification limitations; these do not change interval width by themselves."),
            Available(
                CalibrationUncertaintyFeatureIds.DynamicBoundaryCount,
                CalibrationUncertaintyFeatureValueKind.Count,
                CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly,
                "repositoryEvidence.facts[].tags + measurements",
                "Resolved facts carrying dynamic-boundary tags or positive dynamic measurements; retained for held-out feature testing."),
            Available(
                CalibrationUncertaintyFeatureIds.UnsupportedBoundaryCount,
                CalibrationUncertaintyFeatureValueKind.Count,
                CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly,
                "repositoryEvidence.facts[].tags",
                "Resolved facts carrying unsupported, excluded, or unresolved boundary tags; retained for held-out feature testing."),
            Available(
                CalibrationUncertaintyFeatureIds.ResolvedFactCount,
                CalibrationUncertaintyFeatureValueKind.Count,
                CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly,
                "estimate.workItems[].evidenceIds",
                "Count of distinct supporting facts resolved in the paired evidence artifact; a scale diagnostic, not a width driver."),
            Available(
                CalibrationUncertaintyFeatureIds.AggregateBranchDensity,
                CalibrationUncertaintyFeatureValueKind.Rate,
                CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly,
                "repositoryEvidence.facts[].measurements",
                "Aggregate branch points per function or method where both measurements exist; not a per-function complexity distribution."),
            Available(
                CalibrationUncertaintyFeatureIds.AggregatePublicInterfaceConcentration,
                CalibrationUncertaintyFeatureValueKind.Ratio,
                CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly,
                "repositoryEvidence.facts[].measurements",
                "Aggregate public-symbol measurements divided by declaration measurements where both exist; retained for held-out testing."),
        ],
        DeferredCandidates =
        [
            Deferred(
                "model.sample-support",
                CalibrationUncertaintyFeatureValueKind.Count,
                "Per-cell reviewed sample support and hierarchical fallback identity are not present in estimate artifacts."),
            Deferred(
                "model.out-of-distribution-score",
                CalibrationUncertaintyFeatureValueKind.Ratio,
                "No versioned offline out-of-distribution detector or training-support manifest exists."),
            Deferred(
                "shape.function-complexity-distribution",
                CalibrationUncertaintyFeatureValueKind.Distribution,
                "Per-function complexity median, p90, maximum, and threshold shares are not emitted."),
            Deferred(
                "shape.function-size-distribution",
                CalibrationUncertaintyFeatureValueKind.Distribution,
                "Per-function size median, p90, maximum, and threshold shares are not emitted."),
            Deferred(
                "shape.nesting-distribution",
                CalibrationUncertaintyFeatureValueKind.Distribution,
                "Per-function nesting median, p90, maximum, and threshold shares are not emitted."),
            Deferred(
                "graph.local-fan-in-distribution",
                CalibrationUncertaintyFeatureValueKind.Distribution,
                "Versioned local module/project fan-in distributions are not emitted across ecosystems."),
            Deferred(
                "graph.local-fan-out-distribution",
                CalibrationUncertaintyFeatureValueKind.Distribution,
                "Versioned local module/project fan-out distributions are not emitted across ecosystems."),
            Deferred(
                "graph.dependency-cycle-concentration",
                CalibrationUncertaintyFeatureValueKind.Ratio,
                "Versioned local dependency-cycle membership and concentration are not emitted."),
            Deferred(
                "analysis.ambiguity-concentration",
                CalibrationUncertaintyFeatureValueKind.Ratio,
                "Analyzer ambiguity counts are not yet normalized into a cross-ecosystem concentration contract."),
        ],
    };

    private static CalibrationUncertaintyFeatureDefinition Available(
        string id,
        CalibrationUncertaintyFeatureValueKind kind,
        CalibrationUncertaintyFeatureMonotonicity monotonicity,
        string source,
        string description) => new()
        {
            Id = id,
            Stage = CalibrationUncertaintyFeatureStage.AvailableOffline,
            ValueKind = kind,
            Monotonicity = monotonicity,
            OfflineSource = source,
            Description = description,
        };

    private static CalibrationUncertaintyFeatureDefinition Deferred(
        string id,
        CalibrationUncertaintyFeatureValueKind kind,
        string description) => new()
        {
            Id = id,
            Stage = CalibrationUncertaintyFeatureStage.DeferredEvidence,
            ValueKind = kind,
            Monotonicity = CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly,
            OfflineSource = "unavailable-in-v1-evidence-contract",
            Description = description,
        };
}
