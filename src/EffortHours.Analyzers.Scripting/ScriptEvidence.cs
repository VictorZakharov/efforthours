using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Scripting;

internal static class ScriptEvidence
{
    public const string AnalyzerName = "efforthours.scripting-analyzer";
    public const string AnalyzerVersion = "0.1.0";

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
            Locations = locations?
                .Distinct()
                .OrderBy(location => location.Path, StringComparer.Ordinal)
                .ThenBy(location => location.Line ?? int.MaxValue)
                .Take(50)
                .ToArray() ?? [],
            Measurements = measurements?
                .OrderBy(measurement => measurement.Name, StringComparer.Ordinal)
                .ToArray() ?? [],
            Tags = tags?
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray() ?? [],
        };

    public static EvidenceMeasurement Measurement(string name, decimal value, string unit) => new()
    {
        Name = name,
        Value = value,
        Unit = unit,
    };

    public static EvidenceLocation Location(string path) => new() { Path = path };

    public static Diagnostic Diagnostic(
        string code,
        DiagnosticSeverity severity,
        string message,
        string? path = null) => new()
        {
            Code = code,
            Severity = severity,
            Message = message,
            Locations = path is null ? [] : [Location(path)],
        };

    public static string? TagValue(IEnumerable<string> tags, string prefix) => tags
        .FirstOrDefault(tag => tag.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
}
