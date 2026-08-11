using EffortHours.Analysis;
using EffortHours.Contracts.V1;
using static EffortHours.Analyzers.Sql.SqlFactFactory;

namespace EffortHours.Analyzers.Sql;

public sealed class SqlRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
{
    private readonly IRepositoryFileSystem _fileSystem;

    public SqlRepositoryAnalyzer()
        : this(PhysicalRepositoryFileSystem.Instance)
    {
    }

    public SqlRepositoryAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "sql";

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);
        string rootPath = _fileSystem.GetFullPath(repositoryPath);
        SqlTextReader reader = new(_fileSystem, rootPath);
        SqlScopeResolver scopeResolver = new(evidence);
        EvidenceFact[] sqlFiles =
        [
            .. evidence.Facts
                .Where(IsSqlFile)
                .OrderBy(fact => fact.Scope, StringComparer.Ordinal),
        ];
        Dictionary<string, string> canonicalPathByDuplicateKey =
            CanonicalPathByDuplicateKey(sqlFiles);
        List<EvidenceFact> facts = [];
        List<Diagnostic> diagnostics = [];
        int analyzed = 0;
        int excluded = 0;
        int standalone = 0;

        foreach (EvidenceFact fileFact in sqlFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsMaintained(fileFact))
            {
                excluded++;
                continue;
            }

            SqlTextReadResult read = await reader.ReadAsync(fileFact, cancellationToken)
                .ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }

            SqlSemanticAnalysis analysis = SqlSemanticAnalyzer.Analyze(read.Text!);
            SqlArtifactRoleAssessment role = SqlArtifactClassifier.Classify(
                fileFact,
                read.Text!,
                analysis.Metrics);
            SqlScopeOwnership ownership = scopeResolver.Resolve(fileFact.Scope);
            string? duplicateKey = DuplicateKey(fileFact);
            string? canonicalPath = duplicateKey is null
                ? null
                : canonicalPathByDuplicateKey[duplicateKey];
            bool exactDuplicate = canonicalPath is not null &&
                !StringComparer.Ordinal.Equals(canonicalPath, fileFact.Scope);
            standalone += ownership.Standalone ? 1 : 0;
            analyzed++;
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
                excluded++;
                facts.Add(CreateDumpExclusion(fileFact, ownership, artifact));
                continue;
            }

            if (exactDuplicate)
            {
                excluded++;
                facts.Add(CreateDuplicateExclusion(
                    fileFact,
                    ownership,
                    artifact,
                    canonicalPath!));
                continue;
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
        }

        facts.Add(CreateRepositoryFact(sqlFiles, facts, analyzed, excluded, standalone));
        diagnostics.Add(SqlEvidence.Diagnostic(
            "FB6000",
            DiagnosticSeverity.Information,
            "The SQL analyzer used bounded static token and statement analysis only; it did not connect to a database, choose a server, execute SQL, inspect timestamps, or emit source excerpts."));
        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        diagnostics.Sort(CompareDiagnostics);
        return new RepositoryAnalysisContribution
        {
            Facts = facts,
            Diagnostics = diagnostics,
        };
    }

    private static void AddAnalysisDiagnostics(
        EvidenceFact file,
        SqlSemanticAnalysis analysis,
        SqlArtifactRoleAssessment role,
        List<Diagnostic> diagnostics)
    {
        if (analysis.ParserConfidence == "low")
        {
            diagnostics.Add(SqlEvidence.Diagnostic(
                "FB6002",
                DiagnosticSeverity.Warning,
                $"SQL file '{file.Scope}' exceeded or violated a bounded parser safeguard; recognized evidence is incomplete and confidence is low.",
                file.Scope));
        }
        else if (analysis.Metrics.UnknownStatements > 0 || role.Role == "unknown")
        {
            diagnostics.Add(SqlEvidence.Diagnostic(
                "FB6003",
                DiagnosticSeverity.Information,
                $"SQL file '{file.Scope}' contains unknown or vendor-specific statement shapes; they remain visible but receive no guessed semantic units.",
                file.Scope));
        }
    }

    private static bool IsSqlFile(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        fact.Tags.Contains("language:sql", StringComparer.Ordinal);

    private static bool IsMaintained(EvidenceFact fact) => !fact.Tags.Any(tag => tag is
        "classification:generated" or "classification:minified" or
        "classification:vendored" or "content:binary");

    private static Dictionary<string, string> CanonicalPathByDuplicateKey(
        IEnumerable<EvidenceFact> files) => files
        .Where(IsMaintained)
        .Select(file => new { File = file, Key = DuplicateKey(file) })
        .Where(item => item.Key is not null)
        .GroupBy(item => item.Key!, StringComparer.Ordinal)
        .ToDictionary(
            group => group.Key,
            group => group.Select(item => item.File.Scope).Min(StringComparer.Ordinal)!,
            StringComparer.Ordinal);

    private static string? DuplicateKey(EvidenceFact file)
    {
        string? digest = SqlEvidence.TagValue(file.Tags, "sha256:");
        if (digest is null)
        {
            return null;
        }

        string roleFamily = file.Tags.Contains("classification:test", StringComparer.Ordinal)
            ? "test"
            : "source";
        return $"{roleFamily}|sql|{digest}";
    }

    private static int CompareDiagnostics(Diagnostic left, Diagnostic right)
    {
        int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
        if (code != 0) return code;
        string leftPath = left.Locations.Count == 0 ? string.Empty : left.Locations[0].Path;
        string rightPath = right.Locations.Count == 0 ? string.Empty : right.Locations[0].Path;
        return StringComparer.Ordinal.Compare(leftPath, rightPath);
    }
}
