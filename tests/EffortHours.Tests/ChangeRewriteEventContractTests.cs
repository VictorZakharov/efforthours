using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangeAuthorPeriodManifestContractTests
{
    [Fact]
    public void RewriteEvidenceIsSchemaBoundAndInvalidatesOnlyItsRepositoryCheckpoint()
    {
        ChangeAuthorPeriodManifest source = ValidManifest();
        ChangeRewriteEvent evidence = new()
        {
            OriginalObjectId = new string('a', 40),
            RewrittenObjectId = new string('b', 40),
            OldBaseObjectId = new string('d', 40),
            NewBaseObjectId = new string('e', 40),
            EventTimestamp = Since.AddDays(2),
        };
        ChangeAuthorPeriodManifest paired = source with
        {
            Repositories = [source.Repositories[0] with { RewriteEvents = [evidence] }, source.Repositories[1]],
        };
        Assert.Empty(ContractValidation.Validate(paired));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeAuthorPeriodManifest, ContractJson.Serialize(paired)).IsValid);
        Assert.NotEqual(ChangeAuthorPeriodManifestIdentity.ComputeDigest(source), ChangeAuthorPeriodManifestIdentity.ComputeDigest(paired));
        static string Digest(ChangeAuthorPeriodManifest manifest, string repository) => ChangePortfolioComparisonIdentity.ComputeRepositoryInputDigest(
            manifest, repository, EstimationProfile.Implementation, ChangeEstimator.Version);
        Assert.NotEqual(Digest(source, "repository-a"), Digest(paired, "repository-a"));
        Assert.Equal(Digest(source, "repository-b"), Digest(paired, "repository-b"));
        ChangeAuthorPeriodManifest changed = paired with
        {
            Repositories = [paired.Repositories[0] with
        {
            RewriteEvents = [evidence with { EventTimestamp = Since.AddDays(3) }],
        }, paired.Repositories[1]]
        };
        Assert.NotEqual(Digest(paired, "repository-a"), Digest(changed, "repository-a"));
        Assert.NotEmpty(ContractValidation.Validate(paired with
        {
            Repositories = [paired.Repositories[0] with
        {
            RewriteEvents = [evidence, evidence],
        }]
        }));
    }

}
