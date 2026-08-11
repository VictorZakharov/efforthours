using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Sql;

internal static class SqlFactFactory
{
    public static EvidenceFact CreateRepositoryFact(
        EvidenceFact[] files,
        IReadOnlyList<EvidenceFact> facts,
        int analyzed,
        int excluded,
        int standalone)
    {
        List<string> tags = ["ecosystem:sql", "parser:bounded-token-stream"];
        if (standalone > 0)
        {
            tags.Add("scope:standalone");
        }

        tags.AddRange(facts
            .Where(fact => fact.Kind == EvidenceKinds.SqlArtifact)
            .SelectMany(fact => fact.Tags)
            .Where(tag => tag.StartsWith("dialect:", StringComparison.Ordinal)));
        return SqlEvidence.Fact(
            "sql:repository",
            EvidenceKinds.SqlRepository,
            ".",
            "Bounded static SQL schema, migration, stored-program, and query inventory.",
            EvidenceSourceKind.Observed,
            "common-scanner-admitted SQL files with bounded token and statement analysis",
            files.Select(file => SqlEvidence.Location(file.Scope)),
            [
                SqlEvidence.Measurement("sql-files", files.Length, "files"),
                SqlEvidence.Measurement("analyzed-files", analyzed, "files"),
                SqlEvidence.Measurement("excluded-or-skipped-files", excluded, "files"),
                SqlEvidence.Measurement("standalone-scope-files", standalone, "files"),
            ],
            tags);
    }

    public static EvidenceFact CreateArtifactFact(
        EvidenceFact file,
        SqlScopeOwnership ownership,
        SqlSemanticAnalysis analysis,
        SqlArtifactRoleAssessment role,
        bool exactDuplicate)
    {
        SqlSemanticMetrics metrics = analysis.Metrics;
        List<string> tags = CommonTags(ownership, analysis, role);
        AddSurfaceTags(tags, metrics);
        if (exactDuplicate)
        {
            tags.Add("normalization:exact-content-duplicate");
        }

        return SqlEvidence.Fact(
            $"sql:artifact:{SqlEvidence.IdToken(file.Scope)}",
            EvidenceKinds.SqlArtifact,
            ownership.Scope,
            $"Static SQL {role.Role.Replace('-', ' ')} analysis for '{file.Scope}'.",
            EvidenceSourceKind.Measured,
            "bounded comment/string-aware tokenization and conservative statement-pattern analysis",
            [SqlEvidence.Location(file.Scope, analysis.FirstSemanticLine)],
            RawMeasurements(metrics),
            tags);
    }

    public static EvidenceFact CreateDataFact(
        EvidenceFact file,
        SqlScopeOwnership ownership,
        SqlSemanticAnalysis analysis,
        SqlArtifactRoleAssessment role,
        EvidenceFact artifact)
    {
        SqlSemanticMetrics metrics = analysis.Metrics;
        int dataCalls = role.BoundedSeedData ? 1 : metrics.QueryComplexityUnits;
        List<EvidenceMeasurement> measurements =
        [
            SqlEvidence.Measurement("db-sets", metrics.Tables, "surfaces"),
            SqlEvidence.Measurement("migrations", role.Role == "migration" ? 1 : 0, "files"),
            SqlEvidence.Measurement("entity-configurations", metrics.SchemaConfigurationUnits, "units"),
            SqlEvidence.Measurement("repository-types", metrics.StoredPrograms + metrics.Triggers, "programs"),
            SqlEvidence.Measurement("data-calls", dataCalls, "units"),
            .. RawMeasurements(metrics),
        ];
        List<string> tags = [.. artifact.Tags, "sql-ehe-mapping:existing-data-priors"];
        if (role.BoundedSeedData)
        {
            tags.Add("bulk-row-volume:not-valued");
        }

        return SqlEvidence.Fact(
            $"sql:data:{SqlEvidence.IdToken(file.Scope)}",
            EvidenceKinds.DataAccess,
            ownership.Scope,
            $"Maintained SQL {role.Role.Replace('-', ' ')} semantics in '{file.Scope}'.",
            EvidenceSourceKind.Inferred,
            "recognized SQL schema, program, query, migration, and bounded seed-data units mapped to existing data priors",
            [SqlEvidence.Location(file.Scope, analysis.FirstSemanticLine)],
            measurements,
            tags);
    }

    public static EvidenceFact CreateTestFact(
        EvidenceFact file,
        SqlScopeOwnership ownership,
        SqlSemanticAnalysis analysis,
        EvidenceFact artifact)
    {
        int cases = Math.Clamp(analysis.Metrics.Statements, 1, 50);
        return SqlEvidence.Fact(
            $"sql:test:{SqlEvidence.IdToken(file.Scope)}",
            EvidenceKinds.SqlTest,
            ownership.Scope,
            $"Maintained SQL integration-test or fixture behavior in '{file.Scope}'.",
            EvidenceSourceKind.Inferred,
            "unambiguous test/fixture role with bounded SQL statement evidence",
            [SqlEvidence.Location(file.Scope, analysis.FirstSemanticLine)],
            [
                SqlEvidence.Measurement("test-methods", cases, "cases"),
                SqlEvidence.Measurement("test-cases", cases, "cases"),
                SqlEvidence.Measurement("assertions", 0, "assertions"),
            ],
            [.. artifact.Tags, "test-type:integration", "bulk-row-volume:not-valued"]);
    }

    public static EvidenceFact CreateDeliveryFact(
        EvidenceFact file,
        SqlScopeOwnership ownership,
        SqlSemanticAnalysis analysis,
        EvidenceFact artifact) => SqlEvidence.Fact(
            $"sql:delivery:{SqlEvidence.IdToken(file.Scope)}",
            EvidenceKinds.SqlDelivery,
            ownership.Scope,
            $"Maintained SQL deployment or installation surface in '{file.Scope}'.",
            EvidenceSourceKind.Inferred,
            "unambiguous deployment/install path convention with bounded SQL analysis",
            [SqlEvidence.Location(file.Scope, analysis.FirstSemanticLine)],
            [SqlEvidence.Measurement("release-configurations", 1, "files")],
            artifact.Tags);

    public static EvidenceFact CreateIntegrationFact(
        EvidenceFact file,
        SqlScopeOwnership ownership,
        SqlSemanticAnalysis analysis,
        EvidenceFact artifact) => SqlEvidence.Fact(
            $"sql:integration:{SqlEvidence.IdToken(file.Scope)}",
            EvidenceKinds.Integration,
            ownership.Scope,
            $"SQL cross-database integration semantics in '{file.Scope}'.",
            EvidenceSourceKind.Inferred,
            "explicit foreign-data, linked-query, attached-database, or federated-engine syntax",
            [SqlEvidence.Location(file.Scope, analysis.FirstSemanticLine)],
            [
                SqlEvidence.Measurement("integration-calls", analysis.Metrics.IntegrationSignals.Count, "boundaries"),
            ],
            [
                .. artifact.Tags,
                .. analysis.Metrics.IntegrationSignals.Select(signal => $"technology:sql-{signal}"),
            ]);

    public static EvidenceFact CreateDumpExclusion(
        EvidenceFact file,
        SqlScopeOwnership ownership,
        EvidenceFact artifact) => SqlEvidence.Fact(
            $"sql:excluded:{SqlEvidence.IdToken(file.Scope)}",
            EvidenceKinds.ExcludedContent,
            ownership.Scope,
            $"Bulk SQL dump '{file.Scope}' is retained as metadata but not valued as maintained schema/query logic.",
            EvidenceSourceKind.Inferred,
            "strong dump path/header/bulk-data convention",
            [SqlEvidence.Location(file.Scope)],
            tags: [.. artifact.Tags, "classification:sql-dump"]);

    public static EvidenceFact CreateDuplicateExclusion(
        EvidenceFact file,
        SqlScopeOwnership ownership,
        EvidenceFact artifact,
        string canonicalPath) => SqlEvidence.Fact(
            $"sql:excluded:{SqlEvidence.IdToken(file.Scope)}",
            EvidenceKinds.ExcludedContent,
            ownership.Scope,
            $"Byte-identical SQL file '{file.Scope}' remains traceable but does not add semantic effort.",
            EvidenceSourceKind.Inferred,
            "exact maintained-file content digest normalization",
            [SqlEvidence.Location(file.Scope), SqlEvidence.Location(canonicalPath)],
            tags: [.. artifact.Tags, "classification:exact-duplicate"]);

    private static IEnumerable<EvidenceMeasurement> RawMeasurements(SqlSemanticMetrics metrics) =>
    [
        SqlEvidence.Measurement("statements", metrics.Statements, "statements"),
        SqlEvidence.Measurement("unknown-statements", metrics.UnknownStatements, "statements"),
        SqlEvidence.Measurement("ddl-statements", metrics.DdlStatements, "statements"),
        SqlEvidence.Measurement("tables", metrics.Tables, "surfaces"),
        SqlEvidence.Measurement("views", metrics.Views, "surfaces"),
        SqlEvidence.Measurement("indexes", metrics.Indexes, "surfaces"),
        SqlEvidence.Measurement("constraints", metrics.Constraints, "surfaces"),
        SqlEvidence.Measurement("sequences", metrics.Sequences, "surfaces"),
        SqlEvidence.Measurement("types", metrics.Types, "surfaces"),
        SqlEvidence.Measurement("functions", metrics.Functions, "programs"),
        SqlEvidence.Measurement("procedures", metrics.Procedures, "programs"),
        SqlEvidence.Measurement("triggers", metrics.Triggers, "programs"),
        SqlEvidence.Measurement("queries", metrics.Queries, "statements"),
        SqlEvidence.Measurement("joins", metrics.Joins, "operations"),
        SqlEvidence.Measurement("ctes", metrics.Ctes, "operations"),
        SqlEvidence.Measurement("subqueries", metrics.Subqueries, "operations"),
        SqlEvidence.Measurement("window-functions", metrics.WindowFunctions, "operations"),
        SqlEvidence.Measurement("transactions", metrics.Transactions, "operations"),
        SqlEvidence.Measurement("data-modification-statements", metrics.DataModificationStatements, "statements"),
    ];

    private static List<string> CommonTags(
        SqlScopeOwnership ownership,
        SqlSemanticAnalysis analysis,
        SqlArtifactRoleAssessment role)
    {
        List<string> tags =
        [
            $"ecosystem:{ownership.Ecosystem}",
            $"sql-role:{role.Role}",
            "syntax:token-backed",
            "parser:bounded-token-stream",
            $"parser-confidence:{analysis.ParserConfidence}",
            $"dialect:{analysis.Dialect.Dialect}",
            $"dialect-confidence:{analysis.Dialect.Confidence}",
            ownership.Standalone ? "sql-ownership:standalone" : "sql-ownership:project-or-package",
            "source-excerpts:not-emitted",
        ];
        tags.AddRange(analysis.Dialect.Signals.Select(signal => $"dialect-signal:{signal}"));
        if (role.MigrationOrdering is not null)
        {
            tags.Add($"migration-order:{role.MigrationOrdering}");
        }

        if (analysis.Truncated)
        {
            tags.Add("analysis:token-limit-reached");
        }

        if (!analysis.StructurallyBalanced || analysis.UnterminatedConstruct)
        {
            tags.Add("analysis:structurally-incomplete");
        }

        return tags;
    }

    private static void AddSurfaceTags(List<string> tags, SqlSemanticMetrics metrics)
    {
        if (metrics.DdlStatements > 0) tags.Add("sql-surface:ddl");
        if (metrics.StoredPrograms + metrics.Triggers > 0) tags.Add("sql-surface:stored-program");
        if (metrics.Queries > 0) tags.Add("sql-surface:query");
        if (metrics.DataModificationStatements > 0) tags.Add("sql-surface:data-modification");
        if (metrics.Transactions > 0) tags.Add("sql-surface:transaction");
    }
}
