using EffortHours.Analyzers.GDScript;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class GDScriptChangeTests
{
    [Theory]
    [InlineData("func value():\n    # original\n    return 1\n", "func value( ):\n  # revised\n  return 1")]
    [InlineData("func value():\n    return [1, 2]\n", "func value():\n\treturn [\n\t\t1, 2\n\t]\n")]
    public async Task FormattingAndOrdinaryCommentsHaveZeroEffort(string before, string after)
    {
        ChangeEstimateReport report = await EstimateAsync(before, after);
        Assert.Equal(ChangePathClassification.FormattingOnly, Assert.Single(report.Evidence.Paths).Classification);
        Assert.Equal(0m, report.TotalEffort.Expected);
        Assert.Empty(report.WorkItems);
    }

    [Theory]
    [InlineData("func value():\n    if true:\n        return 1\n    return 0\n", "func value():\n    if true:\n        return 1\nreturn 0\n")]
    [InlineData("## Original docs\nfunc value():\n    return 1\n", "## Changed docs\nfunc value():\n    return 1\n")]
    [InlineData("func value():\n    return r\"one#two\"\n", "func value():\n    return r\"one#three\"\n")]
    [InlineData("func value():\n    return $HUD/Label\n", "func value():\n    return $HUD / Label\n")]
    [InlineData("func value():\n    return 1 <= 2\n", "func value():\n    return 1 < = 2\n")]
    [InlineData("func value():\n    return [1)\n", "func value():\n  return [1)\n")]
    public async Task MeaningfulOrUnprovenChangesRemainRepresented(string before, string after)
    {
        ChangeEstimateReport report = await EstimateAsync(before, after);
        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeEstimateReport, ContractJson.Serialize(report)).IsValid);
    }

    [Theory]
    [InlineData("var x = &'name'\n", "var x = &'changed'\n")]
    [InlineData("var x = ^'Node/Path'\n", "var x = ^'Node/Other'\n")]
    [InlineData("var x = '''first\nsecond'''\n", "var x = '''first\nthird'''\n")]
    [InlineData("@export var x = 1\n", "@export_range(0, 1) var x = 1\n")]
    public void LiteralsAndAnnotationsStayInSignature(string before, string after)
    {
        Assert.True(GDScriptFormattingSignature.TryCreate(before, out string left));
        Assert.True(GDScriptFormattingSignature.TryCreate(after, out string right));
        Assert.NotEqual(left, right);
    }

    [Fact]
    public async Task AddingConventionalGDScriptTestsReachesTestCategory()
    {
        (string Path, string Content)[] before = [("app.gd", "func value():\n    return 1\n")];
        (string Path, string Content)[] after = [.. before, ("tests/test_app.gd", "func test_value():\n    assert(true)\n")];
        ChangeEstimateReport report = await EstimateFilesAsync(before, after);
        Assert.Contains(report.WorkItems, item => item.Category == EffortCategory.UnitTesting);
        Assert.Equal("change-seed/0.20.0+seed-rules/0.4.0", report.EstimatorVersion);
    }

    private static Task<ChangeEstimateReport> EstimateAsync(string before, string after) =>
        EstimateFilesAsync([("app.gd", before)], [("app.gd", after)]);

    private static Task<ChangeEstimateReport> EstimateFilesAsync(
        (string Path, string Content)[] before,
        (string Path, string Content)[] after) => new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "in-memory-gdscript",
                Selection = new ChangeSelection
                {
                    Kind = ChangeSelectionKind.BaseHead,
                    Base = Reference("base", new InMemoryChangeSnapshot(before).ObjectId),
                    Head = Reference("head", new InMemoryChangeSnapshot(after).ObjectId),
                },
                OpenBaseAsync = InMemoryChangeSnapshot.Factory(before),
                OpenHeadAsync = InMemoryChangeSnapshot.Factory(after),
            }, EstimationProfile.Implementation);

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.GitTree,
    };
}
