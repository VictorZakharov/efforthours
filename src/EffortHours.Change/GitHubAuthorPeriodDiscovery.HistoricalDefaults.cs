using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class GitHubAuthorPeriodDiscovery
{
    private async Task<DiscoveredRepository[]> DiscoverHistoricalDefaultHeadsAsync(
        GitHubDiscoveryRepository[] repositories,
        IReadOnlyList<string> aliases,
        string viewer,
        DateTimeOffset since,
        DateTimeOffset until,
        GitHubAuthorPeriodDiscoveryRequest request,
        string workingDirectory,
        ProviderQueryCounters counters,
        GitHubDiscoveryAcquisitionBudget acquisitionBudget,
        CancellationToken token)
    {
        if (repositories.Length > ChangeAuthorPeriodManifestLimits.MaximumRepositories)
            throw GitHubProviderFailure.DiscoveryBudget(GitHubProviderFailure.DefaultHeadPhase, "Historical discovery exceeds the 256-repository acquisition bound; narrow the engineering scope profile or supply a pinned offline manifest. No repository was silently omitted.");
        using SemaphoreSlim gate = new(2, 2);
        Task<(GitHubDiscoveryRepository Repository, DiscoveredHead Head, string Path)?>[] tasks = [.. repositories.Select(async repository =>
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                string? objectId = await GitHubAuthorPeriodDiscoveryJson.ResolveHistoricalDefaultHeadAsync(
                    _commands, workingDirectory, repository, counters, token).ConfigureAwait(false);
                if (objectId is null) return null;
                DiscoveredHead head = new("default", objectId, "refs/heads/" + repository.DefaultBranch);
                RepositoryAcquisitionResult acquisition;
                using (request.ExecutionTelemetry?.Measure(ChangePortfolioExecutionPhases.Acquisition))
                    acquisition = await acquisitionBudget.EnsureAsync(_cache, repository.Identity, [head], GitHubAuthorPeriodDiscoveryJson.OpaqueId("repository", repository.StableId), "unpruned-default-author-date-evidence", token).ConfigureAwait(false);
                counters.AddHistoricalAcquisition(repository.Identity, head.ObjectId, acquisition);
                if (counters.ContributorIdentity is { } identity)
                {
                    string input = ChangePortfolioComparisonIdentity.ComputeTextDigest(viewer + "\n" + identity.Login + "\n" + objectId);
                    string[]? associated = await GitHubHistoricalIdentityCache.ReadAsync(acquisition.RepositoryPath, input, token).ConfigureAwait(false);
                    if (associated is null)
                    {
                        associated = await GitHubAuthorPeriodDiscoveryJson.ResolveHistoricalIdentityAsync(_commands, workingDirectory,
                            repository.Identity, objectId, identity.Login, counters, token).ConfigureAwait(false);
                        await GitHubHistoricalIdentityCache.WriteAsync(acquisition.RepositoryPath, input, associated, token).ConfigureAwait(false);
                    }
                    identity.ObserveEmails(associated);
                }
                return ((GitHubDiscoveryRepository Repository, DiscoveredHead Head, string Path)?)(repository, head, acquisition.RepositoryPath);
            }
            catch (Exception exception) { acquisitionBudget.Stop(exception); throw; }
            finally
            {
                gate.Release();
            }
        })];
        var acquired = (await Task.WhenAll(tasks).ConfigureAwait(false)).OfType<(GitHubDiscoveryRepository Repository, DiscoveredHead Head, string Path)>().ToArray();
        string[] localAliases = counters.ContributorIdentity?.AppendAliases(aliases) ?? [.. aliases];
        Task<DiscoveredRepository?>[] selections = [.. acquired.Select(async source =>
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                GitHubDiscoveryRepository repository = source.Repository;
                DiscoveredHead head = source.Head;
                string objectId = head.ObjectId;
                GitAuthorPeriodCandidateResult candidates = await new GitClient().ListAuthorPeriodCandidatesAsync(
                    source.Path, new GitAuthorPeriodCandidateQuery
                    {
                        HeadObjectIds = [objectId],
                        IdentityGroups = [new GitAuthorPeriodIdentityGroup("requested", localAliases)],
                        SinceInclusive = since, UntilExclusive = until, DateField = request.DateField,
                        IncludeCoauthors = request.CoauthorPolicy == ChangePortfolioCoauthorPolicy.Include,
                    }, token).ConfigureAwait(false);
                GitAuthorPeriodPortfolioOptions selection = new()
                {
                    Aliases = localAliases, SinceInclusive = since, UntilExclusive = until, DateField = request.DateField,
                    MergePolicy = request.MergePolicy, CoauthorPolicy = request.CoauthorPolicy,
                };
                if (AuthorPeriodCommitSelector.Select(candidates.Candidates, selection, localAliases).Commits.Count == 0)
                    return null;
                return new DiscoveredRepository(GitHubAuthorPeriodDiscoveryJson.OpaqueId("repository", repository.StableId), repository.Identity, [head], 0);
            }
            catch (Exception exception) { acquisitionBudget.Stop(exception); throw; }
            finally { gate.Release(); }
        })];
        return [.. (await Task.WhenAll(selections).ConfigureAwait(false)).OfType<DiscoveredRepository>()
            .OrderBy(value => value.RepositoryId, StringComparer.Ordinal)];
    }
}
