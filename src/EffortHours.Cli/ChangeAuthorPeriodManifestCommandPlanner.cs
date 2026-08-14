using EffortHours.Change;

namespace EffortHours.Cli;

internal sealed class ChangeAuthorPeriodManifestCommandPlanner(GitPortfolioPlanner planner)
{
    private readonly GitPortfolioPlanner _planner = planner ??
        throw new ArgumentNullException(nameof(planner));

    public async Task<GitAuthorPeriodManifestPortfolioPlan> PlanAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        ResolvedChangeAuthorPeriodManifest resolved =
            await ChangeAuthorPeriodManifestLoader.LoadAsync(manifestPath, cancellationToken)
                .ConfigureAwait(false);
        return await _planner.PlanAuthorPeriodManifestAsync(
            resolved.Manifest,
            resolved.ManifestDigest,
            resolved.RepositoryPaths,
            cancellationToken).ConfigureAwait(false);
    }
}
