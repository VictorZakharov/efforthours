using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class GitHubAuthorPeriodDiscovery
{
    private async Task<DiscoveredRepository[]> DiscoverHeadsAsync(
        GitHubDiscoveryRepository[] repositories,
        IReadOnlyList<string> aliases,
        string authenticatedLogin,
        DateTimeOffset since,
        DateTimeOffset until,
        GitHubAuthorPeriodDiscoveryRequest request,
        string workingDirectory,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        DiscoveredRepository[] defaults;
        using (request.ExecutionTelemetry?.Measure(ChangePortfolioExecutionPhases.DefaultHeadDiscovery))
        {
            DefaultHeadBatchResult batched =
                await GitHubAuthorPeriodDiscoveryJson.DiscoverDefaultHeadsBatchedAsync(
                    _commands,
                    workingDirectory,
                    repositories,
                    aliases,
                    since,
                    until,
                    request.DateField,
                    request.MergePolicy,
                    request.CoauthorPolicy,
                    counters,
                    cancellationToken).ConfigureAwait(false);
            DiscoveredRepository[] fallback = await DiscoverHeadPhaseAsync(
                batched.FallbackRepositories,
                aliases,
                authenticatedLogin,
                since,
                until,
                request,
                workingDirectory,
                counters,
                includeDefaultHead: true,
                includeOpenPullRequests: false,
                cancellationToken).ConfigureAwait(false);
            defaults = [.. batched.Repositories, .. fallback];
        }

        if (!request.IncludeOpenPullRequests || repositories.Length == 0)
        {
            return defaults;
        }

        DiscoveredRepository[] openPullRequests;
        using (request.ExecutionTelemetry?.Measure(ChangePortfolioExecutionPhases.OpenPullRequestDiscovery))
        {
            GitHubPullAuthorIdentity? identity = counters.PullAuthorIdentity;
            if (identity is not null)
            {
                await GitHubAuthorPeriodDiscoveryJson.ResolveNoreplyAliasesAsync(
                    _commands, workingDirectory, identity, counters, cancellationToken).ConfigureAwait(false);
            }

            string? directLogin = request.ProviderLogin is { } requested
                ? requested.Equals("@me", StringComparison.OrdinalIgnoreCase) ? authenticatedLogin : requested
                : SingleContributorLogin(request, authenticatedLogin);
            string[] pullLogins = directLogin is not null ? [directLogin] :
                identity?.KnownLogins() ?? [.. aliases];
            if (identity is not null && (pullLogins.Length == 0 ||
                pullLogins.Length > 1 && identity.UnresolvedAliases().Length != 0))
            {
                throw GitHubProviderFailure.UnresolvedContributor();
            }

            counters.IdentityResolution = request.ContributorSample is not null ? "team" :
                request.ProviderLogin is not null ? "explicit-login" :
                directLogin is not null ? "direct-login" :
                pullLogins.Length == 1 ? "provider-linked-aliases" : "multiple-logins";
            string? contributorLogin = request.ContributorSample is null && pullLogins.Length == 1
                ? pullLogins[0] : null;
            IReadOnlyList<DiscoveredRepository>? accountWide = contributorLogin is not null
                ? await GitHubAuthorPeriodDiscoveryJson
                    .DiscoverUserOpenPullHeadsAccountWideAsync(
                        _commands,
                        workingDirectory,
                        repositories,
                        contributorLogin!,
                        aliases,
                        since,
                        until,
                        request.DateField,
                        request.MergePolicy,
                        request.CoauthorPolicy,
                        counters,
                        cancellationToken).ConfigureAwait(false)
                : null;
            // A speculative account read may supply previously unseen email associations.
            // It cannot establish complete coverage until every requested alias is resolved.
            identity?.RequireComplete(pullLogins);
            if (accountWide is null)
            {
                counters.AddFallback("open-pr", contributorLogin is null
                    ? "identity-not-single-login" : "account-connection-unavailable", repositories.Length);
            }

            openPullRequests = accountWide is not null
                ? [.. accountWide]
                : await DiscoverHeadPhaseAsync(
                    repositories,
                    aliases,
                    authenticatedLogin,
                    since,
                    until,
                    request,
                    workingDirectory,
                    counters,
                    includeDefaultHead: false,
                    includeOpenPullRequests: true,
                    cancellationToken,
                    pullLogins).ConfigureAwait(false);
            identity?.RequireComplete(pullLogins);
        }

        return MergeDiscoveredHeads(repositories, defaults, openPullRequests);
    }

    private async Task<DiscoveredRepository[]> DiscoverHeadPhaseAsync(
        IReadOnlyList<GitHubDiscoveryRepository> repositories,
        IReadOnlyList<string> aliases,
        string authenticatedLogin,
        DateTimeOffset since,
        DateTimeOffset until,
        GitHubAuthorPeriodDiscoveryRequest request,
        string workingDirectory,
        ProviderQueryCounters counters,
        bool includeDefaultHead,
        bool includeOpenPullRequests,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? pullAuthorLogins = null)
    {
        using SemaphoreSlim gate = new(4, 4);
        Task<DiscoveredRepository?>[] tasks = [.. repositories.Select(async repository =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await GitHubAuthorPeriodDiscoveryJson.DiscoverHeadsAsync(
                    _commands,
                    workingDirectory,
                    repository,
                    aliases,
                    authenticatedLogin,
                    since,
                    until,
                    request.DateField,
                    request.MergePolicy,
                    request.CoauthorPolicy,
                    includeOpenPullRequests,
                    counters,
                    cancellationToken,
                    includeDefaultHead,
                    includeAuthenticatedPullAuthor: false,
                    pullAuthorLogins)
                    .ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        })];
        return [.. (await Task.WhenAll(tasks).ConfigureAwait(false))
            .Where(value => value is not null)
            .Select(value => value!)
            .OrderBy(value => value.RepositoryId, StringComparer.Ordinal)];
    }

    private static DiscoveredRepository[] MergeDiscoveredHeads(
        IReadOnlyList<GitHubDiscoveryRepository> repositories,
        IReadOnlyList<DiscoveredRepository> defaults,
        IReadOnlyList<DiscoveredRepository> openPullRequests)
    {
        Dictionary<string, DiscoveredRepository> defaultByIdentity =
            defaults.ToDictionary(value => value.RepositoryIdentity, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, DiscoveredRepository> pullsByIdentity =
            openPullRequests.ToDictionary(value => value.RepositoryIdentity, StringComparer.OrdinalIgnoreCase);
        List<DiscoveredRepository> merged = [];
        foreach (GitHubDiscoveryRepository repository in repositories)
        {
            defaultByIdentity.TryGetValue(repository.Identity, out DiscoveredRepository? defaultHead);
            pullsByIdentity.TryGetValue(repository.Identity, out DiscoveredRepository? pullHeads);
            DiscoveredHead[] heads =
            [
                .. defaultHead?.Heads ?? [],
                .. pullHeads?.Heads ?? [],
            ];
            if (heads.Length == 0)
            {
                continue;
            }

            merged.Add(new DiscoveredRepository(
                defaultHead?.RepositoryId ?? pullHeads!.RepositoryId,
                repository.Identity,
                heads,
                pullHeads?.OpenPullRequestCount ?? 0));
        }

        return [.. merged.OrderBy(value => value.RepositoryId, StringComparer.Ordinal)];
    }
}
