using System.Globalization;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class GitHeadReachabilityEnvelopeTests
{
    [Fact]
    public void All512HeadsKeepDistinctBitsAndSharedMembershipInOneWalk()
    {
        ChangeAuthorPeriodManifestHead[] heads = [.. Enumerable.Range(0, 512).Select(index => new ChangeAuthorPeriodManifestHead
        { Id = "head-" + index.ToString("D3", CultureInfo.InvariantCulture), ObjectId = Id(index + 1) })];
        string shared = Id(9999);
        int[] boundaryIndices = [0, 31, 32, 63, 64, 255, 256, 511];
        GitHeadReachabilityAccumulator accumulator = new(heads, [shared, .. boundaryIndices.Select(index => heads[index].ObjectId)]);
        foreach (ChangeAuthorPeriodManifestHead head in heads) accumulator.Consume(head.ObjectId + " " + shared);
        accumulator.Consume(shared);
        var result = accumulator.Result();
        Assert.Equal(heads.Select(head => head.Id), result[shared]);
        foreach (int index in boundaryIndices) Assert.Equal([heads[index].Id], result[heads[index].ObjectId]);
        Assert.Equal(0, accumulator.PendingCount);
    }

    [Fact]
    public void MembershipLedgerFailsWithoutPublishingTruncatedReachability()
    {
        string id = Id(1);
        GitHeadReachabilityAccumulator accumulator = new([new() { Id = "head", ObjectId = id }], [id], maximumMembershipBytes: 1);
        Assert.Throws<InvalidOperationException>(() => accumulator.Consume(id));
        Assert.Throws<InvalidOperationException>(() => accumulator.Result());
    }

    [Fact]
    public void OneRepositoryCanUseTheExistingGlobalEnvelopeButCannotExceedIt()
    {
        ChangeAuthorPeriodManifest manifest = new()
        {
            Selection = new()
            {
                SinceInclusive = DateTimeOffset.UnixEpoch,
                UntilExclusive = DateTimeOffset.UnixEpoch.AddYears(1),
                TimeZone = "UTC",
                DateField = ChangePortfolioDateField.Author,
                MergePolicy = ChangePortfolioMergePolicy.Exclude,
                CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include
            },
            Contributors = [new() { Id = "contributor", Aliases = ["synthetic@example.invalid"] }],
            Repositories = [new() { Id = "project", RepositoryPath = ".", Heads = [.. Enumerable.Range(0, 512)
                .Select(index => new ChangeAuthorPeriodManifestHead { Id = "head-" + index.ToString(CultureInfo.InvariantCulture), ObjectId = Id(index + 1) })] }],
        };
        Assert.Empty(ContractValidation.Validate(manifest));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeAuthorPeriodManifest, ContractJson.Serialize(manifest)).IsValid);
        ChangeAuthorPeriodManifest overflow = manifest with
        {
            Repositories = [manifest.Repositories[0] with
            { Heads = [.. manifest.Repositories[0].Heads, new() { Id = "overflow", ObjectId = Id(513) }] }]
        };
        Assert.NotEmpty(ContractValidation.Validate(overflow));
        Assert.False(ContractSchemaValidator.Validate(SchemaNames.ChangeAuthorPeriodManifest, ContractJson.Serialize(overflow)).IsValid);
        Assert.Throws<ArgumentOutOfRangeException>(() => new GitHeadReachabilityAccumulator(overflow.Repositories[0].Heads, []));
    }

    private static string Id(int value) => value.ToString("x40", CultureInfo.InvariantCulture);
}
