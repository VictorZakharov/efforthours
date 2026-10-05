using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Sql;

internal sealed record SqlFileAnalysis(SqlSemanticAnalysis? Analysis,
    SqlArtifactRoleAssessment? Role, Diagnostic? Diagnostic)
{
    public string? ImmutableCacheKey { get; init; }
}

// Only local syntax/role evidence is cached. Ownership, duplicate canonical paths,
// exclusions and repository totals are recomputed against the current full input.
internal sealed class SqlFileAnalysisReader(IRepositoryFileSystem fileSystem, string rootPath)
{
    private readonly SqlTextReader _reader = new(fileSystem, rootPath);

    public async Task<SqlFileAnalysis> ReadAsync(EvidenceFact file,
        CancellationToken cancellationToken)
    {
        RepositoryAnalysisArtifactCache? cache =
            (fileSystem as IRepositoryAnalysisArtifactCacheProvider)?.AnalysisArtifactCache;
        string? digest = SqlEvidence.TagValue(file.Tags, "sha256:");
        if (file.Measurements.Where(item => item.Name == "bytes").Sum(item => item.Value) > SqlTextReader.MaximumBytes)
            return await ReadUncachedAsync(file, cancellationToken).ConfigureAwait(false);
        string? contentId = _reader.ImmutableContentId(file);
        if (cache is null || digest is null || contentId is null)
            return await ReadUncachedAsync(file, cancellationToken).ConfigureAwait(false);
        bool test = file.Tags.Contains("classification:test", StringComparer.Ordinal);
        string key = $"sql-source/{SqlEvidence.AnalyzerVersion}/{contentId}/{digest}/{file.Scope}/test:{test}";
        return await cache.GetOrCreateAsync(key,
            async token => (await ReadUncachedAsync(file, token).ConfigureAwait(false)) with { ImmutableCacheKey = key }, cancellationToken,
            "sql-source:" + file.Scope, shouldRetain: result => result.Diagnostic is null).ConfigureAwait(false);
    }

    private async Task<SqlFileAnalysis> ReadUncachedAsync(EvidenceFact file,
        CancellationToken cancellationToken)
    {
        SqlTextReadResult read = await _reader.ReadAsync(file, cancellationToken).ConfigureAwait(false);
        if (read.Diagnostic is not null) return new(null, null, read.Diagnostic);
        using IDisposable cpuLease = await RepositoryAnalysisConcurrency.AcquireFileAnalysisAsync(
            RepositoryAnalysisWorkKind.SemanticFileAnalysis, cancellationToken).ConfigureAwait(false);
        SqlSemanticAnalysis analysis = SqlSemanticAnalyzer.Analyze(read.Text!);
        return new(analysis, SqlArtifactClassifier.Classify(file, read.Text!, analysis.Metrics), null);
    }
}
