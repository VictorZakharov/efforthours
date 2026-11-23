using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Analyzers.Unreal;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class UnrealAnalyzerTests
{
    private const string Descriptor = """
        {
          "FileVersion": 3,
          "EngineAssociation": "5.9",
          "Description": "Private game title",
          "Plugins": [
            { "Name": "PrivatePluginName", "Enabled": true },
            { "Name": "Other", "Enabled": false }
          ]
        }
        """;

    [Fact]
    public async Task LfsPointerProjectProducesTraceableSchemaValidSizeInferredEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText("Game/Game.uproject", Descriptor);
        repository.WriteText("Game/Content/Player/BP_Player.uasset", Pointer("player", 16 * 1024 + 2_560 * 1_000));
        repository.WriteText("Game/Content/UI/WBP_Menu.uasset", Pointer("menu", 16 * 1024 + 2_560 * 100));
        repository.WriteText("Game/Content/UI/BP_GameHUD.uasset", Pointer("hud", 16 * 1024 + 2_560 * 200));
        repository.WriteText("Game/Content/FX/M_Glow.uasset", Pointer("glow", 8 * 1024 + 1024 * 10));
        repository.WriteText("Game/Content/FX/NS_Spark.uasset", Pointer("spark", 64 * 1024 + 8 * 1024 * 40));
        repository.WriteText("Game/Content/Audio/SCue_Shot.uasset", Pointer("shot", 4 * 1024 + 1024 * 4));
        repository.WriteText("Game/Content/FX/MI_Glow_Red.uasset", Pointer("red", 8_000));
        repository.WriteText("Game/Content/Input/IA_Jump.uasset", Pointer("jump", 1_200));
        repository.WriteText("Game/Content/Art/T_Wall.uasset", Pointer("wall", 9_000_000));
        repository.WriteText("Game/Content/Art/SM_Wall.uasset", Pointer("mesh", 4_000_000));
        repository.WriteText("Game/Content/Maps/L_Arena.umap", Pointer("arena", 30_000_000));
        repository.WriteText("Game/Content/__ExternalActors__/Maps/L_Arena/A/B/ACTOR1.uasset", Pointer("actor", 5_000));
        repository.WriteText("Game/Content/Misc/Mystery.uasset", Pointer("mystery", 500_000));
        repository.WriteText("Game/Saved/Autosaves/BP_Player_Auto.uasset", Pointer("autosave", 9_000_000));
        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains("unreal", evidence.Repository.Ecosystems);
        EvidenceFact package = Single(evidence, EvidenceKinds.EcosystemPackage);
        Assert.Equal("Game", package.Scope);
        Assert.Contains("package-role:application", package.Tags);
        Assert.Equal(1m, Measure(package, "enabled-plugins"));
        Assert.Equal(13m, Measure(package, "package-files"));
        Assert.Equal(13m, Measure(package, "lfs-pointer-assets"));
        Assert.Equal(2m, Measure(package, "imported-media-assets"));
        Assert.Equal(1m, Measure(package, "external-actor-files"));
        Assert.Equal(1m, Measure(package, "unclassified-assets"));

        EvidenceFact structure = Single(evidence, EvidenceKinds.SourceStructure);
        Assert.Equal(EvidenceSourceKind.Inferred, structure.Provenance.SourceKind);
        Assert.Contains("syntax:size-inferred", structure.Tags);
        Assert.Equal(1_000m + 100m + 200m + 10m + 40m + 4m, Measure(structure, "inferred-graph-nodes"));
        Assert.Equal(6m, Measure(structure, "types"));
        Assert.Equal(8m, Measure(structure, "files"));
        Assert.Equal(33.85m, Measure(structure, "functions"));
        Assert.Equal(81.24m, Measure(structure, "branch-points"));

        EvidenceFact ui = Single(evidence, EvidenceKinds.UserInterface);
        Assert.Equal(2m, Measure(ui, "ui-types"));
        Assert.Equal(1m, Measure(ui, "pages"));
        Assert.Equal(1m, Measure(Single(evidence, EvidenceKinds.EntryPoint), "entry-points"));
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8300");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8303");
        Assert.DoesNotContain(evidence.Diagnostics, diagnostic => diagnostic.Code is "FB8301" or "FB8302" or "FB2002");

        EstimateReport estimate = Estimate(evidence);
        WorkItem[] backbone = [.. estimate.WorkItems.Where(item =>
            item.Estimator.Id == "seed-rule:polyglot-source-backbone" && item.Scope == "Game")];
        Assert.NotEmpty(backbone);
        Assert.All(backbone, item =>
        {
            Assert.Equal(ComplexityLevel.Moderate, item.Complexity);
            Assert.Contains(item.UncertaintyReasons, reason => reason.Contains("serialized asset size", StringComparison.Ordinal));
        });
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:ui-surface");
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:application-entry-point");
        Assert.DoesNotContain(estimate.Diagnostics, diagnostic => diagnostic.Code == "FB1002");
        Assert.Empty(ContractValidation.Validate(evidence));
        Assert.Empty(ContractValidation.Validate(estimate));
        string json = ContractJson.Serialize(evidence);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.RepositoryEvidence, json).IsValid);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.EstimateReport, ContractJson.Serialize(estimate)).IsValid);
        Assert.DoesNotContain("Private game title", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PrivatePluginName", json, StringComparison.Ordinal);
        Assert.Equal(json, ContractJson.Serialize(await ScanAsync(repository)));
    }

    [Fact]
    public async Task BinaryBodiesAndLfsPointersOfTheSameSizeProduceIdenticalEstimates()
    {
        byte[] body = new byte[16 * 1024 + 2_560 * 300];
        body[0] = 0xC1;
        body[1] = 0x83;
        InMemoryRepository binary = new();
        binary.WriteText("Game.uproject", Descriptor);
        binary.WriteBytes("Content/BP_Enemy.uasset", body);
        InMemoryRepository pointer = new();
        pointer.WriteText("Game.uproject", Descriptor);
        pointer.WriteText("Content/BP_Enemy.uasset", PointerFor(body));
        RepositoryEvidence fromBinary = await ScanAsync(binary);
        RepositoryEvidence fromPointer = await ScanAsync(pointer);
        EvidenceFact binaryStructure = Single(fromBinary, EvidenceKinds.SourceStructure);
        EvidenceFact pointerStructure = Single(fromPointer, EvidenceKinds.SourceStructure);
        Assert.Equal(300m, Measure(binaryStructure, "inferred-graph-nodes"));
        Assert.Equal(binaryStructure.Measurements, pointerStructure.Measurements);
        Assert.Equal(0m, Measure(Single(fromBinary, EvidenceKinds.EcosystemPackage), "lfs-pointer-assets"));
        Assert.Equal(Estimate(fromBinary).TotalEffort, Estimate(fromPointer).TotalEffort);
    }

    [Fact]
    public async Task DuplicatesGeneratedVendoredAndOversizedAssetsDoNotInflateEffort()
    {
        InMemoryRepository repository = new();
        repository.WriteText("Game.uproject", Descriptor);
        repository.WriteText("Content/BP_Enemy.uasset", Pointer("enemy", 16 * 1024 + 2_560 * 50));
        EstimateReport before = Estimate(await ScanAsync(repository));
        repository.WriteText("Content/Copies/BP_Enemy_Copy.uasset", Pointer("enemy", 16 * 1024 + 2_560 * 50));
        repository.WriteText("Intermediate/Cache/BP_Cache.uasset", Pointer("cache", 50_000_000));
        repository.WriteText("Saved/BP_Saved.uasset", Pointer("saved", 50_000_000));
        repository.WriteText("Content/vendor/BP_Store.uasset", Pointer("store", 50_000_000));
        RepositoryEvidence evidence = await ScanAsync(repository);
        Assert.Equal(before.TotalEffort, Estimate(evidence).TotalEffort);
        Assert.Equal(50m, Measure(Single(evidence, EvidenceKinds.SourceStructure), "inferred-graph-nodes"));

        repository.WriteText("Content/BP_Huge.uasset", Pointer("huge", 1L << 34));
        EvidenceFact capped = Single(await ScanAsync(repository), EvidenceKinds.SourceStructure);
        Assert.Equal(50m + UnrealAssetCatalog.MaximumNodesPerAsset, Measure(capped, "inferred-graph-nodes"));
    }

    [Fact]
    public async Task PluginsOwnTheirContentWithoutEntryPointsAndNonUnrealRepositoriesAreUnchanged()
    {
        InMemoryRepository repository = new();
        repository.WriteText("Game.uproject", Descriptor);
        repository.WriteText("Content/BP_Game.uasset", Pointer("game", 16 * 1024 + 2_560 * 10));
        repository.WriteText("Plugins/Tools/Tools.uplugin", "{\"Modules\":[{\"Name\":\"Tools\"}]}");
        repository.WriteText("Plugins/Tools/Content/BP_Tool.uasset", Pointer("tool", 16 * 1024 + 2_560 * 20));
        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact[] packages = [.. evidence.Facts.Where(fact => fact.Kind == EvidenceKinds.EcosystemPackage)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        Assert.Equal([".", "Plugins/Tools"], packages.Select(fact => fact.Scope));
        Assert.Contains("package-role:plugin", packages[1].Tags);
        Assert.Equal(1m, Measure(packages[1], "code-modules"));
        Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.EntryPoint);
        Assert.Equal([10m, 20m], evidence.Facts.Where(fact => fact.Kind == EvidenceKinds.SourceStructure)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal).Select(fact => Measure(fact, "inferred-graph-nodes")));

        InMemoryRepository plain = new();
        plain.WriteText("Content/BP_Orphan.uasset", Pointer("orphan", 9_000_000));
        plain.WriteText("tool.py", "def run():\n    return 1\n");
        RepositoryEvidence other = await ScanAsync(plain);
        Assert.DoesNotContain("unreal", other.Repository.Ecosystems);
        Assert.DoesNotContain(other.Facts, fact => fact.Id.StartsWith("unreal:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("version https://git-lfs.github.com/spec/v1\nsize 10\n")]
    [InlineData("version https://git-lfs.github.com/spec/v2\noid sha256:{0}\nsize 10\n")]
    [InlineData("version https://git-lfs.github.com/spec/v1\noid sha256:abc\nsize 10\n")]
    [InlineData("version https://git-lfs.github.com/spec/v1\noid sha256:{0}\nsize -10\n")]
    [InlineData("version https://git-lfs.github.com/spec/v1\noid sha256:{0}\nsize 10\nbody text\n")]
    [InlineData("plain text that is not a pointer")]
    public async Task InvalidPointersAreDiagnosedAndNotValued(string template)
    {
        InMemoryRepository repository = new();
        repository.WriteText("Game.uproject", Descriptor);
        repository.WriteText("Content/BP_Broken.uasset", template.Replace("{0}", new string('a', 64), StringComparison.Ordinal));
        RepositoryEvidence evidence = await ScanAsync(repository);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8301");
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
    }

    [Fact]
    public async Task OversizedTextDigestMismatchMalformedDescriptorAndCancellationAreSafe()
    {
        InMemoryRepository repository = new();
        repository.WriteText("Game.uproject", "{ not json");
        repository.WriteText("Content/BP_Text.uasset", new string('x', 2_000));
        repository.WriteText("Content/BP_Ok.uasset", Pointer("ok", 16 * 1024 + 2_560 * 5));
        RepositoryEvidence evidence = await ScanAsync(repository);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8302");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8301" &&
            diagnostic.Locations[0].Path == "Content/BP_Text.uasset");
        Assert.Contains("manifest:unparsed", Single(evidence, EvidenceKinds.EcosystemPackage).Tags);
        Assert.Equal(5m, Measure(Single(evidence, EvidenceKinds.SourceStructure), "inferred-graph-nodes"));

        RepositoryEvidence common = await new RepositoryScanner(repository).ScanAsync(repository.RootPath);
        repository.WriteText("Content/BP_Ok.uasset", Pointer("changed", 16 * 1024 + 2_560 * 5));
        UnrealRepositoryAnalyzer analyzer = new(repository);
        RepositoryAnalysisContribution mismatch = await analyzer.AnalyzeAsync(repository.RootPath, common);
        Assert.Contains(mismatch.Diagnostics, diagnostic => diagnostic.Code == "FB8301" &&
            diagnostic.Message.Contains("changed after common scanning", StringComparison.Ordinal));
        Assert.DoesNotContain(mismatch.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            analyzer.AnalyzeAsync(repository.RootPath, common, cancellation.Token));
    }

    [Theory]
    [InlineData("Content/BP_Player.uasset", "Blueprint")]
    [InlineData("Content/BPI_Touch.uasset", "Blueprint")]
    [InlineData("Content/WBP_Menu.uasset", "WidgetBlueprint")]
    [InlineData("Content/UI_Stick.uasset", "WidgetBlueprint")]
    [InlineData("Content/ABP_Hands.uasset", "AnimationBlueprint")]
    [InlineData("Content/CtrlRig_Warp.uasset", "ControlRig")]
    [InlineData("Content/BT_Enemy.uasset", "AiGraph")]
    [InlineData("Content/MF_Noise.uasset", "Material")]
    [InlineData("Content/NS_Muzzle.uasset", "Effect")]
    [InlineData("Content/SCue_Step.uasset", "SoundGraph")]
    [InlineData("Content/MI_Red.uasset", "Declarative")]
    [InlineData("Content/IMC_Default.uasset", "Declarative")]
    [InlineData("Content/SKM_Body.uasset", "ImportedMedia")]
    [InlineData("Content/SFX_Shot.uasset", "ImportedMedia")]
    [InlineData("Content/Maps/Arena.umap", "Level")]
    [InlineData("Content/__ExternalActors__/Arena/0/AB.uasset", "ExternalActor")]
    [InlineData("Content/BP_.uasset", "Unclassified")]
    [InlineData("Content/Rock.uasset", "Unclassified")]
    public void AssetKindsFollowNamingConventions(string path, string expected) =>
        Assert.Equal(expected, UnrealAssetCatalog.Classify(path).ToString());

    [Fact]
    public void PointerParserAcceptsSpecificationExtensionsAndRejectsDuplicates()
    {
        string oid = new('b', 64);
        Assert.True(UnrealAssetSizeReader.TryParsePointer(Encoding.UTF8.GetBytes(
            $"version https://git-lfs.github.com/spec/v1\r\next-0-foo sha256:{oid}\r\noid sha256:{oid}\r\nsize 42\r\n"),
            out long size, out string? parsed));
        Assert.Equal(42, size);
        Assert.Equal(oid, parsed);
        Assert.False(UnrealAssetSizeReader.TryParsePointer(Encoding.UTF8.GetBytes(
            $"version https://git-lfs.github.com/spec/v1\noid sha256:{oid}\nsize 1\nsize 2\n"), out _, out _));
        Assert.False(UnrealAssetSizeReader.TryParsePointer([0xff, 0xfe], out _, out _));
        Assert.Equal(0, UnrealAssetCatalog.InferNodes(UnrealAssetKind.Declarative, 1_000_000));
        Assert.Equal(0, UnrealAssetCatalog.InferNodes(UnrealAssetKind.Blueprint, 1_000));
    }

    private static string Pointer(string seed, long size) =>
        $"version https://git-lfs.github.com/spec/v1\noid sha256:{Sha(Encoding.UTF8.GetBytes(seed))}\nsize {size}\n";

    private static string PointerFor(byte[] body) =>
        $"version https://git-lfs.github.com/spec/v1\noid sha256:{Sha(body)}\nsize {body.Length}\n";

    private static string Sha(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static EstimateReport Estimate(RepositoryEvidence evidence) =>
        new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);

    private static EvidenceFact Single(RepositoryEvidence evidence, string kind) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == kind &&
            fact.Provenance.Analyzer == "efforthours.unreal-analyzer");

    private static decimal Measure(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;
}
