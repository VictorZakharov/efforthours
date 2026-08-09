using EffortHours.Contracts.V1;

namespace EffortHours.Analysis;

internal static class CoverageEvidence
{
    public static EvidenceFact? CreateFact(
        string reportPath,
        CoverageReportData report,
        CoverageProductionScope scope,
        CoverageCounters counters,
        CoveragePercentages? directPercentages,
        bool inferredSingleScope)
    {
        List<EvidenceMeasurement> measurements = [];
        AddMetric(measurements, "lines", counters.LinesCovered, counters.LinesTotal);
        AddMetric(measurements, "branches", counters.BranchesCovered, counters.BranchesTotal);
        AddMetric(measurements, "functions", counters.FunctionsCovered, counters.FunctionsTotal);
        AddDirectPercentage(measurements, "lines", directPercentages?.Lines);
        AddDirectPercentage(measurements, "branches", directPercentages?.Branches);
        AddDirectPercentage(measurements, "functions", directPercentages?.Functions);
        if (!measurements.Any(measurement => measurement.Unit == "percent"))
        {
            return null;
        }

        List<string> tags =
        [
            "coverage:measured",
            $"coverage-format:{report.Format}",
            $"ecosystem:{scope.Ecosystem}",
        ];
        if (inferredSingleScope)
        {
            tags.Add("coverage-scope:single-production-inferred");
        }

        return new EvidenceFact
        {
            Id = $"coverage:measured:{report.Format}:{reportPath}:{scope.Key}",
            Kind = EvidenceKinds.Coverage,
            Scope = scope.Scope,
            Summary = $"Measured {report.Format.ToUpperInvariant()} coverage for '{scope.Scope}' from '{reportPath}'.",
            Provenance = new EvidenceProvenance
            {
                SourceKind = EvidenceSourceKind.Measured,
                Analyzer = CoverageReportAnalyzer.AnalyzerName,
                AnalyzerVersion = CoverageReportAnalyzer.AnalyzerVersion,
                Method = "bounded static coverage-report parsing with inventory digest verification",
            },
            Locations = [new EvidenceLocation { Path = reportPath }],
            Measurements = [.. measurements.OrderBy(measurement => measurement.Name, StringComparer.Ordinal)],
            Tags = [.. tags.OrderBy(tag => tag, StringComparer.Ordinal)],
        };
    }

    public static Diagnostic Diagnostic(
        string code,
        DiagnosticSeverity severity,
        string message,
        string? path = null) => new()
        {
            Code = code,
            Severity = severity,
            Message = message,
            Locations = path is null ? [] : [new EvidenceLocation { Path = path }],
        };

    public static int CompareDiagnostics(Diagnostic left, Diagnostic right)
    {
        int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
        if (code != 0)
        {
            return code;
        }

        string leftPath = left.Locations.Count == 0 ? string.Empty : left.Locations[0].Path;
        string rightPath = right.Locations.Count == 0 ? string.Empty : right.Locations[0].Path;
        return StringComparer.Ordinal.Compare(leftPath, rightPath);
    }

    private static void AddMetric(
        List<EvidenceMeasurement> measurements,
        string name,
        decimal covered,
        decimal total)
    {
        if (total <= 0m || covered < 0m || covered > total)
        {
            return;
        }

        measurements.Add(Measurement($"{name}-covered", covered, name));
        measurements.Add(Measurement($"{name}-total", total, name));
        measurements.Add(Measurement(
            name,
            decimal.Round(covered * 100m / total, 4, MidpointRounding.AwayFromZero),
            "percent"));
    }

    private static void AddDirectPercentage(
        List<EvidenceMeasurement> measurements,
        string name,
        decimal? value)
    {
        if (value is null || measurements.Any(measurement => measurement.Name == name))
        {
            return;
        }

        measurements.Add(Measurement(name, value.Value, "percent"));
    }

    private static EvidenceMeasurement Measurement(string name, decimal value, string unit) => new()
    {
        Name = name,
        Value = value,
        Unit = unit,
    };
}
