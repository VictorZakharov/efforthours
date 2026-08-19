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

    private sealed record PreparedPortfolioPlan(
        IndexedPortfolioPlan Entry,
        ChangeEstimateInput Input,
        IChangeSnapshot BaseSnapshot,
        IChangeSnapshot HeadSnapshot);
}
