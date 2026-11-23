using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Unreal;

internal static class UnrealFactFactory
{
    private static readonly string[] CommonTags =
        ["ecosystem:unreal", "technology:unreal-engine", "source-excerpts:not-emitted", "asset-bodies:not-loaded"];

    public static EvidenceFact Package(UnrealProjectAnalysis project) => UnrealEvidence.Fact(
        $"unreal:package:{UnrealEvidence.IdToken(project.Directory)}",
        EvidenceKinds.EcosystemPackage,
        project.Directory,
        project.IsPlugin
            ? "Static Unreal plugin ownership for maintained package assets."
            : "Static Unreal project ownership for maintained package assets.",
        EvidenceSourceKind.Observed,
        "deepest scanner-admitted .uproject or .uplugin directory owns Content packages",
        [UnrealEvidence.Location(project.ManifestPath)],
        new[]
        {
            UnrealEvidence.Measurement("package-files", project.Assets.Count, "files"),
            UnrealEvidence.Measurement("lfs-pointer-assets", project.Assets.Count(asset => asset.IsPointer), "files"),
            UnrealEvidence.Measurement("enabled-plugins", project.Manifest?.EnabledPlugins ?? 0, "plugins"),
            UnrealEvidence.Measurement("code-modules", project.Manifest?.Modules ?? 0, "modules"),
        }.Concat(project.Assets
            .GroupBy(asset => UnrealAssetCatalog.MeasurementName(asset.Kind), StringComparer.Ordinal)
            .Select(group => UnrealEvidence.Measurement(group.Key, group.Count(), "files"))),
        [.. CommonTags, "scope:analyzed", "metadata:static-only",
            project.IsPlugin ? "package-role:plugin" : "package-role:application",
            project.Manifest is null ? "manifest:unparsed" : "manifest:structural"]);

    public static EvidenceFact Structure(UnrealProjectAnalysis project, IReadOnlyList<UnrealAssetAnalysis> graphs, int declarative)
    {
        int nodes = graphs.Sum(graph => graph.InferredNodes);
        return UnrealEvidence.Fact(
            $"unreal:graph:{UnrealEvidence.IdToken(project.Directory)}",
            EvidenceKinds.SourceStructure,
            project.Directory,
            "Size-inferred Blueprint, material, effect, sound, and AI graph structure of unique maintained packages.",
            EvidenceSourceKind.Inferred,
            "serialized package size (binary length or Git LFS pointer size) converted to graph nodes by versioned per-kind ratios; " +
                "callable units and branch points derived from inferred nodes; package bodies not parsed",
            graphs.Select(graph => UnrealEvidence.Location(graph.Path)),
            [
                UnrealEvidence.Measurement("files", graphs.Count + declarative, "files"),
                UnrealEvidence.Measurement("types", graphs.Count, "graph-assets"),
                UnrealEvidence.Measurement("functions", decimal.Round(nodes * UnrealAssetCatalog.CallableUnitsPerNode, 2), "inferred-callables"),
                UnrealEvidence.Measurement("branch-points", decimal.Round(nodes * UnrealAssetCatalog.BranchPointsPerNode, 2), "inferred-branches"),
                UnrealEvidence.Measurement("inferred-graph-nodes", nodes, "inferred-nodes"),
                UnrealEvidence.Measurement("declarative-assets", declarative, "files"),
                UnrealEvidence.Measurement("serialized-bytes", graphs.Sum(graph => graph.Bytes), "bytes"),
            ],
            [.. CommonTags, "syntax:size-inferred", "parser:none", "parser-confidence:low",
                "structure:production-only", "structure:projection-normalized", "content-normalization:exact-duplicates",
                "inference:uncalibrated"]);
    }

    public static EvidenceFact UserInterface(UnrealProjectAnalysis project, IReadOnlyList<UnrealAssetAnalysis> interfaces, int levels) =>
        UnrealEvidence.Fact(
            $"unreal:ui:{UnrealEvidence.IdToken(project.Directory)}",
            EvidenceKinds.UserInterface,
            project.Directory,
            "Unreal widget/HUD classes and playable levels identified by asset kind and naming convention.",
            EvidenceSourceKind.Inferred,
            "widget-prefixed and HUD-named Blueprint classes count as UI types; each unique .umap counts as one level surface",
            interfaces.Select(asset => UnrealEvidence.Location(asset.Path)),
            [
                UnrealEvidence.Measurement("ui-types", interfaces.Count, "classes"),
                UnrealEvidence.Measurement("pages", levels, "levels"),
            ],
            [.. CommonTags, "ui-framework:unreal-engine", "level-placement:not-valued"]);

    public static EvidenceFact EntryPoint(UnrealProjectAnalysis project) => UnrealEvidence.Fact(
        $"unreal:entry:{UnrealEvidence.IdToken(project.Directory)}",
        EvidenceKinds.EntryPoint,
        project.Directory,
        "Unreal game project startup descriptor.",
        EvidenceSourceKind.Observed,
        ".uproject descriptor presence",
        [UnrealEvidence.Location(project.ManifestPath)],
        [UnrealEvidence.Measurement("entry-points", 1, "projects")],
        CommonTags);
}

internal sealed record UnrealProjectAnalysis(
    string Directory,
    string ManifestPath,
    bool IsPlugin,
    UnrealProjectManifest? Manifest,
    IReadOnlyList<UnrealAssetAnalysis> Assets);

internal sealed record UnrealAssetAnalysis(
    string Path,
    UnrealAssetKind Kind,
    string ContentKey,
    long Bytes,
    bool IsPointer,
    int InferredNodes);
