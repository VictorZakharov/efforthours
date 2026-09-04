using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class GitHubAuthorPeriodDiscovery
{
    private static ChangeAuthorPeriodManifest CreateManifest(
        IReadOnlyList<DiscoveredRepository> repositories,
        Dictionary<string, string> paths,
        IReadOnlyList<ResolvedDiscoveryContributor> contributors,
        DateTimeOffset since,
        DateTimeOffset until,
        TimeZoneInfo zone,
        GitHubAuthorPeriodDiscoveryRequest request) => new()
        {
            Selection = new ChangeAuthorPeriodManifestSelection
            {
                SinceInclusive = since,
                UntilExclusive = until,
                TimeZone = zone.Id,
                DateField = request.DateField,
                MergePolicy = request.MergePolicy,
                CoauthorPolicy = request.CoauthorPolicy,
            },
            Contributors = [.. contributors.Select(contributor =>
                new ChangeAuthorPeriodManifestContributor
                {
                    Id = contributor.Id,
                    Aliases = contributor.Aliases,
                })],
            Repositories = [.. repositories.Select(repository =>
                new ChangeAuthorPeriodManifestRepository
                {
                    Id = repository.RepositoryId,
                    RepositoryPath = paths[repository.RepositoryId],
                    Heads = [.. repository.Heads.Select(head => new ChangeAuthorPeriodManifestHead
                    {
                        Id = head.Id,
                        ObjectId = head.ObjectId,
                    })],
                })],
        };
}
