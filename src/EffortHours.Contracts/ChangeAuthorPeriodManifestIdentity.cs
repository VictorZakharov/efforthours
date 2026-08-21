using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static class ChangeAuthorPeriodManifestIdentity
{
    public static ChangePortfolioSelection CreateReportSelection(
        ChangeAuthorPeriodManifest manifest,
        string? manifestDigest = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        string digest = manifestDigest ?? ComputeDigest(manifest);
        return new ChangePortfolioSelection
        {
            Kind = ChangePortfolioSelectionKind.AuthorPeriod,
            ManifestBased = true,
            AuthorPeriodManifest = new ChangePortfolioAuthorPeriodManifestSelection
            {
                ManifestDigest = digest,
                SinceInclusive = manifest.Selection.SinceInclusive.ToUniversalTime(),
                UntilExclusive = manifest.Selection.UntilExclusive.ToUniversalTime(),
                TimeZone = manifest.Selection.TimeZone,
                DateField = manifest.Selection.DateField,
                MergePolicy = manifest.Selection.MergePolicy,
                CoauthorPolicy = manifest.Selection.CoauthorPolicy,
                ContributorIds = [.. manifest.Contributors
                    .Select(contributor => contributor.Id)
                    .Order(StringComparer.Ordinal)],
                Repositories = [.. manifest.Repositories
                    .OrderBy(repository => repository.Id, StringComparer.Ordinal)
                    .Select(repository => new ChangePortfolioAuthorPeriodManifestRepository
                    {
                        Id = repository.Id,
                        Heads = [.. repository.Heads
                            .OrderBy(head => head.Id, StringComparer.Ordinal)
                            .Select(head => new ChangePortfolioAuthorPeriodManifestHead
                            {
                                Id = head.Id,
                                ObjectId = head.ObjectId,
                            })],
                    })],
            },
        };
    }

    public static string ComputeDigest(ChangeAuthorPeriodManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        IReadOnlyList<string> errors = ContractValidation.Validate(manifest);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The author-period manifest is semantically invalid: " + string.Join(" ", errors),
                nameof(manifest));
        }

        CanonicalManifest canonical = new(
            manifest.SchemaVersion,
            new CanonicalSelection(
                manifest.Selection.SinceInclusive.ToUniversalTime(),
                manifest.Selection.UntilExclusive.ToUniversalTime(),
                manifest.Selection.TimeZone,
                manifest.Selection.DateField,
                manifest.Selection.MergePolicy,
                manifest.Selection.CoauthorPolicy,
                manifest.Selection.IntervalSemantics),
            [.. manifest.Contributors
                .OrderBy(contributor => contributor.Id, StringComparer.Ordinal)
                .Select(contributor => new CanonicalContributor(
                    contributor.Id,
                    [.. contributor.Aliases
                        .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(alias => alias, StringComparer.Ordinal)]))],
            [.. manifest.Repositories
                .OrderBy(repository => repository.Id, StringComparer.Ordinal)
                .Select(repository => new CanonicalRepository(
                    repository.Id,
                    [.. repository.Heads
                        .OrderBy(head => head.Id, StringComparer.Ordinal)
                        .Select(head => new CanonicalHead(head.Id, head.ObjectId))]))]);
        byte[] bytes = Encoding.UTF8.GetBytes(ContractJson.SerializeCompact(canonical));
        return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed record CanonicalManifest(
        string SchemaVersion,
        CanonicalSelection Selection,
        IReadOnlyList<CanonicalContributor> Contributors,
        IReadOnlyList<CanonicalRepository> Repositories);

    private sealed record CanonicalSelection(
        DateTimeOffset SinceInclusive,
        DateTimeOffset UntilExclusive,
        string TimeZone,
        ChangePortfolioDateField DateField,
        ChangePortfolioMergePolicy MergePolicy,
        ChangePortfolioCoauthorPolicy CoauthorPolicy,
        string IntervalSemantics);

    private sealed record CanonicalContributor(string Id, IReadOnlyList<string> Aliases);

    private sealed record CanonicalRepository(
        string Id,
        IReadOnlyList<CanonicalHead> Heads);

    private sealed record CanonicalHead(string Id, string ObjectId);
}
