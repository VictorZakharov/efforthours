using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Python;

public sealed class PythonRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
{
    private readonly IRepositoryFileSystem _fileSystem;

    public PythonRepositoryAnalyzer()
        : this(PhysicalRepositoryFileSystem.Instance)
    {
    }

    public PythonRepositoryAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "python";

    public IReadOnlyList<LanguageAnalysisSupport> LanguageSupport { get; } =
        [
            new("python", LanguageAnalysisSupport.TokenBacked),
            new("jupyter", LanguageAnalysisSupport.TokenBacked),
        ];

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);
        string rootPath = _fileSystem.GetFullPath(repositoryPath);
        PythonTextReader reader = new(_fileSystem, rootPath);
        EvidenceFact[] allFiles = [.. evidence.Facts
            .Where(fact => fact.Kind == EvidenceKinds.File)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        EvidenceFact[] sourceFiles = [.. allFiles.Where(IsMaintainedPythonSource)];
        EvidenceFact[] notebookFiles = [.. allFiles.Where(IsMaintainedNotebook)];
        PythonProjectReadResult project = await new PythonProjectReader(reader)
            .ReadAsync(allFiles, cancellationToken).ConfigureAwait(false);
        List<Diagnostic> diagnostics = [.. project.Diagnostics];
        List<PythonFileAnalysis> analyses = [];
        List<JupyterNotebookAnalysis> notebooks = [];

        foreach (EvidenceFact sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PythonTextReadResult read = await reader.ReadAsync(sourceFile, cancellationToken)
                .ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }

            PythonPackageModel owner = FindOwner(sourceFile.Scope, project.Packages);
            PythonSyntaxAnalysis syntax = PythonSyntaxAnalyzer.Analyze(read.Text!, sourceFile.Scope);
            analyses.Add(new PythonFileAnalysis(sourceFile, owner, syntax));
            if (syntax.Confidence == "low")
            {
                diagnostics.Add(PythonEvidence.Diagnostic(
                    "FB7002",
                    DiagnosticSeverity.Warning,
                    $"Python file '{sourceFile.Scope}' reached a tokenizer or structure safeguard; recognized evidence is incomplete and confidence is low.",
                    sourceFile.Scope));
            }
        }

        JupyterNotebookTextReader notebookReader = new(_fileSystem, rootPath);
        foreach (EvidenceFact notebookFile in notebookFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PythonTextReadResult read = await notebookReader.ReadAsync(notebookFile, cancellationToken)
                .ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }

            JupyterNotebookParseResult parsed = JupyterNotebookParser.Parse(read.Text!, notebookFile.Scope);
            if (parsed.Error is not null)
            {
                diagnostics.Add(PythonEvidence.Diagnostic(
                    "FB7012",
                    DiagnosticSeverity.Warning,
                    $"Jupyter notebook '{notebookFile.Scope}' was skipped: {parsed.Error}",
                    notebookFile.Scope));
                continue;
            }

            PythonPackageModel owner = FindOwner(notebookFile.Scope, project.Packages);
            notebooks.Add(new JupyterNotebookAnalysis(
                notebookFile,
                owner,
                parsed.Confidence,
                parsed.DeclaredLanguage,
                parsed.TotalCells,
                parsed.CodeCells,
                parsed.PythonCodeCells,
                parsed.MarkdownCells,
                parsed.UniqueMarkdownCells,
                parsed.DuplicateMarkdownCells,
                parsed.RawCells,
                parsed.UnsupportedCodeCells,
                parsed.UniqueCodeCells,
                parsed.DuplicateCodeCells,
                parsed.OutputCells,
                parsed.ExecutionCountCells,
                parsed.AttachmentCells,
                parsed.MagicLines,
                parsed.ShellEscapeLines,
                parsed.MarkdownLines,
                parsed.MarkdownHeadings,
                parsed.MarkdownLinks,
                parsed.HasWidgetState,
                parsed.SafeguardReached,
                IsCanonical: true,
                parsed.MaintainedProjectionDigest,
                parsed.Syntax));
            if (parsed.DeclaredLanguage != "python" || parsed.UnsupportedCodeCells > 0)
            {
                diagnostics.Add(PythonEvidence.Diagnostic(
                    "FB7013",
                    DiagnosticSeverity.Warning,
                    $"Jupyter notebook '{notebookFile.Scope}' has non-Python, mixed, or unsupported code cells; only unambiguous Python cells were analyzed.",
                    notebookFile.Scope));
            }
            if (parsed.SafeguardReached)
            {
                diagnostics.Add(PythonEvidence.Diagnostic(
                    "FB7014",
                    DiagnosticSeverity.Warning,
                    $"Jupyter notebook '{notebookFile.Scope}' reached a cell, source, JSON, or tokenizer safeguard; evidence is incomplete.",
                    notebookFile.Scope));
            }
        }

        HashSet<string> canonicalNotebookPaths = notebooks
            .GroupBy(item => $"{item.Package.Directory}|{item.MaintainedProjectionDigest}", StringComparer.Ordinal)
            .Select(group => group.MinBy(item => item.File.Scope, StringComparer.Ordinal)!.File.Scope)
            .ToHashSet(StringComparer.Ordinal);
        notebooks = [.. notebooks.Select(item => item with
        {
            IsCanonical = canonicalNotebookPaths.Contains(item.File.Scope),
        })];

        List<EvidenceFact> facts = [];
        foreach (PythonPackageModel package in project.Packages.OrderBy(item => item.Directory, StringComparer.Ordinal))
        {
            PythonFileAnalysis[] owned = [.. analyses
                .Where(file => file.Package.Directory == package.Directory)
                .OrderBy(file => file.File.Scope, StringComparer.Ordinal)];
            JupyterNotebookAnalysis[] ownedNotebooks = [.. notebooks
                .Where(item => item.Package.Directory == package.Directory)
                .OrderBy(item => item.File.Scope, StringComparer.Ordinal)];
            JupyterNotebookAnalysis[] canonicalNotebooks = [.. ownedNotebooks.Where(item => item.IsCanonical)];
            PythonFileAnalysis[] notebookCode = [.. canonicalNotebooks
                .Where(item => item.PythonCodeCells > 0)
                .Select(item => new PythonFileAnalysis(item.File, item.Package, item.Syntax))];
            facts.Add(PythonFactFactory.Package(package, [.. owned, .. notebookCode]));
            facts.AddRange(PythonFactFactory.Dependencies(package));
            if (owned.Length > 0)
            {
                facts.Add(PythonFactFactory.SourceStructure(package, owned));
                facts.AddRange(owned.SelectMany(PythonFactFactory.FileSemantics));
            }
            if (notebookCode.Length > 0)
            {
                facts.Add(JupyterNotebookFactFactory.SourceStructure(package, [.. canonicalNotebooks.Where(item => item.PythonCodeCells > 0)]));
                facts.AddRange(notebookCode.SelectMany(PythonFactFactory.FileSemantics));
            }
            facts.AddRange(ownedNotebooks.Select(JupyterNotebookFactFactory.Notebook));
            facts.AddRange(canonicalNotebooks.SelectMany(JupyterNotebookFactFactory.Specialized));
        }

        PythonFileAnalysis[] allPython = [.. analyses, .. notebooks
            .Where(item => item.IsCanonical && item.PythonCodeCells > 0)
            .Select(item => new PythonFileAnalysis(item.File, item.Package, item.Syntax))];
        facts.AddRange(CreateProjectReferences(project.Packages, allPython));
        diagnostics.Add(PythonEvidence.Diagnostic(
            "FB7000",
            DiagnosticSeverity.Information,
            "The Python analyzer used bounded static metadata, token, and indentation analysis only; it did not invoke Python, import modules, resolve an environment, install dependencies, execute setup.py, or emit source excerpts."));
        if (notebookFiles.Length > 0)
        {
            diagnostics.Add(PythonEvidence.Diagnostic(
                "FB7010",
                DiagnosticSeverity.Information,
                "Jupyter analysis parsed bounded JSON and admitted Python code cells only; it excluded outputs, execution counts, widget state, attachments, shell escapes, unsupported magics, embedded payloads, and source excerpts without launching Jupyter or a kernel."));
        }
        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        diagnostics.Sort(CompareDiagnostics);
        return new RepositoryAnalysisContribution
        {
            Facts = facts,
            Diagnostics = diagnostics,
        };
    }

    private static IEnumerable<EvidenceFact> CreateProjectReferences(
        IReadOnlyList<PythonPackageModel> packages,
        IReadOnlyList<PythonFileAnalysis> analyses)
    {
        foreach (PythonPackageModel source in packages)
        {
            PythonFileAnalysis[] sourceFiles = [.. analyses.Where(file => file.Package == source)];
            foreach (PythonPackageModel target in packages.Where(target => target != source))
            {
                string importRoot = target.Name.Replace('-', '_');
                PythonFileAnalysis[] matching = [.. sourceFiles.Where(file =>
                    file.Syntax.Metrics.ImportsSeen.Any(imported =>
                        imported.TrimStart('.').Equals(importRoot, StringComparison.Ordinal) ||
                        imported.TrimStart('.').StartsWith(importRoot + ".", StringComparison.Ordinal)))];
                if (matching.Length > 0)
                {
                    yield return PythonFactFactory.ProjectReference(source, target, matching);
                }
            }
        }
    }

    private static PythonPackageModel FindOwner(
        string path,
        IReadOnlyList<PythonPackageModel> packages) => packages
        .Where(package => IsWithin(path, package.Directory))
        .OrderByDescending(package => package.Directory.Length)
        .ThenBy(package => package.Directory, StringComparer.Ordinal)
        .First();

    private static bool IsMaintainedPythonSource(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        fact.Tags.Contains("language:python", StringComparer.Ordinal) &&
        fact.Tags.Any(tag => tag is "role:source" or "role:test") &&
        !fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:minified" or
            "classification:vendored" or "content:binary");

    private static bool IsMaintainedNotebook(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        fact.Tags.Contains("language:jupyter", StringComparer.Ordinal) &&
        fact.Tags.Any(tag => tag is "role:source" or "role:test") &&
        !fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:minified" or
            "classification:vendored" or "content:binary");

    private static bool IsWithin(string path, string directory) =>
        directory == "." || path.Equals(directory, StringComparison.Ordinal) ||
        path.StartsWith(directory + "/", StringComparison.Ordinal);

    private static int CompareDiagnostics(Diagnostic left, Diagnostic right)
    {
        int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
        if (code != 0) return code;
        string leftPath = left.Locations.Count == 0 ? string.Empty : left.Locations[0].Path;
        string rightPath = right.Locations.Count == 0 ? string.Empty : right.Locations[0].Path;
        return StringComparer.Ordinal.Compare(leftPath, rightPath);
    }
}
