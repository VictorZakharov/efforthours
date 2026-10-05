using System.Collections.Concurrent;
using System.Diagnostics;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed partial class ProviderQueryCounters
{
    private readonly ConcurrentDictionary<(string Repository, string Phase), ChangePortfolioProviderRepositoryObservation> _observations = new();
    public RequestObservation ObserveRequest(IReadOnlyList<string> arguments, string phase)
    {
        string? endpoint = arguments.FirstOrDefault(argument => argument.StartsWith("repos/", StringComparison.Ordinal));
        string[]? parts = endpoint?.Split('/');
        string? digest = parts is { Length: >= 3 } ?
            ChangePortfolioComparisonIdentity.ComputeTextDigest(parts[1] + "/" + parts[2]) : null;
        return new RequestObservation(this, digest, phase);
    }

    internal sealed class RequestObservation(ProviderQueryCounters counters, string? digest, string phase) : IDisposable
    {
        private readonly long _started = Stopwatch.GetTimestamp();
        private int _pages;
        private bool _complete;
        public void Complete(int pages) { _pages = pages; _complete = true; }
        public void Dispose()
        {
            if (digest is null) return;
            ChangePortfolioProviderRepositoryObservation value = new()
            {
                RepositoryDigest = digest,
                Phase = phase,
                State = _complete ? "complete" : "incomplete",
                QueryCount = 1,
                PageCount = _pages,
                ElapsedMilliseconds = decimal.Round((decimal)Stopwatch.GetElapsedTime(_started).TotalMilliseconds, 3),
            };
            counters._observations.AddOrUpdate((digest, phase), value, (_, previous) => value with
            {
                State = previous.State == "incomplete" || value.State == "incomplete" ? "incomplete" : "complete",
                QueryCount = previous.QueryCount + 1,
                PageCount = previous.PageCount + value.PageCount,
                ElapsedMilliseconds = previous.ElapsedMilliseconds + value.ElapsedMilliseconds,
            });
        }
    }

    private IReadOnlyList<ChangePortfolioProviderRepositoryObservation>? RepositoryObservations() =>
        _observations.IsEmpty ? null : [.. _observations.Values
            .OrderBy(value => value.RepositoryDigest, StringComparer.Ordinal)
            .ThenBy(value => value.Phase, StringComparer.Ordinal).Take(768)];
}
