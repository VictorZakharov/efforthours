using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class GitChangePlanner
{
    public async Task<GitChangePlan> PlanPullRequestAsync(
        string repositoryPath,
        string pullRequest,
        string? repository,
        CancellationToken cancellationToken = default) => await PlanPullRequestAsync(
            repositoryPath,
            pullRequest,
            repository,
            fetchMissing: false,
            cancellationToken).ConfigureAwait(false);

    public async Task<GitChangePlan> PlanPullRequestAsync(
        string repositoryPath,
        string pullRequest,
        string? repository,
        bool fetchMissing,
        CancellationToken cancellationToken = default)
    {
        string root = await _git.ResolveRepositoryRootAsync(repositoryPath, cancellationToken)
            .ConfigureAwait(false);
        ResolvedPullRequest resolved = await _pullRequests.ResolveAsync(
            root,
            pullRequest,
            repository,
            cancellationToken).ConfigureAwait(false);
        bool hasBase = await _git.CommitExistsAsync(root, resolved.BaseObjectId, cancellationToken)
            .ConfigureAwait(false);
        bool hasHead = await _git.CommitExistsAsync(root, resolved.HeadObjectId, cancellationToken)
            .ConfigureAwait(false);
        bool acquired = false;
        if (!hasBase || !hasHead)
        {
            if (!fetchMissing)
            {
                throw new InvalidOperationException(
                    "The pull request resolved successfully, but these immutable objects are not in the local Git " +
                    $"database: {string.Join(", ", MissingObjects(resolved, hasBase, hasHead))}. " +
                    "Retry with --fetch-missing to acquire only the selected provider base and PR head refs, " +
                    "or fetch those objects independently. EffortHours does not fetch without explicit authorization.");
            }

            await AcquireMissingObjectsAsync(root, resolved, cancellationToken).ConfigureAwait(false);
            acquired = true;
            hasBase = await _git.CommitExistsAsync(root, resolved.BaseObjectId, cancellationToken)
                .ConfigureAwait(false);
            hasHead = await _git.CommitExistsAsync(root, resolved.HeadObjectId, cancellationToken)
                .ConfigureAwait(false);
            if (!hasBase || !hasHead)
            {
                throw new InvalidOperationException(
                    "Explicit pull-request object acquisition completed, but these resolved immutable objects " +
                    $"remain unavailable: {string.Join(", ", MissingObjects(resolved, hasBase, hasHead))}. " +
                    "The PR may have changed during resolution or the provider may not expose the selected refs; retry.");
            }
        }

        string comparisonBaseObjectId = await _git.ResolveMergeBaseAsync(
            root,
            resolved.BaseObjectId,
            resolved.HeadObjectId,
            cancellationToken).ConfigureAwait(false);
        ChangeSelection selection = Selection(resolved, comparisonBaseObjectId, acquired);
        List<Diagnostic> diagnostics =
        [
            new()
            {
                Code = "FB5104",
                Severity = DiagnosticSeverity.Information,
                Message = "The optional gh adapter supplied immutable PR base-tip/head identities; local Git resolved their unique merge base as the comparison base. PR activity and metadata are not effort signals.",
            },
        ];
        if (acquired)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = "FB5106",
                Severity = DiagnosticSeverity.Information,
                Message = "Explicit --fetch-missing acquisition added objects through only the selected provider base and PR head refs; it did not update local refs, FETCH_HEAD, the index, or the worktree.",
            });
        }

        return FinalDeltaPlan(root, selection, diagnostics);
    }

    private async Task AcquireMissingObjectsAsync(
        string repositoryPath,
        ResolvedPullRequest resolved,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resolved.FetchSource) ||
            string.IsNullOrWhiteSpace(resolved.BaseRefName))
        {
            throw new InvalidOperationException(
                "The pull-request resolver did not supply the execution-only provider source and base ref " +
                "required by --fetch-missing.");
        }

        await _git.FetchPullRequestObjectsAsync(
            repositoryPath,
            resolved.FetchSource,
            resolved.BaseRefName,
            resolved.Reference.Number,
            cancellationToken).ConfigureAwait(false);
    }

    private static ChangeSelection Selection(
        ResolvedPullRequest resolved,
        string comparisonBaseObjectId,
        bool acquired) => new()
        {
            Kind = ChangeSelectionKind.PullRequest,
            Base = GitClient.Reference(comparisonBaseObjectId, comparisonBaseObjectId),
            Head = GitClient.Reference(resolved.HeadObjectId, resolved.HeadObjectId),
            PullRequest = resolved.Reference with
            {
                ProviderBaseObjectId = resolved.BaseObjectId,
                ComparisonBasePolicy = PullRequestComparisonBasePolicy.ProviderBaseHeadMergeBase,
                ObjectAcquisition = acquired
                    ? PullRequestObjectAcquisition.ExplicitFetch
                    : PullRequestObjectAcquisition.LocalReuse,
                ProviderChangedFileCount = resolved.ChangedFileCount,
            },
        };

    private static List<string> MissingObjects(
        ResolvedPullRequest resolved,
        bool hasBase,
        bool hasHead)
    {
        List<string> missing = [];
        if (!hasBase)
        {
            missing.Add($"base {resolved.BaseObjectId}");
        }

        if (!hasHead)
        {
            missing.Add($"head {resolved.HeadObjectId}");
        }

        return missing;
    }
}
