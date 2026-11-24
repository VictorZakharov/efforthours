namespace EffortHours.Analysis;

/// <summary>Single-flight in-progress requests for <see cref="RepositoryAnalysisArtifactCache"/>.</summary>
public sealed partial class RepositoryAnalysisArtifactCache
{
    private void CompleteInflight<T>(
        string key,
        InflightEntry operation,
        T value)
        where T : class
    {
        lock (_gate)
        {
            if (_inflight.TryGetValue(key, out InflightEntry? current) &&
                ReferenceEquals(current, operation))
            {
                _inflight.Remove(key);
            }
        }

        operation.Completion.TrySetResult(value);
    }

    private void FailInflight(
        string key,
        InflightEntry operation,
        Exception exception)
    {
        lock (_gate)
        {
            if (_inflight.TryGetValue(key, out InflightEntry? current) &&
                ReferenceEquals(current, operation))
            {
                _inflight.Remove(key);
            }
        }

        operation.Completion.TrySetException(exception);
        _ = operation.Completion.Task.Exception;
    }

    internal sealed record InflightEntry(
        Type ValueType,
        TaskCompletionSource<object> Completion,
        string? WorkingSetSlot);

    public sealed class RepositoryAnalysisArtifactRequest<T>
        where T : class
    {
        private readonly RepositoryAnalysisArtifactCache? _cache;
        private readonly string? _key;
        private readonly InflightEntry? _operation;
        private readonly Task<T> _result;

        internal RepositoryAnalysisArtifactRequest(
            RepositoryAnalysisArtifactCache cache,
            string key,
            InflightEntry operation,
            bool isOwner)
        {
            _cache = cache;
            _key = key;
            _operation = operation;
            IsOwner = isOwner;
            _result = AwaitAsync(operation.Completion.Task);
        }

        private RepositoryAnalysisArtifactRequest(T value)
        {
            _result = Task.FromResult(value);
        }

        public bool IsOwner { get; }

        public Task<T> Result => _result;

        internal static RepositoryAnalysisArtifactRequest<T> Cached(T value) => new(value);

        internal void Complete(T value) => _cache!.CompleteInflight(
            _key!,
            _operation!,
            value);

        internal void Fail(Exception exception) => _cache!.FailInflight(
            _key!,
            _operation!,
            exception);

        private static async Task<T> AwaitAsync(Task<object> result) =>
            (T)await result.ConfigureAwait(false);
    }
}
