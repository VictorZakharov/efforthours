using System.Collections.Concurrent;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed partial class ProviderQueryCounters
{
    private readonly ConcurrentDictionary<(string Phase, string Reason), int> _fallbacks = new();
    private int _defaultBatches;
    private int _defaultQueries;
    private int _accountQueries;
    private int _pullQueries;

    public void AddDefaultBatch() => Interlocked.Increment(ref _defaultBatches);

    public void AddAccountQuery() => Interlocked.Increment(ref _accountQueries);

    public void AddFallback(string phase, string reason, int repositories) =>
        _fallbacks.AddOrUpdate((phase, reason), repositories, (_, count) => count + repositories);

    public ChangePortfolioProviderDiagnostics Diagnostics(string cacheStatus) => new()
    {
        MetadataCacheStatus = cacheStatus,
        DefaultHeadBatchCount = Volatile.Read(ref _defaultBatches),
        DefaultHeadQueryCount = Volatile.Read(ref _defaultQueries),
        OpenPullRequestAccountQueryCount = Volatile.Read(ref _accountQueries),
        OpenPullRequestQueryCount = Volatile.Read(ref _pullQueries),
        Fallbacks = [.. _fallbacks
            .OrderBy(pair => pair.Key.Phase, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Reason, StringComparer.Ordinal)
            .Select(pair => new ChangePortfolioProviderFallback
            {
                Phase = pair.Key.Phase,
                Reason = pair.Key.Reason,
                RepositoryCount = pair.Value,
            })],
    };
}
