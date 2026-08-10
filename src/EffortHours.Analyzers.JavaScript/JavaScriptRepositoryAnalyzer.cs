using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.JavaScript;

public sealed class JavaScriptRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
{
    private readonly IRepositoryFileSystem _fileSystem;

    public JavaScriptRepositoryAnalyzer()
        : this(PhysicalRepositoryFileSystem.Instance)
    {
    }

    public JavaScriptRepositoryAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "javascript";

    public IReadOnlyList<string> Ecosystems { get; } = ["javascript", "typescript"];

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);

        string rootPath = _fileSystem.GetFullPath(repositoryPath);
        RepositoryTextReader textReader = new(_fileSystem, rootPath);
        JavaScriptPackageReadResult packageResult = await new JavaScriptPackageReader(textReader)
            .ReadAsync(evidence, cancellationToken).ConfigureAwait(false);
        JavaScriptConfigurationReadResult configurationResult =
            await new JavaScriptConfigurationReader(textReader)
                .ReadAsync(evidence, cancellationToken).ConfigureAwait(false);
        (IReadOnlyList<JavaScriptWorkspaceDeclaration> workspaces,
            IReadOnlyList<Diagnostic> workspaceDiagnostics) =
            await new JavaScriptWorkspaceReader(textReader)
                .ReadAsync(evidence, packageResult.Packages, cancellationToken)
                .ConfigureAwait(false);

        List<EvidenceFact> facts = [];
        List<Diagnostic> diagnostics =
        [
            .. packageResult.Diagnostics,
            .. configurationResult.Diagnostics,
            .. workspaceDiagnostics,
        ];
        facts.Add(CreateRepositoryFact(evidence, packageResult.Packages, configurationResult.Configurations, workspaces));
        AddPackageFacts(packageResult.Packages, facts, diagnostics);
        AddWorkspaceFacts(workspaces, packageResult.Packages, facts);
        AddConfigurationFacts(
            rootPath,
            configurationResult.Configurations,
            facts,
            diagnostics);
        Dictionary<string, JavaScriptSourceMetrics> structureByScope = new(StringComparer.Ordinal);
        Dictionary<string, List<EvidenceLocation>> structureLocations = new(StringComparer.Ordinal);
        List<AngularComponentMetadata> angularComponents = [];
        JavaScriptSourceAnalyzer sourceAnalyzer = new(textReader);
        foreach (EvidenceFact fileFact in evidence.Facts
            .Where(IsMaintainedSource)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            JavaScriptPackageModel? package = FindOwningPackage(fileFact.Scope, packageResult.Packages);
            JavaScriptFileAnalysis analysis = await sourceAnalyzer.AnalyzeAsync(
                fileFact,
                package,
                cancellationToken).ConfigureAwait(false);
            facts.AddRange(analysis.Facts);
            diagnostics.AddRange(analysis.Diagnostics);
            angularComponents.AddRange(analysis.AngularComponents);
            if (analysis.Metrics.Files == 0)
            {
                continue;
            }

            string scope = package?.Scope ?? ".";
            if (!structureByScope.TryGetValue(scope, out JavaScriptSourceMetrics? aggregate))
            {
                aggregate = new JavaScriptSourceMetrics();
                structureByScope.Add(scope, aggregate);
                structureLocations.Add(scope, []);
            }

            aggregate.Merge(analysis.Metrics);
            structureLocations[scope].Add(JavaScriptEvidence.Location(fileFact.Scope));
        }

        foreach ((string scope, JavaScriptSourceMetrics metrics) in structureByScope
            .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            facts.Add(CreateSourceStructureFact(scope, metrics, structureLocations[scope]));
        }

        FrontendAnalysisResult frontend = await new FrontendEvidenceAnalyzer(textReader)
            .AnalyzeAsync(
                evidence,
                packageResult.Packages,
                angularComponents,
                cancellationToken)
            .ConfigureAwait(false);
        facts.AddRange(frontend.Facts);
        diagnostics.AddRange(frontend.Diagnostics);

        AddPackageManagerDiagnostic(evidence, packageResult.Packages, diagnostics);
        diagnostics.Add(JavaScriptEvidence.Diagnostic(
            "FB4000",
            DiagnosticSeverity.Information,
            "The JavaScript/TypeScript/frontend analyzer parsed manifests, JSONC configuration, JavaScript/JSX syntax, bounded TypeScript token streams, static Angular component metadata, and bounded HTML/CSS-family semantics; it did not render, compile frameworks or preprocessors, run package managers, load executable configuration, install dependencies, transpile, or execute target code."));
        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        diagnostics.Sort(CompareDiagnostics);
        return new RepositoryAnalysisContribution
        {
            Facts = facts,
            Diagnostics = diagnostics,
        };
    }

    private static EvidenceFact CreateRepositoryFact(
        RepositoryEvidence evidence,
        IReadOnlyList<JavaScriptPackageModel> packages,
        IReadOnlyList<JavaScriptConfigurationModel> configurations,
        IReadOnlyList<JavaScriptWorkspaceDeclaration> workspaces)
    {
        EvidenceFact[] files = [.. evidence.Facts.Where(fact => fact.Kind == EvidenceKinds.File)];
        string[] packageManagers = DetectPackageManagers(files, packages);
        bool hasFrontendAssets = files.Any(file =>
            JavaScriptEvidence.FindTagValue(file.Tags, "language:") is
                "css" or "scss" or "sass" or "less" or "html");
        List<string> tags =
        [
            .. packageManagers.Select(manager => $"package-manager:{manager}"),
            "target-code:not-executed",
            "script-bodies:not-emitted",
        ];
        if (hasFrontendAssets)
        {
            tags.Add("frontend-assets:present");
            if (packages.Count == 0)
            {
                tags.Add("package-role:web-ui");
            }
        }

        return JavaScriptEvidence.Fact(
            "javascript:repository",
            EvidenceKinds.JavaScriptPackage,
            ".",
            "Static JavaScript and TypeScript package, workspace, configuration, and source inventory.",
            EvidenceSourceKind.Observed,
            "manifest, lockfile, JSONC configuration, AST, and token-stream classification",
            packages.Select(package => JavaScriptEvidence.Location(package.ManifestPath)),
            [
                JavaScriptEvidence.Measurement("packages", packages.Count, "packages"),
                JavaScriptEvidence.Measurement("workspace-declarations", workspaces.Count, "workspaces"),
                JavaScriptEvidence.Measurement("configurations", configurations.Count, "files"),
                JavaScriptEvidence.Measurement("dependencies", packages.Sum(package => package.Dependencies.Count), "references"),
                JavaScriptEvidence.Measurement("scripts", packages.Sum(package => package.ScriptNames.Count), "scripts"),
                JavaScriptEvidence.Measurement("lockfiles", CountLockfiles(files), "files"),
            ],
            tags);
    }

    private static void AddPackageFacts(
        IReadOnlyList<JavaScriptPackageModel> packages,
        List<EvidenceFact> facts,
        List<Diagnostic> diagnostics)
    {
        Dictionary<string, JavaScriptPackageModel[]> packagesByName = packages
            .Where(package => IsSafePackageName(package.Name))
            .GroupBy(package => package.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        foreach (JavaScriptPackageModel package in packages.OrderBy(
            package => package.ManifestPath,
            StringComparer.Ordinal))
        {
            List<string> tags =
            [
                $"package-role:{package.Role}",
                package.IsPrivate ? "package:private" : "package:publishable-or-unspecified",
                .. package.Technologies.Select(technology => $"technology:{technology}"),
                .. package.ScriptNames.Where(IsSafeTagValue).Select(script => $"script:{script}"),
            ];
            AddTag(tags, "module-type", package.ModuleType);
            AddTag(tags, "package-manager", package.PackageManager);
            if (package.HasBin)
            {
                tags.Add("package:cli-bin");
            }

            if (package.HasLibraryExports)
            {
                tags.Add("package:library-exports");
            }

            facts.Add(JavaScriptEvidence.Fact(
                $"javascript:package:{package.ManifestPath}",
                EvidenceKinds.JavaScriptPackage,
                package.Scope,
                $"JavaScript package declared by '{package.ManifestPath}'.",
                EvidenceSourceKind.Inferred,
                "static package.json property and dependency classification",
                [JavaScriptEvidence.Location(package.ManifestPath)],
                [
                    JavaScriptEvidence.Measurement("dependencies", package.Dependencies.Count, "references"),
                    JavaScriptEvidence.Measurement("scripts", package.ScriptNames.Count, "scripts"),
                    JavaScriptEvidence.Measurement("workspace-patterns", package.WorkspacePatterns.Count, "patterns"),
                ],
                tags));

            foreach (JavaScriptDependency dependency in package.Dependencies)
            {
                if (packagesByName.TryGetValue(dependency.Name, out JavaScriptPackageModel[]? targets) &&
                    targets.Length == 1 && targets[0].ManifestPath != package.ManifestPath)
                {
                    AddLocalPackageReference(package, dependency, targets[0], facts);
                }
                else
                {
                    AddExternalPackageReference(package, dependency, facts);
                    if (targets is { Length: > 1 })
                    {
                        diagnostics.Add(JavaScriptEvidence.Diagnostic(
                            "FB4009",
                            DiagnosticSeverity.Warning,
                            $"A dependency in '{package.ManifestPath}' matches multiple local package names; it was retained as an external package reference.",
                            package.ManifestPath));
                    }
                }
            }

            if (package.Coverage is not null)
            {
                facts.Add(CreateCoverageFact(package));
            }
        }
    }

    private static void AddExternalPackageReference(
        JavaScriptPackageModel package,
        JavaScriptDependency dependency,
        List<EvidenceFact> facts)
    {
        TechnologyClassification? technology = JavaScriptTechnologyCatalog.Classify(dependency.Name);
        List<string> tags =
        [
            $"dependency-kind:{dependency.Kind}",
            $"specifier-kind:{ClassifySpecifier(dependency.Specifier)}",
        ];
        string displayName;
        if (IsSafePackageName(dependency.Name))
        {
            displayName = $"'{dependency.Name}'";
            tags.Add($"package:{dependency.Name.ToLowerInvariant()}");
        }
        else
        {
            string token = JavaScriptEvidence.StableToken(dependency.Name);
            displayName = $"with opaque identifier '{token}'";
            tags.Add($"package:opaque-{token}");
        }

        string? safeSpecifier = SafeVersionSpecifier(dependency.Specifier);
        AddTag(tags, "version", safeSpecifier);
        if (technology is not null)
        {
            tags.Add($"package-family:{technology.Family}");
            tags.Add($"technology:{technology.Name}");
        }

        facts.Add(JavaScriptEvidence.Fact(
            $"javascript:package-reference:{package.ManifestPath}:{JavaScriptEvidence.IdToken(dependency.Name)}:{dependency.Kind}",
            EvidenceKinds.PackageReference,
            package.Scope,
            $"JavaScript package dependency {displayName}.",
            EvidenceSourceKind.Observed,
            "package.json dependency-map entry",
            [JavaScriptEvidence.Location(package.ManifestPath)],
            tags: tags));
    }

    private static void AddLocalPackageReference(
        JavaScriptPackageModel source,
        JavaScriptDependency dependency,
        JavaScriptPackageModel target,
        List<EvidenceFact> facts)
    {
        facts.Add(JavaScriptEvidence.Fact(
            $"javascript:project-reference:{source.ManifestPath}:{target.ManifestPath}:{dependency.Kind}",
            EvidenceKinds.ProjectReference,
            source.Scope,
            $"Package '{source.ManifestPath}' references local package '{target.ManifestPath}'.",
            EvidenceSourceKind.Inferred,
            "exact local package-name match against a declared dependency",
            [
                JavaScriptEvidence.Location(source.ManifestPath),
                JavaScriptEvidence.Location(target.ManifestPath),
            ],
            tags:
            [
                $"dependency-kind:{dependency.Kind}",
                "reference:resolved",
                $"target:{target.ManifestPath}",
            ]));
    }

    private static EvidenceFact CreateCoverageFact(JavaScriptPackageModel package)
    {
        JavaScriptCoverageDeclaration coverage = package.Coverage!;
        List<EvidenceMeasurement> measurements = [];
        AddCoverageMeasurement(measurements, "lines", coverage.Lines);
        AddCoverageMeasurement(measurements, "branches", coverage.Branches);
        AddCoverageMeasurement(measurements, "functions", coverage.Functions);
        AddCoverageMeasurement(measurements, "statements", coverage.Statements);
        return JavaScriptEvidence.Fact(
            $"javascript:coverage:{package.ManifestPath}",
            EvidenceKinds.Coverage,
            package.Scope,
            $"Jest global coverage thresholds are declared in '{package.ManifestPath}'.",
            EvidenceSourceKind.DeclaredAssumed,
            "static package.json Jest coverageThreshold.global parsing",
            [JavaScriptEvidence.Location(package.ManifestPath)],
            measurements,
            ["coverage:declared-and-assumed", "coverage-tool:jest"]);
    }

    private static void AddWorkspaceFacts(
        IReadOnlyList<JavaScriptWorkspaceDeclaration> workspaces,
        IReadOnlyList<JavaScriptPackageModel> packages,
        List<EvidenceFact> facts)
    {
        foreach (JavaScriptWorkspaceDeclaration workspace in workspaces.OrderBy(
            workspace => workspace.SourcePath,
            StringComparer.Ordinal))
        {
            JavaScriptPackageModel[] matched = [.. packages
                .Where(package => package.ManifestPath != workspace.SourcePath)
                .Where(package => TryGetRelativeScope(workspace.Scope, package.Scope, out string relative) &&
                    WorkspacePatternMatcher.Includes(workspace.Patterns, relative))
                .OrderBy(package => package.ManifestPath, StringComparer.Ordinal)];
            facts.Add(JavaScriptEvidence.Fact(
                $"javascript:workspace:{workspace.SourcePath}",
                EvidenceKinds.JavaScriptWorkspace,
                workspace.Scope,
                $"JavaScript workspace declared by '{workspace.SourcePath}'.",
                EvidenceSourceKind.Observed,
                "static package.json or pnpm workspace pattern matching against discovered manifests",
                [
                    JavaScriptEvidence.Location(workspace.SourcePath),
                    .. matched.Select(package => JavaScriptEvidence.Location(package.ManifestPath)),
                ],
                [
                    JavaScriptEvidence.Measurement("patterns", workspace.Patterns.Count, "patterns"),
                    JavaScriptEvidence.Measurement("matched-packages", matched.Length, "packages"),
                ],
                [$"package-manager:{workspace.PackageManager}"]));
        }
    }

    private static void AddConfigurationFacts(
        string rootPath,
        IReadOnlyList<JavaScriptConfigurationModel> configurations,
        List<EvidenceFact> facts,
        List<Diagnostic> diagnostics)
    {
        HashSet<string> knownPaths = configurations.Select(configuration => configuration.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (JavaScriptConfigurationModel configuration in configurations.OrderBy(
            configuration => configuration.Path,
            StringComparer.Ordinal))
        {
            List<ConfigurationReference> references = [];
            if (!string.IsNullOrWhiteSpace(configuration.Extends))
            {
                references.Add(ResolveConfigurationReference(
                    rootPath,
                    configuration,
                    configuration.Extends!,
                    "extends",
                    knownPaths));
            }

            references.AddRange(configuration.References.Select(reference =>
                ResolveConfigurationReference(
                    rootPath,
                    configuration,
                    reference,
                    "project-reference",
                    knownPaths)));
            List<string> tags =
            [
                $"configuration-kind:{configuration.Kind}",
                .. configuration.Tags,
            ];
            if (references.Any(reference => reference.Status == "external-package"))
            {
                tags.Add("extends:external-package");
            }

            facts.Add(JavaScriptEvidence.Fact(
                $"javascript:configuration:{configuration.Path}",
                EvidenceKinds.JavaScriptConfiguration,
                configuration.Scope,
                $"Static {configuration.Kind} compiler configuration '{configuration.Path}'.",
                EvidenceSourceKind.Observed,
                "JSON-with-comments compiler option and project-reference parsing",
                [JavaScriptEvidence.Location(configuration.Path)],
                [
                    JavaScriptEvidence.Measurement("references", configuration.References.Count, "references"),
                    JavaScriptEvidence.Measurement("compiler-options", configuration.Tags.Count, "options"),
                ],
                tags));

            foreach (ConfigurationReference reference in references)
            {
                if (reference.TargetPath is not null)
                {
                    facts.Add(JavaScriptEvidence.Fact(
                        $"javascript:configuration-reference:{configuration.Path}:{reference.TargetPath}:{reference.Kind}",
                        EvidenceKinds.ProjectReference,
                        configuration.Scope,
                        $"Configuration '{configuration.Path}' references '{reference.TargetPath}'.",
                        EvidenceSourceKind.Inferred,
                        "static relative configuration-path resolution",
                        [
                            JavaScriptEvidence.Location(configuration.Path),
                            JavaScriptEvidence.Location(reference.TargetPath),
                        ],
                        tags: [$"reference-kind:{reference.Kind}", "reference:resolved", $"target:{reference.TargetPath}"]));
                }
                else if (reference.Status is "outside-scope" or "unresolved")
                {
                    diagnostics.Add(JavaScriptEvidence.Diagnostic(
                        reference.Status == "outside-scope" ? "FB4011" : "FB4010",
                        DiagnosticSeverity.Warning,
                        reference.Status == "outside-scope"
                            ? $"A configuration reference in '{configuration.Path}' resolves outside repository scope and was not exposed."
                            : $"A relative configuration reference in '{configuration.Path}' could not be resolved statically.",
                        configuration.Path));
                }
            }
        }
    }

    private static ConfigurationReference ResolveConfigurationReference(
        string rootPath,
        JavaScriptConfigurationModel source,
        string rawReference,
        string kind,
        IReadOnlySet<string> knownPaths)
    {
        if (!rawReference.StartsWith('.') && !Path.IsPathRooted(rawReference))
        {
            return new ConfigurationReference(kind, "external-package", null);
        }

        if (rawReference.Length > 4096)
        {
            return new ConfigurationReference(kind, "unresolved", null);
        }

        string relative;
        try
        {
            string sourceDirectory = source.Scope == "."
                ? rootPath
                : Path.Combine(rootPath, source.Scope.Replace('/', Path.DirectorySeparatorChar));
            string fullTarget = Path.GetFullPath(Path.Combine(
                sourceDirectory,
                rawReference.Replace('/', Path.DirectorySeparatorChar)));
            relative = Path.GetRelativePath(rootPath, fullTarget).Replace('\\', '/');
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new ConfigurationReference(kind, "unresolved", null);
        }

        if (relative == ".." || Path.IsPathRooted(relative) ||
            relative.StartsWith("../", StringComparison.Ordinal))
        {
            return new ConfigurationReference(kind, "outside-scope", null);
        }

        string[] candidates =
        [
            relative,
            relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? relative : relative + ".json",
            relative.TrimEnd('/') + "/tsconfig.json",
        ];
        string? target = candidates.FirstOrDefault(knownPaths.Contains);
        return target is null
            ? new ConfigurationReference(kind, "unresolved", null)
            : new ConfigurationReference(kind, "resolved", target);
    }

    private static EvidenceFact CreateSourceStructureFact(
        string scope,
        JavaScriptSourceMetrics metrics,
        IReadOnlyList<EvidenceLocation> locations) => JavaScriptEvidence.Fact(
            $"javascript:source-structure:{scope}",
            EvidenceKinds.SourceStructure,
            scope,
            $"JavaScript and TypeScript source structure for package scope '{scope}'.",
            EvidenceSourceKind.Measured,
            "Acornima JavaScript/JSX AST traversal and bounded TypeScript/TSX token analysis",
            locations,
            [
                JavaScriptEvidence.Measurement("files", metrics.Files, "files"),
                JavaScriptEvidence.Measurement("parser-backed-files", metrics.ParserBackedFiles, "files"),
                JavaScriptEvidence.Measurement("token-backed-files", metrics.LexerBackedFiles, "files"),
                JavaScriptEvidence.Measurement("imports", metrics.Imports, "imports"),
                JavaScriptEvidence.Measurement("dynamic-imports", metrics.DynamicImports, "imports"),
                JavaScriptEvidence.Measurement("exports", metrics.Exports, "exports"),
                JavaScriptEvidence.Measurement("functions", metrics.Functions, "functions"),
                JavaScriptEvidence.Measurement("methods", metrics.Methods, "methods"),
                JavaScriptEvidence.Measurement("async-functions", metrics.AsyncFunctions, "functions"),
                JavaScriptEvidence.Measurement("classes", metrics.Classes, "classes"),
                JavaScriptEvidence.Measurement("interfaces", metrics.Interfaces, "interfaces"),
                JavaScriptEvidence.Measurement("type-aliases", metrics.TypeAliases, "aliases"),
                JavaScriptEvidence.Measurement("enums", metrics.Enums, "enums"),
                JavaScriptEvidence.Measurement("branch-points", metrics.BranchPoints, "branches"),
                JavaScriptEvidence.Measurement("calls", metrics.Calls, "calls"),
                JavaScriptEvidence.Measurement("decorators", metrics.Decorators, "decorators"),
            ],
            [
                metrics.ParserBackedFiles > 0 ? "syntax:parser-backed" : "syntax:no-parser-backed-files",
                metrics.LexerBackedFiles > 0 ? "syntax:token-backed" : "syntax:no-token-backed-files",
                .. metrics.Technologies.Select(technology => $"technology:{technology}"),
            ]);

    private static JavaScriptPackageModel? FindOwningPackage(
        string path,
        IReadOnlyList<JavaScriptPackageModel> packages) => packages
            .Where(package => package.Scope == "." ||
                path.StartsWith(package.Scope + "/", StringComparison.Ordinal))
            .OrderByDescending(package => package.Scope.Length)
            .ThenBy(package => package.Scope, StringComparer.Ordinal)
            .FirstOrDefault();

    private static bool IsMaintainedSource(EvidenceFact fact)
    {
        if (fact.Kind != EvidenceKinds.File ||
            fact.Tags.Contains("classification:generated", StringComparer.Ordinal) ||
            fact.Tags.Contains("classification:minified", StringComparer.Ordinal) ||
            fact.Tags.Contains("classification:vendored", StringComparer.Ordinal) ||
            fact.Tags.Contains("content:binary", StringComparer.Ordinal))
        {
            return false;
        }

        string? language = JavaScriptEvidence.FindTagValue(fact.Tags, "language:");
        return language is "javascript" or "typescript" or "vue" or "svelte";
    }

    private static void AddPackageManagerDiagnostic(
        RepositoryEvidence evidence,
        IReadOnlyList<JavaScriptPackageModel> packages,
        List<Diagnostic> diagnostics)
    {
        string[] managers = DetectPackageManagers(
            [.. evidence.Facts.Where(fact => fact.Kind == EvidenceKinds.File)],
            packages);
        if (managers.Length > 1)
        {
            diagnostics.Add(JavaScriptEvidence.Diagnostic(
                "FB4003",
                DiagnosticSeverity.Information,
                $"Multiple JavaScript package-manager conventions were detected ({string.Join(", ", managers)}); EffortHours did not choose or execute one."));
        }
    }

    private static string[] DetectPackageManagers(
        IReadOnlyList<EvidenceFact> files,
        IReadOnlyList<JavaScriptPackageModel> packages)
    {
        HashSet<string> managers = new(StringComparer.Ordinal);
        foreach (EvidenceFact file in files)
        {
            string name = Path.GetFileName(file.Scope).ToLowerInvariant();
            switch (name)
            {
                case "package-lock.json":
                case "npm-shrinkwrap.json":
                    managers.Add("npm");
                    break;
                case "pnpm-lock.yaml":
                case "pnpm-workspace.yaml":
                case "pnpm-workspace.yml":
                    managers.Add("pnpm");
                    break;
                case "yarn.lock":
                    managers.Add("yarn");
                    break;
                case "bun.lock":
                case "bun.lockb":
                    managers.Add("bun");
                    break;
            }
        }

        foreach (string manager in packages
            .Select(package => package.PackageManager)
            .Where(manager => manager is not null)
            .Select(manager => manager!))
        {
            if (IsSafeTagValue(manager))
            {
                managers.Add(manager.ToLowerInvariant());
            }
        }

        return [.. managers.OrderBy(manager => manager, StringComparer.Ordinal)];
    }

    private static int CountLockfiles(IEnumerable<EvidenceFact> files) => files.Count(file =>
        Path.GetFileName(file.Scope).ToLowerInvariant() is
            "package-lock.json" or "npm-shrinkwrap.json" or "pnpm-lock.yaml" or
            "yarn.lock" or "bun.lock" or "bun.lockb");

    private static bool TryGetRelativeScope(
        string rootScope,
        string packageScope,
        out string relative)
    {
        if (rootScope == ".")
        {
            relative = packageScope;
            return packageScope != ".";
        }

        if (packageScope.StartsWith(rootScope + "/", StringComparison.Ordinal))
        {
            relative = packageScope[(rootScope.Length + 1)..];
            return true;
        }

        relative = string.Empty;
        return false;
    }

    private static string ClassifySpecifier(string specifier)
    {
        string value = specifier.Trim();
        if (value.StartsWith("workspace:", StringComparison.OrdinalIgnoreCase))
        {
            return "workspace";
        }

        if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("link:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith('.') || Path.IsPathRooted(value))
        {
            return "path";
        }

        if (value.StartsWith("git", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
        {
            return "remote";
        }

        if (value.StartsWith("npm:", StringComparison.OrdinalIgnoreCase))
        {
            return "alias";
        }

        return "registry-range";
    }

    private static string? SafeVersionSpecifier(string specifier)
    {
        string value = specifier.Trim();
        return value.Length is > 0 and <= 80 && value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '+' or '^' or '~' or
                '<' or '>' or '=' or '*' or '|' or ' ')
            ? value
            : null;
    }

    private static bool IsSafePackageName(string? name) =>
        name is { Length: > 0 and <= 214 } &&
        !name.Any(char.IsControl) &&
        name.All(character => char.IsAsciiLetterOrDigit(character) ||
            character is '@' or '/' or '.' or '_' or '-');

    private static bool IsSafeTagValue(string value) => value.Length is > 0 and <= 80 &&
        !value.Any(character => char.IsControl(character) || character is ':' or '\\' or '/');

    private static void AddTag(List<string> tags, string name, string? value)
    {
        if (value is not null && IsSafeTagValue(value))
        {
            tags.Add($"{name}:{value.ToLowerInvariant()}");
        }
    }

    private static void AddCoverageMeasurement(
        List<EvidenceMeasurement> measurements,
        string name,
        decimal? value)
    {
        if (value is not null)
        {
            if (value.Value is >= 0m and <= 100m)
            {
                measurements.Add(JavaScriptEvidence.Measurement(name, value.Value, "percent"));
            }
            else if (value.Value < 0m)
            {
                measurements.Add(JavaScriptEvidence.Measurement(
                    $"{name}-maximum-uncovered",
                    decimal.Abs(value.Value),
                    "items"));
            }
        }
    }

    private static int CompareDiagnostics(Diagnostic left, Diagnostic right)
    {
        int codeComparison = StringComparer.Ordinal.Compare(left.Code, right.Code);
        if (codeComparison != 0)
        {
            return codeComparison;
        }

        string leftPath = left.Locations.Count == 0 ? string.Empty : left.Locations[0].Path;
        string rightPath = right.Locations.Count == 0 ? string.Empty : right.Locations[0].Path;
        int pathComparison = StringComparer.Ordinal.Compare(leftPath, rightPath);
        return pathComparison != 0
            ? pathComparison
            : StringComparer.Ordinal.Compare(left.Message, right.Message);
    }

    private sealed record ConfigurationReference(
        string Kind,
        string Status,
        string? TargetPath);
}
