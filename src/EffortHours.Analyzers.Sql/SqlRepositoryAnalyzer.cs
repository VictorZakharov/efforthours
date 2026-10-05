using EffortHours.Analysis;
using EffortHours.Contracts.V1;
using static EffortHours.Analyzers.Sql.SqlFactFactory;

namespace EffortHours.Analyzers.Sql;

public sealed partial class SqlRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
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

    public IReadOnlyList<LanguageAnalysisSupport> LanguageSupport { get; } =
        [new("sql", LanguageAnalysisSupport.TokenBacked)];

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);
        string rootPath = _fileSystem.GetFullPath(repositoryPath);
        SqlFileAnalysisReader reader = new(_fileSystem, rootPath);
        string? localContext = RepositoryLocalAnalysisLineage.ContextIdentity(_fileSystem, rootPath, evidence, cancellationToken);
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

            SqlFileAnalysis read = await reader.ReadAsync(fileFact, cancellationToken)
                .ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }
            await RepositoryLocalAnalysisLineage.StoreAsync(_fileSystem, rootPath, fileFact,
                localContext, read, cancellationToken).ConfigureAwait(false);
            SqlSemanticAnalysis analysis = read.Analysis!;
            SqlArtifactRoleAssessment role = read.Role!;
            SqlScopeOwnership ownership = scopeResolver.Resolve(fileFact.Scope);
            string? duplicateKey = DuplicateKey(fileFact);
            string? canonicalPath = duplicateKey is null
                ? null
                : canonicalPathByDuplicateKey[duplicateKey];
            bool exactDuplicate = canonicalPath is not null &&
                !StringComparer.Ordinal.Equals(canonicalPath, fileFact.Scope);
            standalone += ownership.Standalone ? 1 : 0;
            analyzed++;
            SqlAnalyzedFileFacts fileFacts = await ReadAnalyzedFileFactsAsync(fileFact, ownership,
                read, exactDuplicate, canonicalPath, cancellationToken).ConfigureAwait(false);
            facts.AddRange(fileFacts.Facts);
            diagnostics.AddRange(fileFacts.Diagnostics);
            excluded += fileFacts.Excluded ? 1 : 0;
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
