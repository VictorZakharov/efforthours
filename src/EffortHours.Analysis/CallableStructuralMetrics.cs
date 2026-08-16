using EffortHours.Contracts.V1;

namespace EffortHours.Analysis;

public readonly record struct CallableStructuralMetric(
    int SyntaxTokens,
    int DecisionComplexity,
    int MaximumNestingDepth);

public static class CallableStructuralMeasurements
{
    public static IReadOnlyList<EvidenceMeasurement> Build(
        IEnumerable<CallableStructuralMetric> samples,
        int detectedCallables,
        int sourceFiles,
        int parserBackedFiles)
    {
        ArgumentNullException.ThrowIfNull(samples);
        CallableStructuralMetric[] values = [.. samples];
        Validate(values, detectedCallables, sourceFiles, parserBackedFiles);

        List<EvidenceMeasurement> measurements =
        [
            Measurement(
                StructuralEvidenceMeasurementNames.SourceFiles,
                sourceFiles,
                "files"),
            Measurement(
                StructuralEvidenceMeasurementNames.ParserBackedFiles,
                parserBackedFiles,
                "files"),
            Measurement(
                StructuralEvidenceMeasurementNames.DetectedCallables,
                detectedCallables,
                "callables"),
            Measurement(
                StructuralEvidenceMeasurementNames.MeasuredCallables,
                values.Length,
                "callables"),
            Measurement(
                StructuralEvidenceMeasurementNames.CallableMeasurementCoverage,
                detectedCallables == 0 ? 1m : Ratio(values.Length, detectedCallables),
                "ratio"),
            Measurement(
                StructuralEvidenceMeasurementNames.AnalyzerAmbiguityConcentration,
                sourceFiles == 0 ? 0m : Ratio(sourceFiles - parserBackedFiles, sourceFiles),
                "ratio"),
        ];

        if (values.Length == 0)
        {
            return measurements;
        }

        AddDistribution(
            measurements,
            values.Select(value => value.SyntaxTokens),
            StructuralEvidenceMeasurementNames.CallableSizeP50,
            StructuralEvidenceMeasurementNames.CallableSizeP90,
            StructuralEvidenceMeasurementNames.CallableSizeMaximum,
            StructuralEvidenceMeasurementNames.OversizedCallableShare,
            StructuralEvidenceThresholds.OversizedCallableTokens,
            "tokens");
        AddDistribution(
            measurements,
            values.Select(value => value.DecisionComplexity),
            StructuralEvidenceMeasurementNames.DecisionComplexityP50,
            StructuralEvidenceMeasurementNames.DecisionComplexityP90,
            StructuralEvidenceMeasurementNames.DecisionComplexityMaximum,
            StructuralEvidenceMeasurementNames.HighDecisionComplexityShare,
            StructuralEvidenceThresholds.HighDecisionComplexity,
            "points");
        AddDistribution(
            measurements,
            values.Select(value => value.MaximumNestingDepth),
            StructuralEvidenceMeasurementNames.NestingDepthP50,
            StructuralEvidenceMeasurementNames.NestingDepthP90,
            StructuralEvidenceMeasurementNames.NestingDepthMaximum,
            StructuralEvidenceMeasurementNames.DeepNestingShare,
            StructuralEvidenceThresholds.DeepNestingLevels,
            "levels");
        return measurements;
    }

    private static void AddDistribution(
        List<EvidenceMeasurement> measurements,
        IEnumerable<int> source,
        string p50Name,
        string p90Name,
        string maximumName,
        string thresholdShareName,
        int threshold,
        string unit)
    {
        int[] values = [.. source.Order()];
        measurements.Add(Measurement(p50Name, NearestRank(values, 50), unit));
        measurements.Add(Measurement(p90Name, NearestRank(values, 90), unit));
        measurements.Add(Measurement(maximumName, values[^1], unit));
        measurements.Add(Measurement(
            thresholdShareName,
            Ratio(values.Count(value => value > threshold), values.Length),
            "ratio"));
    }

    private static int NearestRank(int[] sorted, int percentile)
    {
        int rank = (int)Math.Ceiling(percentile / 100m * sorted.Length);
        return sorted[Math.Max(0, rank - 1)];
    }

    private static decimal Ratio(int numerator, int denominator) =>
        decimal.Round(numerator / (decimal)denominator, 6, MidpointRounding.AwayFromZero);

    private static EvidenceMeasurement Measurement(string name, decimal value, string unit) =>
        new() { Name = name, Value = value, Unit = unit };

    private static void Validate(
        CallableStructuralMetric[] samples,
        int detectedCallables,
        int sourceFiles,
        int parserBackedFiles)
    {
        if (detectedCallables < 0 || sourceFiles < 0 || parserBackedFiles < 0 ||
            parserBackedFiles > sourceFiles || samples.Length > detectedCallables)
        {
            throw new ArgumentOutOfRangeException(
                nameof(detectedCallables),
                "Structural counts must be nonnegative and reconcile to detected files/callables.");
        }

        if (samples.Any(sample => sample.SyntaxTokens <= 0 ||
                sample.DecisionComplexity <= 0 || sample.MaximumNestingDepth < 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(samples),
                "Callable structural samples must contain positive size/complexity and nonnegative nesting.");
        }
    }
}
