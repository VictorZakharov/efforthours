using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static readonly (string Id, CalibrationUncertaintyFeatureValueKind ValueKind,
        CalibrationUncertaintyFeatureMonotonicity Monotonicity)[]
        UncertaintyEvaluationFeatureBoundary =
        [
            ("model.source-confidence", CalibrationUncertaintyFeatureValueKind.Ratio,
                CalibrationUncertaintyFeatureMonotonicity.LowerMustNotNarrow),
            ("evidence.inferred-fact-share", CalibrationUncertaintyFeatureValueKind.Ratio,
                CalibrationUncertaintyFeatureMonotonicity.HigherMustNotNarrow),
            ("analysis.parser-risk", CalibrationUncertaintyFeatureValueKind.Ordinal,
                CalibrationUncertaintyFeatureMonotonicity.HigherMustNotNarrow),
            ("analysis.explicit-uncertainty-count", CalibrationUncertaintyFeatureValueKind.Count,
                CalibrationUncertaintyFeatureMonotonicity.HigherMustNotNarrow),
            ("access.material-unresolved-count", CalibrationUncertaintyFeatureValueKind.Count,
                CalibrationUncertaintyFeatureMonotonicity.HigherMustWiden),
            ("analysis.non-material-offline-limitation-count",
                CalibrationUncertaintyFeatureValueKind.Count,
                CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly),
            ("analysis.dynamic-boundary-count", CalibrationUncertaintyFeatureValueKind.Count,
                CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly),
            ("analysis.unsupported-boundary-count", CalibrationUncertaintyFeatureValueKind.Count,
                CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly),
            ("evidence.resolved-fact-count", CalibrationUncertaintyFeatureValueKind.Count,
                CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly),
            ("shape.aggregate-branch-density", CalibrationUncertaintyFeatureValueKind.Rate,
                CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly),
            ("shape.aggregate-public-interface-concentration",
                CalibrationUncertaintyFeatureValueKind.Ratio,
                CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly),
        ];

    private static readonly (string Id, CalibrationUncertaintyFeatureValueKind ValueKind,
        CalibrationUncertaintyFeatureMonotonicity Monotonicity)[]
        StructuralEvaluationFeatureBoundary =
        [
            Structural("shape.local-callable-size-p50", CalibrationUncertaintyFeatureValueKind.Count),
            Structural("shape.local-callable-size-p90", CalibrationUncertaintyFeatureValueKind.Count),
            Structural(
                "shape.local-callable-size-maximum",
                CalibrationUncertaintyFeatureValueKind.Count),
            Structural(
                "shape.local-oversized-callable-share",
                CalibrationUncertaintyFeatureValueKind.Ratio),
            Structural(
                "shape.local-decision-complexity-p50",
                CalibrationUncertaintyFeatureValueKind.Count),
            Structural(
                "shape.local-decision-complexity-p90",
                CalibrationUncertaintyFeatureValueKind.Count),
            Structural(
                "shape.local-decision-complexity-maximum",
                CalibrationUncertaintyFeatureValueKind.Count),
            Structural(
                "shape.local-high-decision-complexity-share",
                CalibrationUncertaintyFeatureValueKind.Ratio),
            Structural("shape.local-nesting-depth-p50", CalibrationUncertaintyFeatureValueKind.Count),
            Structural("shape.local-nesting-depth-p90", CalibrationUncertaintyFeatureValueKind.Count),
            Structural(
                "shape.local-nesting-depth-maximum",
                CalibrationUncertaintyFeatureValueKind.Count),
            Structural(
                "shape.local-deep-nesting-share",
                CalibrationUncertaintyFeatureValueKind.Ratio),
            Structural(
                "analysis.callable-structural-measurement-coverage",
                CalibrationUncertaintyFeatureValueKind.Ratio),
            Structural(
                "analysis.structural-analyzer-ambiguity-concentration",
                CalibrationUncertaintyFeatureValueKind.Ratio),
        ];

    private static (string Id, CalibrationUncertaintyFeatureValueKind ValueKind,
        CalibrationUncertaintyFeatureMonotonicity Monotonicity) Structural(
            string id,
            CalibrationUncertaintyFeatureValueKind kind) =>
        (id, kind, CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly);
}
