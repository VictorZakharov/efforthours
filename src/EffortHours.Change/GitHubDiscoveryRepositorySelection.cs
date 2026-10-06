using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public static class GitHubDiscoveryRepositorySelection
{
    public static string[] Normalize(string owner, IReadOnlyList<string> requested)
    {
        ArgumentNullException.ThrowIfNull(requested);
        if (requested.Count > ChangeAuthorPeriodManifestLimits.MaximumRepositories)
            throw new ArgumentException("Too many repository restrictions.", nameof(requested));
        string[] normalized = [.. requested.Select(value => GitHubRepositoryIdentity.Normalize(value).ToLowerInvariant()).Order(StringComparer.Ordinal)];
        if (normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length ||
            normalized.Any(value => !value.Split('/')[0].Equals(owner, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Repository restrictions must be unique and belong to the owner.", nameof(requested));
        return normalized;
    }

    internal static ChangePortfolioRepositoryRestriction? Restriction(
        IReadOnlyList<GitHubDiscoveryRepository> inventory, string[] requested) => requested.Length == 0 ? null : new()
        {
            InputDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(string.Join("\n", requested)),
            RequestedRepositoryCount = requested.Length,
            ExcludedRepositoryCount = inventory.Count(repository => !requested.Contains(repository.Identity, StringComparer.OrdinalIgnoreCase)),
        };

    internal static IReadOnlyList<GitHubDiscoveryRepository> Filter(IReadOnlyList<GitHubDiscoveryRepository> inventory, string[] requested)
    {
        if (requested.Length == 0) return inventory;
        if (requested.Any(value => !inventory.Any(repository => repository.Identity.Equals(value, StringComparison.OrdinalIgnoreCase))))
            throw GitHubProviderFailure.DiscoveryBudget(GitHubProviderFailure.OwnerInventoryPhase,
                "A requested repository is absent from the complete accessible owner inventory. Verify --repository and access; no missing repository was treated as zero.");
        return [.. inventory.Where(repository => requested.Contains(repository.Identity, StringComparer.OrdinalIgnoreCase))];
    }
}
