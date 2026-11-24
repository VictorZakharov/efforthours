using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Unreal;

/// <summary>
/// Static Unreal Engine project analysis for checkout-free snapshots. Ownership comes from
/// <c>.uproject</c>/<c>.uplugin</c> descriptors; package value comes only from asset kind, naming
/// convention, and serialized size (binary length or Git LFS pointer size). No package body is
/// parsed, loaded, imported, or executed, and no engine or editor is required.
/// </summary>
public sealed class UnrealRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
{
    private static readonly HashSet<string> GeneratedDirectories =
        new(StringComparer.OrdinalIgnoreCase) { "Binaries", "DerivedDataCache", "Intermediate", "Saved" };

    private readonly IRepositoryFileSystem _fileSystem;

    public UnrealRepositoryAnalyzer() : this(PhysicalRepositoryFileSystem.Instance) { }

    public UnrealRepositoryAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "unreal";

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);
        cancellationToken.ThrowIfCancellationRequested();
        string root = _fileSystem.GetFullPath(repositoryPath);
        UnrealFileReader reader = new(_fileSystem, root);
        UnrealAssetSizeReader sizes = new(_fileSystem, root);
        EvidenceFact[] files = [.. evidence.Facts
            .Where(fact => fact.Kind == EvidenceKinds.File && IsOwnedContent(fact))
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        List<Diagnostic> diagnostics = [];
        SortedDictionary<string, (string Path, bool IsPlugin, bool IsContextOnly, UnrealProjectManifest? Manifest)> projects =
            new(StringComparer.Ordinal);
        foreach (EvidenceFact descriptor in files.Where(file => HasExtension(file.Scope, ".uproject", ".uplugin")))
        {
            UnrealFileRead read = await reader.ReadVerifiedAsync(descriptor, UnrealProjectManifest.MaximumBytes, cancellationToken)
                .ConfigureAwait(false);
            UnrealProjectManifest? manifest = read.Content is null ? null : UnrealProjectManifest.TryParse(read.Content);
            if (manifest is null)
            {
                diagnostics.Add(UnrealEvidence.Diagnostic("FB8302", DiagnosticSeverity.Warning,
                    "Unreal descriptor could not be read as bounded JSON; ownership is retained without plugin or module counts.",
                    descriptor.Scope));
            }

            string directory = DirectoryOf(descriptor.Scope);
            bool isPlugin = HasExtension(descriptor.Scope, ".uplugin");
            if (!projects.TryGetValue(directory, out var existing) || existing.IsPlugin && !isPlugin)
            {
                projects[directory] = (descriptor.Scope, isPlugin, descriptor.Tags.Contains(RepositoryScanner.ContextOnlyTag), manifest);
            }
        }

        Dictionary<string, List<UnrealAssetAnalysis>> owned = projects.Keys
            .ToDictionary(directory => directory, _ => new List<UnrealAssetAnalysis>(), StringComparer.Ordinal);
        int unowned = 0;
        foreach (EvidenceFact asset in files.Where(file => HasExtension(file.Scope, ".uasset", ".umap")))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? directory = projects.Keys
                .Where(candidate => candidate == "." || asset.Scope.StartsWith(candidate + "/", StringComparison.Ordinal))
                .MaxBy(candidate => candidate.Length);
            if (directory is null)
            {
                unowned++;
                continue;
            }

            if (IsGenerated(directory, asset.Scope))
            {
                continue;
            }

            UnrealAssetSize size = await sizes.ReadAsync(asset, cancellationToken).ConfigureAwait(false);
            if (size.Diagnostic is not null)
            {
                diagnostics.Add(size.Diagnostic);
                continue;
            }

            UnrealAssetKind kind = UnrealAssetCatalog.Classify(asset.Scope);
            owned[directory].Add(new UnrealAssetAnalysis(
                asset.Scope, kind, size.ContentSha256, size.Bytes, size.IsPointer,
                UnrealAssetCatalog.InferNodes(kind, size.Bytes)));
        }

        List<EvidenceFact> facts = [];
        int unclassified = 0;
        foreach ((string directory, var descriptor) in projects)
        {
            // A context-only descriptor (an area scan's ancestor) only resolves package ownership: it adds no
            // entry point, and nothing at all when this scope owns none of its packages.
            if (descriptor.IsContextOnly && owned[directory].Count == 0)
            {
                continue;
            }

            UnrealProjectAnalysis project = new(directory, descriptor.Path, descriptor.IsPlugin, descriptor.Manifest, owned[directory]);
            facts.AddRange(ProjectFacts(project, descriptor.IsContextOnly));
            unclassified += project.Assets.Count(asset => asset.Kind == UnrealAssetKind.Unclassified);
        }

        if (unclassified + unowned > 0)
        {
            diagnostics.Add(UnrealEvidence.Diagnostic("FB8303", DiagnosticSeverity.Information,
                $"{unclassified} Unreal package(s) without a recognized asset-name prefix and {unowned} package(s) outside any " +
                ".uproject or .uplugin directory were inventoried but not valued."));
        }

        diagnostics.Add(UnrealEvidence.Diagnostic("FB8300", DiagnosticSeverity.Information,
            "Unreal analysis used descriptors, asset paths, naming conventions, and serialized sizes only (Git LFS pointer sizes " +
            "when bodies are not present). Blueprint and other graph structure is size-inferred and uncalibrated; packages were not " +
            "parsed, loaded, imported, cooked, or executed; imported meshes, textures, audio, animation, and level actor placement " +
            "are not valued."));
        return new RepositoryAnalysisContribution
        {
            Facts = [.. facts.OrderBy(fact => fact.Id, StringComparer.Ordinal)],
            Diagnostics = [.. diagnostics.OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Locations.Count == 0 ? string.Empty : diagnostic.Locations[0].Path, StringComparer.Ordinal)],
        };
    }

    private static IEnumerable<EvidenceFact> ProjectFacts(UnrealProjectAnalysis project, bool contextOnly)
    {
        yield return UnrealFactFactory.Package(project);
        UnrealAssetAnalysis[] unique = [.. project.Assets.DistinctBy(asset => asset.ContentKey, StringComparer.Ordinal)];
        UnrealAssetAnalysis[] graphs = [.. unique.Where(asset => UnrealAssetCatalog.IsGraph(asset.Kind))];
        int declarative = unique.Count(asset => asset.Kind == UnrealAssetKind.Declarative);
        if (graphs.Length + declarative > 0)
        {
            yield return UnrealFactFactory.Structure(project, graphs, declarative);
        }

        UnrealAssetAnalysis[] interfaces = [.. unique.Where(asset => UnrealAssetCatalog.IsUserInterfaceClass(asset.Kind, asset.Path))];
        int levels = unique.Count(asset => asset.Kind == UnrealAssetKind.Level);
        if (interfaces.Length + levels > 0)
        {
            yield return UnrealFactFactory.UserInterface(project, interfaces, levels);
        }

        if (!project.IsPlugin && !contextOnly)
        {
            yield return UnrealFactFactory.EntryPoint(project);
        }
    }

    private static bool IsOwnedContent(EvidenceFact fact) => !fact.Tags.Any(tag => tag is
        "classification:generated" or "classification:minified" or "classification:vendored");

    private static bool IsGenerated(string projectDirectory, string path)
    {
        string relative = projectDirectory == "." ? path : path[(projectDirectory.Length + 1)..];
        int separator = relative.IndexOf('/', StringComparison.Ordinal);
        return separator > 0 && GeneratedDirectories.Contains(relative[..separator]);
    }

    private static bool HasExtension(string path, params string[] extensions) =>
        extensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    private static string DirectoryOf(string path) => path.Contains('/', StringComparison.Ordinal)
        ? path[..path.LastIndexOf('/')]
        : ".";
}
