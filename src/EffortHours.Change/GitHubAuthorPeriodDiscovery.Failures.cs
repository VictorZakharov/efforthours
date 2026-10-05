using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class GitHubAuthorPeriodDiscovery
{
    private const string FailureDiscoveryKey = "EffortHours.GitHubDiscovery.ObservedFailure";
    public static ChangePortfolioHostDiscovery? FailureDiscovery(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current.Data[FailureDiscoveryKey] is ChangePortfolioHostDiscovery discovery) return discovery;
        return null;
    }
}
