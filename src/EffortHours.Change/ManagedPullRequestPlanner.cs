using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed class ManagedPullRequestPlanner
{
    private readonly IPullRequestResolver _resolver;
    private readonly GitHubRepositoryCache _repositoryCache;
    private readonly GitHubPullRequestResolutionCache _resolutionCache;
    private readonly GitChangePlanner _planner;

    public ManagedPullRequestPlanner()
        : this(
            new GitHubPullRequestResolver(),
            new GitHubRepositoryCache(),
            new GitHubPullRequestResolutionCache(),
            new GitChangePlanner())
    {
    }

    internal ManagedPullRequestPlanner(
        IPullRequestResolver resolver,
        GitHubRepositoryCache repositoryCache,
        GitHubPullRequestResolutionCache resolutionCache,
        GitChangePlanner planner)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _repositoryCache = repositoryCache ?? throw new ArgumentNullException(nameof(repositoryCache));
        _resolutionCache = resolutionCache ?? throw new ArgumentNullException(nameof(resolutionCache));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
    }

    public async Task<GitChangePlan> PlanAsync(
        string pullRequest,
        string? repository,
        bool fetchMissing,
        CancellationToken cancellationToken = default)
    {
        GitHubPullRequestLocator locator = GitHubPullRequestLocatorParser.Parse(
            pullRequest,
            repository);
        ResolvedPullRequest resolved;
        RepositoryAcquisitionResult acquisition;
        FileStream? updateLock = fetchMissing
            ? await _resolutionCache.AcquireUpdateLockAsync(locator, cancellationToken)
                .ConfigureAwait(false)
            : null;
        try
        {
            if (fetchMissing)
            {
                resolved = await _resolver.ResolveAsync(
                    Environment.CurrentDirectory,
                    pullRequest,
                    locator.RepositoryIdentity,
                    cancellationToken).ConfigureAwait(false);
                ValidateLiveResolution(locator, resolved);
            }
            else
            {
                resolved = await _resolutionCache.LoadAsync(
                        locator,
                        pullRequest,
                        cancellationToken)
                    .ConfigureAwait(false) ?? throw new InvalidOperationException(
                        "The managed pull-request resolution cache has no immutable identity for this request. " +
                        "Retry with --fetch-missing to resolve it through GitHub; EffortHours does not access " +
                        "the network without that explicit authorization.");
            }

            if (string.IsNullOrWhiteSpace(resolved.BaseRefName))
            {
                throw new InvalidOperationException(
                    "The pull-request resolution does not contain the provider base ref required for acquisition.");
            }

            DiscoveredHead[] heads =
            [
                new("provider-base", resolved.BaseObjectId, $"refs/heads/{resolved.BaseRefName}"),
                new("pull-request", resolved.HeadObjectId, $"refs/pull/{locator.Number}/head"),
            ];
            acquisition = await _repositoryCache.EnsureAsync(
                locator.RepositoryIdentity,
                heads,
                fetchMissing,
                cancellationToken).ConfigureAwait(false);
            if (fetchMissing)
            {
                await _resolutionCache.SaveAsync(locator, resolved, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            if (updateLock is not null)
            {
                await updateLock.DisposeAsync().ConfigureAwait(false);
            }
        }

        PullRequestObjectAcquisition mode = fetchMissing
            ? PullRequestObjectAcquisition.ManagedCacheFetch
            : PullRequestObjectAcquisition.ManagedCacheReuse;
        return await _planner.PlanResolvedPullRequestAsync(
            acquisition.RepositoryPath,
            resolved,
            mode,
            cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateLiveResolution(
        GitHubPullRequestLocator locator,
        ResolvedPullRequest resolved)
    {
        if (resolved.Reference.Number != locator.Number ||
            !string.Equals(
                resolved.Reference.Repository,
                locator.RepositoryIdentity,
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(resolved.Reference.Url))
        {
            throw new InvalidOperationException(
                "The live provider response did not match the requested pull-request identity.");
        }

        GitHubPullRequestLocator resolvedLocator = GitHubPullRequestLocatorParser.Parse(
            resolved.Reference.Url,
            resolved.Reference.Repository);
        if (resolvedLocator != locator)
        {
            throw new InvalidOperationException(
                "The live provider response did not match the requested pull-request identity.");
        }
    }
}
