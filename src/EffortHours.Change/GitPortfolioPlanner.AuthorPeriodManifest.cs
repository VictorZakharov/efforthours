using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record GitAuthorPeriodManifestPortfolioItem
{
    public required string RepositoryId { get; init; }

    public required string SelectorId { get; init; }

    public required GitChangePlan Plan { get; init; }

    public required ChangePortfolioAttribution Attribution { get; init; }
}

public sealed record GitAuthorPeriodManifestPortfolioPlan
{
    public required ChangePortfolioSelection Selection { get; init; }

    public IReadOnlyList<GitAuthorPeriodManifestPortfolioItem> Items { get; init; } = [];

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    public required ChangePortfolioExecutionTelemetry ExecutionTelemetry { get; init; }
}

public sealed partial class GitPortfolioPlanner
{
    public Task<GitAuthorPeriodManifestPortfolioPlan> PlanAuthorPeriodManifestAsync(
        ChangeAuthorPeriodManifest manifest,
        string manifestDigest,
        IReadOnlyDictionary<string, string> repositoryPaths,
        CancellationToken cancellationToken = default) =>
        PlanAuthorPeriodManifestAsync(
            manifest,
            manifestDigest,
            repositoryPaths,
            new ChangePortfolioExecutionTelemetry(),
            cancellationToken);

    public async Task<GitAuthorPeriodManifestPortfolioPlan> PlanAuthorPeriodManifestAsync(
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

        string[] aliases = CanonicalAliases(
            [.. manifest.Contributors.SelectMany(contributor => contributor.Aliases)]);
        PreparedManifestRepository[] repositories;
        using (executionTelemetry.Measure(ChangePortfolioExecutionPhases.HeadValidation))
        {
            repositories = await PreflightRepositoriesAsync(
                manifest,
                repositoryPaths,
                cancellationToken).ConfigureAwait(false);
        }
        List<GitAuthorPeriodManifestPortfolioItem> items = [];
        List<Diagnostic> diagnostics =
        [
            new Diagnostic
            {
                Code = "FB5321",
                Severity = DiagnosticSeverity.Information,
                Message = "The author-period manifest supplied execution-only aliases and local repository paths. Reports retain its digest, public IDs, policy, and immutable objects only.",
            },
            new Diagnostic
            {
                Code = "FB5322",
                Severity = DiagnosticSeverity.Information,
                Message = "Reachability was unioned per repository before exact selection; commits reachable from multiple manifest heads are estimated once.",
            },
        ];
        foreach (PreparedManifestRepository repository in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<GitCommitMetadata> history;
            try
            {
                using (executionTelemetry.Measure(ChangePortfolioExecutionPhases.HistoryUnion))
                {
                    history = await _git.ListAuthorPeriodCandidatesAsync(
                        repository.RootPath,
                        [.. repository.Manifest.Heads.Select(head => head.ObjectId)],
                        aliases,
                        manifest.Selection.CoauthorPolicy == ChangePortfolioCoauthorPolicy.Include,
                        _options.MaximumHistoryCommits + 1,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            catch (ExternalCommandException exception)
            {
                throw new InvalidOperationException(
                    $"Git could not traverse author-period candidates for repository '{repository.Manifest.Id}'.",
                    exception);
            }

            EnsureCandidateLimit(history.Count, _options.MaximumHistoryCommits);
            AuthorPeriodManifestSelectionResult selected;
            using (executionTelemetry.Measure(ChangePortfolioExecutionPhases.Selection))
            {
                selected = AuthorPeriodManifestCommitSelector.Select(
                    history,
                    manifest.Selection,
                    manifest.Contributors);
            }
            diagnostics.AddRange(selected.Diagnostics);
            if (selected.Commits.Count == 0)
            {
                continue;
            }

            if (items.Count + selected.Commits.Count > _options.MaximumSelectedItems)
            {
                throw new InvalidOperationException(
                    $"Manifest author-period selection matched more than {_options.MaximumSelectedItems} changes. " +
                    "This bounded safety limit protects memory while normal closed-month intervals remain one calculation.");
            }

            IReadOnlyDictionary<string, IReadOnlyList<string>> reachableHeads;
            try
            {
                using (executionTelemetry.Measure(ChangePortfolioExecutionPhases.HistoryUnion))
                {
                    reachableHeads = await _headReachability.ResolveAsync(
                        repository.RootPath,
                        repository.Manifest.Heads,
                        [.. selected.Commits.Select(commit => commit.Metadata.ObjectId)],
                        cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (
                exception is ExternalCommandException or InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Git could not resolve manifest-head reachability for repository '{repository.Manifest.Id}'.",
                    exception);
            }

            foreach (SelectedManifestAuthorCommit commit in selected.Commits)
            {
                GitChangePlan plan = _changes.PlanPinnedCommit(repository.RootPath, commit.Metadata);
                ChangePortfolioAttributionKind kind = commit.ContributorMatches.Any(
                    match => match.Kind == ChangePortfolioContributorMatchKind.DirectAuthor)
                    ? ChangePortfolioAttributionKind.DirectAuthor
                    : ChangePortfolioAttributionKind.Coauthor;
                items.Add(new GitAuthorPeriodManifestPortfolioItem
                {
                    RepositoryId = repository.Manifest.Id,
                    SelectorId = $"{repository.Manifest.Id}:commit:{commit.Metadata.ObjectId}",
                    Plan = plan,
                    Attribution = new ChangePortfolioAttribution
                    {
                        Kind = kind,
                        SelectedTimestamp = commit.SelectedTimestamp.ToUniversalTime(),
                        MergeCommit = commit.Metadata.ParentObjectIds.Count > 1,
                        ParentCount = commit.Metadata.ParentObjectIds.Count,
                        ContributorMatches = commit.ContributorMatches,
                        HeadIds = reachableHeads[commit.Metadata.ObjectId],
                        AmbiguityReasons = commit.AmbiguityReasons,
                    },
                });
            }
        }

        if (items.Count == 0)
        {
            throw new InvalidOperationException(
                "No commits matched the manifest contributors, selected timestamp field, and inclusive/exclusive interval.");
        }

        return new GitAuthorPeriodManifestPortfolioPlan
        {
            Selection = ReportSelection(manifest, manifestDigest),
            Items = items,
            Diagnostics = diagnostics,
            ExecutionTelemetry = executionTelemetry,
        };
    }

    private async Task<PreparedManifestRepository[]> PreflightRepositoriesAsync(
        ChangeAuthorPeriodManifest manifest,
        IReadOnlyDictionary<string, string> repositoryPaths,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> repositoryIdByRoot = new(PathComparer);
        List<PreparedManifestRepository> prepared = [];
        foreach (ChangeAuthorPeriodManifestRepository repository in manifest.Repositories
            .OrderBy(repository => repository.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!repositoryPaths.TryGetValue(repository.Id, out string? path) || !Directory.Exists(path))
            {
                throw new DirectoryNotFoundException(
                    $"Repository '{repository.Id}' was not found or is inaccessible.");
            }

            string root;
            try
            {
                root = await _git.ResolveRepositoryRootAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is ArgumentException or DirectoryNotFoundException or ExternalCommandException or
                    InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                throw ManifestRepositoryFailure.Create(repository.Id, exception);
            }

            string filesystemRoot = Path.GetPathRoot(root) ?? string.Empty;
            if (PathComparer.Equals(
                Path.TrimEndingDirectorySeparator(root),
                Path.TrimEndingDirectorySeparator(filesystemRoot)))
            {
                throw new InvalidOperationException(
                    $"Repository '{repository.Id}' cannot resolve to a filesystem root.");
            }

            if (repositoryIdByRoot.TryGetValue(root, out string? priorId))
            {
                throw new InvalidOperationException(
                    $"Manifest repository IDs '{priorId}' and '{repository.Id}' resolve to the same local Git repository. " +
                    "Use one repository ID so repository-scoped deduplication cannot be bypassed.");
            }

            repositoryIdByRoot.Add(root, repository.Id);
            foreach (ChangeAuthorPeriodManifestHead head in repository.Heads
                .OrderBy(head => head.Id, StringComparer.Ordinal))
            {
                bool exists;
                try
                {
                    exists = await _git.CommitExistsAsync(root, head.ObjectId, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (ExternalCommandException exception)
                {
                    throw new InvalidOperationException(
                        $"Repository '{repository.Id}' could not verify pinned head '{head.Id}'.",
                        exception);
                }

                if (!exists)
                {
                    throw new InvalidOperationException(
                        $"Repository '{repository.Id}' does not contain pinned commit '{head.ObjectId}' for head '{head.Id}'. " +
                        "EffortHours does not fetch missing objects implicitly.");
                }
            }

            prepared.Add(new PreparedManifestRepository(repository, root));
        }

        return [.. prepared];
    }

    private static void ValidateManifestInputs(
        ChangeAuthorPeriodManifest manifest,
        string manifestDigest,
        IReadOnlyDictionary<string, string> repositoryPaths)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestDigest);
        ArgumentNullException.ThrowIfNull(repositoryPaths);
        IReadOnlyList<string> errors = ContractValidation.Validate(manifest);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The author-period manifest is semantically invalid: " + string.Join(" ", errors),
                nameof(manifest));
        }

        if (!string.Equals(
            manifestDigest,
            ChangeAuthorPeriodManifestIdentity.ComputeDigest(manifest),
            StringComparison.Ordinal))
        {
            throw new ArgumentException("The author-period manifest digest is invalid.", nameof(manifestDigest));
        }

        if (repositoryPaths.Count != manifest.Repositories.Count ||
            manifest.Repositories.Any(repository => !repositoryPaths.ContainsKey(repository.Id)))
        {
            throw new ArgumentException(
                "Resolved repository paths must map one-to-one to manifest repository IDs.",
                nameof(repositoryPaths));
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(manifest.Selection.TimeZone);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException(
                $"Timezone '{manifest.Selection.TimeZone}' was not found on this host.",
                nameof(manifest),
                exception);
        }
    }

    private static ChangePortfolioSelection ReportSelection(
        ChangeAuthorPeriodManifest manifest,
        string manifestDigest) => new()
        {
            Kind = ChangePortfolioSelectionKind.AuthorPeriod,
            ManifestBased = true,
            AuthorPeriodManifest = new ChangePortfolioAuthorPeriodManifestSelection
            {
                ManifestDigest = manifestDigest,
                SinceInclusive = manifest.Selection.SinceInclusive.ToUniversalTime(),
                UntilExclusive = manifest.Selection.UntilExclusive.ToUniversalTime(),
                TimeZone = manifest.Selection.TimeZone,
                DateField = manifest.Selection.DateField,
                MergePolicy = manifest.Selection.MergePolicy,
                CoauthorPolicy = manifest.Selection.CoauthorPolicy,
                ContributorIds = [.. manifest.Contributors
                    .Select(contributor => contributor.Id)
                    .Order(StringComparer.Ordinal)],
                Repositories = [.. manifest.Repositories
                    .OrderBy(repository => repository.Id, StringComparer.Ordinal)
                    .Select(repository => new ChangePortfolioAuthorPeriodManifestRepository
                    {
                        Id = repository.Id,
                        Heads = [.. repository.Heads
                            .OrderBy(head => head.Id, StringComparer.Ordinal)
                            .Select(head => new ChangePortfolioAuthorPeriodManifestHead
                            {
                                Id = head.Id,
                                ObjectId = head.ObjectId,
                            })],
                    })],
            },
        };

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private sealed record PreparedManifestRepository(
        ChangeAuthorPeriodManifestRepository Manifest,
        string RootPath);
}
