using System.Diagnostics;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record GitHubAuthorPeriodDiscoveryRequest
{
    public required string Owner { get; init; }

    public IReadOnlyList<string> AuthorAliases { get; init; } = [];

    public required DateTimeOffset AsOf { get; init; }

    public required string TimeZone { get; init; }

    public required string Scope { get; init; }

    public bool IncludeOpenPullRequests { get; init; }

    public ChangePortfolioDateField DateField { get; init; } = ChangePortfolioDateField.Author;

    public ChangePortfolioMergePolicy MergePolicy { get; init; } = ChangePortfolioMergePolicy.Exclude;

    public ChangePortfolioCoauthorPolicy CoauthorPolicy { get; init; } =
        ChangePortfolioCoauthorPolicy.Include;

    public ChangePortfolioExecutionTelemetry? ExecutionTelemetry { get; init; }

    public EngineeringScopeProfile? EngineeringScope { get; init; }
}

public sealed record GitHubAuthorPeriodDiscoveryResult
{
    public required ChangeAuthorPeriodManifest Manifest { get; init; }

    public required IReadOnlyDictionary<string, string> RepositoryPaths { get; init; }

    public required IReadOnlyDictionary<string, ChangePathAdmission> PathAdmissions { get; init; }

    public required ChangePortfolioHostDiscovery Discovery { get; init; }

    public required ChangePortfolioScopeProfile ScopeProfile { get; init; }

    public required DateTimeOffset AsOf { get; init; }
}

public sealed partial class GitHubAuthorPeriodDiscovery
{
    private readonly IExternalCommandRunner _commands;
    private readonly GitHubRepositoryCache _cache;

    public GitHubAuthorPeriodDiscovery()
        : this(new ExternalCommandRunner(), new GitHubRepositoryCache())
    {
    }

    internal GitHubAuthorPeriodDiscovery(
        IExternalCommandRunner commands,
        GitHubRepositoryCache cache)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<GitHubAuthorPeriodDiscoveryResult> DiscoverTodayAsync(
        GitHubAuthorPeriodDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        long started = Stopwatch.GetTimestamp();
        TimeZoneInfo zone = ResolveTimeZone(request.TimeZone);
        DateTimeOffset asOf = request.AsOf.ToUniversalTime();
        DateTimeOffset since = LocalDayStart(asOf, zone);
        EngineeringScopeProfile scope = request.EngineeringScope ?? EngineeringScopeProfile.Load();
        ProviderQueryCounters counters = new();
        string workingDirectory = Environment.CurrentDirectory;
        string ownerType;
        string authenticatedLogin;
        IReadOnlyList<GitHubDiscoveryRepository> providerRepositories;
        string[] aliases;
        DiscoveredRepository[] discovered;
        using (request.ExecutionTelemetry?.Measure(ChangePortfolioExecutionPhases.ProviderDiscovery))
        {
            ownerType = await GitHubAuthorPeriodDiscoveryJson.ResolveOwnerTypeAsync(
                _commands,
                workingDirectory,
                request.Owner,
                counters,
                cancellationToken).ConfigureAwait(false);
            authenticatedLogin = await GitHubAuthorPeriodDiscoveryJson.ResolveViewerAsync(
                _commands,
                workingDirectory,
                counters,
                cancellationToken).ConfigureAwait(false);
            providerRepositories = await GitHubAuthorPeriodDiscoveryJson.ListRepositoriesAsync(
                _commands,
                workingDirectory,
                request.Owner,
                ownerType,
                authenticatedLogin,
                counters,
                cancellationToken).ConfigureAwait(false);
            aliases = await ResolveAliasesAsync(
                request,
                workingDirectory,
                authenticatedLogin,
                counters,
                cancellationToken).ConfigureAwait(false);
            GitHubDiscoveryRepository[] considered = [.. providerRepositories.Where(repository =>
                repository.DefaultBranch is not null &&
                !scope.ExcludesRepository(repository.Identity, repository.Archived, repository.Mirror))];
            discovered = await DiscoverHeadsAsync(
                considered,
                aliases,
                authenticatedLogin,
                since,
                asOf,
                request,
                workingDirectory,
                counters,
                cancellationToken).ConfigureAwait(false);
            int headCount = discovered.Sum(repository => repository.Heads.Count);
            if (discovered.Length > ChangeAuthorPeriodManifestLimits.MaximumRepositories ||
                headCount > ChangeAuthorPeriodManifestLimits.MaximumHeads)
            {
                throw new InvalidOperationException(
                    $"GitHub discovery selected {discovered.Length} active repositories and " +
                    $"{headCount} heads; v1 supports at most " +
                    $"{ChangeAuthorPeriodManifestLimits.MaximumRepositories} repositories and " +
                    $"{ChangeAuthorPeriodManifestLimits.MaximumHeads} heads.");
            }
        }

        Dictionary<string, string> paths = new(StringComparer.Ordinal);
        Dictionary<string, ChangePathAdmission> admissions = new(StringComparer.Ordinal);
        int localHeads = 0;
        int acquiredObjects = 0;
        long acquiredBytes = 0;
        using (request.ExecutionTelemetry?.Measure(ChangePortfolioExecutionPhases.Acquisition))
        {
            foreach (DiscoveredRepository repository in discovered)
            {
                RepositoryAcquisitionResult acquisition = await _cache.EnsureAsync(
                    repository.RepositoryIdentity,
                    repository.Heads,
                    cancellationToken).ConfigureAwait(false);
                paths.Add(repository.RepositoryId, acquisition.RepositoryPath);
                admissions.Add(
                    repository.RepositoryId,
                    scope.CreateAdmission(repository.RepositoryIdentity));
                localHeads += acquisition.LocalHeadCount;
                acquiredObjects += acquisition.AcquiredObjectCount;
                acquiredBytes += acquisition.AcquiredBytes;
            }
        }

        ChangeAuthorPeriodManifest manifest = CreateManifest(
            discovered,
            paths,
            aliases,
            since,
            asOf,
            zone,
            request);
        int openHeads = discovered.Sum(repository =>
            repository.Heads.Count(head => head.Id != "default"));
        int defaultHeads = discovered.Sum(repository =>
            repository.Heads.Count(head => head.Id == "default"));
        ChangePortfolioHostDiscovery summary = new()
        {
            ScopeDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(
                ChangeAuthorPeriodManifestIdentity.ComputeDigest(manifest) + "\n" + scope.Contract.Digest),
            IdentitySources = IdentitySources(request.AuthorAliases),
            Complete = true,
            ProviderRepositoryCount = providerRepositories.Count,
            ConsideredRepositoryCount = providerRepositories.Count(repository =>
                repository.DefaultBranch is not null &&
                !scope.ExcludesRepository(repository.Identity, repository.Archived, repository.Mirror)),
            ActiveRepositoryCount = discovered.Length,
            DefaultHeadCount = defaultHeads,
            OpenPullRequestHeadCount = openHeads,
            OpenPullRequestCount = counters.OpenPullRequestCount,
            ProviderQueryCount = counters.QueryCount,
            ProviderPageCount = counters.PageCount,
            LocalObjectCount = localHeads,
            AcquiredObjectCount = acquiredObjects,
            AcquiredBytes = acquiredBytes,
            ElapsedMilliseconds = decimal.Round(
                (decimal)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                3,
                MidpointRounding.AwayFromZero),
        };
        return new GitHubAuthorPeriodDiscoveryResult
        {
            Manifest = manifest,
            RepositoryPaths = paths,
            PathAdmissions = admissions,
            Discovery = summary,
            ScopeProfile = scope.Contract,
            AsOf = asOf,
        };
    }

    private async Task<DiscoveredRepository[]> DiscoverHeadsAsync(
        IReadOnlyList<GitHubDiscoveryRepository> repositories,
        IReadOnlyList<string> aliases,
        string authenticatedLogin,
        DateTimeOffset since,
        DateTimeOffset until,
        GitHubAuthorPeriodDiscoveryRequest request,
        string workingDirectory,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
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
                    request.IncludeOpenPullRequests,
                    counters,
                    cancellationToken).ConfigureAwait(false);
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

    private static ChangeAuthorPeriodManifest CreateManifest(
        IReadOnlyList<DiscoveredRepository> repositories,
        Dictionary<string, string> paths,
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
                    RepositoryPath = paths[repository.RepositoryId],
                    Heads = [.. repository.Heads.Select(head => new ChangeAuthorPeriodManifestHead
                    {
                        Id = head.Id,
                        ObjectId = head.ObjectId,
                    })],
                })],
        };
}

internal sealed record DiscoveredRepository(
    string RepositoryId,
    string RepositoryIdentity,
    IReadOnlyList<DiscoveredHead> Heads,
    int OpenPullRequestCount);

internal sealed record DiscoveredHead(string Id, string ObjectId, string FetchRef);

internal sealed class ProviderQueryCounters
{
    private int _queries;
    private int _pages;
    private int _openPullRequests;

    public int QueryCount => Volatile.Read(ref _queries);

    public int PageCount => Volatile.Read(ref _pages);

    public int OpenPullRequestCount => Volatile.Read(ref _openPullRequests);

    public void AddQuery() => Interlocked.Increment(ref _queries);

    public void AddPages(int count) => Interlocked.Add(ref _pages, count);

    public void AddOpenPullRequests(int count) => Interlocked.Add(ref _openPullRequests, count);
}
