using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static class ChangePortfolioComparisonIdentity
{
    public static string ComputeBucketDigest(ChangePortfolioBucketManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        IReadOnlyList<string> errors = ContractValidation.Validate(manifest);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The bucket manifest is semantically invalid: " + string.Join(" ", errors),
                nameof(manifest));
        }

        object canonical = new
        {
            manifest.SchemaVersion,
            Buckets = manifest.Buckets
                .OrderBy(bucket => bucket.SinceInclusive)
                .ThenBy(bucket => bucket.Id, StringComparer.Ordinal)
                .Select(bucket => new
                {
                    bucket.Id,
                    bucket.Label,
                    SinceInclusive = bucket.SinceInclusive.ToUniversalTime(),
                    UntilExclusive = bucket.UntilExclusive.ToUniversalTime(),
                }),
        };
        return ComputeJsonDigest(canonical);
    }

    public static string ComputeCapacityDigest(ChangePortfolioCapacityManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        IReadOnlyList<string> errors = ContractValidation.Validate(manifest);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The capacity manifest is semantically invalid: " + string.Join(" ", errors),
                nameof(manifest));
        }

        object canonical = new
        {
            manifest.SchemaVersion,
            manifest.CalendarPolicy,
            Entries = manifest.Entries
                .OrderBy(entry => entry.BucketId, StringComparer.Ordinal)
                .ThenBy(entry => entry.ContributorId, StringComparer.Ordinal)
                .Select(entry => new { entry.BucketId, entry.ContributorId, entry.Hours }),
        };
        return ComputeJsonDigest(canonical);
    }

    public static string ComputeRepositoryInputDigest(
        ChangeAuthorPeriodManifest manifest,
        string repositoryId,
        EstimationProfile profile,
        string estimatorVersion)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(estimatorVersion);
        ChangeAuthorPeriodManifestRepository repository = manifest.Repositories.Single(
            candidate => string.Equals(candidate.Id, repositoryId, StringComparison.Ordinal));
        object canonical = new
        {
            Protocol = ChangePortfolioComparisonPolicies.RepositoryEvidenceShardsV1,
            manifest.SchemaVersion,
            Selection = new
            {
                SinceInclusive = manifest.Selection.SinceInclusive.ToUniversalTime(),
                UntilExclusive = manifest.Selection.UntilExclusive.ToUniversalTime(),
                manifest.Selection.TimeZone,
                manifest.Selection.DateField,
                manifest.Selection.MergePolicy,
                manifest.Selection.CoauthorPolicy,
                manifest.Selection.IntervalSemantics,
            },
            Contributors = manifest.Contributors
                .OrderBy(contributor => contributor.Id, StringComparer.Ordinal)
                .Select(contributor => new
                {
                    contributor.Id,
                    Aliases = contributor.Aliases
                        .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(alias => alias, StringComparer.Ordinal),
                }),
            Repository = new
            {
                repository.Id,
                Heads = repository.Heads
                    .OrderBy(head => head.Id, StringComparer.Ordinal)
                    .Select(head => new { head.Id, head.ObjectId }),
            },
            Profile = profile,
            EstimatorVersion = estimatorVersion,
        };
        return ComputeJsonDigest(canonical);
    }

    public static string ComputePortfolioDigest(ChangePortfolioReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return ComputeJsonDigest(report);
    }

    public static string ComputeSemanticDigest(
        ChangePortfolioReport source,
        ChangePortfolioComparisonBucketPolicy bucketPolicy,
        IReadOnlyList<ChangePortfolioComparisonBucket> buckets,
        IReadOnlyList<ChangePortfolioComparisonSeries> series)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(bucketPolicy);
        ArgumentNullException.ThrowIfNull(buckets);
        ArgumentNullException.ThrowIfNull(series);
        object canonical = new
        {
            Protocol = ChangePortfolioComparisonPolicies.ExclusiveContributorSeriesV1,
            SourcePortfolioDigest = ComputePortfolioDigest(source),
            BucketPolicy = new
            {
                bucketPolicy.Kind,
                bucketPolicy.Policy,
                bucketPolicy.InputDigest,
                bucketPolicy.CapacityCalendarPolicy,
                bucketPolicy.CapacityInputDigest,
                bucketPolicy.RollingWindowBucketCount,
            },
            Buckets = buckets,
            Series = series,
        };
        return ComputeJsonDigest(canonical);
    }

    public static string ComputeTextDigest(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string ComputeJsonDigest<T>(T value) =>
        ComputeTextDigest(ContractJson.SerializeCompact(value));
}
