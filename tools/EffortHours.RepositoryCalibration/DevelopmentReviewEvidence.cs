using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal sealed class DevelopmentReviewEvidence
{
    private static readonly string[] SummaryMeasurements =
    [
        "files",
        "types",
        "methods",
        "functions",
        "branch-points",
        "test-methods",
        "test-cases",
        "assertions",
        "controllers",
        "attributed-endpoints",
        "endpoints",
        "components",
        "pages",
        "forms",
        "template-structure-units",
        "style-structure-units",
        "migrations",
        "data-calls",
        "integration-calls",
        "authorization-attributes",
        "accessibility-units",
        "validation-rules",
    ];

    private readonly Dictionary<string, EvidenceFact> facts;

    public DevelopmentReviewEvidence(RepositoryEvidence evidence)
    {
        facts = evidence.Facts.ToDictionary(fact => fact.Id, StringComparer.Ordinal);
        Repository = evidence.Repository;
    }

    public RepositoryDescriptor Repository { get; }

    public void RequireCompleteLineage(CalibrationAuthoringPacket packet)
    {
        string? missing = packet.Targets
            .SelectMany(target => target.EvidenceIds)
            .FirstOrDefault(id => !facts.ContainsKey(id));
        if (missing is not null)
        {
            throw new InvalidDataException($"Blind packet references missing evidence '{missing}'.");
        }

        if (!string.Equals(
                Repository.SourceDigest,
                packet.Repository.SourceDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("Blind packet and repository evidence source digests differ.");
        }
    }

    public decimal Sum(CalibrationAuthoringTarget target, string measurement) => target.EvidenceIds
        .Select(id => facts[id])
        .SelectMany(fact => fact.Measurements)
        .Where(item => item.Name == measurement)
        .Sum(item => item.Value);

    public bool HasKind(CalibrationAuthoringTarget target, string kind) => target.EvidenceIds
        .Select(id => facts[id])
        .Any(fact => fact.Kind == kind);

    public string Summarize(CalibrationAuthoringTarget target)
    {
        string[] measurements =
        [
            .. SummaryMeasurements
                .Select(name => (Name: name, Value: Sum(target, name)))
                .Where(item => item.Value > 0m)
                .Take(4)
                .Select(item => $"{item.Name}={item.Value}"),
        ];
        return measurements.Length == 0
            ? $"{target.EvidenceIds.Count} evidence fact(s)"
            : string.Join(", ", measurements);
    }
}
