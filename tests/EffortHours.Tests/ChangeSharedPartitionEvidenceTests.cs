using System.Collections;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangeLogicalMarginalityTests
{
    [Fact]
    public void SharedPartitionEvidenceIsEnumeratedOnceAndPreservesTheFullUnion()
    {
        CapabilityFixture capability = new("large", EffortCategory.DataModelingPersistenceAndMigrations,
            ModifiedPath, 1m, 1000);
        CountingIds shared = new([EvidenceId(capability.Id)]);
        EstimateReport before = Report("base", [capability]);
        EstimateReport after = Report("head", [capability]);
        RepositoryEvidence baseEvidence = Evidence("base", [capability], 1m);
        RepositoryEvidence headEvidence = Evidence("head", [capability], 2m);
        ChangeSelection selection = Selection();
        ChangeEvidence change = new()
        {
            Selection = selection,
            Repository = Repository("head"),
            BaseEvidenceDigest = "sha256:base",
            HeadEvidenceDigest = "sha256:head",
            Paths = [Path(ModifiedPath, ChangePathStatus.Modified)],
        };
        ChangeWorkItemResult ordinary = ChangeWorkItemBuilder.Build(selection, change, baseEvidence,
            headEvidence, before, after, EstimationProfile.Implementation);
        ChangeWorkItemResult reused = ChangeWorkItemBuilder.Build(selection, change, baseEvidence,
            headEvidence, before with
            {
                WorkItems = [.. before.WorkItems.Select(item => item with { EvidenceIds = shared })],
            }, after with
            {
                WorkItems = [.. after.WorkItems.Select(item => item with { EvidenceIds = shared })],
            }, EstimationProfile.Implementation);
        Assert.Equal(2, shared.Enumerations);
        Assert.Equal(ContractJson.Serialize(ordinary), ContractJson.Serialize(reused));
    }

    private sealed class CountingIds(IReadOnlyList<string> ids) : IReadOnlyList<string>
    {
        public int Enumerations { get; private set; }
        public int Count => ids.Count;
        public string this[int index] => ids[index];
        public IEnumerator<string> GetEnumerator()
        {
            Enumerations++;
            return ids.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
