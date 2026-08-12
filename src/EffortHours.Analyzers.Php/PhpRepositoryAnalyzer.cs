using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Php;

public sealed class PhpRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
{
    private readonly IRepositoryFileSystem _fileSystem;

    public PhpRepositoryAnalyzer()
        : this(PhysicalRepositoryFileSystem.Instance)
    {
    }

    public PhpRepositoryAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "php";

    public IReadOnlyList<LanguageAnalysisSupport> LanguageSupport { get; } =
        [new("php", LanguageAnalysisSupport.TokenBacked)];

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);
        string rootPath = _fileSystem.GetFullPath(repositoryPath);
        PhpTextReader reader = new(_fileSystem, rootPath);
        EvidenceFact[] allFiles = [.. evidence.Facts
            .Where(fact => fact.Kind == EvidenceKinds.File)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        EvidenceFact[] sourceFiles = [.. allFiles.Where(IsMaintainedPhpSource)];
        if (sourceFiles.Length == 0 && !allFiles.Any(IsComposerManifest))
            return new RepositoryAnalysisContribution();

        PhpComposerReadResult composer = await new PhpComposerReader(reader)
            .ReadAsync(allFiles, cancellationToken).ConfigureAwait(false);
        List<Diagnostic> diagnostics = [.. composer.Diagnostics];
        List<PhpFileAnalysis> analyses = [];
        foreach (EvidenceFact sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PhpTextReadResult read = await reader.ReadAsync(sourceFile, cancellationToken)
                .ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }

            PhpPackageModel owner = FindOwner(sourceFile.Scope, composer.Packages);
            PhpSyntaxAnalysis syntax = PhpSyntaxAnalyzer.Analyze(read.Text!, sourceFile.Scope, owner);
            PhpTemplateMetrics template = PhpTemplateAnalyzer.Analyze(read.Text!, sourceFile.Scope);
            analyses.Add(new PhpFileAnalysis(sourceFile, owner, syntax, template));
            if (syntax.Confidence == "low")
                diagnostics.Add(PhpEvidence.Diagnostic(
                    "FB8702",
                    DiagnosticSeverity.Warning,
                    $"PHP file '{sourceFile.Scope}' reached a tokenizer or structure safeguard; recognized evidence is incomplete and confidence is low.",
                    sourceFile.Scope));
        }

        PopulateInternalImports(analyses);
        PhpPackageModel[] packages = [.. composer.Packages.Select(package => package with
        {
            Role = Role(package, analyses.Where(file => file.Package.Directory == package.Directory)),
        })];
        analyses = [.. analyses.Select(file => file with
        {
            Package = packages.Single(package => package.Directory == file.Package.Directory),
        })];

        List<EvidenceFact> facts = [];
        foreach (PhpPackageModel package in packages.OrderBy(item => item.Directory, StringComparer.Ordinal))
        {
            PhpFileAnalysis[] owned = [.. analyses
                .Where(file => file.Package.Directory == package.Directory)
                .OrderBy(file => file.File.Scope, StringComparer.Ordinal)];
            facts.Add(PhpFactFactory.Package(package, owned));
            facts.AddRange(PhpFactFactory.Dependencies(package));
            EvidenceFact? build = PhpFactFactory.BuildConfiguration(package);
            if (build is not null) facts.Add(build);
            if (owned.Length == 0) continue;
            facts.Add(PhpFactFactory.SourceStructure(package, owned));
            facts.AddRange(owned.SelectMany(PhpFactFactory.FileSemantics));
        }

        facts.AddRange(CreateProjectReferences(packages, analyses));
        AddBoundaryDiagnostics(packages, analyses, diagnostics);
        diagnostics.Add(PhpEvidence.Diagnostic(
            "FB8700",
            DiagnosticSeverity.Information,
            "The PHP analyzer used bounded static Composer JSON, token, namespace/import, attribute, call-shape, path, and template analysis only; it did not invoke PHP, Composer, framework bootstraps, autoloaders, scripts, dependency resolution, containers, routes, reflection, tests, or generated caches, and emitted no source excerpts."));
        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        diagnostics.Sort(CompareDiagnostics);
        return new RepositoryAnalysisContribution { Facts = facts, Diagnostics = diagnostics };
    }

    private static IEnumerable<EvidenceFact> CreateProjectReferences(
        IReadOnlyList<PhpPackageModel> packages,
        IReadOnlyList<PhpFileAnalysis> analyses)
    {
        foreach (PhpPackageModel source in packages)
        {
            PhpFileAnalysis[] sourceFiles = [.. analyses.Where(file => file.Package.Directory == source.Directory)];
            foreach (PhpPackageModel target in packages.Where(target => target.Directory != source.Directory))
            {
                string[] namespaceRoots = [.. target.AutoloadNamespaces
                    .Concat(analyses.Where(file => file.Package.Directory == target.Directory)
                        .Select(file => file.Syntax.Metrics.Namespace))
                    .Where(value => value.Length > 0)
                    .Distinct(StringComparer.Ordinal)];
                PhpFileAnalysis[] importEvidence = [.. sourceFiles.Where(file =>
                    file.Syntax.Imports.ImportsSeen.Any(imported => namespaceRoots.Any(root =>
                        imported.Equals(root.TrimEnd('\\'), StringComparison.Ordinal) ||
                        imported.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.Ordinal))))];
                bool buildReference = source.PathRepositoryDirectories.Contains(target.Directory, StringComparer.Ordinal) ||
                    source.Dependencies.Any(dependency =>
                        dependency.Name.Equals(target.Name, StringComparison.OrdinalIgnoreCase));
                if (buildReference || importEvidence.Length > 0)
                    yield return PhpFactFactory.ProjectReference(source, target, importEvidence, buildReference);
            }
        }
    }

    private static void PopulateInternalImports(IReadOnlyList<PhpFileAnalysis> analyses)
    {
        foreach (IGrouping<string, PhpFileAnalysis> packageFiles in analyses
            .GroupBy(file => file.Package.Directory, StringComparer.Ordinal))
        {
            string[] roots = [.. packageFiles.First().Package.AutoloadNamespaces
                .Concat(packageFiles.Select(file => file.Syntax.Metrics.Namespace))
                .Where(value => value.Length > 0)
                .Select(value => value.TrimEnd('\\'))
                .Distinct(StringComparer.Ordinal)];
            foreach (PhpFileAnalysis file in packageFiles)
                file.Syntax.Metrics.InternalImports = file.Syntax.Imports.ImportsSeen.Count(imported =>
                    roots.Any(root => imported == root || imported.StartsWith(root + "\\", StringComparison.Ordinal)));
        }
    }

    private static void AddBoundaryDiagnostics(
        IReadOnlyList<PhpPackageModel> packages,
        IReadOnlyList<PhpFileAnalysis> analyses,
        List<Diagnostic> diagnostics)
    {
        int unresolvedPaths = packages.Sum(package => package.UnresolvedPaths);
        if (unresolvedPaths > 0)
            diagnostics.Add(PhpEvidence.Diagnostic(
                "FB8704",
                DiagnosticSeverity.Information,
                $"{unresolvedPaths} dynamic, external, missing, or out-of-scope Composer path value(s) were not resolved; literal in-scope metadata remains available."));
        int dynamic = analyses.Sum(file => file.Syntax.Metrics.DynamicIncludes +
            file.Syntax.Metrics.MagicMethods + file.Syntax.Metrics.ReflectionUsages);
        if (dynamic > 0)
            diagnostics.Add(PhpEvidence.Diagnostic(
                "FB8705",
                DiagnosticSeverity.Information,
                $"{dynamic} dynamic include, magic-method, variable-call, or reflection boundary signal(s) were inventoried; runtime targets and container resolution were not inferred."));
        int templates = analyses.Count(file => file.Template.Represented);
        if (templates > 0)
            diagnostics.Add(PhpEvidence.Diagnostic(
                "FB8706",
                DiagnosticSeverity.Information,
                $"{templates} maintained PHP/Blade template file(s) contributed bounded UI semantics; framework compilation, runtime rendering, linked frontend assets, and generated template caches were not analyzed by the PHP path."));
    }

    private static string Role(PhpPackageModel package, IEnumerable<PhpFileAnalysis> files)
    {
        PhpSourceMetrics[] metrics = [.. files
            .Where(file => !file.File.Tags.Contains("classification:test", StringComparer.Ordinal))
            .Select(file => file.Syntax.Metrics)];
        if (metrics.Any(item => item.ApiEndpoints + item.ApiTypes > 0)) return "server";
        if (metrics.Any(item => item.BackgroundUsages > 0)) return "worker";
        if (metrics.Any(item => item.EntryPoints + item.CliCommands > 0)) return "cli";
        return package.Role;
    }

    private static PhpPackageModel FindOwner(
        string path,
        IReadOnlyList<PhpPackageModel> packages) => packages
        .Where(package => PhpPath.IsWithin(path, package.Directory))
        .OrderByDescending(package => package.Directory.Length)
        .ThenBy(package => package.Directory, StringComparer.Ordinal)
        .First();

    private static bool IsMaintainedPhpSource(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File && fact.Tags.Contains("language:php", StringComparer.Ordinal) &&
        fact.Tags.Any(tag => tag is "role:source" or "role:test") &&
        !fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:minified" or
            "classification:vendored" or "content:binary");

    private static bool IsComposerManifest(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        Path.GetFileName(fact.Scope).Equals("composer.json", StringComparison.OrdinalIgnoreCase) &&
        !fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:vendored" or "content:binary");

    private static int CompareDiagnostics(Diagnostic left, Diagnostic right)
    {
        int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
        if (code != 0) return code;
        string leftPath = left.Locations.Count == 0 ? string.Empty : left.Locations[0].Path;
        string rightPath = right.Locations.Count == 0 ? string.Empty : right.Locations[0].Path;
        return StringComparer.Ordinal.Compare(leftPath, rightPath);
    }
}
