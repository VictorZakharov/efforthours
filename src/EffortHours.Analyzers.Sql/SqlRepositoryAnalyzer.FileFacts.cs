using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;
using static EffortHours.Analyzers.Sql.SqlFactFactory;

namespace EffortHours.Analyzers.Sql;

public sealed partial class SqlRepositoryAnalyzer
{
    private sealed record SqlAnalyzedFileFacts(IReadOnlyList<EvidenceFact> Facts,
        IReadOnlyList<Diagnostic> Diagnostics, bool Excluded);

    private async Task<SqlAnalyzedFileFacts> ReadAnalyzedFileFactsAsync(EvidenceFact file,
        SqlScopeOwnership ownership, SqlFileAnalysis read, bool exactDuplicate,
        string? canonicalPath, CancellationToken cancellationToken)
    {
        RepositoryAnalysisArtifactCache? cache =
            (_fileSystem as IRepositoryAnalysisArtifactCacheProvider)?.AnalysisArtifactCache;
        if (cache is null || read.ImmutableCacheKey is null)
            return CreateAnalyzedFileFacts(file, ownership, read.Analysis!, read.Role!, exactDuplicate, canonicalPath);
        // Resolve current global ownership and duplicate canonical path before
        // requesting any local fact reuse; neither may be inferred from a cache hit.
        string identity = string.Join('\0', read.ImmutableCacheKey, ownership.Scope, ownership.Ecosystem,
            ownership.Standalone.ToString(), ownership.Ambiguous.ToString(), exactDuplicate.ToString(), canonicalPath);
        string key = "sql-file-facts/" + SqlEvidence.AnalyzerVersion + "/" +
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return await cache.GetOrCreateAsync(key, _ => Task.FromResult(CreateAnalyzedFileFacts(
            file, ownership, read.Analysis!, read.Role!, exactDuplicate, canonicalPath)), cancellationToken,
            "sql-file-facts:" + file.Scope).ConfigureAwait(false);
    }

    private static SqlAnalyzedFileFacts CreateAnalyzedFileFacts(EvidenceFact fileFact,
        SqlScopeOwnership ownership, SqlSemanticAnalysis analysis, SqlArtifactRoleAssessment role,
        bool exactDuplicate, string? canonicalPath)
    {
        List<EvidenceFact> facts = [];
        List<Diagnostic> diagnostics = [];
        EvidenceFact artifact = CreateArtifactFact(
            fileFact,
            ownership,
            analysis,
            role,
            exactDuplicate);
        facts.Add(artifact);

        if (ownership.Ambiguous)
        {
            diagnostics.Add(SqlEvidence.Diagnostic(
                "FB6005",
                DiagnosticSeverity.Information,
                $"SQL file '{fileFact.Scope}' has ambiguous project/package ownership and remains in the standalone SQL scope.",
                fileFact.Scope));
        }

        if (role.Excluded)
        {
            facts.Add(CreateDumpExclusion(fileFact, ownership, artifact));
            return new(facts, diagnostics, true);
        }

        if (exactDuplicate)
        {
            facts.Add(CreateDuplicateExclusion(
                fileFact,
                ownership,
                artifact,
                canonicalPath!));
            return new(facts, diagnostics, true);
        }

        if (role.Role == "test-fixture")
        {
            facts.Add(CreateTestFact(fileFact, ownership, analysis, artifact));
        }
        else if (role.Role == "delivery")
        {
            facts.Add(CreateDeliveryFact(fileFact, ownership, analysis, artifact));
        }
        else if (analysis.Metrics.HasDataSemantics ||
            role.Role is "migration" or "seed-data")
        {
            facts.Add(CreateDataFact(fileFact, ownership, analysis, role, artifact));
        }

        if (analysis.Metrics.IntegrationSignals.Count > 0)
        {
            facts.Add(CreateIntegrationFact(fileFact, ownership, analysis, artifact));
        }

        AddAnalysisDiagnostics(fileFact, analysis, role, diagnostics);
        return new(facts, diagnostics, false);
    }
}
