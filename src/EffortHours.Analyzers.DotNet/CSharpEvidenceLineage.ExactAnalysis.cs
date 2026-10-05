using EffortHours.Analysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace EffortHours.Analyzers.DotNet;

internal static partial class CSharpEvidenceLineage
{
    private static readonly IReadOnlyList<CallableStructuralMetric> EmptyCallableMetrics = [];
    internal static async Task<bool> TryAdvanceExactEvidenceAsync(
        IRepositoryFileSystem fileSystem, string fullPath, string relativePath,
        byte[] bytes, string immutableContextIdentity, CancellationToken cancellationToken)
    {
        if (bytes.Length > MaximumNeutralChangeCharacters) return false;
        string? contentId = fileSystem.GetFileMetadata(fullPath).ContentId;
        if (!TryGetChangedLineage(fileSystem, fullPath, contentId, out RepositoryVersionedAnalysisCache? cache,
            out RepositoryFileVersion version) ||
            !cache.TryGetCompleted(ArtifactKey(version.ContentId, relativePath), out CSharpEvidenceState previous) ||
            previous.SyntaxErrors != 0 || previous.Analysis is null || previous.ProjectScope is null ||
            !string.Equals(previous.ImmutableContextIdentity, immutableContextIdentity, StringComparison.Ordinal))
            return false;
        SourceText text = CreateSourceText(bytes);
        CSharpSyntaxTree tree = Parse(text, relativePath, cancellationToken);
        if (CountSyntaxErrors(tree, cancellationToken) != 0) return false;
        CSharpFileAnalysis analysis = CSharpFileAnalyzer.AnalyzeParsed(tree, 0,
            relativePath, previous.ProjectScope, previous.IsTestFile, cancellationToken);
        if (!Equivalent(previous.Analysis, analysis)) return false;
        _ = await cache.GetOrCreateAsync(ArtifactKey(contentId!, relativePath),
            _ => Task.FromResult(new RepositoryVersionedAnalysisArtifact<CSharpEvidenceState>(
                new(text, tree, 0, analysis, previous.ProjectScope, previous.IsTestFile, immutableContextIdentity), RetainedTextBytes(text))),
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    // Scheduling only. A positive hint still requires all repository/common and
    // exact local-context proofs above before any evidence or stock is reused.
    internal static bool IsSmallEquivalentLocalEdit(byte[] before, byte[] after,
        string path, CancellationToken cancellationToken)
    {
        if (before.Length != after.Length || before.Length > MaximumNeutralChangeCharacters) return false;
        CSharpSyntaxTree oldTree = Parse(CreateSourceText(before), path, cancellationToken);
        CSharpSyntaxTree newTree = Parse(CreateSourceText(after), path, cancellationToken);
        if (CountSyntaxErrors(oldTree, cancellationToken) != 0 ||
            CountSyntaxErrors(newTree, cancellationToken) != 0) return false;
        return Equivalent(CSharpFileAnalyzer.AnalyzeParsed(oldTree, 0, path, ".", false, cancellationToken),
            CSharpFileAnalyzer.AnalyzeParsed(newTree, 0, path, ".", false, cancellationToken));
    }

    private static bool Equivalent(CSharpFileAnalysis before, CSharpFileAnalysis after) =>
        before.Structure with { CallableStructuralMetrics = EmptyCallableMetrics } ==
        after.Structure with { CallableStructuralMetrics = EmptyCallableMetrics } &&
        before.Structure.CallableStructuralMetrics.SequenceEqual(after.Structure.CallableStructuralMetrics) &&
        before.Facts.Count == after.Facts.Count && before.Facts.Zip(after.Facts)
            .All(pair => RepositoryEvidenceValueEquality.Fact(pair.First, pair.Second)) &&
        before.Diagnostics.Count == after.Diagnostics.Count && before.Diagnostics.Zip(after.Diagnostics)
            .All(pair => RepositoryEvidenceValueEquality.Diagnostic(pair.First, pair.Second));
}
