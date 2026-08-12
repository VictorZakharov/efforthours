using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Java;

public sealed class KotlinRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
{
    private readonly IRepositoryFileSystem _fileSystem;

    public KotlinRepositoryAnalyzer()
        : this(PhysicalRepositoryFileSystem.Instance)
    {
    }

    public KotlinRepositoryAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "kotlin";

    public IReadOnlyList<LanguageAnalysisSupport> LanguageSupport { get; } =
        [new("kotlin", LanguageAnalysisSupport.TokenBacked)];

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);
        string rootPath = _fileSystem.GetFullPath(repositoryPath);
        KotlinTextReader reader = new(_fileSystem, rootPath);
        EvidenceFact[] allFiles = [.. evidence.Facts
            .Where(fact => fact.Kind == EvidenceKinds.File)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        EvidenceFact[] sourceFiles = [.. allFiles.Where(IsMaintainedKotlinSource)];
        EvidenceFact[] kotlinBuildScripts = [.. allFiles.Where(IsKotlinBuildScript)];
        if (sourceFiles.Length == 0 && kotlinBuildScripts.Length == 0)
            return new RepositoryAnalysisContribution();

        JavaProjectReadResult readProjects = await new JavaProjectReader(
                reader.Inner, "kotlin", "Kotlin/JVM", "FB8208")
            .ReadAsync(allFiles, sourceFiles.Length > 0, cancellationToken).ConfigureAwait(false);
        List<Diagnostic> diagnostics = [.. readProjects.Diagnostics];
        List<KotlinFileAnalysis> analyses = [];
        foreach (EvidenceFact sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JavaTextReadResult read = await reader.ReadAsync(sourceFile, cancellationToken)
                .ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }

            JavaProjectModel owner = FindOwner(sourceFile.Scope, readProjects.Projects);
            KotlinSyntaxAnalysis syntax = KotlinSyntaxAnalyzer.Analyze(read.Text!, sourceFile.Scope);
            analyses.Add(new KotlinFileAnalysis(sourceFile, owner, syntax));
            if (syntax.Confidence == "low")
                diagnostics.Add(KotlinEvidence.Diagnostic(
                    "FB8202",
                    DiagnosticSeverity.Warning,
                    $"Kotlin file '{sourceFile.Scope}' reached a tokenizer or structure safeguard; recognized evidence is incomplete and confidence is low.",
                    sourceFile.Scope));
        }

        PopulateInternalImports(analyses);
        JavaProjectModel[] projects = [.. readProjects.Projects.Select(project => project with
        {
            Role = Role(analyses.Where(file => file.Project.Directory == project.Directory)),
        })];
        analyses = [.. analyses.Select(file => file with
        {
            Project = projects.Single(project => project.Directory == file.Project.Directory),
        })];

        HashSet<string> existingJvmScopes = [.. evidence.Facts
            .Where(fact => fact.Kind == EvidenceKinds.EcosystemPackage &&
                fact.Tags.Contains("scope:analyzed", StringComparer.Ordinal) &&
                fact.Tags.Contains("ecosystem:java", StringComparer.Ordinal))
            .Select(fact => NormalizeScope(fact.Scope))];
        List<EvidenceFact> facts = [];
        foreach (JavaProjectModel project in projects.OrderBy(item => item.Directory, StringComparer.Ordinal))
        {
            KotlinFileAnalysis[] owned = [.. analyses
                .Where(file => file.Project.Directory == project.Directory)
                .OrderBy(file => file.File.Scope, StringComparer.Ordinal)];
            if (!existingJvmScopes.Contains(NormalizeScope(project.Directory)))
            {
                facts.Add(KotlinFactFactory.Project(project, owned));
                facts.AddRange(KotlinFactFactory.Dependencies(project));
                EvidenceFact? build = KotlinFactFactory.BuildConfiguration(project);
                if (build is not null) facts.Add(build);
            }
            facts.AddRange(KotlinFactFactory.Packages(project, owned));
            if (owned.Length == 0) continue;
            facts.Add(KotlinFactFactory.SourceStructure(project, owned));
            facts.AddRange(owned.SelectMany(KotlinFactFactory.FileSemantics));
        }

        facts.AddRange(CreateProjectReferences(projects, analyses, evidence.Facts));
        AddBoundaryDiagnostics(projects, analyses, kotlinBuildScripts, diagnostics);
        diagnostics.Add(KotlinEvidence.Diagnostic(
            "FB8200",
            DiagnosticSeverity.Information,
            "The Kotlin analyzer used bounded static Maven XML, conservative Gradle text, token, import, annotation, type, and filename analysis only; it did not invoke a JVM, Gradle, Maven, wrappers, Kotlin compilers, compiler plugins, KSP, kapt, tests, dependency resolution, reflection, runtime DSLs, or generated code, and emitted no source excerpts."));
        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        diagnostics.Sort(CompareDiagnostics);
        return new RepositoryAnalysisContribution { Facts = facts, Diagnostics = diagnostics };
    }

    private static IEnumerable<EvidenceFact> CreateProjectReferences(
        IReadOnlyList<JavaProjectModel> projects,
        IReadOnlyList<KotlinFileAnalysis> analyses,
        IReadOnlyList<EvidenceFact> existingFacts)
    {
        foreach (JavaProjectModel source in projects)
        {
            KotlinFileAnalysis[] sourceFiles = [.. analyses
                .Where(file => file.Project.Directory == source.Directory)];
            foreach (JavaProjectModel target in projects.Where(target => target.Directory != source.Directory))
            {
                bool existing = existingFacts.Any(fact =>
                    fact.Kind == EvidenceKinds.ProjectReference &&
                    NormalizeScope(fact.Scope) == NormalizeScope(source.Directory) &&
                    fact.Tags.Contains($"target-scope:{target.Directory}", StringComparer.Ordinal));
                if (existing) continue;
                string[] packageRoots = [.. analyses
                    .Where(file => file.Project.Directory == target.Directory)
                    .Select(file => file.Syntax.Metrics.PackageName)
                    .Where(name => name.Length > 0)
                    .Distinct(StringComparer.Ordinal)];
                if (packageRoots.Length == 0 && target.Coordinate is not null)
                    packageRoots = [target.Coordinate.Split(':')[0]];
                KotlinFileAnalysis[] imports = packageRoots.Length == 0
                    ? []
                    : [.. sourceFiles.Where(file =>
                        file.Syntax.Imports.ImportsSeen.Any(imported => packageRoots.Any(root =>
                            imported == root || imported.StartsWith(root + ".", StringComparison.Ordinal))))];
                bool uniqueTargetName = projects.Count(project =>
                    project.Name.Equals(target.Name, StringComparison.Ordinal)) == 1;
                bool buildReference = source.LocalProjectDirectories.Any(directory =>
                    directory.Equals(target.Directory, StringComparison.Ordinal) ||
                    uniqueTargetName && Path.GetFileName(directory).Equals(target.Name, StringComparison.Ordinal));
                buildReference |= source.Dependencies.Any(dependency =>
                    (target.Coordinate is not null && dependency == target.Coordinate) ||
                    (uniqueTargetName && (dependency.Equals(target.Name, StringComparison.Ordinal) ||
                        dependency.EndsWith(":" + target.Name, StringComparison.Ordinal))));
                if (buildReference || imports.Length > 0)
                    yield return KotlinFactFactory.ProjectReference(source, target, imports, buildReference);
            }
        }
    }

    private static void AddBoundaryDiagnostics(
        IReadOnlyList<JavaProjectModel> projects,
        IReadOnlyList<KotlinFileAnalysis> analyses,
        EvidenceFact[] buildScripts,
        List<Diagnostic> diagnostics)
    {
        int unresolved = projects.Sum(project => project.UnresolvedValues);
        if (unresolved > 0)
            diagnostics.Add(KotlinEvidence.Diagnostic(
                "FB8204",
                DiagnosticSeverity.Information,
                $"{unresolved} dynamic or property-backed Maven/Gradle value(s) were not resolved; literal Kotlin/JVM build evidence remains available."));
        if (buildScripts.Length > 0)
            diagnostics.Add(KotlinEvidence.Diagnostic(
                "FB8203",
                DiagnosticSeverity.Information,
                $"{buildScripts.Length} Gradle Kotlin DSL script(s) were treated as static build configuration, not maintained executable Kotlin product source."));
        int scripts = analyses.Count(file => file.Syntax.Metrics.IsScript);
        if (scripts > 0)
            diagnostics.Add(KotlinEvidence.Diagnostic(
                "FB8205",
                DiagnosticSeverity.Information,
                $"{scripts} maintained Kotlin script(s) were analyzed as token-backed source and potential entry surfaces without execution."));
        string[] plugins = [.. projects.SelectMany(project => project.Plugins)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        int generators = projects.Sum(project => project.AnnotationProcessors) +
            plugins.Count(plugin => ContainsAny(plugin, "ksp", "kapt", "serialization", "allopen", "noarg"));
        if (generators > 0)
            diagnostics.Add(KotlinEvidence.Diagnostic(
                "FB8206",
                DiagnosticSeverity.Information,
                $"{generators} Kotlin code-generation or compiler-plugin declaration(s) were inventoried; generated behavior was not executed or inferred."));
        if (plugins.Any(plugin => ContainsAny(plugin, "multiplatform", "android")) ||
            analyses.Any(file => file.File.Scope.Contains("commonMain", StringComparison.OrdinalIgnoreCase) ||
                file.File.Scope.Contains("androidMain", StringComparison.OrdinalIgnoreCase)))
            diagnostics.Add(KotlinEvidence.Diagnostic(
                "FB8207",
                DiagnosticSeverity.Information,
                "Android or Kotlin Multiplatform boundaries were recognized conservatively; source-set expect/actual resolution, runtime DSL behavior, platform packaging, and compiler-plugin expansion were not proven."));
    }

    private static void PopulateInternalImports(IReadOnlyList<KotlinFileAnalysis> analyses)
    {
        foreach (IGrouping<string, KotlinFileAnalysis> projectFiles in analyses
            .GroupBy(file => file.Project.Directory, StringComparer.Ordinal))
        {
            HashSet<string> packages = [.. projectFiles
                .Select(file => file.Syntax.Metrics.PackageName)
                .Where(packageName => packageName.Length > 0)];
            foreach (KotlinFileAnalysis file in projectFiles)
                file.Syntax.Metrics.InternalImports = file.Syntax.Imports.ImportsSeen.Count(imported =>
                    IsImportFromDeclaredPackage(imported, packages));
        }
    }

    private static bool IsImportFromDeclaredPackage(string imported, HashSet<string> packages)
    {
        string candidate = imported.EndsWith(".*", StringComparison.Ordinal) ? imported[..^2] : imported;
        while (candidate.Length > 0)
        {
            if (packages.Contains(candidate)) return true;
            int separator = candidate.LastIndexOf('.');
            if (separator < 0) return false;
            candidate = candidate[..separator];
        }
        return false;
    }

    private static string Role(IEnumerable<KotlinFileAnalysis> files)
    {
        KotlinSourceMetrics[] metrics = [.. files
            .Where(file => !file.File.Tags.Contains("classification:test", StringComparer.Ordinal))
            .Select(file => file.Syntax.Metrics)];
        if (metrics.Any(item => item.AndroidComponents + item.UiSurfaces > 0)) return "application";
        if (metrics.Any(item => item.ApiEndpoints + item.ApiTypes > 0)) return "server";
        if (metrics.Any(item => item.BackgroundUsages > 0)) return "worker";
        if (metrics.Any(item => item.EntryPoints + item.CliCommands > 0)) return "cli";
        return "library";
    }

    private static JavaProjectModel FindOwner(string path, IReadOnlyList<JavaProjectModel> projects) =>
        projects.Where(project => JavaPath.IsWithin(path, project.Directory))
            .OrderByDescending(project => project.Directory.Length)
            .ThenBy(project => project.Directory, StringComparer.Ordinal)
            .First();

    private static bool IsMaintainedKotlinSource(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        fact.Tags.Contains("language:kotlin", StringComparer.Ordinal) &&
        fact.Tags.Any(tag => tag is "role:source" or "role:test") &&
        !Path.GetFileName(fact.Scope).EndsWith(".gradle.kts", StringComparison.OrdinalIgnoreCase) &&
        !fact.Tags.Any(tag => tag is "classification:generated" or "classification:minified" or
            "classification:vendored" or "content:binary");

    private static bool IsKotlinBuildScript(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        Path.GetFileName(fact.Scope).EndsWith(".gradle.kts", StringComparison.OrdinalIgnoreCase) &&
        !fact.Tags.Any(tag => tag is "classification:generated" or "classification:vendored" or "content:binary");

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeScope(string value) =>
        string.IsNullOrWhiteSpace(value) ? "." : value.Replace('\\', '/').TrimEnd('/');

    private static int CompareDiagnostics(Diagnostic left, Diagnostic right)
    {
        int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
        if (code != 0) return code;
        string leftPath = left.Locations.Count == 0 ? string.Empty : left.Locations[0].Path;
        string rightPath = right.Locations.Count == 0 ? string.Empty : right.Locations[0].Path;
        return StringComparer.Ordinal.Compare(leftPath, rightPath);
    }
}

internal sealed class KotlinTextReader
{
    public KotlinTextReader(IRepositoryFileSystem fileSystem, string rootPath)
    {
        Inner = new JavaTextReader(fileSystem, rootPath, "Kotlin/JVM", "FB8201");
    }

    public JavaTextReader Inner { get; }

    public Task<JavaTextReadResult> ReadAsync(EvidenceFact file, CancellationToken cancellationToken) =>
        Inner.ReadAsync(file, cancellationToken);
}
