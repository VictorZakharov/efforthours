using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record GitAuthorPeriodPortfolioOptions
{
    public IReadOnlyList<string> Aliases { get; init; } = [];

    public required DateTimeOffset SinceInclusive { get; init; }

    public required DateTimeOffset UntilExclusive { get; init; }

    public string TimeZone { get; init; } = "UTC";

    public ChangePortfolioDateField DateField { get; init; } = ChangePortfolioDateField.Author;

    public ChangePortfolioMergePolicy MergePolicy { get; init; } = ChangePortfolioMergePolicy.Exclude;

    public ChangePortfolioCoauthorPolicy CoauthorPolicy { get; init; } =
        ChangePortfolioCoauthorPolicy.Include;

    public string HeadRevision { get; init; } = "HEAD";
}

public sealed record GitPortfolioPlannerOptions
{
    public const int DefaultMaximumHistoryCommits = 10_000;
    public const int MaximumSupportedHistoryCommits = 100_000;
    public const int DefaultMaximumSelectedItems = 128;

    public int MaximumHistoryCommits { get; init; } = DefaultMaximumHistoryCommits;

    public int MaximumSelectedItems { get; init; } = DefaultMaximumSelectedItems;
}

public sealed record GitAuthorPeriodPortfolioItem
{
    public required string SelectorId { get; init; }

    public required GitChangePlan Plan { get; init; }

    public required ChangePortfolioAttribution Attribution { get; init; }
}

public sealed record GitAuthorPeriodPortfolioPlan
{
    public required string RepositoryId { get; init; }

    public required ChangePortfolioSelection Selection { get; init; }

    public IReadOnlyList<GitAuthorPeriodPortfolioItem> Items { get; init; } = [];

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];
}

public sealed partial class GitPortfolioPlanner
{
    private readonly GitClient _git;
    private readonly GitChangePlanner _changes;
    private readonly GitPortfolioPlannerOptions _options;
    private readonly IGitHeadReachabilityResolver _headReachability;

    public GitPortfolioPlanner()
        : this(new GitClient(), new GitPortfolioPlannerOptions())
    {
    }

    private GitPortfolioPlanner(
        GitClient git,
        GitPortfolioPlannerOptions options)
        : this(
            git,
            new GitChangePlanner(git, new GitHubPullRequestResolver()),
            options)
    {
    }

    internal GitPortfolioPlanner(
        GitClient git,
        GitChangePlanner changes,
        GitPortfolioPlannerOptions options)
        : this(git, changes, options, new GitHeadReachabilityResolver())
    {
    }

    internal GitPortfolioPlanner(
        GitClient git,
        GitChangePlanner changes,
        GitPortfolioPlannerOptions options,
        IGitHeadReachabilityResolver headReachability)
    {
        _git = git ?? throw new ArgumentNullException(nameof(git));
        _changes = changes ?? throw new ArgumentNullException(nameof(changes));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _headReachability = headReachability ?? throw new ArgumentNullException(nameof(headReachability));
        if (_options.MaximumHistoryCommits is < 1 or >
                GitPortfolioPlannerOptions.MaximumSupportedHistoryCommits ||
            _options.MaximumSelectedItems is < 1 or > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }

    public async Task<GitAuthorPeriodPortfolioPlan> PlanAuthorPeriodAsync(
        string repositoryPath,
        GitAuthorPeriodPortfolioOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Aliases);
        string[] aliases = CanonicalAliases(options.Aliases);
        if (options.SinceInclusive >= options.UntilExclusive)
        {
            throw new ArgumentException("The inclusive start must be earlier than the exclusive end.", nameof(options));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(options.TimeZone);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.HeadRevision);
        if (!Enum.IsDefined(options.DateField) || !Enum.IsDefined(options.MergePolicy) ||
            !Enum.IsDefined(options.CoauthorPolicy))
        {
            throw new ArgumentException(
                "Author-period date, merge, and co-author policies must be recognized values.",
                nameof(options));
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException(
                $"Timezone '{options.TimeZone}' was not found on this host.",
                nameof(options),
                exception);
        }

        string root = await _git.ResolveRepositoryRootAsync(repositoryPath, cancellationToken)
            .ConfigureAwait(false);
        string headObjectId = await _git.ResolveCommitAsync(root, options.HeadRevision, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<GitCommitMetadata> history = await _git.ListAuthorPeriodCandidatesAsync(
            root,
            headObjectId,
            aliases,
            options.CoauthorPolicy == ChangePortfolioCoauthorPolicy.Include,
            _options.MaximumHistoryCommits + 1,
            cancellationToken).ConfigureAwait(false);
        EnsureCandidateLimit(history.Count, _options.MaximumHistoryCommits);

        AuthorPeriodSelectionResult selected = AuthorPeriodCommitSelector.Select(history, options, aliases);
        if (selected.Commits.Count == 0)
        {
            throw new InvalidOperationException(
                "No commits matched the exact aliases, selected timestamp field, and inclusive/exclusive interval.");
        }

        if (selected.Commits.Count > _options.MaximumSelectedItems)
        {
            throw new InvalidOperationException(
                $"Author-period selection matched more than {_options.MaximumSelectedItems} changes. " +
                "Use a narrower interval so each row and allocation remain reviewable.");
        }

        List<GitAuthorPeriodPortfolioItem> items = [];
        foreach (SelectedAuthorCommit commit in selected.Commits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GitChangePlan plan = _changes.PlanPinnedCommit(root, commit.Metadata);
            items.Add(new GitAuthorPeriodPortfolioItem
            {
                SelectorId = $"commit:{commit.Metadata.ObjectId}",
                Plan = plan,
                Attribution = new ChangePortfolioAttribution
                {
                    Kind = commit.Kind,
                    SelectedTimestamp = commit.SelectedTimestamp.ToUniversalTime(),
                    MergeCommit = commit.Metadata.ParentObjectIds.Count > 1,
                    ParentCount = commit.Metadata.ParentObjectIds.Count,
                    AmbiguityReasons = commit.AmbiguityReasons,
                },
            });
        }

        string repositoryId = Path.GetFileName(Path.TrimEndingDirectorySeparator(root));
        return new GitAuthorPeriodPortfolioPlan
        {
            RepositoryId = string.IsNullOrWhiteSpace(repositoryId) ? "repository" : repositoryId,
            Selection = new ChangePortfolioSelection
            {
                Kind = ChangePortfolioSelectionKind.AuthorPeriod,
                AuthorPeriod = new ChangePortfolioAuthorPeriodSelection
                {
                    Aliases = aliases,
                    SinceInclusive = options.SinceInclusive.ToUniversalTime(),
                    UntilExclusive = options.UntilExclusive.ToUniversalTime(),
                    TimeZone = options.TimeZone,
                    DateField = options.DateField,
                    MergePolicy = options.MergePolicy,
                    CoauthorPolicy = options.CoauthorPolicy,
                    HeadSelector = options.HeadRevision,
                    HeadObjectId = headObjectId,
                },
            },
            Items = items,
            Diagnostics = selected.Diagnostics,
        };
    }

    private static string[] CanonicalAliases(IReadOnlyList<string> aliases)
    {
        if (aliases.Count is < 1 or > 128 || aliases.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Author-period selection requires between 1 and 128 non-empty identity aliases.",
                nameof(aliases));
        }

        string[] values = [.. aliases
            .Select(alias => alias.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];
        if (values.Length is < 1 or > 128)
        {
            throw new ArgumentException(
                "Author-period selection requires between 1 and 128 unique non-empty identity aliases.",
                nameof(aliases));
        }

        return values;
    }

    internal static void EnsureCandidateLimit(int candidateCount, int maximumCount)
    {
        if (candidateCount > maximumCount)
        {
            throw new InvalidOperationException(
                $"Author-period selection matched more than {maximumCount} " +
                "identity-prefiltered commit candidates. Use narrower aliases or a history boundary; " +
                "EffortHours will not load an unbounded identity ledger.");
        }
    }
}

internal sealed record SelectedAuthorCommit(
    GitCommitMetadata Metadata,
    ChangePortfolioAttributionKind Kind,
    DateTimeOffset SelectedTimestamp,
    IReadOnlyList<string> AmbiguityReasons);

internal sealed record AuthorPeriodSelectionResult(
    IReadOnlyList<SelectedAuthorCommit> Commits,
    IReadOnlyList<Diagnostic> Diagnostics);

internal static class AuthorPeriodCommitSelector
{
    public static AuthorPeriodSelectionResult Select(
        IReadOnlyList<GitCommitMetadata> history,
        GitAuthorPeriodPortfolioOptions options,
        IReadOnlyList<string> aliases)
    {
        List<(GitCommitMetadata Metadata, ChangePortfolioAttributionKind Kind, DateTimeOffset Timestamp)> matches = [];
        int excludedMerges = 0;
        foreach (GitCommitMetadata commit in history)
        {
            DateTimeOffset timestamp = options.DateField == ChangePortfolioDateField.Author
                ? commit.AuthorTimestamp
                : commit.CommitterTimestamp;
            if (timestamp < options.SinceInclusive || timestamp >= options.UntilExclusive)
            {
                continue;
            }

            ChangePortfolioAttributionKind? kind = AuthorIdentityMatcher.Matches(commit.Author, aliases)
                ? ChangePortfolioAttributionKind.DirectAuthor
                : options.CoauthorPolicy == ChangePortfolioCoauthorPolicy.Include &&
                    commit.Coauthors.Any(identity => AuthorIdentityMatcher.Matches(identity, aliases))
                    ? ChangePortfolioAttributionKind.Coauthor
                    : null;
            if (kind is null)
            {
                continue;
            }

            if (commit.ParentObjectIds.Count > 1 &&
                options.MergePolicy == ChangePortfolioMergePolicy.Exclude)
            {
                excludedMerges++;
                continue;
            }

            matches.Add((commit, kind.Value, timestamp));
        }

        (GitCommitMetadata Metadata, ChangePortfolioAttributionKind Kind, DateTimeOffset Timestamp)[] ordered =
        [.. matches.OrderBy(match => match.Timestamp).ThenBy(match => match.Metadata.ObjectId, StringComparer.Ordinal)];
        List<SelectedAuthorCommit> selected = [];
        for (int index = 0; index < ordered.Length; index++)
        {
            var (Metadata, Kind, Timestamp) = ordered[index];
            List<string> ambiguity = [];
            if (Metadata.Coauthors.Count > 0)
            {
                ambiguity.Add(
                    "Co-authored-by metadata indicates shared repository attribution but cannot infer shared-credit proportions or pair-work.");
            }

            if (Metadata.ParentObjectIds.Count > 1)
            {
                ambiguity.Add(
                    "The selected merge is valued against its first parent; branch-side authorship and shared integration credit remain ambiguous.");
            }

            if (index > 0 &&
                (Metadata.ParentObjectIds.Count == 0 ||
                    !Metadata.ParentObjectIds.Contains(ordered[index - 1].Metadata.ObjectId, StringComparer.Ordinal)))
            {
                ambiguity.Add(
                    "The previous selected change is not this commit's direct parent; unselected or interleaved work may be embedded in shared changed blobs.");
            }

            selected.Add(new SelectedAuthorCommit(
                Metadata,
                Kind,
                Timestamp,
                ambiguity));
        }

        List<Diagnostic> diagnostics =
        [
            new Diagnostic
            {
                Code = "FB5310",
                Severity = DiagnosticSeverity.Information,
                Message = "Author aliases and timestamps were read only to select immutable commits; identity and time are not effort multipliers.",
            },
        ];
        if (excludedMerges > 0)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = "FB5311",
                Severity = DiagnosticSeverity.Information,
                Message = $"{excludedMerges} matching merge commit(s) were excluded by the explicit merge policy.",
            });
        }

        return new AuthorPeriodSelectionResult(selected, diagnostics);
    }
}
