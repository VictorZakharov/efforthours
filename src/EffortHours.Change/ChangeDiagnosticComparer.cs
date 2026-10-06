using EffortHours.Contracts.V1;

namespace EffortHours.Change;

// Diagnostic records contain collection properties. Record equality compares those
// collection instances, which varies between cold scans and shared cached evidence.
internal sealed class ChangeDiagnosticComparer : IEqualityComparer<Diagnostic>
{
    public static ChangeDiagnosticComparer Instance { get; } = new();

    public bool Equals(Diagnostic? first, Diagnostic? second) => ReferenceEquals(first, second) ||
        first is not null && second is not null && first.Code == second.Code &&
        first.Severity == second.Severity && first.Message == second.Message &&
        first.EvidenceIds.SequenceEqual(second.EvidenceIds, StringComparer.Ordinal) &&
        first.Locations.SequenceEqual(second.Locations);

    public int GetHashCode(Diagnostic diagnostic)
    {
        HashCode hash = new();
        hash.Add(diagnostic.Code, StringComparer.Ordinal);
        hash.Add(diagnostic.Severity);
        hash.Add(diagnostic.Message, StringComparer.Ordinal);
        foreach (string id in diagnostic.EvidenceIds) hash.Add(id, StringComparer.Ordinal);
        foreach (EvidenceLocation location in diagnostic.Locations) hash.Add(location);
        return hash.ToHashCode();
    }
}
