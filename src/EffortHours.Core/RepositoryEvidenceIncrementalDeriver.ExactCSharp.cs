using EffortHours.Analysis;
using EffortHours.Analyzers.DotNet;
using EffortHours.Contracts.V1;

namespace EffortHours.Core;

internal static partial class RepositoryEvidenceIncrementalDeriver
{
    private static async Task<bool> TryAdvanceExactCSharpAsync(RepositoryEvidence repository, EvidenceFact previous,
        IRepositoryFileSystem fileSystem, string rootPath, string fullPath, string relativePath,
        byte[] bytes, CancellationToken cancellationToken)
    {
        if (bytes.Length > 64 * 1024 || !previous.Tags.Any(tag => tag is "role:source" or "role:test"))
            return false;
        string? contextIdentity = new DotNetProjectReader(fileSystem, rootPath)
            .GetImmutableContextIdentity(repository, cancellationToken);
        if (contextIdentity is null) return false;
        using IDisposable lease = await RepositoryAnalysisConcurrency.AcquireFileAnalysisAsync(
            RepositoryAnalysisWorkKind.SemanticFileAnalysis, cancellationToken).ConfigureAwait(false);
        EvidenceFact current = RepositoryScanner.InspectImmutableFileFact(relativePath, bytes, cancellationToken);
        // The original derivation already proves byte length and old/new digest
        // uniqueness. Every remaining common field must be exactly unchanged.
        if (!RepositoryEvidenceValueEquality.Fact(previous, current with { Tags = previous.Tags }) ||
            !previous.Tags.Where(tag => !tag.StartsWith("sha256:", StringComparison.Ordinal))
                .SequenceEqual(current.Tags.Where(tag => !tag.StartsWith("sha256:", StringComparison.Ordinal))))
            return false;
        return await CSharpEvidenceLineage.TryAdvanceExactEvidenceAsync(
            fileSystem, fullPath, relativePath, bytes, contextIdentity, cancellationToken).ConfigureAwait(false);
    }
}
