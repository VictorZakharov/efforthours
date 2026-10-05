using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.JavaScript;

internal sealed record FrontendAssetAnalysis(
    FrontendMarkupMetrics Markup, StylesheetMetrics Styles, Diagnostic? Diagnostic);

// Ownership and component references are resolved again for every full snapshot.
// This cache contains only bounded, digest-verified local markup/style metrics.
internal sealed class FrontendAssetAnalysisReader(RepositoryTextReader reader)
{
    private const long MaximumAssetBytes = 4 * 1024 * 1024;

    public async Task<FrontendAssetAnalysis> ReadAsync(EvidenceFact asset,
        CancellationToken cancellationToken)
    {
        if (asset.Measurements.FirstOrDefault(item => item.Name == "bytes")?.Value > MaximumAssetBytes)
            return await ReadUncachedAsync(asset, cancellationToken).ConfigureAwait(false);
        string? contentId = reader.ImmutableContentId(asset);
        string? digest = JavaScriptEvidence.FindTagValue(asset.Tags, "sha256:");
        string? language = JavaScriptEvidence.FindTagValue(asset.Tags, "language:");
        RepositoryAnalysisArtifactCache? cache = reader.AnalysisArtifactCache;
        if (cache is null || contentId is null || digest is null || language is null)
            return await ReadUncachedAsync(asset, cancellationToken).ConfigureAwait(false);
        string key = $"frontend-asset/{JavaScriptEvidence.AnalyzerVersion}/{contentId}/{digest}/{asset.Scope}/{language}";
        return await cache.GetOrCreateAsync(key,
            token => ReadUncachedAsync(asset, token), cancellationToken,
            "frontend-asset:" + asset.Scope, shouldRetain: result => result.Diagnostic is null).ConfigureAwait(false);
    }

    private async Task<FrontendAssetAnalysis> ReadUncachedAsync(EvidenceFact asset,
        CancellationToken cancellationToken)
    {
        RepositoryTextReadResult read = await reader.ReadAsync(asset, MaximumAssetBytes,
            "FB4201", cancellationToken).ConfigureAwait(false);
        if (read.Diagnostic is not null) return new(new(), new(), read.Diagnostic);
        using IDisposable cpuLease = await RepositoryAnalysisConcurrency.AcquireFileAnalysisAsync(
            RepositoryAnalysisWorkKind.SemanticFileAnalysis, cancellationToken).ConfigureAwait(false);
        string language = JavaScriptEvidence.FindTagValue(asset.Tags, "language:")!;
        return language == "html"
            ? new(FrontendMarkupAnalyzer.Analyze(read.Text!), new(), null)
            : new(new(), StylesheetAnalyzer.Analyze(read.Text!, language), null);
    }
}
