using System.Diagnostics;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record GitHubAuthorPeriodDiscoveryRequest
{
    public required string Owner { get; init; }

    public required string WorkspacePath { get; init; }

    public IReadOnlyList<string> AuthorAliases { get; init; } = [];

    public required DateTimeOffset AsOf { get; init; }

    public required string TimeZone { get; init; }

    public bool IncludeOpenPullRequests { get; init; }

    public bool FetchMissing { get; init; }

    public ChangePortfolioDateField DateField { get; init; } = ChangePortfolioDateField.Author;

    public ChangePortfolioMergePolicy MergePolicy { get; init; } = ChangePortfolioMergePolicy.Exclude;

    public ChangePortfolioCoauthorPolicy CoauthorPolicy { get; init; } =
        ChangePortfolioCoauthorPolicy.Include;
}

public sealed record GitHubAuthorPeriodDiscoveryResult
{
    public required ChangeAuthorPeriodManifest Manifest { get; init; }

    public required IReadOnlyDictionary<string, string> RepositoryPaths { get; init; }

    public required ChangePortfolioHostDiscovery Discovery { get; init; }

    public required DateTimeOffset AsOf { get; init; }
}

public sealed partial class GitHubAuthorPeriodDiscovery
{
    private readonly IExternalCommandRunner _commands;
    private readonly GitClient _git;
    private readonly GitPortfolioPlanner _planner;

    public GitHubAuthorPeriodDiscovery()
        : this(new ExternalCommandRunner(), new GitClient(), new GitPortfolioPlanner())
    {
    }

    internal GitHubAuthorPeriodDiscovery(
        IExternalCommandRunner commands,
        GitClient git,
        GitPortfolioPlanner planner)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _git = git ?? throw new ArgumentNullException(nameof(git));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
    }

    public async Task<GitHubAuthorPeriodDiscoveryResult> DiscoverTodayAsync(
        GitHubAuthorPeriodDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        long started = Stopwatch.GetTimestamp();
        string workspace = Path.GetFullPath(request.WorkspacePath);
        TimeZoneInfo zone = ResolveTimeZone(request.TimeZone);
        DateTimeOffset asOf = request.AsOf.ToUniversalTime();
        (DateTimeOffset since, DateTimeOffset until) = LocalDay(asOf, zone);
        ProviderQueryCounters counters = new();
        string ownerType = await GitHubAuthorPeriodDiscoveryJson.ResolveOwnerTypeAsync(
            _commands,
            workspace,
            request.Owner,
            counters,
            cancellationToken).ConfigureAwait(false);
        string authenticatedLogin = await GitHubAuthorPeriodDiscoveryJson.ResolveViewerAsync(
            _commands,
            workspace,
            counters,
            cancellationToken).ConfigureAwait(false);
        IReadOnlyList<GitHubDiscoveryRepository> providerRepositories =
            await GitHubAuthorPeriodDiscoveryJson.ListRepositoriesAsync(
                _commands,
                workspace,
                request.Owner,
                ownerType,
                authenticatedLogin,
                counters,
                cancellationToken).ConfigureAwait(false);
        IReadOnlyList<WorkspaceGitHubRepository> workspaceRepositories =
            await WorkspaceGitHubRepositoryCatalog.DiscoverAsync(
                workspace,
                _commands,
                cancellationToken).ConfigureAwait(false);
        IReadOnlyList<MappedRepository> mapped = MapRepositories(
            providerRepositories,
            workspaceRepositories);
        if (mapped.Count == 0)
        {
            throw new InvalidOperationException(
                "No readable GitHub checkout under --workspace maps by remote identity to the requested owner.");
        }

        if (mapped.Count > ChangeAuthorPeriodManifestLimits.MaximumRepositories)
        {
            throw new InvalidOperationException(
                $"The owner/workspace intersection contains {mapped.Count} repositories; v1 supports at most " +
                $"{ChangeAuthorPeriodManifestLimits.MaximumRepositories}. Narrow --workspace and retry.");
        }

        string[] aliases = await ResolveAliasesAsync(
            request,
            workspace,
            mapped,
            authenticatedLogin,
            counters,
            cancellationToken).ConfigureAwait(false);
        DiscoveredRepository[] discovered = await DiscoverHeadsAsync(
            mapped,
            aliases,
            since,
            until,
            request,
            request.IncludeOpenPullRequests,
            workspace,
            counters,
            cancellationToken).ConfigureAwait(false);
        (int localObjects, int acquiredObjects) = await EnsureObjectsAsync(
            discovered,
            request.FetchMissing,
            cancellationToken).ConfigureAwait(false);
        ChangeAuthorPeriodManifest broad = CreateManifest(
            discovered,
            aliases,
            since,
            until,
            zone,
            request);
        Dictionary<string, string> broadPaths = discovered.ToDictionary(
            value => value.RepositoryId,
            value => value.LocalPath,
            StringComparer.Ordinal);
        GitAuthorPeriodManifestPortfolioPlan measured =
            await _planner.PlanAuthorPeriodManifestAsync(
                broad,
                ChangeAuthorPeriodManifestIdentity.ComputeDigest(broad),
                broadPaths,
                new ChangePortfolioExecutionTelemetry(),
                allowEmptySelection: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        ChangeAuthorPeriodManifest narrowed = NarrowToActiveHeads(broad, measured.Items);
        IReadOnlyDictionary<string, string> paths = narrowed.Repositories.ToDictionary(
            repository => repository.Id,
            repository => broadPaths[repository.Id],
            StringComparer.Ordinal);
        int openHeads = narrowed.Repositories.Sum(repository =>
            repository.Heads.Count(head => head.Id != "default"));
        ChangePortfolioHostDiscovery summary = new()
        {
            ScopeDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(
                ChangeAuthorPeriodManifestIdentity.ComputeDigest(broad)),
            IdentitySources = IdentitySources(request.AuthorAliases),
            Complete = true,
            ProviderRepositoryCount = providerRepositories.Count,
            WorkspaceRepositoryCount = workspaceRepositories.Count,
            ConsideredRepositoryCount = mapped.Count,
            ActiveRepositoryCount = measured.Items.Count == 0 ? 0 : narrowed.Repositories.Count,
            DefaultHeadCount = measured.Items.Count == 0 ? 0 : narrowed.Repositories.Count,
            OpenPullRequestHeadCount = openHeads,
            ProviderQueryCount = counters.QueryCount,
            ProviderPageCount = counters.PageCount,
            LocalObjectCount = localObjects,
            AcquiredObjectCount = acquiredObjects,
            ElapsedMilliseconds = decimal.Round(
                (decimal)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                3,
                MidpointRounding.AwayFromZero),
        };
        return new GitHubAuthorPeriodDiscoveryResult
        {
            Manifest = narrowed,
            RepositoryPaths = paths,
            Discovery = summary,
            AsOf = asOf,
        };
    }

    private async Task<DiscoveredRepository[]> DiscoverHeadsAsync(
        IReadOnlyList<MappedRepository> mapped,
        IReadOnlyList<string> aliases,
        DateTimeOffset since,
        DateTimeOffset until,
        GitHubAuthorPeriodDiscoveryRequest request,
        bool includeOpenPullRequests,
        string workspace,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        using SemaphoreSlim gate = new(4, 4);
        Task<DiscoveredRepository>[] tasks = [.. mapped.Select(async repository =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await GitHubAuthorPeriodDiscoveryJson.DiscoverHeadsAsync(
                    _commands,
                    workspace,
                    repository,
                    aliases,
                    since,
                    until,
                    request.DateField,
                    request.MergePolicy,
                    request.CoauthorPolicy,
                    includeOpenPullRequests,
                    counters,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        })];
        return [.. (await Task.WhenAll(tasks).ConfigureAwait(false))
            .OrderBy(value => value.RepositoryId, StringComparer.Ordinal)];
    }

    private async Task<(int Local, int Acquired)> EnsureObjectsAsync(
        IReadOnlyList<DiscoveredRepository> repositories,
        bool fetchMissing,
        CancellationToken cancellationToken)
    {
        int local = 0;
        int acquired = 0;
        foreach (DiscoveredRepository repository in repositories)
        {
            List<DiscoveredHead> missing = [];
            foreach (DiscoveredHead head in repository.Heads)
            {
                if (await _git.CommitExistsAsync(
                    repository.LocalPath,
                    head.ObjectId,
                    cancellationToken).ConfigureAwait(false))
                {
                    local++;
                }
                else
                {
                    missing.Add(head);
                }
            }

            if (missing.Count == 0)
            {
                continue;
            }

            if (!fetchMissing)
            {
                throw new InvalidOperationException(
                    $"Repository '{repository.RepositoryId}' is missing {missing.Count} discovered immutable " +
                    "head object(s). Retry with --fetch-missing for narrow no-ref acquisition.");
            }

            await _git.FetchAuthorPeriodHeadObjectsAsync(
                repository.LocalPath,
                repository.FetchSource,
                [.. missing.Select(head => head.FetchRef)],
                cancellationToken).ConfigureAwait(false);
            foreach (DiscoveredHead head in missing)
            {
                if (!await _git.CommitExistsAsync(
                    repository.LocalPath,
                    head.ObjectId,
                    cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException(
                        $"Repository '{repository.RepositoryId}' still lacks a discovered immutable head " +
                        "after narrow acquisition; the provider ref may have moved during discovery.");
                }

                acquired++;
            }
        }

        return (local, acquired);
    }

    private static ChangeAuthorPeriodManifest CreateManifest(
        IReadOnlyList<DiscoveredRepository> repositories,
        IReadOnlyList<string> aliases,
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
            Contributors =
            [
                new ChangeAuthorPeriodManifestContributor
                {
                    Id = "me",
                    Aliases = aliases,
                },
            ],
            Repositories = [.. repositories.Select(repository =>
                new ChangeAuthorPeriodManifestRepository
                {
                    Id = repository.RepositoryId,
                    RepositoryPath = repository.LocalPath,
                    Heads = [.. repository.Heads.Select(head => new ChangeAuthorPeriodManifestHead
                    {
                        Id = head.Id,
                        ObjectId = head.ObjectId,
                    })],
                })],
        };

    private static ChangeAuthorPeriodManifest NarrowToActiveHeads(
        ChangeAuthorPeriodManifest broad,
        IReadOnlyList<GitAuthorPeriodManifestPortfolioItem> items)
    {
        if (items.Count == 0)
        {
            return broad with
            {
                Repositories = [.. broad.Repositories.Select(repository => repository with
                {
                    Heads = [repository.Heads.Single(head => head.Id == "default")],
                })],
            };
        }

        HashSet<string> activeRepositories = items
            .Select(item => item.RepositoryId)
            .ToHashSet(StringComparer.Ordinal);
        return broad with
        {
            Repositories = [.. broad.Repositories
                .Where(repository => activeRepositories.Contains(repository.Id))
                .Select(repository => repository with
                {
                    Heads = [.. repository.Heads.Where(head =>
                        head.Id == "default" || items.Any(item =>
                            item.RepositoryId == repository.Id &&
                            item.Attribution.HeadIds?.Contains(head.Id, StringComparer.Ordinal) == true &&
                            item.Attribution.HeadIds.Contains("default", StringComparer.Ordinal) == false))],
                })],
        };
    }

    private static IReadOnlyList<MappedRepository> MapRepositories(
        IReadOnlyList<GitHubDiscoveryRepository> provider,
        IReadOnlyList<WorkspaceGitHubRepository> workspace)
    {
        Dictionary<string, WorkspaceGitHubRepository> localByIdentity =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (WorkspaceGitHubRepository local in workspace)
        {
            foreach (string identity in local.RepositoryIdentities)
            {
                if (localByIdentity.TryGetValue(identity, out WorkspaceGitHubRepository? prior) &&
                    !string.Equals(prior.RootPath, local.RootPath, PathComparison))
                {
                    throw new InvalidOperationException(
                        "Two workspace checkouts map to the same GitHub repository identity.");
                }

                localByIdentity[identity] = local;
            }
        }

        return [.. provider
            .Where(repository => repository.DefaultBranch is not null &&
                localByIdentity.ContainsKey(repository.Identity))
            .Select(repository => new MappedRepository(
                repository,
                localByIdentity[repository.Identity].RootPath,
                localByIdentity[repository.Identity]))
            .OrderBy(value => value.Provider.StableId, StringComparer.Ordinal)];
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}

internal sealed record MappedRepository(
    GitHubDiscoveryRepository Provider,
    string LocalPath,
    WorkspaceGitHubRepository Local);

internal sealed record DiscoveredRepository(
    string RepositoryId,
    string LocalPath,
    string FetchSource,
    IReadOnlyList<DiscoveredHead> Heads);

internal sealed record DiscoveredHead(string Id, string ObjectId, string FetchRef);

internal sealed class ProviderQueryCounters
{
    private int _queries;
    private int _pages;

    public int QueryCount => Volatile.Read(ref _queries);

    public int PageCount => Volatile.Read(ref _pages);

    public void AddQuery() => Interlocked.Increment(ref _queries);

    public void AddPages(int count) => Interlocked.Add(ref _pages, count);
}
