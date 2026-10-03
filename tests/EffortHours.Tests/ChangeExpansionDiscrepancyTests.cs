using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangeLogicalMarginalityTests
{
    [Theory]
    [InlineData(EffortCategory.IntegrationContractAndComponentTesting, "role:test")]
    [InlineData(EffortCategory.ProductionImplementation, "role:source")]
    public void ExistingBroadCapabilityPreservesSupportedPositiveGrowthBeyondModificationCap(
        EffortCategory category, string role)
    {
        CapabilityFixture before = new("broad-capability", category, "scope/existing.cs", 64m, 1);
        CapabilityFixture after = before with { ExpectedPerPartition = 128m };
        ChangePathEvidence[] paths = [Path(before.Path, ChangePathStatus.Modified, role) with
        {
            BaseObjectId = "existing-before", HeadObjectId = "existing-after",
        }, .. Enumerable.Range(0, 65).Select(index => Path("scope/addition-" + index + ".cs", ChangePathStatus.Added, role) with
        {
            HeadObjectId = "distinct-addition-" + index,
        })];
        if (category == EffortCategory.IntegrationContractAndComponentTesting)
        {
            paths = [.. paths.Select(path => path with { Tags = [.. path.Tags, "test-type:integration"] })];
        }
        ChangeSelection selection = Selection();
        RepositoryEvidence baseEvidence = Evidence("base", [before], measurement: 1m);
        RepositoryEvidence headEvidence = Evidence("head", [after], measurement: 358m) with
        {
            Facts = [Fact(after, 358m) with
            {
                Locations = [.. paths.Select(path => new EvidenceLocation { Path = path.Path })],
            }],
        };
        string measurement = category == EffortCategory.ProductionImplementation ? "methods" : "test-cases";
        baseEvidence = baseEvidence with
        {
            Facts = [.. baseEvidence.Facts.Select(fact => fact with
        {
            Measurements = [new EvidenceMeasurement { Name = measurement, Value = 1m, Unit = "units" }],
        })]
        };
        headEvidence = headEvidence with
        {
            Facts = [.. headEvidence.Facts.Select(fact => fact with
        {
            Measurements = [new EvidenceMeasurement { Name = measurement, Value = 358m, Unit = "units" }],
        })]
        };
        ChangeWorkItemResult result = ChangeWorkItemBuilder.Build(selection, new ChangeEvidence
        {
            Selection = selection,
            Repository = Repository("head"),
            Paths = paths,
            BaseEvidenceDigest = "sha256:base",
            HeadEvidenceDigest = "sha256:head",
        }, baseEvidence, headEvidence, Report("base", [before]), Report("head", [after]), EstimationProfile.Implementation);

        // Attributed semantic growth preserves its marginal. Display partitions
        // remain presentation only and do not multiply the capability total.
        Assert.Equal(66, paths.Length);
        Assert.Equal(64m, CategoryHours(result, category).Expected);
        Assert.All(result.WorkItems.Where(item => item.Category == category), item =>
            Assert.InRange(item.Hours.Expected, 0.01m, 1.5m));
        Assert.Equal(64m, after.ExpectedPerPartition - before.ExpectedPerPartition);
        ChangeWorkItemResult repartitioned = ChangeWorkItemBuilder.Build(selection, new ChangeEvidence
        {
            Selection = selection,
            Repository = Repository("head"),
            Paths = paths,
            BaseEvidenceDigest = "sha256:base",
            HeadEvidenceDigest = "sha256:head",
        }, baseEvidence, headEvidence, Report("base", [before]),
            Report("head", [after with { PartitionCount = 64, ExpectedPerPartition = 2m }]), EstimationProfile.Implementation);
        Assert.Equal(CategoryHours(result, category), CategoryHours(repartitioned, category));
    }
}
