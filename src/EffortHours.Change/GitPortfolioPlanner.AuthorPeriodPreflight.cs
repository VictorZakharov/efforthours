using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record GitAuthorPeriodManifestScopeContributor
{
    public required string ContributorId { get; init; }

    public int CandidateCount { get; init; }

    public int DirectAuthorCandidateCount { get; init; }

    public int CoauthorCandidateCount { get; init; }

    public int? SelectedChangeCount { get; init; }
}

public sealed record GitAuthorPeriodManifestRepositoryScope
{
    public required string RepositoryId { get; init; }

    public int HeadCount { get; init; }

    public int CandidateCount { get; init; }

    public bool CandidateCountIsLowerBound { get; init; }

    public int? SelectedChangeCount { get; init; }

    public int? SharedContributorChangeCount { get; init; }

    public long? ProjectedSnapshotRequests { get; init; }

    public int SelectionChunkCount { get; init; }

    public int? AnalysisChunkCount { get; init; }

    public long ChargedCandidateLedgerBytes { get; init; }

    public long MaximumCandidateLedgerBytes { get; init; }

    public string? BlockingResource { get; init; }

    public IReadOnlyList<GitAuthorPeriodManifestScopeContributor> Contributors { get; init; } = [];
}

public sealed record GitAuthorPeriodManifestScopePlan
{
    public required ChangeAuthorPeriodManifest Manifest { get; init; }

    public required ChangePortfolioSelection Selection { get; init; }

    public IReadOnlyList<GitAuthorPeriodManifestRepositoryScope> Repositories { get; init; } = [];

    public bool CompleteScope { get; init; }

    public required ChangePortfolioExecutionTelemetry ExecutionTelemetry { get; init; }
}

public sealed partial class GitPortfolioPlanner
{
    public async Task<GitAuthorPeriodManifestScopePlan> MeasureAuthorPeriodManifestAsync(
        ChangeAuthorPeriodManifest manifest,
        string manifestDigest,
        IReadOnlyDictionary<string, string> repositoryPaths,
        ChangePortfolioExecutionTelemetry executionTelemetry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executionTelemetry);
        using (executionTelemetry.Measure(ChangePortfolioExecutionPhases.ManifestValidation))
        {
            ValidateManifestInputs(manifest, manifestDigest, repositoryPaths);
        }

        PreparedManifestRepository[] repositories;
        using (executionTelemetry.Measure(ChangePortfolioExecutionPhases.HeadValidation))
        {
            repositories = await PreflightRepositoriesAsync(
                manifest,
                repositoryPaths,
                cancellationToken).ConfigureAwait(false);
        }

        GitAuthorPeriodIdentityGroup[] identityGroups = CreateIdentityGroups(manifest);
        List<GitAuthorPeriodManifestRepositoryScope> scopes = [];
        foreach (PreparedManifestRepository repository in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                PreparedManifestSelection prepared = await SelectManifestRepositoryAsync(
                    repository,
                    manifest,
                    identityGroups,
                    executionTelemetry,
                    cancellationToken).ConfigureAwait(false);
                scopes.Add(CreateCompleteScope(repository, manifest, prepared));
            }
            catch (GitAuthorPeriodCandidateBudgetException exception)
            {
                scopes.Add(CreateBlockedScope(repository, exception));
            }
        }

        return new GitAuthorPeriodManifestScopePlan
        {
            Manifest = manifest,
            Selection = ChangeAuthorPeriodManifestIdentity.CreateReportSelection(
                manifest,
                manifestDigest),
            Repositories = scopes,
            CompleteScope = scopes.All(scope => scope.BlockingResource is null),
            ExecutionTelemetry = executionTelemetry,
        };
    }

    private static GitAuthorPeriodManifestRepositoryScope CreateCompleteScope(
        PreparedManifestRepository repository,
        ChangeAuthorPeriodManifest manifest,
        PreparedManifestSelection prepared)
    {
        IReadOnlyList<SelectedManifestAuthorCommit> selected = prepared.Selected.Commits;
        GitAuthorPeriodCandidateResources resources = prepared.CandidateResult.Resources;
        IReadOnlyDictionary<string, int> selectedByContributor = manifest.Contributors.ToDictionary(
            contributor => contributor.Id,
            contributor => selected.Count(commit => commit.ContributorMatches.Any(
                match => match.ContributorId == contributor.Id)),
            StringComparer.Ordinal);
        return new GitAuthorPeriodManifestRepositoryScope
        {
            RepositoryId = repository.Manifest.Id,
            HeadCount = repository.Manifest.Heads.Count,
            CandidateCount = resources.CandidateCount,
            CandidateCountIsLowerBound = false,
            SelectedChangeCount = selected.Count,
            SharedContributorChangeCount = selected.Count(commit => commit.ContributorMatches.Count > 1),
            ProjectedSnapshotRequests = checked(selected.Count * 2L),
            SelectionChunkCount = resources.SelectionChunkCount,
            AnalysisChunkCount = ChunkCount(selected.Count, ChangeEstimator.PortfolioDeltaPrimeChunkSize),
            ChargedCandidateLedgerBytes = resources.ChargedLedgerBytes,
            MaximumCandidateLedgerBytes = resources.MaximumLedgerBytes,
            Contributors = CreateContributorScopes(
                prepared.CandidateResult.GroupCounts,
                selectedByContributor),
        };
    }

    private static GitAuthorPeriodManifestRepositoryScope CreateBlockedScope(
        PreparedManifestRepository repository,
        GitAuthorPeriodCandidateBudgetException exception) => new()
        {
            RepositoryId = repository.Manifest.Id,
            HeadCount = repository.Manifest.Heads.Count,
            CandidateCount = exception.ObservedCount,
            CandidateCountIsLowerBound = true,
            SelectionChunkCount = GitAuthorPeriodCandidateResourceMeter.ChunkCount(
                exception.ObservedCount),
            ChargedCandidateLedgerBytes = exception.ObservedLedgerBytes,
            MaximumCandidateLedgerBytes = exception.MaximumLedgerBytes,
            BlockingResource = exception.Budget == GitAuthorPeriodCandidateBudgetKind.LedgerBytes
                ? "candidate-ledger-bytes"
                : "emergency-candidate-count",
            Contributors = CreateContributorScopes(exception.GroupCounts, selectedByContributor: null),
        };

    private static IReadOnlyList<GitAuthorPeriodManifestScopeContributor> CreateContributorScopes(
        IReadOnlyList<GitAuthorPeriodCandidateGroupCount> counts,
        IReadOnlyDictionary<string, int>? selectedByContributor) =>
        [.. counts
            .OrderBy(count => count.Id, StringComparer.Ordinal)
            .Select(count => new GitAuthorPeriodManifestScopeContributor
            {
                ContributorId = count.Id,
                CandidateCount = count.TotalCount,
                DirectAuthorCandidateCount = count.DirectAuthorCount,
                CoauthorCandidateCount = count.CoauthorCount,
                SelectedChangeCount = selectedByContributor?.GetValueOrDefault(count.Id),
            })];

    private static int ChunkCount(int count, int chunkSize) => count == 0
        ? 0
        : ((count - 1) / chunkSize) + 1;
}
