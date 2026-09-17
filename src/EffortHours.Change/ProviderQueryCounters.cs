namespace EffortHours.Change;

internal sealed partial class ProviderQueryCounters
{
    private readonly ChangePortfolioExecutionTelemetry? _telemetry;
    private int _queries;
    private int _pages;
    private int _openPullRequests;
    private int _processes;
    private long _processStartupTicks;

    public ProviderQueryCounters(ChangePortfolioExecutionTelemetry? telemetry = null)
    {
        _telemetry = telemetry;
    }

    public GitHubContributorIdentity? ContributorIdentity { get; set; }

    public void ObserveIdentity(string? login, GitCommitMetadata commit) =>
        ContributorIdentity?.Observe(login, commit);

    public int QueryCount => Volatile.Read(ref _queries);

    public int PageCount => Volatile.Read(ref _pages);

    public int OpenPullRequestCount => Volatile.Read(ref _openPullRequests);

    public int ProcessCount => Volatile.Read(ref _processes);

    public TimeSpan ProcessStartupElapsed =>
        TimeSpan.FromTicks(Volatile.Read(ref _processStartupTicks));

    public void AddQuery(string phase)
    {
        Interlocked.Increment(ref _queries);
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
