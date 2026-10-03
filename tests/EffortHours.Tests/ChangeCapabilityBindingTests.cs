using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangeLogicalMarginalityTests
{
    [Fact]
    public void UnlocatedGrowthCannotBorrowUnrelatedPaths()
    {
        CapabilityFixture unbound = new("unlocated", EffortCategory.ProductionImplementation,
            "unlocated.cs", 64m, 1, MapsPath: false);
        ChangeWorkItemResult result = Build([], [unbound],
            [Path("guide.md", ChangePathStatus.Modified, "role:documentation")]);

        Assert.DoesNotContain(result.WorkItems, item =>
            item.Estimator.Id.StartsWith("change-rule:capability-", StringComparison.Ordinal));
        Assert.Contains(result.WorkItems, item =>
            item.Estimator.Id == "change-rule:maintained-artifact-fallback" &&
            item.Category == EffortCategory.Documentation);
        Diagnostic warning = Assert.Single(result.Diagnostics, item => item.Code == "FB5211");
        Assert.Contains("unboundCapabilities=1", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("unlocated", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("guide.md", warning.Message, StringComparison.Ordinal);
        Assert.Contains(result.Diagnostics, item => item.Code == "FB5210" &&
            item.Message.Contains("growthNotRetained=64;", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzerRecognizedTestsCannotEnableProductionBackboneGrowth()
    {
        CapabilityFixture productionBefore = new("source", EffortCategory.ProductionImplementation,
            "unconventional/specification.py", 8m, 1);
        CapabilityFixture productionAfter = productionBefore with { ExpectedPerPartition = 72m };
        CapabilityFixture testBefore = new("tests", EffortCategory.UnitTesting, productionBefore.Path, 4m, 1);
        CapabilityFixture testAfter = testBefore with { ExpectedPerPartition = 8m };
        RepositoryEvidence EvidenceAt(string identity, decimal units) => Evidence(identity, [], units) with
        {
            Facts =
            [
                Fact(productionBefore, units) with
                {
                    Measurements = [new EvidenceMeasurement { Name = "methods", Value = units, Unit = "methods" }],
                },
                Fact(testBefore, units) with
                {
                    Kind = EvidenceKinds.EcosystemTest,
                    Measurements = [new EvidenceMeasurement { Name = "test-cases", Value = units, Unit = "cases" }],
                },
                Fact(productionBefore, units) with
                {
                    Id = "common:file", Kind = EvidenceKinds.File, Scope = productionBefore.Path,
                    Tags = ["role:source"],
                },
            ],
        };
        EstimateReport SourceReport(string identity, CapabilityFixture production, CapabilityFixture tests) =>
            Report(identity, [production, tests]) with
            {
                WorkItems = [.. Report(identity, [production, tests]).WorkItems.Select(item =>
                    item.Category == EffortCategory.ProductionImplementation ? item with
                    {
                        Estimator = item.Estimator with { Id = "seed-rule:polyglot-source-backbone" },
                    } : item)],
            };
        ChangeSelection selection = Selection();
        ChangeWorkItemResult result = ChangeWorkItemBuilder.Build(selection, new ChangeEvidence
        {
            Selection = selection,
            Repository = Repository("head"),
            Paths = [Path(productionBefore.Path, ChangePathStatus.Modified, "role:source")],
            BaseEvidenceDigest = "sha256:base",
            HeadEvidenceDigest = "sha256:head",
        }, EvidenceAt("base", 1m), EvidenceAt("head", 65m),
            SourceReport("base", productionBefore, testBefore), SourceReport("head", productionAfter, testAfter),
            EstimationProfile.Implementation);
        Assert.DoesNotContain(result.WorkItems, item => item.Category == EffortCategory.ProductionImplementation);
        Assert.Equal(4m, CategoryHours(result, EffortCategory.UnitTesting).Expected);
        Assert.Contains(result.Diagnostics, item => item.Code == "FB5211");
        Assert.DoesNotContain(result.WorkItems, item => item.Estimator.Id == "change-rule:maintained-artifact-fallback");
    }

    [Fact]
    public void DistinctAddedCapabilityKeepsGrowthWhenUnrelatedArtifactIsModified()
    {
        CapabilityFixture added = new("added", EffortCategory.ProductionImplementation,
            "new/source.cs", 32m, 1);
        ChangeWorkItemResult alone = Build([], [added], [Path(added.Path, ChangePathStatus.Added)]);
        ChangeWorkItemResult together = Build([], [added],
            [Path(added.Path, ChangePathStatus.Added),
                Path("unrelated/guide.md", ChangePathStatus.Modified, "role:documentation")]);

        Assert.Equal(CategoryHours(alone, added.Category), CategoryHours(together, added.Category));
        Assert.Equal(32m, CategoryHours(together, added.Category).Expected);
        Assert.All(together.WorkItems.Where(item => item.Estimator.Id == "change-rule:capability-marginal"),
            item => Assert.Equal(["change:path:" + added.Path], item.EvidenceIds));
    }
}
