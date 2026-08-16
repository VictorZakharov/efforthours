namespace EffortHours.Contracts.V1;

/// <summary>
/// Frozen, label-independent structural measurements emitted by parser-backed analyzers.
/// These measurements are diagnostic evidence and are not effort multipliers.
/// </summary>
public static class StructuralEvidenceVersions
{
    public const string CallableMetricsV1 = "callable-structural-metrics/1.0.0";

    public const string CallableMetricsV1Tag = "structural-metrics:callable-v1";
}

public static class StructuralEvidenceThresholds
{
    public const int OversizedCallableTokens = 200;

    public const int HighDecisionComplexity = 10;

    public const int DeepNestingLevels = 4;
}

public static class StructuralEvidenceMeasurementNames
{
    public const string SourceFiles = "structural-source-files";
    public const string ParserBackedFiles = "structural-parser-backed-files";
    public const string DetectedCallables = "structural-detected-callables";
    public const string MeasuredCallables = "structural-measured-callables";
    public const string CallableMeasurementCoverage =
        "structural-callable-measurement-coverage";
    public const string AnalyzerAmbiguityConcentration =
        "structural-analyzer-ambiguity-concentration";

    public const string CallableSizeP50 = "structural-callable-size-p50";
    public const string CallableSizeP90 = "structural-callable-size-p90";
    public const string CallableSizeMaximum = "structural-callable-size-maximum";
    public const string OversizedCallableShare = "structural-oversized-callable-share";

    public const string DecisionComplexityP50 = "structural-decision-complexity-p50";
    public const string DecisionComplexityP90 = "structural-decision-complexity-p90";
    public const string DecisionComplexityMaximum =
        "structural-decision-complexity-maximum";
    public const string HighDecisionComplexityShare =
        "structural-high-decision-complexity-share";

    public const string NestingDepthP50 = "structural-nesting-depth-p50";
    public const string NestingDepthP90 = "structural-nesting-depth-p90";
    public const string NestingDepthMaximum = "structural-nesting-depth-maximum";
    public const string DeepNestingShare = "structural-deep-nesting-share";
}
