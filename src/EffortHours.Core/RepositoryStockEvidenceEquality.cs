using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Core;

// Seed stock never reads raw file hashes except to establish exact duplicate
// groups. Fully analyzed snapshots may share stock only if changed hashes remain
// singleton groups and every other input is exactly equal.
internal static class RepositoryStockEvidenceEquality
{
    private const int MaximumChangedDigests = 64;
    private static readonly IReadOnlyList<string> EmptyStrings = [];
    private static readonly IReadOnlyList<EvidenceFact> EmptyFacts = [];
    private static readonly IReadOnlyList<Diagnostic> EmptyDiagnostics = [];

    internal static bool Equivalent(RepositoryEvidence before, RepositoryEvidence after,
        CancellationToken cancellationToken)
    {
        if (before with { Repository = after.Repository, Facts = EmptyFacts, Diagnostics = EmptyDiagnostics } !=
            after with { Facts = EmptyFacts, Diagnostics = EmptyDiagnostics } ||
            before.Repository with { SourceDigest = after.Repository.SourceDigest, Ecosystems = EmptyStrings } !=
            after.Repository with { Ecosystems = EmptyStrings } ||
            !before.Repository.Ecosystems.SequenceEqual(after.Repository.Ecosystems) ||
            before.Facts.Count != after.Facts.Count) return false;
        Diagnostic[] oldDiagnostics = [.. before.Diagnostics.Where(item => item.Code != "FB5205")];
        Diagnostic[] newDiagnostics = [.. after.Diagnostics.Where(item => item.Code != "FB5205")];
        if (oldDiagnostics.Length != newDiagnostics.Length || !oldDiagnostics.Zip(newDiagnostics)
            .All(pair => RepositoryEvidenceValueEquality.Diagnostic(pair.First, pair.Second))) return false;
        HashSet<string> oldHashes = new(StringComparer.Ordinal);
        HashSet<string> newHashes = new(StringComparer.Ordinal);
        for (int index = 0; index < before.Facts.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EvidenceFact oldFact = before.Facts[index], newFact = after.Facts[index];
            if (ReferenceEquals(oldFact, newFact) || RepositoryEvidenceValueEquality.Fact(oldFact, newFact)) continue;
            if (oldFact.Kind != EvidenceKinds.File || newFact.Kind != EvidenceKinds.File ||
                !RepositoryEvidenceValueEquality.Fact(oldFact, newFact with { Tags = oldFact.Tags }) ||
                !oldFact.Tags.Where(tag => !tag.StartsWith("sha256:", StringComparison.Ordinal))
                    .SequenceEqual(newFact.Tags.Where(tag => !tag.StartsWith("sha256:", StringComparison.Ordinal))))
                return false;
            string? oldHash = Digest(oldFact), newHash = Digest(newFact);
            if (oldHash is null || newHash is null || oldHash == newHash ||
                !oldHashes.Add(oldHash) || !newHashes.Add(newHash) || oldHashes.Count > MaximumChangedDigests)
                return false;
        }
        return SingletonHashes(before, oldHashes, cancellationToken) &&
            SingletonHashes(after, newHashes, cancellationToken);
    }

    private static string? Digest(EvidenceFact fact)
    {
        string[] hashes = [.. fact.Tags.Where(tag => tag.StartsWith("sha256:", StringComparison.Ordinal))];
        return hashes.Length == 1 && hashes[0].Length == 71 && hashes[0].AsSpan(7).ToArray().All(Uri.IsHexDigit)
            ? hashes[0] : null;
    }

    internal static bool SingletonHashes(RepositoryEvidence evidence, HashSet<string> changed,
        CancellationToken cancellationToken)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (EvidenceFact fact in evidence.Facts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (fact.Kind != EvidenceKinds.File) continue;
            foreach (string tag in fact.Tags)
                if (changed.Contains(tag) && !seen.Add(tag)) return false;
        }
        return seen.Count == changed.Count;
    }
}
