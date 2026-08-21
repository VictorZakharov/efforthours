using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class GitClient
{
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
        CandidateLedger ledger = new(query);
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

        if (query.MaximumLedgerBytes is < 1 or >
                ChangeAuthorPeriodManifestLimits.MaximumCandidateLedgerBytesPerRepository ||
            query.EmergencyMaximumCandidates is < 1 or >
                ChangeAuthorPeriodManifestLimits.EmergencyMaximumIdentityCandidatesPerRepository)
        {
            throw new ArgumentOutOfRangeException(nameof(query));
        }
    }

    private sealed class CandidateLedger
    {
        private readonly GitAuthorPeriodCandidateQuery _query;
        private readonly Dictionary<string, CandidateLedgerEntry> _entries =
            new(StringComparer.Ordinal);
        private readonly int[] _directCounts;
        private readonly int[] _coauthorCounts;
        private readonly int[] _totalCounts;

        private long _chargedLedgerBytes;

        public CandidateLedger(GitAuthorPeriodCandidateQuery query)
        {
            _query = query;
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
                long charge = GitAuthorPeriodCandidateResourceMeter.Charge(commit);
                long observedBytes = _chargedLedgerBytes + charge;
                if (_entries.Count >= _query.EmergencyMaximumCandidates)
                {
                    throw Budget(
                        GitAuthorPeriodCandidateBudgetKind.EmergencyCandidateCount,
                        _entries.Count + 1,
                        observedBytes,
                        GroupCountsIncluding(matches, coauthorPass));
                }

                if (observedBytes > _query.MaximumLedgerBytes)
                {
                    throw Budget(
                        GitAuthorPeriodCandidateBudgetKind.LedgerBytes,
                        _entries.Count + 1,
                        observedBytes,
                        GroupCountsIncluding(matches, coauthorPass));
                }

                entry = new CandidateLedgerEntry(commit);
                _entries.Add(commit.ObjectId, entry);
                _chargedLedgerBytes = observedBytes;
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
            return new GitAuthorPeriodCandidateResult(
                [.. _entries.Values
                    .Select(entry => entry.Metadata)
                    .OrderBy(commit => commit.ObjectId, StringComparer.Ordinal)],
                GroupCounts(),
                new GitAuthorPeriodCandidateResources(
                    _entries.Count,
                    _chargedLedgerBytes,
                    _query.MaximumLedgerBytes,
                    GitAuthorPeriodCandidateResourceMeter.ChunkCount(_entries.Count),
                    ChangeAuthorPeriodManifestLimits.SelectionChunkSize,
                    _query.EmergencyMaximumCandidates));
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

        private GitAuthorPeriodCandidateBudgetException Budget(
            GitAuthorPeriodCandidateBudgetKind budget,
            int observedCount,
            long observedLedgerBytes,
            IReadOnlyList<GitAuthorPeriodCandidateGroupCount> groupCounts) => new(
                budget,
                observedCount,
                observedLedgerBytes,
                _query,
                groupCounts);

        private IReadOnlyList<GitAuthorPeriodCandidateGroupCount> GroupCountsIncluding(
            ulong matches,
            bool coauthorPass) =>
            [.. _query.IdentityGroups.Select((group, index) =>
            {
                bool matched = (matches & (1UL << index)) != 0;
                return new GitAuthorPeriodCandidateGroupCount(
                    group.Id,
                    _directCounts[index] + (matched && !coauthorPass ? 1 : 0),
                    _coauthorCounts[index] + (matched && coauthorPass ? 1 : 0),
                    _totalCounts[index] + (matched ? 1 : 0));
            })];

        private IReadOnlyList<GitAuthorPeriodCandidateGroupCount> GroupCounts() =>
            [.. _query.IdentityGroups.Select((group, index) =>
                    new GitAuthorPeriodCandidateGroupCount(
                        group.Id,
                        _directCounts[index],
                        _coauthorCounts[index],
                        _totalCounts[index]))];

        private sealed class CandidateLedgerEntry(GitCommitMetadata metadata)
        {
            public GitCommitMetadata Metadata { get; } = metadata;

            public ulong DirectMatches { get; set; }

            public ulong CoauthorMatches { get; set; }
        }
    }
}
