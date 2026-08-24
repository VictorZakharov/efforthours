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
    private readonly GitHubProviderMetadataCache _metadataCache;

    public GitHubAuthorPeriodDiscovery()
        : this(
            new ExternalCommandRunner(),
            new GitHubRepositoryCache(),
            new GitHubProviderMetadataCache())
    {
    }

    internal GitHubAuthorPeriodDiscovery(
        IExternalCommandRunner commands,
        GitHubRepositoryCache cache,
        GitHubProviderMetadataCache? metadataCache = null)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _metadataCache = metadataCache ?? new GitHubProviderMetadataCache();
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
        ProviderQueryCounters counters = new(request.ExecutionTelemetry);
        string workingDirectory = Environment.CurrentDirectory;
        string ownerType;
        string authenticatedLogin;
        IReadOnlyList<GitHubDiscoveryRepository> providerRepositories;
        string[] aliases;
        ResolvedAliases resolvedAliases;
        DiscoveredRepository[] discovered;
        GitHubProviderMetadata? cachedMetadata = null;
        DateTimeOffset cacheObservedAt = DateTimeOffset.UtcNow;
        using (request.ExecutionTelemetry?.Measure(ChangePortfolioExecutionPhases.ProviderDiscovery))
        {
            using (request.ExecutionTelemetry?.Measure(
                ChangePortfolioExecutionPhases.ProviderAuthentication))
            {
                authenticatedLogin = await GitHubAuthorPeriodDiscoveryJson.ResolveViewerAsync(
                    _commands,
                    workingDirectory,
                    counters,
                    cancellationToken).ConfigureAwait(false);
                cachedMetadata = await _metadataCache.ReadAsync(
                    request.Owner,
                    authenticatedLogin,
                    cacheObservedAt,
                    cancellationToken).ConfigureAwait(false);
                resolvedAliases = await ResolveAliasesAsync(
                    request,
                    workingDirectory,
                    authenticatedLogin,
                    cachedMetadata?.VerifiedEmails,
                    counters,
                    cancellationToken).ConfigureAwait(false);
                aliases = resolvedAliases.Values;
            }

            using (request.ExecutionTelemetry?.Measure(ChangePortfolioExecutionPhases.OwnerInventory))
            {
                ownerType = cachedMetadata?.OwnerType ??
                    await GitHubAuthorPeriodDiscoveryJson.ResolveOwnerTypeAsync(
                        _commands,
                        workingDirectory,
                        request.Owner,
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
                if (request.AuthorAliases.Any(alias =>
                    alias.Equals("@me", StringComparison.OrdinalIgnoreCase)))
                {
                    await _metadataCache.WriteAsync(
                        request.Owner,
                        authenticatedLogin,
                        ownerType,
                        resolvedAliases.VerifiedEmails,
                        providerRepositories,
                        cacheObservedAt,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            GitHubDiscoveryRepository[] considered;
            using (request.ExecutionTelemetry?.Measure(ChangePortfolioExecutionPhases.CandidateDiscovery))
            {
                considered = [.. providerRepositories.Where(repository =>
                    repository.DefaultBranch is not null &&
                    !scope.ExcludesRepository(repository.Identity, repository.Archived, repository.Mirror))];
            }

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
            ProviderProcessCount = counters.ProcessCount,
            ProviderProcessStartupMilliseconds = decimal.Round(
                (decimal)counters.ProcessStartupElapsed.TotalMilliseconds,
                3,
                MidpointRounding.AwayFromZero),
            ProviderMetadataCacheHit = cachedMetadata is not null,
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
        DiscoveredRepository[] defaults;
        using (request.ExecutionTelemetry?.Measure(ChangePortfolioExecutionPhases.DefaultHeadDiscovery))
        {
            IReadOnlyList<DiscoveredRepository>? batched =
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
            defaults = batched is not null
                ? [.. batched]
                : await DiscoverHeadPhaseAsync(
                    repositories,
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
        }

        if (!request.IncludeOpenPullRequests)
        {
            return defaults;
        }

        DiscoveredRepository[] openPullRequests;
        using (request.ExecutionTelemetry?.Measure(ChangePortfolioExecutionPhases.OpenPullRequestDiscovery))
        {
            bool viewerOnly = request.AuthorAliases.Count == 1 &&
                request.AuthorAliases[0].Equals("@me", StringComparison.OrdinalIgnoreCase);
            IReadOnlyList<DiscoveredRepository>? accountWide = viewerOnly
                ? await GitHubAuthorPeriodDiscoveryJson
                    .DiscoverViewerOpenPullHeadsAccountWideAsync(
                        _commands,
                        workingDirectory,
                        repositories,
                        authenticatedLogin,
                        aliases,
                        since,
                        until,
                        request.DateField,
                        request.MergePolicy,
                        request.CoauthorPolicy,
                        counters,
                        cancellationToken).ConfigureAwait(false)
                : null;
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
                    cancellationToken).ConfigureAwait(false);
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
                    includeOpenPullRequests,
                    counters,
                    cancellationToken,
                    includeDefaultHead).ConfigureAwait(false);
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
