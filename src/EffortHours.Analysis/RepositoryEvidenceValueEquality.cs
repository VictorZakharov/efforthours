using EffortHours.Contracts.V1;

namespace EffortHours.Analysis;

// Compare raw values, including UTF-16 strings, without a lossy serialization
// round trip. Record equality covers every scalar and any future added field;
// known collection fields are compared element by element in their exact order.
internal static class RepositoryEvidenceValueEquality
{
    private static readonly IReadOnlyList<EvidenceLocation> EmptyLocations = [];
    private static readonly IReadOnlyList<EvidenceMeasurement> EmptyMeasurements = [];
    private static readonly IReadOnlyList<string> EmptyStrings = [];
    internal static bool Fact(EvidenceFact left, EvidenceFact right) =>
        left with
        {
            Locations = EmptyLocations,
            Measurements = EmptyMeasurements,
            Tags = EmptyStrings
        } ==
        right with
        {
            Locations = EmptyLocations,
            Measurements = EmptyMeasurements,
            Tags = EmptyStrings
        } &&
        left.Locations.SequenceEqual(right.Locations) &&
        left.Measurements.SequenceEqual(right.Measurements) && left.Tags.SequenceEqual(right.Tags);

    internal static bool Diagnostic(Diagnostic left, Diagnostic right) =>
        left with { Locations = EmptyLocations, EvidenceIds = EmptyStrings } ==
        right with { Locations = EmptyLocations, EvidenceIds = EmptyStrings } &&
        left.Locations.SequenceEqual(right.Locations) && left.EvidenceIds.SequenceEqual(right.EvidenceIds);
}
