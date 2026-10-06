using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ChangeReplayProofTests
{
    [Fact]
    public void SeparatesExactTransplantsInheritedUpstreamAndDeclaredConflictsIncludingDeletion()
    {
        InMemoryChangeSnapshot baseline = new(("conflict", "base"), ("deleted", "old"), ("unchanged", "base"));
        InMemoryChangeSnapshot original = new(("conflict", "feature"), ("exact", "feature"), ("unchanged", "base"));
        InMemoryChangeSnapshot upstream = new(("conflict", "upstream"), ("deleted", "old"), ("unchanged", "base"), ("inherited", "upstream"));
        InMemoryChangeSnapshot replay = new(("conflict", "declared resolution"), ("exact", "feature"), ("unchanged", "base"), ("inherited", "upstream"));
        ChangeReplayProof proof = ChangeReplayProofBuilder.Verify(baseline, original, upstream, replay, CancellationToken.None);
        Assert.Equal(2, proof.ExactReplayPathCount);
        Assert.Equal(1, proof.InheritedUpstreamPathCount);
        Assert.Equal(1, proof.DeclaredConflictPathCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RejectsHiddenNovelReplayContentAndMissingNonconflictingOriginalDelta(bool hidden)
    {
        InMemoryChangeSnapshot baseline = new(("feature", "base"));
        InMemoryChangeSnapshot original = new(("feature", "feature"));
        InMemoryChangeSnapshot upstream = new(("feature", "base"), ("upstream", "upstream"));
        InMemoryChangeSnapshot replay = hidden
            ? new(("feature", "feature"), ("upstream", "altered"))
            : new(("feature", "base"), ("upstream", "upstream"));
        Assert.Throws<InvalidOperationException>(() => ChangeReplayProofBuilder.Verify(baseline, original, upstream, replay, CancellationToken.None));
    }

    [Fact]
    public void ProofRemainsCancellableAndRejectsOversizedInventories()
    {
        InMemoryChangeSnapshot empty = new();
        InMemoryChangeSnapshot one = new(("one", "one"));
        Assert.Throws<OperationCanceledException>(() => ChangeReplayProofBuilder.Verify(empty, one, empty, one, new CancellationToken(true)));
        InMemoryChangeSnapshot large = new([.. Enumerable.Range(0, ChangeReplayProofBuilder.MaximumInventoryFiles + 1).Select(index => (index.ToString(System.Globalization.CultureInfo.InvariantCulture), ""))]);
        Assert.Throws<InvalidOperationException>(() => ChangeReplayProofBuilder.Verify(empty, large, empty, empty, CancellationToken.None));
    }

    [Fact]
    public void ManifestRequiresFullImmutableEndpointsUtcPeriodAndIndependentProvenance()
    {
        ChangeRewriteReviewManifest manifest = new()
        {
            RepositoryId = "public-repository",
            RepositoryPath = ".",
            OldBaseObjectId = new('a', 40),
            OriginalObjectId = new('b', 40),
            NewBaseObjectId = new('c', 40),
            RewrittenObjectId = new('d', 40),
            SinceInclusive = new(2026, 1, 19, 0, 0, 0, TimeSpan.Zero),
            UntilExclusive = new(2026, 1, 24, 0, 0, 0, TimeSpan.Zero),
        };
        Assert.Empty(ContractValidation.Validate(manifest));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeRewriteReviewManifest, ContractJson.Serialize(manifest)).IsValid);
        Assert.NotEmpty(ContractValidation.Validate(manifest with { OriginalObjectId = "main" }));
        Assert.NotEmpty(ContractValidation.Validate(manifest with { ReplayObjectId = new('e', 40) }));
        Assert.NotEmpty(ContractValidation.Validate(manifest with { EventTimestamp = manifest.SinceInclusive }));
        Assert.NotEmpty(ContractValidation.Validate(manifest with { UntilExclusive = manifest.SinceInclusive }));
        Assert.NotEmpty(ContractValidation.Validate(manifest with { SinceInclusive = manifest.SinceInclusive.ToOffset(TimeSpan.FromHours(1)) }));
    }
}
