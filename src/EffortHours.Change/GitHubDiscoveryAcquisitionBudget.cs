using System.Diagnostics;
using System.Globalization;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record GitHubAcquisitionProgress(string RepositoryId, string Reason, string State, int HeadCount, long AcquiredBytes, decimal ElapsedMilliseconds)
{
    public string SafeMessage() => $"eh: acquisition repository={RepositoryId}; reason={Reason}; state={State}; heads={HeadCount}; observed-object-store-growth={AcquiredBytes.ToString(CultureInfo.InvariantCulture)} bytes; elapsed={ElapsedMilliseconds.ToString("F3", CultureInfo.InvariantCulture)} ms";
}

internal sealed class GitHubDiscoveryAcquisitionBudget(
    GitHubAuthorPeriodDiscoveryRequest request, CancellationTokenSource deadline, CancellationToken callerToken)
{
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly Lock _gate = new();
    private readonly Dictionary<string, long> _growth = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _repositories = new(StringComparer.OrdinalIgnoreCase);
    private int _cacheHits;
    private int _objects;
    private bool _bytesExceeded;

    public ChangePortfolioAcquisitionSummary Summary()
    {
        lock (_gate) return new()
        {
            MaximumBytes = request.MaximumAcquiredBytes,
            TimeoutSeconds = request.DiscoveryTimeoutSeconds,
            RepositoryCount = _repositories.Count,
            CacheHitHeadCount = _cacheHits,
            AcquiredObjectCount = _objects,
            AcquiredBytes = _growth.Values.Sum(),
        };
    }

    public ProviderQueryCounters? Counters { get; set; }
    private string? _acquisitionFailurePhase;
    private Exception? _rootFailure;

    public Exception Failure(Exception exception)
    {
        lock (_gate)
        {
            if (callerToken.IsCancellationRequested) return exception;
            if (_rootFailure is not null) return _rootFailure;
            return exception is OperationCanceledException ? BudgetFailure() : exception;
        }
    }

    public void Stop(Exception exception)
    {
        lock (_gate)
        {
            if (exception is not OperationCanceledException) _rootFailure ??= exception;
        }
        deadline.Cancel();
    }

    private GitHubProviderException BudgetFailure()
    {
        string phase = _acquisitionFailurePhase ?? Counters?.LastPhase ?? GitHubProviderFailure.CandidateDiscoveryPhase;
        bool singleAcquisition = request.Repositories.Count == 1 && phase == GitHubProviderFailure.ManagedCachePhase;
        string message = _bytesExceeded
            ? singleAcquisition
                ? "Native acquisition exceeded its observed object-store growth budget for the restricted repository. Inspect acquisition, explicitly increase --max-acquired-mib, or use a complete pinned manifest; no aggregate was published."
                : "Native acquisition exceeded its observed object-store growth budget. Narrow --repository or explicitly increase --max-acquired-mib; no aggregate was published."
            : "Native discovery/acquisition exceeded its deadline; inspect the recorded request plan and active subphase. Completed immutable cache objects remain reusable, and no aggregate was published.";
        string suggestion = singleAcquisition ? "inspect-acquisition-or-use-pinned-manifest"
            : !_bytesExceeded && request.Repositories.Count == 1 && phase == GitHubProviderFailure.OpenPullRequestPhase
                ? "inspect-pr-discovery-or-use-pinned-manifest" : "narrow-scope-or-use-pinned-manifest";
        return GitHubProviderFailure.DiscoveryBudget(phase, message, suggestion);
    }

    public async Task<RepositoryAcquisitionResult> EnsureAsync(GitHubRepositoryCache cache, string identity,
        IReadOnlyList<DiscoveredHead> heads, string repositoryId, string reason, CancellationToken token)
    {
        long prior;
        lock (_gate)
        {
            _repositories.Add(identity);
            prior = _growth.GetValueOrDefault(identity);
        }
        Notify("started");
        try
        {
            RepositoryAcquisitionResult result = await cache.EnsureAsync(identity, heads, true, token,
                growth => { RecordGrowth(growth); Notify("running"); }).ConfigureAwait(false);
            lock (_gate) { _cacheHits += result.LocalHeadCount; _objects += result.AcquiredObjectCount; }
            RecordGrowth(result.AcquiredBytes);
            Notify(result.AcquiredHeadCount == 0 ? "cache-reuse" : "completed");
            return result;
        }
        catch (Exception exception)
        {
            _acquisitionFailurePhase = GitHubProviderFailure.ManagedCachePhase;
            Stop(exception);
            throw;
        }

        void RecordGrowth(long growth)
        {
            bool exceeded;
            lock (_gate)
            {
                _growth[identity] = Math.Max(_growth.GetValueOrDefault(identity), checked(prior + growth));
                exceeded = _growth.Values.Sum() > request.MaximumAcquiredBytes;
                _bytesExceeded |= exceeded;
            }
            if (exceeded)
            {
                callerToken.ThrowIfCancellationRequested();
                _acquisitionFailurePhase = GitHubProviderFailure.ManagedCachePhase;
                throw BudgetFailure();
            }
        }
        void Notify(string state) => request.AcquisitionProgress?.Invoke(new(repositoryId, reason, state, heads.Count, Summary().AcquiredBytes, (decimal)Stopwatch.GetElapsedTime(_started).TotalMilliseconds));
    }
}
