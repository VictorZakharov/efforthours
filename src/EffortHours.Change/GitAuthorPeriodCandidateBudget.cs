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

    public long MaximumLedgerBytes { get; init; } =
        ChangeAuthorPeriodManifestLimits.MaximumCandidateLedgerBytesPerRepository;

    public int EmergencyMaximumCandidates { get; init; } =
        ChangeAuthorPeriodManifestLimits.EmergencyMaximumIdentityCandidatesPerRepository;
}

internal sealed record GitAuthorPeriodCandidateGroupCount(
    string Id,
    int DirectAuthorCount,
    int CoauthorCount,
    int TotalCount);

internal sealed record GitAuthorPeriodCandidateResources(
    int CandidateCount,
    long ChargedLedgerBytes,
    long MaximumLedgerBytes,
    int SelectionChunkCount,
    int SelectionChunkSize,
    int EmergencyMaximumCandidates);

internal sealed record GitAuthorPeriodCandidateResult(
    IReadOnlyList<GitCommitMetadata> Candidates,
    IReadOnlyList<GitAuthorPeriodCandidateGroupCount> GroupCounts,
    GitAuthorPeriodCandidateResources Resources);

internal enum GitAuthorPeriodCandidateBudgetKind
{
    LedgerBytes,
    EmergencyCandidateCount,
}

internal sealed class GitAuthorPeriodCandidateBudgetException : InvalidOperationException
{
    public GitAuthorPeriodCandidateBudgetException(
        GitAuthorPeriodCandidateBudgetKind budget,
        int observedCount,
        long observedLedgerBytes,
        GitAuthorPeriodCandidateQuery query,
        IReadOnlyList<GitAuthorPeriodCandidateGroupCount> groupCounts)
        : base(FormatMessage(
            budget,
            observedCount,
            observedLedgerBytes,
            query,
            groupCounts))
    {
        Budget = budget;
        ObservedCount = observedCount;
        ObservedLedgerBytes = observedLedgerBytes;
        MaximumLedgerBytes = query.MaximumLedgerBytes;
        EmergencyMaximumCandidates = query.EmergencyMaximumCandidates;
        GroupCounts = groupCounts;
    }

    public GitAuthorPeriodCandidateBudgetKind Budget { get; }

    public int ObservedCount { get; }

    public long ObservedLedgerBytes { get; }

    public long MaximumLedgerBytes { get; }

    public int EmergencyMaximumCandidates { get; }

    public IReadOnlyList<GitAuthorPeriodCandidateGroupCount> GroupCounts { get; }

    private static string FormatMessage(
        GitAuthorPeriodCandidateBudgetKind budget,
        int observedCount,
        long observedLedgerBytes,
        GitAuthorPeriodCandidateQuery query,
        IReadOnlyList<GitAuthorPeriodCandidateGroupCount> groupCounts)
    {
        string breakdown = string.Join(
            ", ",
            groupCounts.Select(group =>
                $"{group.Id}={group.TotalCount} " +
                $"(direct={group.DirectAuthorCount}, coauthor={group.CoauthorCount})"));
        string limit = budget == GitAuthorPeriodCandidateBudgetKind.LedgerBytes
            ? $"the {query.MaximumLedgerBytes}-byte candidate-ledger budget"
            : $"the {query.EmergencyMaximumCandidates}-candidate emergency circuit breaker";
        return $"Author-period selection exceeded {limit} after observing at least {observedCount} exact " +
            $"in-window candidate(s); accepting the next record would produce a " +
            $"{observedLedgerBytes}-byte deterministic ledger charge. Observed counts by requested " +
            $"contributor: {breakdown}. No partial candidate set or estimate was emitted. " +
            "Run the same manifest with --preflight for an agent-readable scope recommendation; " +
            "do not add separately reconciled interval fragments.";
    }
}

internal static class GitAuthorPeriodCandidateResourceMeter
{
    // This deterministic, conservative charge describes retained ledger state.
    // It is not a sampled managed-heap measurement.
    private const long EntryOverheadBytes = 512;
    private const long StringOverheadBytes = 32;
    private const long CollectionOverheadBytes = 32;
    private const long ReferenceBytes = 8;

    public static long Charge(GitCommitMetadata commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        long bytes = EntryOverheadBytes + String(commit.ObjectId);
        bytes += CollectionOverheadBytes + (ReferenceBytes * commit.ParentObjectIds.Count);
        bytes += commit.ParentObjectIds.Sum(String);
        bytes += Identity(commit.Author) + Identity(commit.Committer);
        bytes += CollectionOverheadBytes + (ReferenceBytes * commit.Coauthors.Count);
        bytes += commit.Coauthors.Sum(Identity);
        return bytes;
    }

    public static int ChunkCount(int candidateCount) => candidateCount == 0
        ? 0
        : ((candidateCount - 1) /
            ChangeAuthorPeriodManifestLimits.SelectionChunkSize) + 1;

    private static long Identity(GitCommitIdentity identity) =>
        CollectionOverheadBytes + String(identity.Name) + String(identity.Email);

    private static long String(string value) =>
        StringOverheadBytes + (sizeof(char) * (long)value.Length);
}
