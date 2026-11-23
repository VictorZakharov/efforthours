namespace EffortHours.Analyzers.Unreal;

/// <summary>
/// Classifies Unreal package files by extension, path, and conventional asset-name prefix,
/// and holds the versioned, uncalibrated serialized-size conversion for graph-bearing kinds.
/// </summary>
internal static class UnrealAssetCatalog
{
    /// <summary>Upper bound on inferred graph nodes for one asset, so one large binary cannot dominate.</summary>
    public const int MaximumNodesPerAsset = 4_000;

    /// <summary>Expected callable units (functions and events) per inferred graph node.</summary>
    public const decimal CallableUnitsPerNode = 0.025m;

    /// <summary>Expected branch, select, and switch nodes per inferred graph node.</summary>
    public const decimal BranchPointsPerNode = 0.06m;

    private static readonly (string Prefix, UnrealAssetKind Kind)[] Prefixes =
    [
        ("BPFL_", UnrealAssetKind.Blueprint),
        ("BPML_", UnrealAssetKind.Blueprint),
        ("BPI_", UnrealAssetKind.Blueprint),
        ("BPC_", UnrealAssetKind.Blueprint),
        ("BP_", UnrealAssetKind.Blueprint),
        ("B_", UnrealAssetKind.Blueprint),
        ("WBP_", UnrealAssetKind.WidgetBlueprint),
        ("WB_", UnrealAssetKind.WidgetBlueprint),
        ("W_", UnrealAssetKind.WidgetBlueprint),
        ("UI_", UnrealAssetKind.WidgetBlueprint),
        ("ABP_", UnrealAssetKind.AnimationBlueprint),
        ("AnimBP_", UnrealAssetKind.AnimationBlueprint),
        ("CtrlRig_", UnrealAssetKind.ControlRig),
        ("CR_", UnrealAssetKind.ControlRig),
        ("BT_", UnrealAssetKind.AiGraph),
        ("EQS_", UnrealAssetKind.AiGraph),
        ("ST_", UnrealAssetKind.AiGraph),
        ("MF_", UnrealAssetKind.Material),
        ("M_", UnrealAssetKind.Material),
        ("NS_", UnrealAssetKind.Effect),
        ("NE_", UnrealAssetKind.Effect),
        ("NMS_", UnrealAssetKind.Effect),
        ("NM_", UnrealAssetKind.Effect),
        ("PS_", UnrealAssetKind.Effect),
        ("SCue_", UnrealAssetKind.SoundGraph),
        ("SC_", UnrealAssetKind.SoundGraph),
        ("MSS_", UnrealAssetKind.SoundGraph),
        ("MSP_", UnrealAssetKind.SoundGraph),
        ("MS_", UnrealAssetKind.SoundGraph),
        ("MIC_", UnrealAssetKind.Declarative),
        ("MI_", UnrealAssetKind.Declarative),
        ("MPC_", UnrealAssetKind.Declarative),
        ("IMC_", UnrealAssetKind.Declarative),
        ("IA_", UnrealAssetKind.Declarative),
        ("PDA_", UnrealAssetKind.Declarative),
        ("DA_", UnrealAssetKind.Declarative),
        ("DT_", UnrealAssetKind.Declarative),
        ("CT_", UnrealAssetKind.Declarative),
        ("Curve_", UnrealAssetKind.Declarative),
        ("ATT_", UnrealAssetKind.Declarative),
        ("BB_", UnrealAssetKind.Declarative),
        ("ENUM_", UnrealAssetKind.Declarative),
        ("E_", UnrealAssetKind.Declarative),
        ("SKEL_", UnrealAssetKind.ImportedMedia),
        ("SKM_", UnrealAssetKind.ImportedMedia),
        ("SK_", UnrealAssetKind.ImportedMedia),
        ("SM_", UnrealAssetKind.ImportedMedia),
        ("PHYS_", UnrealAssetKind.ImportedMedia),
        ("PA_", UnrealAssetKind.ImportedMedia),
        ("TX_", UnrealAssetKind.ImportedMedia),
        ("T_", UnrealAssetKind.ImportedMedia),
        ("ABS_", UnrealAssetKind.ImportedMedia),
        ("AM_", UnrealAssetKind.ImportedMedia),
        ("AO_", UnrealAssetKind.ImportedMedia),
        ("AS_", UnrealAssetKind.ImportedMedia),
        ("BS_", UnrealAssetKind.ImportedMedia),
        ("A_", UnrealAssetKind.ImportedMedia),
        ("AMB_", UnrealAssetKind.ImportedMedia),
        ("SFX_", UnrealAssetKind.ImportedMedia),
        ("SW_", UnrealAssetKind.ImportedMedia),
        ("VO_", UnrealAssetKind.ImportedMedia),
        ("S_", UnrealAssetKind.ImportedMedia),
        ("Font_", UnrealAssetKind.ImportedMedia),
        ("F_", UnrealAssetKind.ImportedMedia),
    ];

    public static UnrealAssetKind Classify(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Contains("/__ExternalActors__/", StringComparison.Ordinal) ||
            path.Contains("/__ExternalObjects__/", StringComparison.Ordinal) ||
            path.StartsWith("__ExternalActors__/", StringComparison.Ordinal) ||
            path.StartsWith("__ExternalObjects__/", StringComparison.Ordinal))
        {
            return UnrealAssetKind.ExternalActor;
        }

        if (path.EndsWith(".umap", StringComparison.OrdinalIgnoreCase))
        {
            return UnrealAssetKind.Level;
        }

        string name = Path.GetFileNameWithoutExtension(path);
        foreach ((string prefix, UnrealAssetKind kind) in Prefixes)
        {
            if (name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        return UnrealAssetKind.Unclassified;
    }

    public static bool IsGraph(UnrealAssetKind kind) => Conversion(kind) is not null;

    public static bool IsUserInterfaceClass(UnrealAssetKind kind, string path) =>
        kind == UnrealAssetKind.WidgetBlueprint ||
        kind == UnrealAssetKind.Blueprint &&
            Path.GetFileNameWithoutExtension(path).Contains("HUD", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Infers graph nodes from serialized package bytes: <c>(bytes - base) / bytesPerNode</c>,
    /// floored and capped. This is a size proxy, not a parsed node count.
    /// </summary>
    public static int InferNodes(UnrealAssetKind kind, long bytes)
    {
        (long Base, long PerNode)? conversion = Conversion(kind);
        if (conversion is null || bytes <= conversion.Value.Base)
        {
            return 0;
        }

        long nodes = (bytes - conversion.Value.Base) / conversion.Value.PerNode;
        return (int)Math.Min(MaximumNodesPerAsset, nodes);
    }

    public static string MeasurementName(UnrealAssetKind kind) => kind switch
    {
        UnrealAssetKind.Blueprint => "blueprint-assets",
        UnrealAssetKind.WidgetBlueprint => "widget-blueprint-assets",
        UnrealAssetKind.AnimationBlueprint => "animation-blueprint-assets",
        UnrealAssetKind.ControlRig => "control-rig-assets",
        UnrealAssetKind.AiGraph => "ai-graph-assets",
        UnrealAssetKind.Material => "material-graph-assets",
        UnrealAssetKind.Effect => "effect-graph-assets",
        UnrealAssetKind.SoundGraph => "sound-graph-assets",
        UnrealAssetKind.Declarative => "declarative-assets",
        UnrealAssetKind.Level => "levels",
        UnrealAssetKind.ExternalActor => "external-actor-files",
        UnrealAssetKind.ImportedMedia => "imported-media-assets",
        _ => "unclassified-assets",
    };

    private static (long Base, long PerNode)? Conversion(UnrealAssetKind kind) => kind switch
    {
        UnrealAssetKind.Blueprint or UnrealAssetKind.WidgetBlueprint => (16 * 1024, 2_560),
        UnrealAssetKind.AnimationBlueprint => (16 * 1024, 6 * 1024),
        UnrealAssetKind.ControlRig => (16 * 1024, 16 * 1024),
        UnrealAssetKind.AiGraph => (8 * 1024, 2 * 1024),
        UnrealAssetKind.Material => (8 * 1024, 1024),
        UnrealAssetKind.Effect => (64 * 1024, 8 * 1024),
        UnrealAssetKind.SoundGraph => (4 * 1024, 1024),
        _ => null,
    };
}

internal enum UnrealAssetKind
{
    Blueprint,
    WidgetBlueprint,
    AnimationBlueprint,
    ControlRig,
    AiGraph,
    Material,
    Effect,
    SoundGraph,
    Declarative,
    Level,
    ExternalActor,
    ImportedMedia,
    Unclassified,
}
