using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal sealed class LogicalCandidateEvidenceNormalizer
{
    private readonly IReadOnlyList<FileIdentity> _files;
    private readonly Dictionary<string, string> _hashByPath;

    private LogicalCandidateEvidenceNormalizer(IReadOnlyList<FileIdentity> files)
    {
        _files = files;
        _hashByPath = files.ToDictionary(file => file.Path, file => file.Hash, StringComparer.Ordinal);
    }

    public static LogicalCandidateEvidenceNormalizer Create(IEnumerable<EvidenceFact> facts)
    {
        FileIdentity[] files =
        [
            .. facts.Where(fact => fact.Kind == "file")
                .Select(FileIdentity.Create)
                .Where(file => file is not null)
                .Cast<FileIdentity>()
                .OrderBy(file => file.Path, StringComparer.Ordinal),
        ];
        return new LogicalCandidateEvidenceNormalizer(files);
    }

    public LogicalCandidateNormalizedEvidence Accumulate(
        IReadOnlyList<string> evidenceIds,
        IReadOnlyDictionary<string, EvidenceFact> facts,
        string capabilityKind,
        string scope,
        bool normalize,
        CancellationToken cancellationToken)
    {
        Dictionary<string, decimal> measurements = [];
        HashSet<string> admittedContent = new(StringComparer.Ordinal);
        int admittedCount = 0;
        foreach (string evidenceId in evidenceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!facts.TryGetValue(evidenceId, out EvidenceFact? fact))
            {
                throw new InvalidDataException(
                    $"Logical capability references missing evidence '{evidenceId}'.");
            }

            string? contentKey = normalize ? ContentKey(fact) : null;
            if (contentKey is not null && !admittedContent.Add(contentKey))
            {
                continue;
            }

            admittedCount++;
            decimal factor = normalize
                ? AggregateUniquenessFactor(fact, capabilityKind, scope)
                : 1m;
            foreach (EvidenceMeasurement measurement in fact.Measurements)
            {
                measurements[measurement.Name] =
                    measurements.GetValueOrDefault(measurement.Name) + measurement.Value * factor;
            }
        }

        return new LogicalCandidateNormalizedEvidence(measurements, admittedCount);
    }

    private string? ContentKey(EvidenceFact fact)
    {
        string? path = fact.Locations.Select(location => Normalize(location.Path)).FirstOrDefault(
            _hashByPath.ContainsKey);
        return path is null
            ? null
            : $"{fact.Provenance.Analyzer}|{fact.Kind}|{_hashByPath[path]}";
    }

    private decimal AggregateUniquenessFactor(
        EvidenceFact fact,
        string capabilityKind,
        string scope)
    {
        if (fact.Kind != "source-structure" || !TryLanguage(capabilityKind, out string? language))
        {
            return 1m;
        }

        decimal reportedFiles = fact.Measurements
            .FirstOrDefault(measurement => measurement.Name == "files")?.Value ?? 0m;
        FileIdentity[] matching =
        [
            .. _files.Where(file =>
                file.IsSource &&
                file.Languages.Contains(language) &&
                IsWithinScope(file.Path, scope)),
        ];
        if (reportedFiles <= 0m || matching.Length != reportedFiles)
        {
            return 1m;
        }

        int unique = matching.Select(file => file.Hash).Distinct(StringComparer.Ordinal).Count();
        return unique == matching.Length ? 1m : (decimal)unique / matching.Length;
    }

    private static bool TryLanguage(string capabilityKind, out string language)
    {
        language = capabilityKind switch
        {
            "dotnet-source-backbone" => "csharp",
            "javascript-source-backbone" => "javascript-typescript",
            _ => string.Empty,
        };
        return language.Length > 0;
    }

    private static bool IsWithinScope(string path, string scope)
    {
        string normalized = Normalize(scope);
        string root = Path.HasExtension(normalized)
            ? Normalize(Path.GetDirectoryName(normalized) ?? ".")
            : normalized;
        return root is "." or "" ||
               path.StartsWith($"{root.TrimEnd('/')}/", StringComparison.Ordinal);
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/').TrimStart('.', '/');

    private sealed record FileIdentity(
        string Path,
        string Hash,
        bool IsSource,
        IReadOnlySet<string> Languages)
    {
        public static FileIdentity? Create(EvidenceFact fact)
        {
            string? hash = fact.Tags.FirstOrDefault(tag =>
                tag.Length == 71 && tag.StartsWith("sha256:", StringComparison.Ordinal));
            string? path = fact.Locations.Select(location => Normalize(location.Path)).FirstOrDefault();
            if (hash is null || path is null)
            {
                return null;
            }

            HashSet<string> languages = new(StringComparer.Ordinal);
            foreach (string tag in fact.Tags.Where(tag => tag.StartsWith("language:", StringComparison.Ordinal)))
            {
                string value = tag["language:".Length..];
                languages.Add(value is "javascript" or "typescript"
                    ? "javascript-typescript"
                    : value);
            }

            return new FileIdentity(
                path,
                hash,
                fact.Tags.Contains("role:source", StringComparer.Ordinal),
                languages);
        }
    }
}

internal sealed record LogicalCandidateNormalizedEvidence(
    IReadOnlyDictionary<string, decimal> Measurements,
    int EvidenceCount);
