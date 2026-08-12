using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Scripting;

public sealed class ScriptingRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
{
    private readonly IRepositoryFileSystem _fileSystem;

    public ScriptingRepositoryAnalyzer()
        : this(PhysicalRepositoryFileSystem.Instance)
    {
    }

    public ScriptingRepositoryAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "scripting";

    public IReadOnlyList<string> Ecosystems { get; } = ["shell", "powershell"];

    public IReadOnlyList<LanguageAnalysisSupport> LanguageSupport { get; } =
    [
        new("shell", LanguageAnalysisSupport.TokenBacked),
        new("powershell", LanguageAnalysisSupport.TokenBacked),
    ];

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);
        string rootPath = _fileSystem.GetFullPath(repositoryPath);
        ScriptTextReader reader = new(_fileSystem, rootPath);
        EvidenceFact[] allFiles = [.. evidence.Facts
            .Where(fact => fact.Kind == EvidenceKinds.File)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        EvidenceFact[] scriptFiles = [.. allFiles.Where(IsMaintainedScript)];
        ScriptInvocationReadResult invocation = await new ScriptInvocationContextReader(reader)
            .ReadAsync(allFiles, scriptFiles, cancellationToken).ConfigureAwait(false);
        List<Diagnostic> diagnostics = [.. invocation.Diagnostics];
        List<ScriptFileAnalysis> analyses = [];

        foreach (EvidenceFact file in scriptFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScriptTextReadResult read = await reader.ReadAsync(file, cancellationToken)
                .ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }

            ScriptLanguage language = Language(file);
            ScriptSyntaxAnalysis syntax = language == ScriptLanguage.Shell
                ? ShellSyntaxAnalyzer.Analyze(read.Text!)
                : PowerShellSyntaxAnalyzer.Analyze(read.Text!);
            IReadOnlyList<ScriptRole> roles = invocation.RolesByPath.GetValueOrDefault(file.Scope) ?? [];
            ScriptRole role = ScriptRoleClassifier.Classify(file, language, roles, syntax);
            analyses.Add(new ScriptFileAnalysis(file, language, role, syntax));
            AddFileDiagnostics(diagnostics, file.Scope, syntax, roles);
        }

        List<EvidenceFact> facts = [.. ScriptFactFactory.Create(analyses)];
        diagnostics.Add(ScriptEvidence.Diagnostic(
            "FB8500",
            DiagnosticSeverity.Information,
            "The Shell and PowerShell analyzer used bounded static token and invocation-context analysis only; it did not start a shell, resolve commands or modules, source files, evaluate expansions, access the network, or emit source excerpts."));
        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        diagnostics.Sort(CompareDiagnostics);
        return new RepositoryAnalysisContribution { Facts = facts, Diagnostics = diagnostics };
    }

    private static void AddFileDiagnostics(
        List<Diagnostic> diagnostics,
        string path,
        ScriptSyntaxAnalysis syntax,
        IReadOnlyList<ScriptRole> roles)
    {
        if (syntax.Confidence == "low")
            diagnostics.Add(ScriptEvidence.Diagnostic(
                "FB8502",
                DiagnosticSeverity.Warning,
                $"Script file '{path}' reached a tokenizer safeguard; recognized evidence is incomplete and confidence is low.",
                path));
        if (syntax.Metrics.HasUnresolvedSourcing)
            diagnostics.Add(ScriptEvidence.Diagnostic(
                "FB8503",
                DiagnosticSeverity.Information,
                $"Script file '{path}' has sourced or imported content that static analysis did not resolve.",
                path));
        if (syntax.Metrics.HasDynamicInvocation)
            diagnostics.Add(ScriptEvidence.Diagnostic(
                "FB8504",
                DiagnosticSeverity.Information,
                $"Script file '{path}' has dynamic invocation or expansion whose runtime behavior was not inferred.",
                path));
        if (ScriptRoleClassifier.HasConflictingAutomationRoles(roles))
            diagnostics.Add(ScriptEvidence.Diagnostic(
                "FB8505",
                DiagnosticSeverity.Information,
                $"Script file '{path}' is invoked by more than one automation role; the most specific deterministic role was selected.",
                path));
    }

    private static bool IsMaintainedScript(EvidenceFact fact) =>
        fact.Tags.Any(tag => tag is "language:shell" or "language:powershell") &&
        !fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:minified" or
            "classification:vendored" or "content:binary");

    private static ScriptLanguage Language(EvidenceFact file) =>
        file.Tags.Contains("language:shell", StringComparer.Ordinal)
            ? ScriptLanguage.Shell
            : ScriptLanguage.PowerShell;

    private static int CompareDiagnostics(Diagnostic left, Diagnostic right)
    {
        int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
        if (code != 0) return code;
        string leftPath = left.Locations.Count == 0 ? string.Empty : left.Locations[0].Path;
        string rightPath = right.Locations.Count == 0 ? string.Empty : right.Locations[0].Path;
        return StringComparer.Ordinal.Compare(leftPath, rightPath);
    }
}
