using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal sealed record CalibrationUncertaintyStructuralFact(
    EvidenceFact Fact,
    int SourceFiles,
    int ParserBackedFiles,
    int DetectedCallables,
    int MeasuredCallables,
    IReadOnlyDictionary<string, decimal> FeatureMeasurements)
{
    private static readonly string[] DistributionMeasurements =
    [
        StructuralEvidenceMeasurementNames.CallableSizeP50,
        StructuralEvidenceMeasurementNames.CallableSizeP90,
        StructuralEvidenceMeasurementNames.CallableSizeMaximum,
        StructuralEvidenceMeasurementNames.OversizedCallableShare,
        StructuralEvidenceMeasurementNames.DecisionComplexityP50,
        StructuralEvidenceMeasurementNames.DecisionComplexityP90,
        StructuralEvidenceMeasurementNames.DecisionComplexityMaximum,
        StructuralEvidenceMeasurementNames.HighDecisionComplexityShare,
        StructuralEvidenceMeasurementNames.NestingDepthP50,
        StructuralEvidenceMeasurementNames.NestingDepthP90,
        StructuralEvidenceMeasurementNames.NestingDepthMaximum,
        StructuralEvidenceMeasurementNames.DeepNestingShare,
    ];

    public static IReadOnlyDictionary<string, CalibrationUncertaintyStructuralFact> Parse(
        IEnumerable<EvidenceFact> facts)
    {
        Dictionary<string, CalibrationUncertaintyStructuralFact> parsed =
            new(StringComparer.Ordinal);
        List<string> errors = [];
        foreach (EvidenceFact fact in facts.Where(IsCompatible).OrderBy(
            fact => fact.Id,
            StringComparer.Ordinal))
        {
            CalibrationUncertaintyStructuralFact? value = ParseFact(fact, errors);
            if (value is not null)
            {
                parsed.Add(fact.Id, value);
            }
        }

        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }

        return parsed;
    }

    public static bool IsCompatible(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.SourceStructure &&
        fact.Tags.Contains(
            StructuralEvidenceVersions.CallableMetricsV1Tag,
            StringComparer.Ordinal);

    private static CalibrationUncertaintyStructuralFact? ParseFact(
        EvidenceFact fact,
        List<string> errors)
    {
        Dictionary<string, decimal> measurements = new(StringComparer.Ordinal);
        HashSet<string> recognized =
        [
            StructuralEvidenceMeasurementNames.SourceFiles,
            StructuralEvidenceMeasurementNames.ParserBackedFiles,
            StructuralEvidenceMeasurementNames.DetectedCallables,
            StructuralEvidenceMeasurementNames.MeasuredCallables,
            StructuralEvidenceMeasurementNames.CallableMeasurementCoverage,
            StructuralEvidenceMeasurementNames.AnalyzerAmbiguityConcentration,
            .. DistributionMeasurements,
        ];
        foreach (EvidenceMeasurement measurement in fact.Measurements.Where(measurement =>
            recognized.Contains(measurement.Name)))
        {
            if (!measurements.TryAdd(measurement.Name, measurement.Value))
            {
                errors.Add(
                    $"Structural evidence fact '{fact.Id}' duplicates measurement '{measurement.Name}'.");
            }
        }

        if (!TryReadCount(fact.Id, measurements, StructuralEvidenceMeasurementNames.SourceFiles, errors, out int sourceFiles) ||
            !TryReadCount(fact.Id, measurements, StructuralEvidenceMeasurementNames.ParserBackedFiles, errors, out int parserFiles) ||
            !TryReadCount(fact.Id, measurements, StructuralEvidenceMeasurementNames.DetectedCallables, errors, out int detected) ||
            !TryReadCount(fact.Id, measurements, StructuralEvidenceMeasurementNames.MeasuredCallables, errors, out int measured) ||
            !TryReadRatio(fact.Id, measurements, StructuralEvidenceMeasurementNames.CallableMeasurementCoverage, errors, out decimal coverage) ||
            !TryReadRatio(fact.Id, measurements, StructuralEvidenceMeasurementNames.AnalyzerAmbiguityConcentration, errors, out decimal ambiguity))
        {
            return null;
        }

        if (sourceFiles <= 0 || parserFiles > sourceFiles || measured > detected)
        {
            errors.Add($"Structural evidence fact '{fact.Id}' has inconsistent file or callable counts.");
        }

        decimal expectedCoverage = detected == 0 ? 1m : Ratio(measured, detected);
        decimal expectedAmbiguity = Ratio(sourceFiles - parserFiles, sourceFiles);
        if (coverage != expectedCoverage || ambiguity != expectedAmbiguity)
        {
            errors.Add($"Structural evidence fact '{fact.Id}' has inconsistent coverage ratios.");
        }

        Dictionary<string, decimal> featureMeasurements = new(StringComparer.Ordinal)
        {
            [StructuralEvidenceMeasurementNames.CallableMeasurementCoverage] = coverage,
            [StructuralEvidenceMeasurementNames.AnalyzerAmbiguityConcentration] = ambiguity,
        };
        if (measured == 0)
        {
            if (DistributionMeasurements.Any(measurements.ContainsKey))
            {
                errors.Add(
                    $"Structural evidence fact '{fact.Id}' cannot emit a callable distribution without measured callables.");
            }

            return new CalibrationUncertaintyStructuralFact(
                fact,
                sourceFiles,
                parserFiles,
                detected,
                measured,
                featureMeasurements);
        }

        foreach (string name in DistributionMeasurements)
        {
            bool ratio = name.EndsWith("-share", StringComparison.Ordinal);
            decimal value;
            bool valid;
            if (ratio)
            {
                valid = TryReadRatio(fact.Id, measurements, name, errors, out value);
            }
            else
            {
                valid = TryReadCount(fact.Id, measurements, name, errors, out int count);
                value = count;
            }

            if (valid)
            {
                featureMeasurements[name] = value;
            }
        }

        ValidateOrder(
            fact.Id,
            featureMeasurements,
            StructuralEvidenceMeasurementNames.CallableSizeP50,
            StructuralEvidenceMeasurementNames.CallableSizeP90,
            StructuralEvidenceMeasurementNames.CallableSizeMaximum,
            minimum: 1m,
            errors);
        ValidateOrder(
            fact.Id,
            featureMeasurements,
            StructuralEvidenceMeasurementNames.DecisionComplexityP50,
            StructuralEvidenceMeasurementNames.DecisionComplexityP90,
            StructuralEvidenceMeasurementNames.DecisionComplexityMaximum,
            minimum: 1m,
            errors);
        ValidateOrder(
            fact.Id,
            featureMeasurements,
            StructuralEvidenceMeasurementNames.NestingDepthP50,
            StructuralEvidenceMeasurementNames.NestingDepthP90,
            StructuralEvidenceMeasurementNames.NestingDepthMaximum,
            minimum: 0m,
            errors);

        return new CalibrationUncertaintyStructuralFact(
            fact,
            sourceFiles,
            parserFiles,
            detected,
            measured,
            featureMeasurements);
    }

    private static bool TryReadCount(
        string factId,
        Dictionary<string, decimal> measurements,
        string name,
        List<string> errors,
        out int value)
    {
        value = 0;
        if (!measurements.TryGetValue(name, out decimal raw))
        {
            errors.Add($"Structural evidence fact '{factId}' is missing measurement '{name}'.");
            return false;
        }

        if (raw < 0m || raw > int.MaxValue || decimal.Truncate(raw) != raw)
        {
            errors.Add($"Structural evidence fact '{factId}' measurement '{name}' must be a nonnegative integer.");
            return false;
        }

        value = (int)raw;
        return true;
    }

    private static bool TryReadRatio(
        string factId,
        Dictionary<string, decimal> measurements,
        string name,
        List<string> errors,
        out decimal value)
    {
        if (!measurements.TryGetValue(name, out value))
        {
            errors.Add($"Structural evidence fact '{factId}' is missing measurement '{name}'.");
            return false;
        }

        if (value is < 0m or > 1m)
        {
            errors.Add($"Structural evidence fact '{factId}' measurement '{name}' must be between zero and one.");
            return false;
        }

        return true;
    }

    private static void ValidateOrder(
        string factId,
        Dictionary<string, decimal> measurements,
        string p50Name,
        string p90Name,
        string maximumName,
        decimal minimum,
        List<string> errors)
    {
        if (!measurements.TryGetValue(p50Name, out decimal p50) ||
            !measurements.TryGetValue(p90Name, out decimal p90) ||
            !measurements.TryGetValue(maximumName, out decimal maximum))
        {
            return;
        }

        if (p50 < minimum || p50 > p90 || p90 > maximum)
        {
            errors.Add(
                $"Structural evidence fact '{factId}' distribution '{p50Name}'/'{p90Name}'/'{maximumName}' is inconsistent.");
        }
    }

    private static decimal Ratio(int numerator, int denominator) =>
        decimal.Round(numerator / (decimal)denominator, 6, MidpointRounding.AwayFromZero);
}
