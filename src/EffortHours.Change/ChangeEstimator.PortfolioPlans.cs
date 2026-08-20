using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class ChangeEstimator
{
    private static IndexedPortfolioPlan ValidatePlan(GitChangePlan? plan, int index)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Selection.Kind is not (ChangeSelectionKind.Commit or ChangeSelectionKind.PullRequest))
        {
            throw new ArgumentException(
                "Portfolio candidates must be immutable commit or pull-request changes.",
                nameof(plan));
        }

        return new IndexedPortfolioPlan(
            index,
            plan,
            Path.GetFullPath(plan.RepositoryPath));
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private sealed record IndexedPortfolioPlan(
        int Index,
        GitChangePlan Plan,
        string CacheNamespace);

    private sealed class PreparedPortfolioPlan
    {
        private readonly TaskCompletionSource _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _claimed;

        public PreparedPortfolioPlan(
            IndexedPortfolioPlan entry,
            ChangeEstimateInput input,
            IChangeSnapshot baseSnapshot,
            IChangeSnapshot headSnapshot,
            Task dependency)
        {
            Entry = entry;
            BaseSnapshot = baseSnapshot;
            HeadSnapshot = headSnapshot;
            Dependency = dependency;
            Input = input with
            {
                OpenBaseAsync = _ =>
                {
                    Interlocked.Exchange(ref _claimed, 1);
                    return Task.FromResult(BaseSnapshot);
                },
                OpenHeadAsync = _ => Task.FromResult(HeadSnapshot),
            };
        }

        public IndexedPortfolioPlan Entry { get; }

        public ChangeEstimateInput Input { get; }

        public Task Dependency { get; }

        public Task Completion => _completion.Task;

        private IChangeSnapshot BaseSnapshot { get; }

        private IChangeSnapshot HeadSnapshot { get; }

        public void Complete() => _completion.TrySetResult();

        public void Cancel() => _completion.TrySetCanceled();

        public async ValueTask DisposeIfUnclaimedAsync()
        {
            Cancel();
            if (Interlocked.Exchange(ref _claimed, 1) != 0)
            {
                return;
            }

            try
            {
                await BaseSnapshot.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                await HeadSnapshot.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
