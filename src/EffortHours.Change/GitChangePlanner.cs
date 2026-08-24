using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record GitChangePlan
{
    public required string RepositoryPath { get; init; }

    public required ChangeSelection Selection { get; init; }

    public required Func<CancellationToken, Task<IChangeSnapshot>> OpenBaseAsync { get; init; }

    public required Func<CancellationToken, Task<IChangeSnapshot>> OpenHeadAsync { get; init; }

    public IReadOnlyList<ChangeComponentInput> Components { get; init; } = [];

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    public string? RepositoryName { get; init; }

    public ChangePathAdmission? PathAdmission { get; init; }

    internal GitSnapshotSession? SnapshotSession { get; init; }
}

public sealed record GitChangePlannerOptions
{
    public const int DefaultMaximumRangeComponents = 256;
    public const int MaximumSupportedRangeComponents = 1_024;

    public int MaximumRangeComponents { get; init; } = DefaultMaximumRangeComponents;
}

public sealed partial class GitChangePlanner
{
    private readonly GitClient _git;
    private readonly IPullRequestResolver _pullRequests;
    private readonly GitChangePlannerOptions _options;

    public GitChangePlanner()
        : this(new GitClient(), new GitHubPullRequestResolver(), new GitChangePlannerOptions())
    {
    }

    public GitChangePlanner(GitClient git, IPullRequestResolver pullRequests)
        : this(git, pullRequests, new GitChangePlannerOptions())
    {
    }

    public GitChangePlanner(
        GitClient git,
        IPullRequestResolver pullRequests,
        GitChangePlannerOptions options)
    {
        _git = git ?? throw new ArgumentNullException(nameof(git));
        _pullRequests = pullRequests ?? throw new ArgumentNullException(nameof(pullRequests));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (_options.MaximumRangeComponents is < 1 or > GitChangePlannerOptions.MaximumSupportedRangeComponents)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                _options.MaximumRangeComponents,
                $"Maximum range components must be between 1 and " +
                $"{GitChangePlannerOptions.MaximumSupportedRangeComponents}.");
        }
    }

    public async Task<GitChangePlan> PlanBaseHeadAsync(
        string repositoryPath,
        string baseRevision,
        string headRevision,
        CancellationToken cancellationToken = default)
    {
        string root = await _git.ResolveRepositoryRootAsync(repositoryPath, cancellationToken)
            .ConfigureAwait(false);
        string baseObjectId = await _git.ResolveCommitAsync(root, baseRevision, cancellationToken)
            .ConfigureAwait(false);
        string headObjectId = await _git.ResolveCommitAsync(root, headRevision, cancellationToken)
            .ConfigureAwait(false);
        ChangeSelection selection = new()
        {
            Kind = ChangeSelectionKind.BaseHead,
            Base = GitClient.Reference(baseRevision, baseObjectId),
            Head = GitClient.Reference(headRevision, headObjectId),
        };
        return FinalDeltaPlan(root, selection, []);
    }

    public async Task<GitChangePlan> PlanCommitAsync(
        string repositoryPath,
        string revision,
        string? parentRevision,
        CancellationToken cancellationToken = default)
    {
        string root = await _git.ResolveRepositoryRootAsync(repositoryPath, cancellationToken)
            .ConfigureAwait(false);
        string commitObjectId = await _git.ResolveCommitAsync(root, revision, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<string> parents = await _git.GetParentsAsync(root, commitObjectId, cancellationToken)
            .ConfigureAwait(false);
        List<Diagnostic> diagnostics = [PinnedReferenceDiagnostic()];
        string baseObjectId;
        string baseSelector;
        ChangeSnapshotKind baseKind;
        if (parents.Count == 0)
        {
            if (parentRevision is not null)
            {
                throw new InvalidOperationException("A root commit has no parent; omit --parent.");
            }

            baseObjectId = GitClient.EmptyTreeObjectId;
            baseSelector = "<empty-tree>";
            baseKind = ChangeSnapshotKind.EmptyTree;
            diagnostics.Add(new Diagnostic
            {
                Code = "FB5101",
                Severity = DiagnosticSeverity.Information,
                Message = "The selected root commit is compared with Git's empty tree.",
            });
        }
        else if (parents.Count == 1)
        {
            if (parentRevision is null)
            {
                baseObjectId = parents[0];
                baseSelector = $"{revision}^1";
            }
            else
            {
                baseObjectId = await _git.ResolveCommitAsync(root, parentRevision, cancellationToken)
                    .ConfigureAwait(false);
                baseSelector = parentRevision;
                if (!parents.Contains(baseObjectId, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Selected parent '{parentRevision}' is not a parent of commit '{revision}'.");
                }
            }

            baseKind = ChangeSnapshotKind.GitCommit;
        }
        else
        {
            if (parentRevision is null)
            {
                throw new InvalidOperationException(
                    $"Commit '{revision}' is a merge with {parents.Count} parents. " +
                    "Select one explicitly with --parent <revision>.");
            }

            baseObjectId = await _git.ResolveCommitAsync(root, parentRevision, cancellationToken)
                .ConfigureAwait(false);
            if (!parents.Contains(baseObjectId, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Selected parent '{parentRevision}' is not a parent of merge commit '{revision}'.");
            }

            baseSelector = parentRevision;
            baseKind = ChangeSnapshotKind.GitCommit;
            diagnostics.Add(new Diagnostic
            {
                Code = "FB5102",
                Severity = DiagnosticSeverity.Information,
                Message = "The merge commit is valued relative to the explicitly selected parent only.",
            });
        }

        ChangeSelection selection = new()
        {
            Kind = ChangeSelectionKind.Commit,
            Base = GitClient.Reference(baseSelector, baseObjectId, baseKind),
            Head = GitClient.Reference(revision, commitObjectId),
            Commit = revision,
            Parent = parentRevision,
        };
        ChangeComponentInput component = Component(root, revision, baseObjectId, commitObjectId);
        return CreatePlan(root, selection, [component], diagnostics);
    }

    public async Task<GitChangePlan> PlanRangeAsync(
        string repositoryPath,
        string range,
        CancellationToken cancellationToken = default)
    {
        (string baseRevision, string headRevision) = ParseRange(range);
        string root = await _git.ResolveRepositoryRootAsync(repositoryPath, cancellationToken)
            .ConfigureAwait(false);
        string baseObjectId = await _git.ResolveCommitAsync(root, baseRevision, cancellationToken)
            .ConfigureAwait(false);
        string headObjectId = await _git.ResolveCommitAsync(root, headRevision, cancellationToken)
            .ConfigureAwait(false);
        await _git.EnsureAncestorAsync(root, baseObjectId, headObjectId, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<string> commits = await _git.ListRangeCommitsAsync(
            root,
            baseObjectId,
            headObjectId,
            _options.MaximumRangeComponents + 1,
            cancellationToken).ConfigureAwait(false);
        List<Diagnostic> diagnostics = [PinnedReferenceDiagnostic()];
        List<ChangeComponentInput> components = [];
        if (commits.Count > _options.MaximumRangeComponents)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = "FB5105",
                Severity = DiagnosticSeverity.Warning,
                Message = $"The range contains more than {_options.MaximumRangeComponents} commits. " +
                    "The bounded per-commit reconciliation audit was omitted; the immutable final " +
                    "base-to-head estimate remains complete and authoritative.",
            });
            components.Add(Component(
                root,
                range,
                baseObjectId,
                headObjectId,
                ChangeComponentKind.FinalDelta));
        }
        else
        {
            foreach (string commit in commits)
            {
                IReadOnlyList<string> parents = await _git.GetParentsAsync(root, commit, cancellationToken)
                    .ConfigureAwait(false);
                string parent = parents.Count == 0 ? GitClient.EmptyTreeObjectId : parents[0];
                if (parents.Count > 1)
                {
                    diagnostics.Add(new Diagnostic
                    {
                        Code = "FB5103",
                        Severity = DiagnosticSeverity.Warning,
                        Message = "Range reconciliation contains a merge component valued against its first " +
                            "parent; the normalized final base-to-head estimate remains authoritative.",
                    });
                }

                components.Add(Component(root, commit, parent, commit));
            }
        }

        ChangeSelection selection = new()
        {
            Kind = ChangeSelectionKind.Range,
            Base = GitClient.Reference(baseRevision, baseObjectId),
            Head = GitClient.Reference(headRevision, headObjectId),
            Range = range,
        };
        if (components.Count == 0)
        {
            components.Add(Component(root, range, baseObjectId, headObjectId, ChangeComponentKind.FinalDelta));
        }

        return CreatePlan(root, selection, components, diagnostics);
    }

    private GitChangePlan FinalDeltaPlan(
        string repositoryPath,
        ChangeSelection selection,
        IReadOnlyList<Diagnostic> diagnostics) =>
        CreatePlan(
            repositoryPath,
            selection,
            [
                Component(
                    repositoryPath,
                    selection.Kind == ChangeSelectionKind.PullRequest
                        ? $"pr:{selection.PullRequest!.Number}"
                        : "final-delta",
                    selection.Base.ObjectId,
                    selection.Head.ObjectId,
                    ChangeComponentKind.FinalDelta),
            ],
            [PinnedReferenceDiagnostic(), .. diagnostics]);

    private GitChangePlan CreatePlan(
        string repositoryPath,
        ChangeSelection selection,
        IReadOnlyList<ChangeComponentInput> components,
        IReadOnlyList<Diagnostic> diagnostics,
        GitSnapshotSession? snapshotSession = null) => new()
        {
            RepositoryPath = repositoryPath,
            Selection = selection,
            OpenBaseAsync = cancellationToken => OpenSnapshotAsync(
                repositoryPath,
                selection.Base.ObjectId,
                snapshotSession,
                cancellationToken),
            OpenHeadAsync = cancellationToken => OpenSnapshotAsync(
                repositoryPath,
                selection.Head.ObjectId,
                snapshotSession,
                cancellationToken),
            Components = components,
            Diagnostics = [.. diagnostics
                .Distinct()
                .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)],
            SnapshotSession = snapshotSession,
        };

    private ChangeComponentInput Component(
        string repositoryPath,
        string selector,
        string baseObjectId,
        string headObjectId,
        ChangeComponentKind kind = ChangeComponentKind.Commit,
        GitSnapshotSession? snapshotSession = null) => new()
        {
            Kind = kind,
            Selector = selector,
            BaseObjectId = baseObjectId,
            HeadObjectId = headObjectId,
            OpenBaseAsync = cancellationToken => OpenSnapshotAsync(
                repositoryPath,
                baseObjectId,
                snapshotSession,
                cancellationToken),
            OpenHeadAsync = cancellationToken => OpenSnapshotAsync(
                repositoryPath,
                headObjectId,
                snapshotSession,
                cancellationToken),
        };

}
