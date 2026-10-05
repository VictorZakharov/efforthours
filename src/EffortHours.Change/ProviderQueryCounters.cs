namespace EffortHours.Change;

internal sealed partial class ProviderQueryCounters
{
    internal const int MaximumQueries = 2048;
    private readonly ChangePortfolioExecutionTelemetry? _telemetry;
    private int _queries;
    private int _pages;
    private int _openPullRequests;
    private int _historicalPullRequests;
    private int _processes;
    private long _processStartupTicks;

    public ProviderQueryCounters(ChangePortfolioExecutionTelemetry? telemetry = null)
    {
        _telemetry = telemetry;
    }

    public GitHubContributorIdentity? ContributorIdentity { get; set; }

    public GitHubPullAuthorIdentity? PullAuthorIdentity { get; set; }

    public void ObserveIdentity(string? login, GitCommitMetadata commit)
    {
        ContributorIdentity?.Observe(login, commit);
        PullAuthorIdentity?.Observe(login, commit);
    }

    public int QueryCount => Volatile.Read(ref _queries);

    public int PageCount => Volatile.Read(ref _pages);

    public int OpenPullRequestCount => Volatile.Read(ref _openPullRequests);

    public int HistoricalPullRequestCount => Volatile.Read(ref _historicalPullRequests);

    public void AddHistoricalPullRequests(int count) => Interlocked.Add(ref _historicalPullRequests, count);

    public int ProcessCount => Volatile.Read(ref _processes);

    public TimeSpan ProcessStartupElapsed =>
        TimeSpan.FromTicks(Volatile.Read(ref _processStartupTicks));

    public void AddQuery(string phase)
    {
        int count;
        do
        {
            count = Volatile.Read(ref _queries);
            if (count >= MaximumQueries)
                throw GitHubProviderFailure.DiscoveryBudget(phase,
                    "Provider discovery reached its 2,048-request safety bound; narrow engineering scope or use a pinned offline manifest. No partial selection is a zero result.");
        } while (Interlocked.CompareExchange(ref _queries, count + 1, count) != count);
        if (phase == GitHubProviderFailure.DefaultHeadPhase)
        {
            Interlocked.Increment(ref _defaultQueries);
        }
        else if (phase == GitHubProviderFailure.OpenPullRequestPhase)
        {
            Interlocked.Increment(ref _pullQueries);
        }
    }

    public void AddPages(int count) => Interlocked.Add(ref _pages, count);

    public void AddOpenPullRequests(int count) => Interlocked.Add(ref _openPullRequests, count);

    public void AddProcess(TimeSpan startupElapsed)
    {
        Interlocked.Increment(ref _processes);
        Interlocked.Add(ref _processStartupTicks, startupElapsed.Ticks);
        _telemetry?.Add(ChangePortfolioExecutionPhases.ProviderProcessStartup, startupElapsed);
    }
}
