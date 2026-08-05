using Fairbill.Contracts.V1;

namespace Fairbill.Analyzers.DotNet;

internal static class DotNetEvidence
{
    public const string AnalyzerName = "fairbill.dotnet-analyzer";
    public const string AnalyzerVersion = "0.3.0";

    public static EvidenceFact Fact(
        string id,
        string kind,
        string scope,
        string summary,
        EvidenceSourceKind sourceKind,
        string method,
        IEnumerable<EvidenceLocation>? locations = null,
        IEnumerable<EvidenceMeasurement>? measurements = null,
        IEnumerable<string>? tags = null) => new()
        {
            Id = id,
            Kind = kind,
            Scope = scope,
            Summary = summary,
            Provenance = new EvidenceProvenance
            {
                SourceKind = sourceKind,
                Analyzer = AnalyzerName,
                AnalyzerVersion = AnalyzerVersion,
                Method = method,
            },
            Locations = locations?.ToArray() ?? [],
            Measurements = measurements?.ToArray() ?? [],
            Tags = tags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray() ?? [],
        };

    public static EvidenceMeasurement Measurement(string name, decimal value, string unit) => new()
    {
        Name = name,
        Value = value,
        Unit = unit,
    };

    public static EvidenceLocation Location(string path, int? line = null, string? symbol = null) => new()
    {
        Path = path,
        Line = line,
        Symbol = symbol,
    };

    public static Diagnostic Diagnostic(
        string code,
        DiagnosticSeverity severity,
        string message,
        string? path = null,
        int? line = null) => new()
        {
            Code = code,
            Severity = severity,
            Message = message,
            Locations = path is null ? [] : [Location(path, line)],
        };
}
