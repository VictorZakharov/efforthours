using System.Collections.Concurrent;
using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    private static readonly SemaphoreSlim HistoricalPullGate = new(4, 4);

    private static async Task<IReadOnlyList<DiscoveredRepository>> DiscoverHistoricalPullsBatchedAsync(
        IExternalCommandRunner commands, string directory, IReadOnlyList<GitHubDiscoveryRepository> repositories,
        AccountPullRequest[] pulls, IReadOnlyList<string> aliases, DateTimeOffset since, DateTimeOffset until,
        ChangePortfolioDateField dateField, ChangePortfolioMergePolicy mergePolicy, ChangePortfolioCoauthorPolicy coauthorPolicy,
        ProviderQueryCounters counters, CancellationToken token)
    {
        ConcurrentBag<ResolvedPullHead> selected = [];
        counters.PlanHistoricalPulls(pulls.Length);
        using CancellationTokenSource children = CancellationTokenSource.CreateLinkedTokenSource(token);
        token = children.Token;
        Exception? rootFailure = null;
        Task[] tasks = [.. pulls.GroupBy(pull => pull.RepositoryIdentity, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.Chunk(12)).Select(ProcessBatch)];
        try { await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch when (rootFailure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(rootFailure).Throw(); throw; }

        async Task ProcessBatch(AccountPullRequest[] batch)
        {
            await HistoricalPullGate.WaitAsync(token).ConfigureAwait(false);
            string phase = GitHubProviderFailure.HistoricalHeaderPhase;
            try
            {
                token.ThrowIfCancellationRequested();
                batch = await ResolveHistoricalPullHeadersBatchAsync(commands, directory, batch, counters, token).ConfigureAwait(false);
                List<AccountPullRequest> misses = [];
                phase = GitHubProviderFailure.HistoricalSelectionPhase;
                using (counters.MeasureHistoricalPhase(phase))
                {
                    foreach (AccountPullRequest pull in batch)
                    {
                        token.ThrowIfCancellationRequested();
                        GitHubPullMetadata? cached = counters.PullMetadataCache is { } cache
                            ? await cache.ReadAsync(pull.RepositoryIdentity, pull.Number, pull.ObjectId!, pull.BaseObjectId!, pull.CommitCount!.Value, token).ConfigureAwait(false) : null;
                        if (cached is null) misses.Add(pull);
                        else { counters.HistoricalPullCacheHit(); Admit(pull, cached); }
                    }
                }
                if (misses.Count == 0) return;
                phase = GitHubProviderFailure.HistoricalMetadataPhase;
                List<string> arguments = ["api", "graphql", "-f", "query=" + HistoricalPullBatchQuery(misses.Count)];
                for (int index = 0; index < misses.Count; index++)
                {
                    string[] identity = misses[index].RepositoryIdentity.Split('/');
                    arguments.AddRange(["-F", $"owner{index}={identity[0]}", "-F", $"name{index}={identity[1]}", "-F", $"number{index}={misses[index].Number}"]);
                }
                string? json = await RunApiAsync(commands, directory, arguments, counters, false, false, token,
                    capabilityFallback: true, failurePhase: phase).ConfigureAwait(false);
                using JsonDocument? document = ParseHistoricalPullBatchDocument(json);
                for (int index = 0; index < misses.Count; index++)
                {
                    token.ThrowIfCancellationRequested();
                    AccountPullRequest pull = misses[index];
                    phase = GitHubProviderFailure.HistoricalMetadataPhase;
                    GitHubPullMetadata? metadata = ParseHistoricalPullBatch(document?.RootElement, index, pull);
                    if (metadata is null)
                    {
                        counters.HistoricalPullFallback();
                        metadata = await ResolveHistoricalPullRestAsync(commands, directory, pull, counters, token).ConfigureAwait(false);
                    }
                    phase = GitHubProviderFailure.HistoricalSelectionPhase;
                    using (counters.MeasureHistoricalPhase(phase))
                    {
                        if (counters.PullMetadataCache is { } cache &&
                            await cache.WriteAsync(pull.RepositoryIdentity, pull.Number, metadata, token).ConfigureAwait(false))
                            counters.HistoricalCacheWrite();
                        Admit(pull, metadata);
                    }
                }
            }
            catch (Exception exception)
            {
                if (exception is OperationCanceledException) exception.Data[GitHubProviderFailure.InterruptedPhaseKey] = phase;
                else
                {
                    if (exception is JsonException or KeyNotFoundException or FormatException ||
                        exception is InvalidOperationException and not GitHubProviderException)
                        exception = GitHubProviderFailure.Malformed(phase, exception);
                    Interlocked.CompareExchange(ref rootFailure, exception, null);
                }
                await children.CancelAsync().ConfigureAwait(false);
                throw;
            }
            finally { HistoricalPullGate.Release(); }
        }
        return [.. selected.GroupBy(pull => pull.RepositoryIdentity, StringComparer.OrdinalIgnoreCase).Select(group =>
        {
            GitHubDiscoveryRepository repository = repositories.Single(value => value.Identity.Equals(group.Key, StringComparison.OrdinalIgnoreCase));
            DiscoveredHead[] heads = [.. group.OrderBy(pull => pull.Number).Select(pull => new DiscoveredHead(
                OpaqueId("open", repository.StableId + ":" + pull.Number), pull.ObjectId, $"refs/pull/{pull.Number}/head", pull.Open))
                .DistinctBy(head => head.ObjectId, StringComparer.Ordinal)];
            if (heads.Length > ChangeAuthorPeriodManifestLimits.MaximumHeadsPerRepository)
                throw GitHubProviderFailure.DiscoveryBudget(GitHubProviderFailure.HistoricalSelectionPhase,
                    $"Historical PR discovery selected {heads.Length} distinct heads in one repository, exceeding the {ChangeAuthorPeriodManifestLimits.MaximumHeadsPerRepository}-head bound; no heads were dropped.",
                    "inspect-head-scope-or-use-pinned-manifest");
            return new DiscoveredRepository(OpaqueId("repository", repository.StableId), group.Key, heads,
                pulls.Count(pull => pull.RepositoryIdentity.Equals(group.Key, StringComparison.OrdinalIgnoreCase)));
        }).OrderBy(repository => repository.RepositoryId, StringComparer.Ordinal)];

        void Admit(AccountPullRequest pull, GitHubPullMetadata metadata)
        {
            GitAuthorPeriodPortfolioOptions options = new()
            {
                Aliases = aliases,
                SinceInclusive = since,
                UntilExclusive = until,
                DateField = dateField,
                MergePolicy = mergePolicy,
                CoauthorPolicy = coauthorPolicy
            };
            bool matching = false;
            foreach (GitHubPullCommitEvidence evidence in metadata.Commits)
            {
                GitCommitMetadata commit = evidence.Commit;
                counters.ObserveIdentity(evidence.Login, commit);
                matching |= AuthorPeriodCommitSelector.Select([commit], options, aliases).Commits.Count > 0 ||
                    aliases.Contains(evidence.Login, StringComparer.OrdinalIgnoreCase) && SelectedTimestamp(commit, dateField) >= since &&
                    SelectedTimestamp(commit, dateField) < until && (commit.ParentObjectIds.Count <= 1 || mergePolicy == ChangePortfolioMergePolicy.FirstParent);
            }
            counters.CompleteHistoricalPull(matching);
            if (matching) selected.Add(new(pull.RepositoryIdentity, pull.Number, metadata.Head, pull.Open));
        }
    }

}
