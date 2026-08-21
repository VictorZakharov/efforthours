using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed record GitAuthorPeriodIdentityGroup(
    string Id,
    IReadOnlyList<string> Aliases);

internal sealed record GitAuthorPeriodCandidateQuery
{
    public IReadOnlyList<string> HeadObjectIds { get; init; } = [];

    public IReadOnlyList<GitAuthorPeriodIdentityGroup> IdentityGroups { get; init; } = [];

    public required DateTimeOffset SinceInclusive { get; init; }

    public required DateTimeOffset UntilExclusive { get; init; }

    public required ChangePortfolioDateField DateField { get; init; }

    public bool IncludeCoauthors { get; init; }

    public int MaximumCandidates { get; init; } =
        ChangeAuthorPeriodManifestLimits.MaximumIdentityCandidatesPerRepository;
}

internal sealed record GitAuthorPeriodCandidateGroupCount(
    string Id,
    int DirectAuthorCount,
    int CoauthorCount,
    int TotalCount);

internal sealed record GitAuthorPeriodCandidateResult(
    IReadOnlyList<GitCommitMetadata> Candidates,
    IReadOnlyList<GitAuthorPeriodCandidateGroupCount> GroupCounts);

internal sealed class GitAuthorPeriodCandidateLimitException : InvalidOperationException
{
    public GitAuthorPeriodCandidateLimitException(
        int observedCount,
        bool countIsLowerBound,
        int maximumCount,
        IReadOnlyList<GitAuthorPeriodCandidateGroupCount> groupCounts)
        : base(FormatMessage(observedCount, countIsLowerBound, maximumCount, groupCounts))
    {
        ObservedCount = observedCount;
        CountIsLowerBound = countIsLowerBound;
        MaximumCount = maximumCount;
        GroupCounts = groupCounts;
    }

    public int ObservedCount { get; }

    public bool CountIsLowerBound { get; }

    public int MaximumCount { get; }

    public IReadOnlyList<GitAuthorPeriodCandidateGroupCount> GroupCounts { get; }

    private static string FormatMessage(
        int observedCount,
        bool countIsLowerBound,
        int maximumCount,
        IReadOnlyList<GitAuthorPeriodCandidateGroupCount> groupCounts)
    {
        string count = countIsLowerBound
            ? $"at least {observedCount}"
            : observedCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string breakdown = string.Join(
            ", ",
            groupCounts.Select(group =>
                $"{group.Id}={group.TotalCount} " +
                $"(direct={group.DirectAuthorCount}, coauthor={group.CoauthorCount})"));
        string bounded = countIsLowerBound
            ? " The diagnostic count stopped at its separate bounded ceiling."
            : string.Empty;
        return $"Author-period selection found {count} exact identity candidates inside the requested " +
            $"inclusive/exclusive interval; the per-repository limit is {maximumCount}. " +
            "Counts by requested contributor (one commit can match several contributors): " +
            $"{breakdown}. Reduce the interval or identity scope; EffortHours will not retain an " +
            $"unbounded in-window identity ledger.{bounded}";
    }
}

public sealed partial class GitClient
{
    private const int MaximumCandidateDiagnosticCount = 100_000;

    internal async Task<GitAuthorPeriodCandidateResult> ListAuthorPeriodCandidatesAsync(
        string repositoryPath,
        GitAuthorPeriodCandidateQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateCandidateQuery(query);
        string[] aliases = [.. query.IdentityGroups
            .SelectMany(group => group.Aliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ThenBy(alias => alias, StringComparer.Ordinal)];
        CandidateLedger ledger = new(query, MaximumCandidateDiagnosticCount);
        await ReadFilteredCommitMetadataAsync(
            repositoryPath,
            query.HeadObjectIds,
            aliases,
            "--author",
            commit => ledger.Add(commit, coauthorPass: false),
            cancellationToken).ConfigureAwait(false);
        if (query.IncludeCoauthors)
        {
            await ReadFilteredCommitMetadataAsync(
                repositoryPath,
                query.HeadObjectIds,
                aliases,
                "--grep",
                commit => ledger.Add(commit, coauthorPass: true),
                cancellationToken).ConfigureAwait(false);
        }

        return ledger.Complete();
    }

    private async Task ReadFilteredCommitMetadataAsync(
        string repositoryPath,
        IReadOnlyList<string> headObjectIds,
        IReadOnlyList<string> aliases,
        string filter,
        Action<GitCommitMetadata> onCommit,
        CancellationToken cancellationToken)
    {
        List<string> arguments =
        [
            "log",
            "--reverse",
            "--topo-order",
            "--fixed-strings",
            "--regexp-ignore-case",
            "--format=%H%x00%P%x00%an%x00%ae%x00%aI%x00%cn%x00%ce%x00%cI%x00%(trailers:key=Co-authored-by,valueonly,separator=%x1f)%x00",
        ];
        arguments.AddRange(aliases.Select(alias => $"{filter}={alias}"));
        arguments.AddRange(headObjectIds
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal));
        _ = await _commands.RunStreamingAsync(
            "git",
            repositoryPath,
            arguments,
            (reader, token) => GitCommitMetadataParser.ParseAsync(reader, onCommit, token),
            cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateCandidateQuery(GitAuthorPeriodCandidateQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.HeadObjectIds.Count == 0 || query.HeadObjectIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "At least one non-empty history head is required.",
                nameof(query));
        }

        if (query.IdentityGroups.Count is < 1 or > ChangeAuthorPeriodManifestLimits.MaximumContributors ||
            query.IdentityGroups.Any(group =>
                string.IsNullOrWhiteSpace(group.Id) ||
                group.Aliases.Count == 0 ||
                group.Aliases.Any(string.IsNullOrWhiteSpace)) ||
            query.IdentityGroups.Select(group => group.Id).Distinct(StringComparer.Ordinal).Count() !=
                query.IdentityGroups.Count)
        {
            throw new ArgumentException(
                "Author-period candidates require unique identity groups with non-empty aliases.",
                nameof(query));
        }

        if (query.SinceInclusive >= query.UntilExclusive || !Enum.IsDefined(query.DateField))
        {
            throw new ArgumentException(
                "Author-period candidates require a recognized date field and a non-empty interval.",
                nameof(query));
        }

        if (query.MaximumCandidates is < 1 or > MaximumCandidateDiagnosticCount)
        {
            throw new ArgumentOutOfRangeException(nameof(query));
        }
    }

    private sealed class CandidateLedger
    {
        private readonly GitAuthorPeriodCandidateQuery _query;
        private readonly int _diagnosticLimit;
        private readonly Dictionary<string, CandidateLedgerEntry> _entries =
            new(StringComparer.Ordinal);
        private readonly int[] _directCounts;
        private readonly int[] _coauthorCounts;
        private readonly int[] _totalCounts;

        public CandidateLedger(GitAuthorPeriodCandidateQuery query, int diagnosticLimit)
        {
            _query = query;
            _diagnosticLimit = diagnosticLimit;
            _directCounts = new int[query.IdentityGroups.Count];
            _coauthorCounts = new int[query.IdentityGroups.Count];
            _totalCounts = new int[query.IdentityGroups.Count];
        }

        public void Add(GitCommitMetadata commit, bool coauthorPass)
        {
            DateTimeOffset timestamp = _query.DateField == ChangePortfolioDateField.Author
                ? commit.AuthorTimestamp
                : commit.CommitterTimestamp;
            if (timestamp < _query.SinceInclusive || timestamp >= _query.UntilExclusive)
            {
                return;
            }

            ulong matches = MatchGroups(commit, coauthorPass);
            if (matches == 0)
            {
                return;
            }

            if (!_entries.TryGetValue(commit.ObjectId, out CandidateLedgerEntry? entry))
            {
                if (_entries.Count >= _diagnosticLimit)
                {
                    throw Limit(_diagnosticLimit + 1, countIsLowerBound: true);
                }

                entry = new CandidateLedgerEntry(
                    _entries.Count < _query.MaximumCandidates ? commit : null);
                _entries.Add(commit.ObjectId, entry);
            }

            ulong priorAny = entry.DirectMatches | entry.CoauthorMatches;
            ulong priorKind = coauthorPass ? entry.CoauthorMatches : entry.DirectMatches;
            ulong addedKind = matches & ~priorKind;
            if (coauthorPass)
            {
                addedKind &= ~entry.DirectMatches;
                entry.CoauthorMatches |= matches;
            }
            else
            {
                entry.DirectMatches |= matches;
            }

            ulong addedTotal = matches & ~priorAny;
            for (int index = 0; index < _query.IdentityGroups.Count; index++)
            {
                ulong bit = 1UL << index;
                if ((addedKind & bit) != 0)
                {
                    if (coauthorPass)
                    {
                        _coauthorCounts[index]++;
                    }
                    else
                    {
                        _directCounts[index]++;
                    }
                }

                if ((addedTotal & bit) != 0)
                {
                    _totalCounts[index]++;
                }
            }
        }

        public GitAuthorPeriodCandidateResult Complete()
        {
            if (_entries.Count > _query.MaximumCandidates)
            {
                throw Limit(_entries.Count, countIsLowerBound: false);
            }

            return new GitAuthorPeriodCandidateResult(
                [.. _entries.Values
                    .Select(entry => entry.Metadata!)
                    .OrderBy(commit => commit.ObjectId, StringComparer.Ordinal)],
                GroupCounts());
        }

        private ulong MatchGroups(GitCommitMetadata commit, bool coauthorPass)
        {
            ulong result = 0;
            for (int index = 0; index < _query.IdentityGroups.Count; index++)
            {
                IReadOnlyList<string> aliases = _query.IdentityGroups[index].Aliases;
                bool matches = coauthorPass
                    ? commit.Coauthors.Any(identity => AuthorIdentityMatcher.Matches(identity, aliases))
                    : AuthorIdentityMatcher.Matches(commit.Author, aliases);
                if (matches)
                {
                    result |= 1UL << index;
                }
            }

            return result;
        }

        private GitAuthorPeriodCandidateLimitException Limit(
            int observedCount,
            bool countIsLowerBound) => new(
                observedCount,
                countIsLowerBound,
                _query.MaximumCandidates,
                GroupCounts());

        private IReadOnlyList<GitAuthorPeriodCandidateGroupCount> GroupCounts() =>
            [.. _query.IdentityGroups.Select((group, index) =>
                    new GitAuthorPeriodCandidateGroupCount(
                        group.Id,
                        _directCounts[index],
                        _coauthorCounts[index],
                        _totalCounts[index]))];

        private sealed class CandidateLedgerEntry(GitCommitMetadata? metadata)
        {
            public GitCommitMetadata? Metadata { get; } = metadata;

            public ulong DirectMatches { get; set; }

            public ulong CoauthorMatches { get; set; }
        }
    }
}
