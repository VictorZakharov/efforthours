using System.Collections.Concurrent;
using System.Diagnostics;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed partial class ProviderQueryCounters
{
    private readonly ConcurrentDictionary<(string Repository, string Phase), ChangePortfolioProviderRepositoryObservation> _observations = new();
    private readonly ConcurrentDictionary<(string Repository, string Phase), (long Start, long End)> _observationRanges = new();
    private readonly Lock _observationGate = new();
    private ChangePortfolioProviderRequestObservation? _lastRequest;

    public RequestObservation ObserveRequest(IReadOnlyList<string> arguments, string phase)
    {
        string? endpoint = arguments.FirstOrDefault(argument => argument.StartsWith("repos/", StringComparison.Ordinal));
        string[]? parts = endpoint?.Split('/');
        string? identity = parts is { Length: >= 3 } ? parts[1] + "/" + parts[2] : null;
        string? query = arguments.FirstOrDefault(argument => argument.StartsWith("query=", StringComparison.Ordinal));
        if (query?.Contains("pullRequest(number:", StringComparison.Ordinal) == true)
        {
            string? owner = arguments.FirstOrDefault(argument => argument.StartsWith("owner0=", StringComparison.Ordinal));
            string? name = arguments.FirstOrDefault(argument => argument.StartsWith("name0=", StringComparison.Ordinal));
            if (owner is not null && name is not null) identity = owner[7..] + "/" + name[6..];
        }
        if (query?.Contains("repository(owner:$owner,name:$name)", StringComparison.Ordinal) == true)
        {
            string? owner = arguments.FirstOrDefault(argument => argument.StartsWith("owner=", StringComparison.Ordinal));
            string? name = arguments.FirstOrDefault(argument => argument.StartsWith("name=", StringComparison.Ordinal));
            if (owner is not null && name is not null) identity = owner[6..] + "/" + name[5..];
        }
        string operation = query?.Contains("pullRequest(number:", StringComparison.Ordinal) == true ? "pull-metadata-batch" :
            query?.Contains("pullRequests(", StringComparison.Ordinal) == true || endpoint?.Contains("pulls?", StringComparison.Ordinal) == true ? "pull-inventory" :
            endpoint?.Contains("/pulls/", StringComparison.Ordinal) == true ? endpoint.Contains("/commits", StringComparison.Ordinal) ? "pull-commits" : "pull-detail" :
            phase == GitHubProviderFailure.AuthenticationPhase ? "authentication" : phase == GitHubProviderFailure.OwnerInventoryPhase ? "owner-inventory" :
            phase == GitHubProviderFailure.DefaultHeadPhase ? "default-head" : "candidate-discovery";
        return new RequestObservation(this, identity is null ? null : ChangePortfolioComparisonIdentity.ComputeTextDigest(identity), phase, operation);
    }

    private ChangePortfolioProviderRequestObservation? LastRequest() { lock (_observationGate) return _lastRequest; }

    internal sealed class RequestObservation(ProviderQueryCounters counters, string? digest, string phase, string operation) : IDisposable
    {
        private readonly long _started = Stopwatch.GetTimestamp();
        private int _pages;
        private bool _complete;
        public void Complete(int pages) { _pages = pages; _complete = true; }
        public void Dispose()
        {
            long ended = Stopwatch.GetTimestamp();
            decimal elapsed = decimal.Round((decimal)Stopwatch.GetElapsedTime(_started, ended).TotalMilliseconds, 3);
            lock (counters._observationGate)
            {
                // Preserve an interrupted request over completed siblings drained during cancellation.
                if (counters._lastRequest?.State != "incomplete" || !_complete)
                    counters._lastRequest = new()
                    {
                        Phase = phase,
                        Operation = operation,
                        RepositoryDigest = digest,
                        State = _complete ? "complete" : "incomplete",
                        PageCount = _pages,
                        ElapsedMilliseconds = elapsed
                    };
                if (digest is null) return;
                var key = (digest, phase);
                (long Start, long End) = counters._observationRanges.AddOrUpdate(key, (_started, ended),
                    (_, previous) => (Math.Min(previous.Start, _started), Math.Max(previous.End, ended)));
                ChangePortfolioProviderRepositoryObservation value = new()
                {
                    RepositoryDigest = digest,
                    Phase = phase,
                    State = _complete ? "complete" : "incomplete",
                    QueryCount = 1,
                    PageCount = _pages,
                    ElapsedMilliseconds = elapsed,
                    ElapsedKind = "cumulative-request",
                    WallElapsedMilliseconds = decimal.Round((decimal)Stopwatch.GetElapsedTime(Start, End).TotalMilliseconds, 3),
                };
                counters._observations.AddOrUpdate(key, value, (_, previous) => value with
                {
                    State = previous.State == "incomplete" || value.State == "incomplete" ? "incomplete" : "complete",
                    QueryCount = previous.QueryCount + 1,
                    PageCount = previous.PageCount + value.PageCount,
                    ElapsedMilliseconds = previous.ElapsedMilliseconds + value.ElapsedMilliseconds,
                });
            }
        }
    }

    private IReadOnlyList<ChangePortfolioProviderRepositoryObservation>? RepositoryObservations() =>
        _observations.IsEmpty ? null : [.. _observations.Values.OrderBy(value => value.RepositoryDigest, StringComparer.Ordinal)
            .ThenBy(value => value.Phase, StringComparer.Ordinal).Take(768)];
}
