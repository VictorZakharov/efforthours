using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record GitPortfolioReplayRangePlan(
    string RepositoryId,
    string RootPath,
    ChangePortfolioReplayEvent Event,
    IReadOnlyList<string> OriginalObjectIds,
    IReadOnlyList<string> RetainedObjectIds);

public sealed partial class GitPortfolioPlanner
{
    private async Task<IReadOnlyList<GitPortfolioReplayRangePlan>> PrepareReplayRangesAsync(
        PreparedManifestRepository repository, CancellationToken token)
    {
        if (repository.Manifest.ReplayEvents is not { Count: > 0 }) return [];
        List<GitPortfolioReplayRangePlan> ranges = [];
        HashSet<string> seen = (repository.Manifest.RewriteEvents ?? []).SelectMany(value =>
            new[] { value.OriginalObjectId, value.RewrittenObjectId }).ToHashSet(StringComparer.Ordinal);
        foreach (ChangePortfolioReplayEvent value in (repository.Manifest.ReplayEvents ?? []).OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            await _git.EnsureAncestorAsync(repository.RootPath, value.OldBaseObjectId, value.NewBaseObjectId, token).ConfigureAwait(false);
            IReadOnlyList<string> original = await Read(value.OldBaseObjectId, value.OriginalObjectId);
            IReadOnlyList<string> retained = await Read(value.NewBaseObjectId, value.RewrittenObjectId);
            foreach (string id in original.Concat(retained))
                if (!seen.Add(id)) throw new InvalidOperationException("Replay ranges and legacy rewrite pairs must be disjoint; competing or chained mappings are unsupported.");
            ranges.Add(new(repository.Manifest.Id, repository.RootPath, value, original, retained));
        }
        return ranges;

        async Task<IReadOnlyList<string>> Read(string before, string after)
        {
            await _git.EnsureAncestorAsync(repository.RootPath, before, after, token).ConfigureAwait(false);
            IReadOnlyList<string> ids = await _git.ListRangeCommitsAsync(repository.RootPath, before, after,
                ChangeRewriteReviewer.MaximumRangeCommits + 1, token).ConfigureAwait(false);
            if (ids.Count is < 1 or > ChangeRewriteReviewer.MaximumRangeCommits)
                throw new InvalidOperationException("Replay attribution requires complete non-empty ranges of at most 1,024 commits; no history was truncated.");
            return ids;
        }
    }

    private static AuthorPeriodManifestSelectionResult SelectReplayRanges(
        IReadOnlyList<GitCommitMetadata> history, ChangeAuthorPeriodManifest manifest,
        ChangeAuthorPeriodManifestRepository repository, IReadOnlyList<GitPortfolioReplayRangePlan> ranges)
    {
        if (ranges.Count == 0) return SelectRewriteEvents(history, manifest, repository);
        Dictionary<string, GitCommitMetadata> metadataById = history.ToDictionary(value => value.ObjectId, StringComparer.Ordinal);
        HashSet<string> rangeIds = ranges.SelectMany(range => range.OriginalObjectIds.Concat(range.RetainedObjectIds)).ToHashSet(StringComparer.Ordinal);
        AuthorPeriodManifestSelectionResult ordinary = SelectRewriteEvents([.. history.Where(value => !rangeIds.Contains(value.ObjectId))], manifest, repository);
        List<SelectedManifestAuthorCommit> selected = [.. ordinary.Commits];
        List<Diagnostic> diagnostics = [.. ordinary.Diagnostics];
        foreach (GitPortfolioReplayRangePlan range in ranges)
        {
            GitCommitMetadata[] original = Require(range.OriginalObjectIds, range.Event.OldBaseObjectId);
            GitCommitMetadata[] retained = Require(range.RetainedObjectIds, range.Event.NewBaseObjectId);
            bool resolved = range.Event.ReplayObjectId is not null && range.Event.EventTimestamp is not null;
            DateTimeOffset? anchor = original.Select(Date).Where(InWindow).Cast<DateTimeOffset?>().FirstOrDefault()
                ?? (range.Event.EventTimestamp is { } instant && InWindow(instant) ? instant : null);
            if (resolved && anchor is null) continue;
            IReadOnlyList<ChangePortfolioContributorMatch>? identities = null;
            Add(original, "original");
            Add(retained, "retained");
            diagnostics.Add(new Diagnostic
            {
                Code = "FB5343",
                Severity = DiagnosticSeverity.Warning,
                Message = "Replay range attribution conserves the existing jointly deduplicated budget. Independent artifact comparisons are non-additive; declared replay/date provenance does not establish actual labor or historical causation. Missing replay or dates remain unresolved.",
            });

            void Add(IEnumerable<GitCommitMetadata> members, string role)
            {
                foreach (GitCommitMetadata metadata in members)
                {
                    DateTimeOffset originalDate = Date(metadata);
                    DateTimeOffset date = role == "retained" && resolved ? range.Event.EventTimestamp!.Value : originalDate;
                    DateTimeOffset effective = resolved && !InWindow(date) ? anchor!.Value : date;
                    // Match every immutable member, including out-of-window support, before selecting rows.
                    DateTimeOffset matchDate = InWindow(effective) ? effective : manifest.Selection.SinceInclusive;
                    SelectedManifestAuthorCommit match = AuthorPeriodManifestCommitSelector.Select(
                        [metadata with { AuthorTimestamp = matchDate, CommitterTimestamp = matchDate }],
                        manifest.Selection, manifest.Contributors).Commits.SingleOrDefault()
                        ?? throw new InvalidOperationException("Every replay range member must match the requested contributor identities.");
                    if (identities is not null && !identities.SequenceEqual(match.ContributorMatches))
                        throw new InvalidOperationException("Replay ranges require the same exact contributor match set; cross-contributor reassignment is unsupported.");
                    identities = match.ContributorMatches;
                    if (!resolved && !InWindow(date)) continue;
                    selected.Add(match with
                    {
                        Metadata = metadata,
                        SelectedTimestamp = effective.ToUniversalTime(),
                        Replay = new ChangePortfolioReplayAttribution
                        {
                            EventId = range.Event.Id,
                            Role = role,
                            OriginalSelectedTimestamp = originalDate.ToUniversalTime(),
                            SupportOnly = resolved && !InWindow(date),
                        },
                    });
                }
            }
        }
        return new([.. selected.OrderBy(value => value.SelectedTimestamp).ThenBy(value => value.Metadata.ObjectId, StringComparer.Ordinal)], diagnostics);

        DateTimeOffset Date(GitCommitMetadata metadata) => manifest.Selection.DateField == ChangePortfolioDateField.Author
            ? metadata.AuthorTimestamp : metadata.CommitterTimestamp;
        bool InWindow(DateTimeOffset date) => date >= manifest.Selection.SinceInclusive && date < manifest.Selection.UntilExclusive;
        GitCommitMetadata[] Require(IReadOnlyList<string> ids, string before)
        {
            List<GitCommitMetadata> result = [];
            foreach (string id in ids)
            {
                metadataById.TryGetValue(id, out GitCommitMetadata? member);
                if (member is null || member.ParentObjectIds.Count != 1 || member.ParentObjectIds[0] != before)
                    throw new InvalidOperationException($"Replay allocation requires complete reachable non-merge first-parent ranges with matching identities: object={id}; expectedParent={before}; observedParents={string.Join(",", member?.ParentObjectIds ?? [])}. Missing or branched evidence cannot certify zero.");
                result.Add(member); before = id;
            }
            return [.. result];
        }
    }
}
