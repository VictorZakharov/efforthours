using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangeLogicalMarginalityTests
{
    [Theory]
    [InlineData(false, 16.00, 1.00)]
    [InlineData(true, 9.14, 7.86)]
    public void PositiveNegativeAndRoleBudgetsExplainEndpointVersusSignedStock(
        bool mixedRoles, double productionHours, double testHours)
    {
        CapabilityFixture productionBefore = new("growing", EffortCategory.ProductionImplementation,
            "scope/source.cs", 8m, 1);
        CapabilityFixture productionAfter = productionBefore with { ExpectedPerPartition = 24m };
        CapabilityFixture testBefore = new("shrinking", EffortCategory.UnitTesting,
            "scope/tests.cs", 8m, 1);
        CapabilityFixture testAfter = testBefore with { ExpectedPerPartition = 4m };
        ChangePathEvidence[] paths =
        [
            Path(productionBefore.Path, ChangePathStatus.Modified),
            Path(testBefore.Path, ChangePathStatus.Modified, "role:test"),
        ];
        EvidenceFact ProductionFact(decimal methods) => Fact(productionBefore, methods) with
        {
            Locations = mixedRoles
                ? [new EvidenceLocation { Path = productionBefore.Path },
                    new EvidenceLocation { Path = testBefore.Path }]
                : [new EvidenceLocation { Path = productionBefore.Path }],
            Measurements = [new EvidenceMeasurement { Name = "methods", Value = methods, Unit = "methods" }],
        };
        RepositoryEvidence beforeEvidence = Evidence("base", [productionBefore, testBefore], 1m) with
        {
            Facts = [ProductionFact(1m), Fact(testBefore, 1m)],
        };
        RepositoryEvidence afterEvidence = Evidence("head", [productionAfter, testAfter], 1m) with
        {
            Facts = [ProductionFact(65m), Fact(testAfter, 1m)],
        };
        ChangeSelection selection = Selection();
        ChangeWorkItemResult result = ChangeWorkItemBuilder.Build(selection, new ChangeEvidence
        {
            Selection = selection,
            Repository = Repository("head"),
            Paths = paths,
            BaseEvidenceDigest = "sha256:base",
            HeadEvidenceDigest = "sha256:head",
        }, beforeEvidence, afterEvidence, Report("base", [productionBefore, testBefore]),
            Report("head", [productionAfter, testAfter]), EstimationProfile.Implementation);

        RepositoryEvidence WithoutSemanticGrowth(RepositoryEvidence evidence) => evidence with
        {
            Facts = [.. evidence.Facts.Select(fact => fact with
            {
                Measurements = [new EvidenceMeasurement
                {
                    Name = "logical-change", Value = 1m, Unit = "capabilities",
                }],
            })],
        };
        ChangeWorkItemResult modificationOnly = ChangeWorkItemBuilder.Build(selection, new ChangeEvidence
        {
            Selection = selection,
            Repository = Repository("head"),
            Paths = paths,
            BaseEvidenceDigest = "sha256:base",
            HeadEvidenceDigest = "sha256:head",
        }, WithoutSemanticGrowth(beforeEvidence), WithoutSemanticGrowth(afterEvidence),
            Report("base", [productionBefore, testBefore]), Report("head", [productionAfter, testAfter]),
            EstimationProfile.Implementation);
        Assert.Equal(mixedRoles ? 1.50m : 1.00m, modificationOnly.WorkItems.Where(item =>
            item.Estimator.Id == "change-rule:capability-marginal").Sum(item => item.Hours.Expected));
        // Signed stock: +16 production and -4 tests = +12. Change retains the
        // +16 supported growth and charges +1 bounded removal, yielding 17.
        // Mixed roles redistribute the same 16 budget; they cannot add hours.
        Assert.Equal(12m, productionAfter.ExpectedPerPartition + testAfter.ExpectedPerPartition -
            productionBefore.ExpectedPerPartition - testBefore.ExpectedPerPartition);
        Assert.Equal(16m, result.WorkItems.Where(item =>
            item.Estimator.Id == "change-rule:capability-marginal").Sum(item => item.Hours.Expected));
        Assert.Equal(1m, result.WorkItems.Where(item =>
            item.Estimator.Id == "change-rule:capability-removal").Sum(item => item.Hours.Expected));
        Assert.Equal((decimal)productionHours, CategoryHours(result, EffortCategory.ProductionImplementation).Expected);
        Assert.Equal((decimal)testHours, CategoryHours(result, EffortCategory.UnitTesting).Expected);
        Assert.Equal(17m, result.Categories.Where(category => category.Category is
            EffortCategory.ProductionImplementation or EffortCategory.UnitTesting).Sum(category => category.Hours.Expected));
        Assert.DoesNotContain(result.WorkItems, item =>
            item.Estimator.Id == "change-rule:maintained-artifact-fallback");
        Assert.All(result.WorkItems, item =>
        {
            Assert.True(item.Hours.Low <= item.Hours.Expected);
            Assert.True(item.Hours.Expected <= item.Hours.High);
        });
    }
}
