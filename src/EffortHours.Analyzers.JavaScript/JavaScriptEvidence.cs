using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.JavaScript;

internal static class JavaScriptEvidence
{
    public const string AnalyzerName = "efforthours.javascript-analyzer";
    public const string AnalyzerVersion = "0.5.2";

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
            Locations = locations?.Distinct().OrderBy(location => location.Path, StringComparer.Ordinal)
                .ThenBy(location => location.Line ?? int.MaxValue)
                .ThenBy(location => location.Symbol, StringComparer.Ordinal)
                .Take(50)
                .ToArray() ?? [],
            Measurements = measurements?.OrderBy(measurement => measurement.Name, StringComparer.Ordinal)
                .ToArray() ?? [],
            Tags = tags?.Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray() ?? [],
        };

    public static EvidenceFact FactWithPrimaryLocation(
        string id,
        string kind,
        string scope,
        string summary,
        EvidenceSourceKind sourceKind,
        string method,
        EvidenceLocation primaryLocation,
        IEnumerable<EvidenceLocation>? relatedLocations = null,
        IEnumerable<EvidenceMeasurement>? measurements = null,
        IEnumerable<string>? tags = null)
    {
        EvidenceFact fact = Fact(
            id,
            kind,
            scope,
            summary,
            sourceKind,
            method,
            measurements: measurements,
            tags: tags);
        return fact with
        {
            Locations =
            [
                primaryLocation,
                .. (relatedLocations ?? [])
                    .Where(location => location != primaryLocation)
                    .Distinct()
                    .OrderBy(location => location.Path, StringComparer.Ordinal)
                    .ThenBy(location => location.Line ?? int.MaxValue)
                    .ThenBy(location => location.Symbol, StringComparer.Ordinal)
                    .Take(49),
            ],
        };
    }

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

    public static string IdToken(string value)
    {
        StringBuilder builder = new(value.Length);
        bool lastWasSeparator = false;
        foreach (char character in value.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' or '@')
            {
                builder.Append(character);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                builder.Append('~');
                lastWasSeparator = true;
            }
        }

        string token = builder.ToString().Trim('~');
        return token.Length > 0 ? token : StableToken(value);
    }

    public static string StableToken(string value)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(digest.AsSpan(0, 8)).ToLowerInvariant();
    }

    public static string? FindTagValue(IEnumerable<string> tags, string prefix) => tags
        .FirstOrDefault(tag => tag.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
}
