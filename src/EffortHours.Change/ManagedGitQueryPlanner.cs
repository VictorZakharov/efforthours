using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record ManagedRepositoryHead(
    string RepositoryPath,
    string ObjectId,
    bool Fetched,
    bool ProviderResolved);

public sealed class ManagedGitQueryPlanner
{
    private const string BaseHeadKind = "base-head";
    private const string CommitKind = "commit";
    private const string RangeKind = "range";
    private readonly IGitHubRevisionResolver _resolver;
    private readonly GitHubRepositoryCache _repositoryCache;
    private readonly GitHubRevisionResolutionCache _resolutionCache;
    private readonly GitChangePlanner _planner;

    public ManagedGitQueryPlanner()
        : this(
            new GitHubRevisionResolver(),
            new GitHubRepositoryCache(),
            new GitHubRevisionResolutionCache(),
            new GitChangePlanner())
    {
    }

    internal ManagedGitQueryPlanner(
        IGitHubRevisionResolver resolver,
        GitHubRepositoryCache repositoryCache,
        GitHubRevisionResolutionCache resolutionCache,
        GitChangePlanner planner)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _repositoryCache = repositoryCache ?? throw new ArgumentNullException(nameof(repositoryCache));
        _resolutionCache = resolutionCache ?? throw new ArgumentNullException(nameof(resolutionCache));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
    }

    public async Task<GitChangePlan> PlanBaseHeadAsync(
        string repositoryIdentity,
        string baseRevision,
        string headRevision,
        bool fetchMissing,
        CancellationToken cancellationToken = default)
    {
        ManagedResolutionResult managed = await ResolveAsync(
            BaseHeadKind,
            repositoryIdentity,
            [baseRevision, headRevision],
            fetchMissing,
            cancellationToken).ConfigureAwait(false);
        GitChangePlan plan = await _planner.PlanBaseHeadAsync(
            managed.RepositoryPath,
            managed.Revisions[0].ObjectId,
            managed.Revisions[1].ObjectId,
            cancellationToken).ConfigureAwait(false);
        return Annotate(plan with
        {
            Selection = plan.Selection with
            {
                Base = plan.Selection.Base with { Selector = baseRevision },
                Head = plan.Selection.Head with { Selector = headRevision },
            },
        }, managed.Fetched, fetchMissing);
    }

    public async Task<GitChangePlan> PlanCommitAsync(
        string repositoryIdentity,
        string revision,
        string? parentRevision,
        bool fetchMissing,
        CancellationToken cancellationToken = default)
    {
        string[] selectors = parentRevision is null ? [revision] : [revision, parentRevision];
        ManagedResolutionResult managed = await ResolveAsync(
            CommitKind,
            repositoryIdentity,
            selectors,
            fetchMissing,
            cancellationToken).ConfigureAwait(false);
        GitChangePlan plan = await _planner.PlanCommitAsync(
            managed.RepositoryPath,
            managed.Revisions[0].ObjectId,
            parentRevision is null ? null : managed.Revisions[1].ObjectId,
            cancellationToken).ConfigureAwait(false);
        string baseSelector = plan.Selection.Base.Kind == ChangeSnapshotKind.EmptyTree
            ? plan.Selection.Base.Selector
            : parentRevision ?? revision + "^1";
        return Annotate(plan with
        {
            Selection = plan.Selection with
            {
                Base = plan.Selection.Base with { Selector = baseSelector },
                Head = plan.Selection.Head with { Selector = revision },
                Commit = revision,
                Parent = parentRevision,
            },
            Components = [.. plan.Components.Select(component => component with
            {
                Selector = revision,
            })],
        }, managed.Fetched, fetchMissing);
    }

    public async Task<GitChangePlan> PlanRangeAsync(
        string repositoryIdentity,
        string range,
        bool fetchMissing,
        CancellationToken cancellationToken = default)
    {
        (string baseRevision, string headRevision) = ParseRange(range);
        ManagedResolutionResult managed = await ResolveAsync(
            RangeKind,
            repositoryIdentity,
            [baseRevision, headRevision],
            fetchMissing,
            cancellationToken).ConfigureAwait(false);
        GitChangePlan plan = await _planner.PlanRangeAsync(
            managed.RepositoryPath,
            managed.Revisions[0].ObjectId + ".." + managed.Revisions[1].ObjectId,
            cancellationToken).ConfigureAwait(false);
        return Annotate(plan with
        {
            Selection = plan.Selection with
            {
                Base = plan.Selection.Base with { Selector = baseRevision },
                Head = plan.Selection.Head with { Selector = headRevision },
                Range = range,
            },
        }, managed.Fetched, fetchMissing);
    }

    public async Task<ManagedRepositoryHead> PrepareHeadAsync(
        string repositoryIdentity,
        string headRevision,
        bool fetchMissing,
        CancellationToken cancellationToken = default)
    {
        ManagedResolutionResult managed = await ResolveAsync(
            "reachable-head",
            repositoryIdentity,
            [headRevision],
            fetchMissing,
            cancellationToken).ConfigureAwait(false);
        return new ManagedRepositoryHead(
            managed.RepositoryPath,
            managed.Revisions[0].ObjectId,
            managed.Fetched,
            fetchMissing);
    }

    public async Task<string> PreparePinnedObjectsAsync(
        string repositoryIdentity,
        IReadOnlyList<string> objectIds,
        bool fetchMissing,
        CancellationToken cancellationToken = default)
    {
        DiscoveredHead[] heads = CreatePinnedHeads(objectIds);
        RepositoryAcquisitionResult acquisition = await _repositoryCache.EnsureAsync(
            GitHubRepositoryIdentity.Normalize(repositoryIdentity),
            heads,
            fetchMissing,
            cancellationToken).ConfigureAwait(false);
        return acquisition.RepositoryPath;
    }

    public async Task<string> LocatePinnedObjectsAsync(
        string repositoryIdentity,
        IReadOnlyList<string> objectIds,
        CancellationToken cancellationToken = default) =>
        await _repositoryCache.UseExistingAsync(
            GitHubRepositoryIdentity.Normalize(repositoryIdentity),
            CreatePinnedHeads(objectIds),
            cancellationToken).ConfigureAwait(false);

    private async Task<ManagedResolutionResult> ResolveAsync(
        string kind,
        string repositoryIdentity,
        IReadOnlyList<string> selectors,
        bool fetchMissing,
        CancellationToken cancellationToken)
    {
        string repository = GitHubRepositoryIdentity.Normalize(repositoryIdentity);
        ManagedGitResolution resolution;
        RepositoryAcquisitionResult acquisition;
        FileStream? updateLock = fetchMissing
            ? await _resolutionCache.AcquireUpdateLockAsync(
                kind,
                repository,
                selectors,
                cancellationToken).ConfigureAwait(false)
            : null;
        try
        {
            if (fetchMissing)
            {
                IReadOnlyList<ResolvedGitRevision> revisions = await _resolver.ResolveAsync(
                    repository,
                    selectors,
                    cancellationToken).ConfigureAwait(false);
                resolution = new ManagedGitResolution(kind, repository, revisions);
            }
            else
            {
                resolution = await _resolutionCache.LoadAsync(
                        kind,
                        repository,
                        selectors,
                        cancellationToken)
                    .ConfigureAwait(false) ?? throw new InvalidOperationException(
                        "The managed Git revision cache has no immutable identity for this request. " +
                        "Retry with --fetch-missing to resolve it through GitHub; EffortHours does not access " +
                        "the network without that explicit authorization.");
            }

            DiscoveredHead[] heads = [.. resolution.Revisions
                .Select((revision, index) => new DiscoveredHead(
                    "revision-" + index,
                    revision.ObjectId,
                    revision.ObjectId))];
            acquisition = await _repositoryCache.EnsureAsync(
                repository,
                heads,
                fetchMissing,
                cancellationToken).ConfigureAwait(false);
            if (fetchMissing)
            {
                await _resolutionCache.SaveAsync(resolution, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (updateLock is not null)
            {
                await updateLock.DisposeAsync().ConfigureAwait(false);
            }
        }

        return new ManagedResolutionResult(
            acquisition.RepositoryPath,
            resolution.Revisions,
            acquisition.AcquiredHeadCount > 0);
    }

    private static GitChangePlan Annotate(
        GitChangePlan plan,
        bool fetched,
        bool providerResolved)
    {
        Diagnostic diagnostic = new()
        {
            Code = "FB5108",
            Severity = DiagnosticSeverity.Information,
            Message = fetched
                ? "Checkout-free --fetch-missing acquisition populated the private EffortHours bare object cache with the exact provider-resolved commits; no checkout, user ref, FETCH_HEAD, index, or worktree was created or changed."
                : providerResolved
                    ? "Checkout-free --fetch-missing resolution refreshed the immutable provider identities and reused their objects from the private EffortHours bare cache; no checkout, user ref, FETCH_HEAD, index, or worktree was created or changed."
                : "Checkout-free Git analysis reused provider-resolved immutable commits from the private EffortHours bare cache without provider or network access.",
        };
        return plan with
        {
            Diagnostics = [.. plan.Diagnostics
                .Append(diagnostic)
                .Distinct()
                .OrderBy(value => value.Code, StringComparer.Ordinal)],
        };
    }

    private static (string Base, string Head) ParseRange(string range)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(range);
        int separator = range.IndexOf("..", StringComparison.Ordinal);
        if (separator <= 0 || separator != range.LastIndexOf("..", StringComparison.Ordinal) ||
            separator + 2 >= range.Length || range.Contains("...", StringComparison.Ordinal))
        {
            throw new ArgumentException("Range must use exactly one '<base>..<head>' separator.", nameof(range));
        }

        return (range[..separator], range[(separator + 2)..]);
    }

    private static DiscoveredHead[] CreatePinnedHeads(IReadOnlyList<string> objectIds)
    {
        ArgumentNullException.ThrowIfNull(objectIds);
        if (objectIds.Count is < 1 or > 32)
        {
            throw new ArgumentException(
                "Managed repository acquisition requires between 1 and 32 immutable objects.",
                nameof(objectIds));
        }

        return [.. objectIds.Select((objectId, index) =>
            new DiscoveredHead("manifest-head-" + index, objectId, objectId))];
    }

    private sealed record ManagedResolutionResult(
        string RepositoryPath,
        IReadOnlyList<ResolvedGitRevision> Revisions,
        bool Fetched);
}
