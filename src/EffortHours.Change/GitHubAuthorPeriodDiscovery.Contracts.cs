using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed record DiscoveredRepository(
    string RepositoryId,
    string RepositoryIdentity,
    IReadOnlyList<DiscoveredHead> Heads,
    int OpenPullRequestCount);

internal sealed record DiscoveredHead(string Id, string ObjectId, string FetchRef);

internal sealed record ResolvedDiscoveryContributor(
    string Id,
    string Login,
    IReadOnlyList<string> Aliases);

internal sealed record ResolvedDiscoveryContributors(
    IReadOnlyList<ResolvedDiscoveryContributor> Contributors,
    ChangePortfolioContributorSelection Selection,
    IReadOnlyList<string> VerifiedEmails);

internal sealed record GitHubActiveContributor(
    string Login,
    IReadOnlyList<string> Aliases);

internal sealed class ProviderQueryCounters
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

    public int QueryCount => Volatile.Read(ref _queries);

    public int PageCount => Volatile.Read(ref _pages);

    public int OpenPullRequestCount => Volatile.Read(ref _openPullRequests);

    public int ProcessCount => Volatile.Read(ref _processes);

    public TimeSpan ProcessStartupElapsed =>
        TimeSpan.FromTicks(Volatile.Read(ref _processStartupTicks));

    public void AddQuery() => Interlocked.Increment(ref _queries);

    public void AddPages(int count) => Interlocked.Add(ref _pages, count);

    public void AddOpenPullRequests(int count) => Interlocked.Add(ref _openPullRequests, count);

    public void AddProcess(TimeSpan startupElapsed)
    {
        Interlocked.Increment(ref _processes);
        Interlocked.Add(ref _processStartupTicks, startupElapsed.Ticks);
        _telemetry?.Add(ChangePortfolioExecutionPhases.ProviderProcessStartup, startupElapsed);
    }
}
