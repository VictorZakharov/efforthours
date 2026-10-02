using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangeLogicalMarginalityTests
{
    // Diagnostic baseline, not the desired corrected behavior or an effort label.
    [Theory]
    [InlineData(EffortCategory.IntegrationContractAndComponentTesting, "role:test")]
    [InlineData(EffortCategory.ProductionImplementation, "role:source")]
    public void ExistingBroadCapabilityDiscardsLargeSupportedPositiveMarginalAtModificationCap(
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
        ChangeWorkItemResult result = ChangeWorkItemBuilder.Build(selection, new ChangeEvidence
        {
            Selection = selection,
            Repository = Repository("head"),
            Paths = paths,
            BaseEvidenceDigest = "sha256:base",
            HeadEvidenceDigest = "sha256:head",
        }, baseEvidence, headEvidence, Report("base", [before]), Report("head", [after]), EstimationProfile.Implementation);

        // Sixty-five distinct additions and a larger normalized capability still
        // enter one modification budget. Display task splitting cannot recover it.
        Assert.Equal(66, paths.Length);
        Assert.Equal(8m, CategoryHours(result, category).Expected);
        Assert.All(result.WorkItems.Where(item => item.Category == category), item =>
            Assert.InRange(item.Hours.Expected, 0.01m, 1.5m));
        Assert.Equal(64m, after.ExpectedPerPartition - before.ExpectedPerPartition);
    }
}
