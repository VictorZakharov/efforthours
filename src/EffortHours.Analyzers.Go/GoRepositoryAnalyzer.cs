using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Go;

public sealed class GoRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
{
    private readonly IRepositoryFileSystem _fileSystem;

    public GoRepositoryAnalyzer()
        : this(PhysicalRepositoryFileSystem.Instance)
    {
    }

    public GoRepositoryAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "go";

    public IReadOnlyList<LanguageAnalysisSupport> LanguageSupport { get; } =
        [new("go", LanguageAnalysisSupport.TokenBacked)];

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);
        string rootPath = _fileSystem.GetFullPath(repositoryPath);
        GoTextReader reader = new(_fileSystem, rootPath);
        EvidenceFact[] allFiles = [.. evidence.Facts
            .Where(fact => fact.Kind == EvidenceKinds.File)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        EvidenceFact[] sourceFiles = [.. allFiles.Where(IsMaintainedGoSource)];
        if (sourceFiles.Length == 0 && !allFiles.Any(IsGoModuleOrWorkspace))
            return new RepositoryAnalysisContribution();
        GoProjectReadResult project = await new GoProjectReader(reader)
            .ReadAsync(allFiles, sourceFiles.Length > 0, cancellationToken).ConfigureAwait(false);
        List<Diagnostic> diagnostics = [.. project.Diagnostics];
        List<GoFileAnalysis> analyses = [];

        foreach (EvidenceFact sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GoTextReadResult read = await reader.ReadAsync(sourceFile, cancellationToken)
                .ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }

            GoModuleModel owner = FindOwner(sourceFile.Scope, project.Modules);
            GoSyntaxAnalysis syntax = GoSyntaxAnalyzer.Analyze(
                read.Text!,
                sourceFile.Scope,
                owner.ModulePath == "repository" ? string.Empty : owner.ModulePath);
            analyses.Add(new GoFileAnalysis(sourceFile, owner, syntax));
            if (syntax.Confidence == "low")
            {
                diagnostics.Add(GoEvidence.Diagnostic(
                    "FB8002",
                    DiagnosticSeverity.Warning,
                    $"Go file '{sourceFile.Scope}' reached a tokenizer or structure safeguard; recognized evidence is incomplete and confidence is low.",
                    sourceFile.Scope));
            }
        }

        GoModuleModel[] modules = [.. project.Modules
            .Select(module => module with
            {
                Role = Role(analyses.Where(file => file.Module.Directory == module.Directory)),
            })];
        analyses = [.. analyses.Select(file => file with
        {
            Module = modules.Single(module => module.Directory == file.Module.Directory),
        })];

        List<EvidenceFact> facts = [];
        if (project.Workspace is not null) facts.Add(GoFactFactory.Workspace(project.Workspace));
        foreach (GoModuleModel module in modules.OrderBy(item => item.Directory, StringComparer.Ordinal))
        {
            GoFileAnalysis[] owned = [.. analyses
                .Where(file => file.Module.Directory == module.Directory)
                .OrderBy(file => file.File.Scope, StringComparer.Ordinal)];
            facts.Add(GoFactFactory.Module(module, owned));
            facts.AddRange(GoFactFactory.Packages(module, owned));
            facts.AddRange(GoFactFactory.Dependencies(module));
            if (owned.Length == 0) continue;
            facts.Add(GoFactFactory.SourceStructure(module, owned));
            EvidenceFact? build = GoFactFactory.BuildSemantics(module, owned);
            if (build is not null) facts.Add(build);
            facts.AddRange(owned.SelectMany(GoFactFactory.FileSemantics));
        }

        facts.AddRange(CreateProjectReferences(modules, analyses, project.Workspace));
        AddBoundaryDiagnostics(analyses, diagnostics);
        diagnostics.Add(GoEvidence.Diagnostic(
            "FB8000",
            DiagnosticSeverity.Information,
            "The Go analyzer used bounded static module, workspace, token, and filename analysis only; it did not invoke the Go toolchain, resolve build constraints, expand go:embed, run go:generate, compile cgo, load plugins, install modules, or emit source excerpts."));
        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        diagnostics.Sort(CompareDiagnostics);
        return new RepositoryAnalysisContribution { Facts = facts, Diagnostics = diagnostics };
    }

    private static IEnumerable<EvidenceFact> CreateProjectReferences(
        IReadOnlyList<GoModuleModel> modules,
        IReadOnlyList<GoFileAnalysis> analyses,
        GoWorkspaceModel? workspace)
    {
        foreach (GoModuleModel source in modules)
        {
            GoFileAnalysis[] sourceFiles = [.. analyses.Where(file => file.Module.Directory == source.Directory)];
            foreach (GoModuleModel target in modules.Where(target => target.Directory != source.Directory))
            {
                GoFileAnalysis[] importEvidence = [.. sourceFiles.Where(file =>
                    file.Syntax.Metrics.ImportsSeen.Any(imported =>
                        imported == target.ModulePath ||
                        imported.StartsWith(target.ModulePath + "/", StringComparison.Ordinal)))];
                bool replacement = source.LocalReplacements.Any(item =>
                        item.TargetDirectory == target.Directory) ||
                    workspace?.LocalReplacements.Any(item =>
                        item.TargetDirectory == target.Directory &&
                        source.Dependencies.Contains(item.ModulePath, StringComparer.Ordinal)) == true;
                if (importEvidence.Length > 0 || replacement)
                    yield return GoFactFactory.ProjectReference(source, target, importEvidence, replacement);
            }
        }
    }

    private static void AddBoundaryDiagnostics(
        IReadOnlyList<GoFileAnalysis> analyses,
        List<Diagnostic> diagnostics)
    {
        GoFileAnalysis[] constrained = [.. analyses.Where(file =>
            file.Syntax.Metrics.BuildConstraints + file.Syntax.Metrics.PlatformFiles > 0)];
        if (constrained.Length > 0)
            diagnostics.Add(GoEvidence.Diagnostic(
                "FB8004",
                DiagnosticSeverity.Information,
                $"{constrained.Length} Go file(s) use build constraints or platform filename selection; all admitted files were analyzed, but the active target set was not resolved."));
        GoFileAnalysis[] cgo = [.. analyses.Where(file => file.Syntax.Metrics.CgoFiles > 0)];
        if (cgo.Length > 0)
            diagnostics.Add(GoEvidence.Diagnostic(
                "FB8005",
                DiagnosticSeverity.Warning,
                $"{cgo.Length} Go file(s) import C; cgo preambles, native compilation, ABI behavior, and platform availability were not proven."));
        GoFileAnalysis[] runtime = [.. analyses.Where(file => file.Syntax.Metrics.BlankImports > 0)];
        if (runtime.Length > 0)
            diagnostics.Add(GoEvidence.Diagnostic(
                "FB8006",
                DiagnosticSeverity.Information,
                $"{runtime.Sum(file => file.Syntax.Metrics.BlankImports)} blank Go import(s) may register runtime behavior; static analysis records the imports but cannot prove registration effects."));
    }

    private static string Role(IEnumerable<GoFileAnalysis> files)
    {
        GoSourceMetrics[] metrics = [.. files.Select(file => file.Syntax.Metrics)];
        if (metrics.Any(item => item.ApiEndpoints + item.ApiTypes > 0)) return "server";
        if (metrics.Any(item => item.BackgroundUsages > 0)) return "worker";
        if (metrics.Any(item => item.EntryPoints + item.CliCommands > 0)) return "cli";
        return "library";
    }

    private static GoModuleModel FindOwner(
        string path,
        IReadOnlyList<GoModuleModel> modules) => modules
        .Where(module => GoPath.IsWithin(path, module.Directory))
        .OrderByDescending(module => module.Directory.Length)
        .ThenBy(module => module.Directory, StringComparer.Ordinal)
        .First();

    private static bool IsMaintainedGoSource(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        fact.Tags.Contains("language:go", StringComparer.Ordinal) &&
        fact.Tags.Any(tag => tag is "role:source" or "role:test") &&
        !fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:minified" or
            "classification:vendored" or "content:binary");

    private static bool IsGoModuleOrWorkspace(EvidenceFact fact) =>
        Path.GetFileName(fact.Scope).Equals("go.mod", StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(fact.Scope).Equals("go.work", StringComparison.OrdinalIgnoreCase);

    private static int CompareDiagnostics(Diagnostic left, Diagnostic right)
    {
        int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
        if (code != 0) return code;
        string leftPath = left.Locations.Count == 0 ? string.Empty : left.Locations[0].Path;
        string rightPath = right.Locations.Count == 0 ? string.Empty : right.Locations[0].Path;
        return StringComparer.Ordinal.Compare(leftPath, rightPath);
    }
}
