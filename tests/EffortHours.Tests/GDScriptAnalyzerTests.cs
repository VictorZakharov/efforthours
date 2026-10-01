using EffortHours.Analysis;
using EffortHours.Analyzers.GDScript;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class GDScriptAnalyzerTests
{
    [Fact]
    public async Task StructureTestsAndMixedOwnershipProduceTraceableSchemaValidEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText("game/project.godot", "config_version=5\n[application]\nconfig/name=\"Private title\"\n");
        repository.WriteText("game/player.gd", """
            @tool
            class_name Player
            extends CharacterBody2D
            signal health_changed(value: int)
            @export_range(0, 100) var health: int = 100
            @onready var label = $HUD/Label
            func _ready():
                var secret_marker = "must-not-leak"
                await get_tree().process_frame
            @rpc("any_peer")
            func update_health(value: int) -> void:
                if value > 0 and value < 100:
                    health = value
            class Inventory:
                func clear():
                    pass
            """);
        repository.WriteText("game/tests/test_player.gd", "extends GutTest\nfunc test_health():\n    assert_eq(1, 1)\n");
        repository.WriteText("tools/helper.gd", "func value():\n    return 1\n");
        repository.WriteText("web/package.json", "{\"name\":\"web\"}");
        repository.WriteText("web/app.js", "export function value() { return 1; }");
        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact structure = Assert.Single(evidence.Facts, fact =>
            fact.Kind == EvidenceKinds.SourceStructure && fact.Scope == "game");
        Assert.Equal(3m, Measure(structure, "methods"));
        Assert.Equal(2m, Measure(structure, "types"));
        Assert.Equal(1m, Measure(structure, "signals"));
        Assert.Equal(1m, Measure(structure, "async-units"));
        Assert.Equal(2m, Measure(structure, "branch-points"));
        Assert.Equal(1m, Measure(structure, "export-annotations"));
        Assert.Equal(1m, Measure(structure, "rpc-annotations"));
        Assert.Equal(1m, Measure(structure, "lifecycle-methods"));
        EvidenceFact test = Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemTest);
        Assert.Equal(1m, Measure(test, "test-cases"));
        Assert.Equal(1m, Measure(test, "assertions"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemPackage && fact.Scope == ".");
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Language &&
            fact.Tags.Contains("language:gdscript", StringComparer.Ordinal) &&
            fact.Tags.Contains("analysis-depth:token-backed", StringComparer.Ordinal));
        Assert.DoesNotContain(evidence.Diagnostics, diagnostic => diagnostic.Code is "FB2002" or "FB8102");
        EstimateReport estimate = Estimate(evidence);
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:polyglot-source-backbone");
        Assert.Contains(estimate.WorkItems, item => item.Category == EffortCategory.UnitTesting);
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:javascript-source-backbone");
        Assert.Empty(ContractValidation.Validate(evidence));
        Assert.Empty(ContractValidation.Validate(estimate));
        string json = ContractJson.Serialize(evidence);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.RepositoryEvidence, json).IsValid);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.EstimateReport, ContractJson.Serialize(estimate)).IsValid);
        Assert.DoesNotContain("secret_marker", json, StringComparison.Ordinal);
        Assert.DoesNotContain("must-not-leak", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Private title", json, StringComparison.Ordinal);
        Assert.Equal(json, ContractJson.Serialize(await ScanAsync(repository)));
    }

    [Fact]
    public async Task DeepestProjectOwnsSourcesAndScopesHaveUniqueIds()
    {
        InMemoryRepository repository = new();
        repository.WriteText("game/project.godot", "config_version=5\n");
        repository.WriteText("game/player.gd", "func run():\n    pass\n");
        repository.WriteText("game/nested/project.godot", "config_version=5\n");
        repository.WriteText("game/nested/player.gd", "func stop():\n    pass\n");
        RepositoryEvidence evidence = await ScanAsync(repository);
        Assert.Equal(["game", "game/nested"], evidence.Facts.Where(fact =>
            fact.Kind == EvidenceKinds.EcosystemPackage).Select(fact => fact.Scope).Order(StringComparer.Ordinal));
        Assert.All(evidence.Facts.Where(fact => fact.Kind == EvidenceKinds.SourceStructure), fact =>
            Assert.Equal(1m, Measure(fact, "files")));
        Assert.Equal(evidence.Facts.Count, evidence.Facts.Select(fact => fact.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task ExactCopiesGeneratedVendorAndGodotCachesDoNotIncreaseProductionEffort()
    {
        InMemoryRepository repository = new();
        const string source = "extends Node\nfunc run():\n    if true:\n        return 1\n    return 0\n";
        repository.WriteText("player.gd", source);
        EstimateReport before = Estimate(await ScanAsync(repository));
        repository.WriteText("copy.gd", source);
        repository.WriteText("generated/player.gd", source);
        repository.WriteText("vendor/plugin.gd", source);
        repository.WriteText(".godot/editor/cache.gd", source);
        repository.WriteText(".import/cache.gd", source);
        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport after = Estimate(evidence);
        Assert.Equal(before.TotalEffort, after.TotalEffort);
        Assert.Equal(1m, Measure(Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure), "files"));
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.File &&
            (fact.Scope.StartsWith(".godot/", StringComparison.Ordinal) || fact.Scope.StartsWith(".import/", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task TestsAndLiteralNamesakesDoNotAddProductionStructure()
    {
        InMemoryRepository repository = new();
        repository.WriteText("app.gd", "func value():\n    return 1\n");
        EstimateReport before = Estimate(await ScanAsync(repository));
        repository.WriteText("tests/test_app.gd", "func test_value():\n    assert(true)\n");
        EstimateReport after = Estimate(await ScanAsync(repository));
        Assert.Equal(Category(before, EffortCategory.ProductionImplementation), Category(after, EffortCategory.ProductionImplementation));
        repository.WriteText("app.gd", "var sample = \"func fake(): if await signal\"\n# func ignored():\nfunc value():\n    return 1\n");
        EvidenceFact structure = Assert.Single((await ScanAsync(repository)).Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
        Assert.Equal(1m, Measure(structure, "methods"));
        Assert.DoesNotContain(structure.Measurements, measurement => measurement.Name is "async-units" or "signals" or "branch-points");
    }

    [Fact]
    public async Task DigestMismatchOversizedInvalidUtf8AndCancellationAreSafe()
    {
        InMemoryRepository repository = new();
        repository.WriteText("app.gd", "func value():\n    return 1\n");
        RepositoryEvidence common = await new RepositoryScanner(repository).ScanAsync(repository.RootPath);
        repository.WriteText("app.gd", "func value():\n    return 2\n");
        GDScriptRepositoryAnalyzer analyzer = new(repository);
        RepositoryAnalysisContribution mismatch = await analyzer.AnalyzeAsync(repository.RootPath, common);
        Assert.Contains(mismatch.Diagnostics, diagnostic => diagnostic.Code == "FB8101");
        Assert.Empty(mismatch.Facts);
        repository.WriteBytes("app.gd", [0xff, 0xfe, 0xfd]);
        Assert.DoesNotContain((await ScanAsync(repository)).Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
        repository.WriteText("app.gd", new string(' ', 8 * 1024 * 1024 + 1));
        RepositoryEvidence oversized = await ScanAsync(repository);
        Assert.DoesNotContain(oversized.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
        Assert.Contains(oversized.Diagnostics, diagnostic => diagnostic.Code == "FB8101");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => analyzer.AnalyzeAsync(repository.RootPath, common, cancellation.Token));
    }

    [Theory]
    [InlineData("func broken():\n    return [1)\n")]
    [InlineData("func broken():\n    return \"unterminated\n")]
    [InlineData("func broken():\n    if true:\n        pass\n      return 1\n")]
    public async Task MalformedStructureIsExplicitlyLowConfidence(string source)
    {
        InMemoryRepository repository = new();
        repository.WriteText("app.gd", source);
        RepositoryEvidence evidence = await ScanAsync(repository);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8102");
        Assert.Contains("parser-confidence:low", Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure).Tags);
    }

    [Fact]
    public async Task TokenLimitIsBoundedAndDiagnosed()
    {
        InMemoryRepository repository = new();
        repository.WriteText("app.gd", string.Concat(Enumerable.Repeat("var x = 1\n", 50_001)));
        Assert.Contains((await ScanAsync(repository)).Diagnostics, diagnostic => diagnostic.Code == "FB8102");
    }

    private static Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static EstimateReport Estimate(RepositoryEvidence evidence) =>
        new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);

    private static decimal Measure(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;

    private static EffortRange Category(EstimateReport report, EffortCategory category) =>
        Assert.Single(report.Categories, item => item.Category == category).Hours;
}
