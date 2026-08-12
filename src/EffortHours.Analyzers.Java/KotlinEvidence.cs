using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Java;

internal static class KotlinEvidence
{
    public const string AnalyzerName = "efforthours.kotlin-analyzer";
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

    public static EvidenceLocation Location(string path, int? line = null) => new()
    {
        Path = path,
        Line = line,
    };

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

    public static string IdToken(string value)
    {
        StringBuilder builder = new(value.Length);
        bool separated = false;
        foreach (char character in value.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')
            {
                builder.Append(character);
                separated = false;
            }
            else if (!separated)
            {
                builder.Append('~');
                separated = true;
            }
        }

        string token = builder.ToString().Trim('~');
        if (token.Length is > 0 and <= 160) return token;
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(digest.AsSpan(0, 10)).ToLowerInvariant();
    }
}
