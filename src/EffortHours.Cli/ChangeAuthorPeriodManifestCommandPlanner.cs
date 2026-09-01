using EffortHours.Change;

namespace EffortHours.Cli;

internal sealed class ChangeAuthorPeriodManifestCommandPlanner
{
    private readonly GitPortfolioPlanner _planner;
    private readonly ManagedGitQueryPlanner _managedGitQueries;

    public ChangeAuthorPeriodManifestCommandPlanner(GitPortfolioPlanner planner)
        : this(planner, new ManagedGitQueryPlanner())
    {
    }

    internal ChangeAuthorPeriodManifestCommandPlanner(
        GitPortfolioPlanner planner,
        ManagedGitQueryPlanner managedGitQueries)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _managedGitQueries = managedGitQueries ??
            throw new ArgumentNullException(nameof(managedGitQueries));
    }

    public async Task<GitAuthorPeriodManifestPortfolioPlan> PlanAsync(
        string manifestPath,
        ChangePortfolioExecutionTelemetry executionTelemetry,
        CancellationToken cancellationToken) => await PlanAsync(
            manifestPath,
            fetchMissing: false,
            executionTelemetry,
            cancellationToken).ConfigureAwait(false);

    public async Task<GitAuthorPeriodManifestPortfolioPlan> PlanAsync(
        string manifestPath,
        bool fetchMissing,
        ChangePortfolioExecutionTelemetry executionTelemetry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(executionTelemetry);
        ResolvedChangeAuthorPeriodManifest resolved;
        using (executionTelemetry.Measure(ChangePortfolioExecutionPhases.ManifestValidation))
        {
            resolved = await ChangeAuthorPeriodManifestLoader.LoadAsync(manifestPath, cancellationToken)
                .ConfigureAwait(false);
            resolved = await ChangeAuthorPeriodManifestRepositoryLocator.MaterializeAsync(
                resolved,
                _managedGitQueries,
                fetchMissing,
                readOnly: false,
                cancellationToken).ConfigureAwait(false);
        }

        return await _planner.PlanAuthorPeriodManifestAsync(
            resolved.Manifest,
            resolved.ManifestDigest,
            resolved.RepositoryPaths,
            executionTelemetry,
            allowEmptySelection: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<GitAuthorPeriodManifestScopePlan> MeasureAsync(
        string manifestPath,
        ChangePortfolioExecutionTelemetry executionTelemetry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(executionTelemetry);
        ResolvedChangeAuthorPeriodManifest resolved;
        using (executionTelemetry.Measure(ChangePortfolioExecutionPhases.ManifestValidation))
        {
            resolved = await ChangeAuthorPeriodManifestLoader.LoadAsync(manifestPath, cancellationToken)
                .ConfigureAwait(false);
            resolved = await ChangeAuthorPeriodManifestRepositoryLocator.MaterializeAsync(
                resolved,
                _managedGitQueries,
                fetchMissing: false,
                readOnly: true,
                cancellationToken).ConfigureAwait(false);
        }

        return await _planner.MeasureAuthorPeriodManifestAsync(
            resolved.Manifest,
            resolved.ManifestDigest,
            resolved.RepositoryPaths,
            executionTelemetry,
            cancellationToken).ConfigureAwait(false);
    }
}
