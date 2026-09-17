using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed record DiscoveredRepository(
    string RepositoryId,
    string RepositoryIdentity,
    IReadOnlyList<DiscoveredHead> Heads,
    int OpenPullRequestCount);

internal sealed record DiscoveredHead(string Id, string ObjectId, string FetchRef);

internal sealed record ResolvedDiscoveryContributor(
    string Id,
    string Login,
    IReadOnlyList<string> Aliases);

internal sealed record ResolvedDiscoveryContributors(
    IReadOnlyList<ResolvedDiscoveryContributor> Contributors,
    ChangePortfolioContributorSelection Selection,
    IReadOnlyList<string> VerifiedEmails);

internal sealed record GitHubActiveContributor(
    string Login,
    IReadOnlyList<string> Aliases);

internal sealed record DefaultHeadBatchResult(
    IReadOnlyList<DiscoveredRepository> Repositories,
    IReadOnlyList<GitHubDiscoveryRepository> FallbackRepositories);
