using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Java;

public sealed class JavaRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
{
    private readonly IRepositoryFileSystem _fileSystem;

    public JavaRepositoryAnalyzer()
        : this(PhysicalRepositoryFileSystem.Instance)
    {
    }

    public JavaRepositoryAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "java";

    public IReadOnlyList<LanguageAnalysisSupport> LanguageSupport { get; } =
        [new("java", LanguageAnalysisSupport.TokenBacked)];

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);
        string rootPath = _fileSystem.GetFullPath(repositoryPath);
        JavaTextReader reader = new(_fileSystem, rootPath);
        EvidenceFact[] allFiles = [.. evidence.Facts
            .Where(fact => fact.Kind == EvidenceKinds.File)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        EvidenceFact[] sourceFiles = [.. allFiles.Where(IsMaintainedJavaSource)];
        if (sourceFiles.Length == 0 && !allFiles.Any(IsJavaBuildDescriptor))
            return new RepositoryAnalysisContribution();

        JavaProjectReadResult readProjects = await new JavaProjectReader(reader)
            .ReadAsync(allFiles, sourceFiles.Length > 0, cancellationToken).ConfigureAwait(false);
        List<Diagnostic> diagnostics = [.. readProjects.Diagnostics];
        List<JavaFileAnalysis> analyses = [];
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
            JavaSyntaxAnalysis syntax = JavaSyntaxAnalyzer.Analyze(read.Text!, sourceFile.Scope);
            analyses.Add(new JavaFileAnalysis(sourceFile, owner, syntax));
            if (syntax.Confidence == "low")
                diagnostics.Add(JavaEvidence.Diagnostic(
                    "FB8102",
                    DiagnosticSeverity.Warning,
                    $"Java file '{sourceFile.Scope}' reached a tokenizer or structure safeguard; recognized evidence is incomplete and confidence is low.",
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

        List<EvidenceFact> facts = [];
        foreach (JavaProjectModel project in projects.OrderBy(item => item.Directory, StringComparer.Ordinal))
        {
            JavaFileAnalysis[] owned = [.. analyses
                .Where(file => file.Project.Directory == project.Directory)
                .OrderBy(file => file.File.Scope, StringComparer.Ordinal)];
            facts.Add(JavaFactFactory.Project(project, owned));
            facts.AddRange(JavaFactFactory.Packages(project, owned));
            facts.AddRange(JavaFactFactory.Dependencies(project));
            EvidenceFact? build = JavaFactFactory.BuildConfiguration(project);
            if (build is not null) facts.Add(build);
            if (owned.Length == 0) continue;
            facts.Add(JavaFactFactory.SourceStructure(project, owned));
            facts.AddRange(owned.SelectMany(JavaFactFactory.FileSemantics));
        }

        facts.AddRange(CreateProjectReferences(projects, analyses));
        AddBoundaryDiagnostics(projects, analyses, diagnostics);
        diagnostics.Add(JavaEvidence.Diagnostic(
            "FB8100",
            DiagnosticSeverity.Information,
            "The Java analyzer used bounded static Maven XML, conservative Gradle text, token, import, annotation, and filename analysis only; it did not invoke a JVM, Maven, Gradle, wrappers, annotation processors, compilers, tests, dependency resolution, reflection, or runtime discovery, and emitted no source excerpts."));
        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        diagnostics.Sort(CompareDiagnostics);
        return new RepositoryAnalysisContribution { Facts = facts, Diagnostics = diagnostics };
    }

    private static IEnumerable<EvidenceFact> CreateProjectReferences(
        IReadOnlyList<JavaProjectModel> projects,
        IReadOnlyList<JavaFileAnalysis> analyses)
    {
        foreach (JavaProjectModel source in projects)
        {
            JavaFileAnalysis[] sourceFiles = [.. analyses.Where(file => file.Project.Directory == source.Directory)];
            foreach (JavaProjectModel target in projects.Where(target => target.Directory != source.Directory))
            {
                string[] packageRoots = [.. analyses
                    .Where(file => file.Project.Directory == target.Directory)
                    .Select(file => file.Syntax.Metrics.PackageName)
                    .Where(name => name.Length > 0)
                    .Distinct(StringComparer.Ordinal)];
                if (packageRoots.Length == 0 && target.Coordinate is not null)
                    packageRoots = [target.Coordinate.Split(':')[0]];
                JavaFileAnalysis[] importEvidence = packageRoots.Length == 0
                    ? []
                    : [.. sourceFiles.Where(file => file.Syntax.Imports.ImportsSeen.Any(imported =>
                        packageRoots.Any(packageRoot => imported == packageRoot ||
                            imported.StartsWith(packageRoot + ".", StringComparison.Ordinal))))];
                bool uniqueTargetName = projects.Count(project =>
                    project.Name.Equals(target.Name, StringComparison.Ordinal)) == 1;
                bool buildReference = source.LocalProjectDirectories.Any(directory =>
                    directory.Equals(target.Directory, StringComparison.Ordinal) ||
                    uniqueTargetName && Path.GetFileName(directory).Equals(target.Name, StringComparison.Ordinal));
                buildReference |= source.Dependencies.Any(dependency =>
                    (target.Coordinate is not null && dependency == target.Coordinate) ||
                    (uniqueTargetName && (dependency.Equals(target.Name, StringComparison.Ordinal) ||
                        dependency.EndsWith(":" + target.Name, StringComparison.Ordinal))));
                if (buildReference || importEvidence.Length > 0)
                    yield return JavaFactFactory.ProjectReference(source, target, importEvidence, buildReference);
            }
        }
    }

    private static void AddBoundaryDiagnostics(
        IReadOnlyList<JavaProjectModel> projects,
        IReadOnlyList<JavaFileAnalysis> analyses,
        List<Diagnostic> diagnostics)
    {
        int unresolved = projects.Sum(project => project.UnresolvedValues);
        if (unresolved > 0)
            diagnostics.Add(JavaEvidence.Diagnostic(
                "FB8104",
                DiagnosticSeverity.Information,
                $"{unresolved} dynamic or property-backed Maven/Gradle value(s) were not resolved; literal build evidence remains available."));
        int processors = projects.Sum(project => project.AnnotationProcessors);
        if (processors > 0)
            diagnostics.Add(JavaEvidence.Diagnostic(
                "FB8105",
                DiagnosticSeverity.Information,
                $"{processors} annotation processor declaration(s) were inventoried; generated types and processor behavior were not executed or inferred."));
        int moduleFiles = analyses.Count(file =>
            file.Syntax.Metrics.ModuleName.Length > 0 ||
            Path.GetFileName(file.File.Scope).Equals("module-info.java", StringComparison.OrdinalIgnoreCase));
        if (moduleFiles > 0)
            diagnostics.Add(JavaEvidence.Diagnostic(
                "FB8106",
                DiagnosticSeverity.Information,
                $"{moduleFiles} Java module descriptor(s) were analyzed statically; module resolution and runtime accessibility were not proven."));
    }

    private static void PopulateInternalImports(IReadOnlyList<JavaFileAnalysis> analyses)
    {
        foreach (IGrouping<string, JavaFileAnalysis> projectFiles in analyses
            .GroupBy(file => file.Project.Directory, StringComparer.Ordinal))
        {
            HashSet<string> packages = [.. projectFiles
                .Select(file => file.Syntax.Metrics.PackageName)
                .Where(packageName => packageName.Length > 0)
                .Distinct(StringComparer.Ordinal)];
            foreach (JavaFileAnalysis file in projectFiles)
            {
                file.Syntax.Metrics.InternalImports = file.Syntax.Imports.ImportsSeen.Count(imported =>
                    IsImportFromDeclaredPackage(imported, packages));
            }
        }
    }

    private static bool IsImportFromDeclaredPackage(
        string imported,
        HashSet<string> declaredPackages)
    {
        string candidate = imported.EndsWith(".*", StringComparison.Ordinal)
            ? imported[..^2]
            : imported;
        while (candidate.Length > 0)
        {
            if (declaredPackages.Contains(candidate)) return true;
            int separator = candidate.LastIndexOf('.');
            if (separator < 0) return false;
            candidate = candidate[..separator];
        }

        return false;
    }

    private static string Role(IEnumerable<JavaFileAnalysis> files)
    {
        JavaSourceMetrics[] metrics = [.. files
            .Where(file => !file.File.Tags.Contains("classification:test", StringComparer.Ordinal))
            .Select(file => file.Syntax.Metrics)];
        if (metrics.Any(item => item.ApiEndpoints + item.ApiTypes > 0)) return "server";
        if (metrics.Any(item => item.BackgroundUsages > 0)) return "worker";
        if (metrics.Any(item => item.EntryPoints + item.CliCommands > 0)) return "cli";
        return "library";
    }

    private static JavaProjectModel FindOwner(
        string path,
        IReadOnlyList<JavaProjectModel> projects) => projects
        .Where(project => JavaPath.IsWithin(path, project.Directory))
        .OrderByDescending(project => project.Directory.Length)
        .ThenBy(project => project.Directory, StringComparer.Ordinal)
        .First();

    private static bool IsMaintainedJavaSource(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        fact.Tags.Contains("language:java", StringComparer.Ordinal) &&
        fact.Tags.Any(tag => tag is "role:source" or "role:test") &&
        !fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:minified" or
            "classification:vendored" or "content:binary");

    private static bool IsJavaBuildDescriptor(EvidenceFact fact)
    {
        string name = Path.GetFileName(fact.Scope).ToLowerInvariant();
        return name is "pom.xml" or "build.gradle" or "build.gradle.kts" or
            "settings.gradle" or "settings.gradle.kts";
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
