using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Cpp;

public sealed class CppRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
{
    private readonly IRepositoryFileSystem _fileSystem;

    public CppRepositoryAnalyzer()
        : this(PhysicalRepositoryFileSystem.Instance)
    {
    }

    public CppRepositoryAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "cpp";

    public IReadOnlyList<LanguageAnalysisSupport> LanguageSupport { get; } =
    [
        new("c", LanguageAnalysisSupport.TokenBacked),
        new("cpp", LanguageAnalysisSupport.TokenBacked),
    ];

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);
        string rootPath = _fileSystem.GetFullPath(repositoryPath);
        CppTextReader reader = new(_fileSystem, rootPath);
        EvidenceFact[] allFiles = [.. evidence.Facts.Where(fact => fact.Kind == EvidenceKinds.File)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        EvidenceFact[] sourceFiles = [.. allFiles.Where(IsMaintainedSource)];
        if (sourceFiles.Length == 0 && !allFiles.Any(IsActivationArtifact))
            return new RepositoryAnalysisContribution();

        CppBuildReadResult build = await new CppBuildMetadataReader(reader, rootPath)
            .ReadAsync(allFiles, sourceFiles.Length > 0, cancellationToken).ConfigureAwait(false);
        List<Diagnostic> diagnostics = [.. build.Diagnostics];
        List<CppRawFileAnalysis> raw = [];
        foreach (EvidenceFact sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CppTextReadResult read = await reader.ReadAsync(sourceFile, cancellationToken).ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }
            CppProjectModel owner = CppProjectResolver.PreliminaryOwner(sourceFile.Scope, build.Projects);
            CppSyntaxAnalysis syntax = CppSyntaxAnalyzer.Analyze(
                read.Text!,
                sourceFile.Scope,
                owner.DependencyNames,
                cancellationToken);
            raw.Add(new CppRawFileAnalysis(sourceFile, syntax));
            if (syntax.Confidence == "low")
                diagnostics.Add(CppEvidence.Diagnostic(
                    "FB8902",
                    DiagnosticSeverity.Warning,
                    $"C/C++ file '{sourceFile.Scope}' reached a tokenizer, preprocessor, or structure safeguard; recognized evidence is incomplete and confidence is low.",
                    sourceFile.Scope));
        }

        IReadOnlyList<CppFileAnalysis> analyses = CppProjectResolver.Resolve(raw, build.Projects);
        CppProjectModel[] projects = [.. build.Projects.Select(project => project with
        {
            Role = Role(project, analyses.Where(file => file.Project.Directory == project.Directory)),
        })];
        Dictionary<string, CppProjectModel> projectsByDirectory = projects
            .ToDictionary(project => project.Directory, StringComparer.Ordinal);
        analyses = [.. analyses.Select(file => file with
        {
            Project = projectsByDirectory[file.Project.Directory],
        })];

        List<EvidenceFact> facts = [];
        foreach (CppProjectModel project in projects.OrderBy(item => item.Directory, StringComparer.Ordinal))
        {
            CppFileAnalysis[] owned = [.. analyses
                .Where(file => file.Project.Directory == project.Directory)
                .OrderBy(file => file.File.Scope, StringComparer.Ordinal)];
            facts.Add(CppFactFactory.Project(project, owned));
            EvidenceFact? buildFact = CppFactFactory.BuildConfiguration(project, owned);
            if (buildFact is not null) facts.Add(buildFact);
            EvidenceFact? delivery = CppFactFactory.Delivery(project);
            if (delivery is not null) facts.Add(delivery);
            CppFileAnalysis[] production = [.. owned.Where(file => !IsTest(file))];
            if (production.Length > 0) facts.Add(CppFactFactory.SourceStructure(project, production));
            facts.AddRange(owned.SelectMany(CppFactFactory.FileSemantics));
        }
        facts.AddRange(ProjectReferences(projects, analyses));
        AddBoundaryDiagnostics(allFiles, projects, analyses, diagnostics);
        diagnostics.Add(CppEvidence.Diagnostic(
            "FB8900",
            DiagnosticSeverity.Information,
            "The C/C++ analyzer used bounded static build metadata, managed tokenization, declaration recognition, include resolution, and non-expanding preprocessor analysis only; it invoked no compiler, preprocessor, linker, generator, build system, package manager, tests, sanitizer, or target code, resolved no system headers, and emitted no source excerpts, literals, macro bodies, compiler commands, or recipe bodies."));
        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        diagnostics.Sort(CompareDiagnostics);
        return new RepositoryAnalysisContribution { Facts = facts, Diagnostics = diagnostics };
    }

    private static IEnumerable<EvidenceFact> ProjectReferences(
        IReadOnlyList<CppProjectModel> projects,
        IReadOnlyList<CppFileAnalysis> analyses)
    {
        Dictionary<string, CppProjectModel> ownerByPath = analyses.ToDictionary(
            file => file.File.Scope,
            file => file.Project,
            StringComparer.Ordinal);
        foreach (CppProjectModel source in projects)
        {
            foreach (CppProjectModel target in projects.Where(target => target.Directory != source.Directory))
            {
                CppFileAnalysis[] referencing = [.. analyses.Where(file =>
                    file.Project.Directory == source.Directory &&
                    file.LocalIncludes.Any(path => ownerByPath.TryGetValue(path, out CppProjectModel? owner) &&
                        owner.Directory == target.Directory))];
                bool buildReference = source.LocalReferenceDirectories.Contains(
                        target.Directory,
                        StringComparer.Ordinal) ||
                    source.DependencyNames.Any(dependency =>
                        target.TargetNames.Contains(dependency, StringComparer.OrdinalIgnoreCase));
                if (referencing.Length > 0 || buildReference)
                    yield return CppFactFactory.ProjectReference(
                        source,
                        target,
                        referencing,
                        buildReference);
            }
        }
    }

    private static void AddBoundaryDiagnostics(
        IReadOnlyList<EvidenceFact> allFiles,
        IReadOnlyList<CppProjectModel> projects,
        IReadOnlyList<CppFileAnalysis> analyses,
        List<Diagnostic> diagnostics)
    {
        int unresolved = projects.Sum(project => project.UnresolvedValues);
        int ambiguous = analyses.Count(file => file.OwnershipAmbiguous);
        if (unresolved + ambiguous > 0)
            diagnostics.Add(CppEvidence.Diagnostic(
                "FB8904",
                DiagnosticSeverity.Information,
                $"{unresolved} dynamic, conditional, malformed, or unsupported build value(s) and {ambiguous} conflicting source/header ownership claim(s) remain unresolved; each maintained body is still analyzed at most once."));

        int macros = analyses.Sum(file => file.Syntax.Preprocessor.MacroDefinitions);
        int conditions = analyses.Sum(file => file.Syntax.Preprocessor.ConditionalGroups);
        if (macros + conditions > 0)
            diagnostics.Add(CppEvidence.Diagnostic(
                "FB8905",
                DiagnosticSeverity.Information,
                $"{macros} macro definition(s) and {conditions} conditional group(s) were inventoried without expansion or active-branch selection; sibling branch structure is normalized componentwise and build variability remains uncertainty."));

        int generated = allFiles.Count(file =>
            file.Tags.Any(tag => tag is "language:c" or "language:cpp") &&
            file.Tags.Contains("classification:generated", StringComparer.Ordinal));
        if (generated > 0)
            diagnostics.Add(CppEvidence.Diagnostic(
                "FB8906",
                DiagnosticSeverity.Information,
                $"{generated} generated C/C++ body file(s) remain excluded; protobuf/gRPC, Qt, SWIG, bindgen, parser-generator, unity, amalgamation, and other generated bodies are not reconstructed or expanded."));
    }

    private static string Role(CppProjectModel project, IEnumerable<CppFileAnalysis> files)
    {
        CppSourceMetrics[] metrics = [.. files.Where(file => !IsTest(file))
            .Select(file => file.Syntax.Metrics)];
        if (metrics.Any(item => item.UiSurfaces > 0)) return "desktop-ui";
        if (metrics.Any(item => item.ApiSurfaces > 0)) return "server";
        if (metrics.Any(item => item.ConcurrencyUsages + item.AsyncUnits > 0)) return "worker";
        if (metrics.Any(item => item.CliCommands + item.EntryPoints > 0)) return "cli";
        return project.Role;
    }

    private static bool IsMaintainedSource(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        fact.Tags.Any(tag => tag is "language:c" or "language:cpp") &&
        fact.Tags.Any(tag => tag is "role:source" or "role:test") &&
        !fact.Tags.Any(tag => tag is "classification:generated" or "classification:minified" or
            "classification:vendored" or "content:binary");

    private static bool IsBuildArtifact(EvidenceFact fact)
    {
        if (fact.Kind != EvidenceKinds.File || fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:vendored" or "content:binary")) return false;
        string name = Path.GetFileName(fact.Scope).ToLowerInvariant();
        string extension = Path.GetExtension(fact.Scope).ToLowerInvariant();
        return name is "cmakelists.txt" or "makefile" or "gnumakefile" or "meson.build" or
            "meson.options" or "meson_options.txt" or "cmakepresets.json" or
            "cmakeuserpresets.json" or "compile_commands.json" or
            "vcpkg.json" or "conanfile.txt" ||
            extension is ".vcxproj" or ".props" or ".cmake" or ".mk";
    }

    private static bool IsActivationArtifact(EvidenceFact fact)
    {
        if (!IsBuildArtifact(fact)) return false;
        string name = Path.GetFileName(fact.Scope).ToLowerInvariant();
        string extension = Path.GetExtension(fact.Scope).ToLowerInvariant();
        return name is "cmakelists.txt" or "meson.build" or "cmakepresets.json" or
            "cmakeuserpresets.json" or "compile_commands.json" or "vcpkg.json" or
            "conanfile.txt" || extension == ".vcxproj";
    }

    private static bool IsTest(CppFileAnalysis file) =>
        file.File.Tags.Contains("classification:test", StringComparer.Ordinal) ||
        file.Syntax.Metrics.TestCases + file.Syntax.Metrics.Benchmarks + file.Syntax.Metrics.FuzzTargets > 0;

    private static int CompareDiagnostics(Diagnostic left, Diagnostic right)
    {
        int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
        if (code != 0) return code;
        string leftPath = left.Locations.Count == 0 ? string.Empty : left.Locations[0].Path;
        string rightPath = right.Locations.Count == 0 ? string.Empty : right.Locations[0].Path;
        return StringComparer.Ordinal.Compare(leftPath, rightPath);
    }
}
