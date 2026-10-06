using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class GitPortfolioPlanner
{
    private static AuthorPeriodManifestSelectionResult SelectRewriteEvents(
        IReadOnlyList<GitCommitMetadata> history,
        ChangeAuthorPeriodManifest manifest,
        ChangeAuthorPeriodManifestRepository repository)
    {
        IReadOnlyList<ChangeRewriteEvent> events = repository.RewriteEvents ?? [];
        HashSet<string> paired = events.SelectMany(value =>
            new[] { value.OriginalObjectId, value.RewrittenObjectId }).ToHashSet(StringComparer.Ordinal);
        AuthorPeriodManifestSelectionResult ordinary = AuthorPeriodManifestCommitSelector.Select(
            [.. history.Where(value => !paired.Contains(value.ObjectId))], manifest.Selection, manifest.Contributors);
        List<SelectedManifestAuthorCommit> selected = [.. ordinary.Commits];
        List<Diagnostic> diagnostics = [.. ordinary.Diagnostics];
        if (events.Count > 0)
            diagnostics.Add(new Diagnostic
            {
                Code = "FB5342",
                Severity = DiagnosticSeverity.Warning,
                Message = "Declared rewrite event values allocate a jointly reconciled retained budget after preserving the original baseline. This remainder is not a causal conflict-resolution breakdown or actual integration labor; zero retained increment does not certify zero work.",
            });
        foreach (ChangeRewriteEvent evidence in events)
        {
            GitCommitMetadata original = Require(evidence.OriginalObjectId, evidence.OldBaseObjectId);
            GitCommitMetadata rewritten = Require(evidence.RewrittenObjectId, evidence.NewBaseObjectId);
            DateTimeOffset originalDate = manifest.Selection.DateField == ChangePortfolioDateField.Author
                ? original.AuthorTimestamp : original.CommitterTimestamp;
            DateTimeOffset novelDate = evidence.EventTimestamp ?? originalDate;
            if (evidence.EventTimestamp is null)
                diagnostics.Add(new Diagnostic
                {
                    Code = "FB5340",
                    Severity = DiagnosticSeverity.Warning,
                    Message = "Rewrite event attribution is unresolved: immutable Git author/committer timestamps do not prove a resolution workday. The original timestamp view is retained; unobserved intermediate history is not a certified zero.",
                });
            if (!InWindow(originalDate) && !InWindow(novelDate)) continue;
            DateTimeOffset anchor = InWindow(originalDate) ? originalDate : novelDate;
            Add(original, originalDate, "original");
            Add(rewritten, novelDate, "rewritten");


            void Add(GitCommitMetadata metadata, DateTimeOffset timestamp, string role)
            {
                DateTimeOffset effective = InWindow(timestamp) ? timestamp : anchor;
                AuthorPeriodManifestSelectionResult result = AuthorPeriodManifestCommitSelector.Select(
                    [metadata with { AuthorTimestamp = effective, CommitterTimestamp = effective }],
                    manifest.Selection, manifest.Contributors);
                SelectedManifestAuthorCommit commit = result.Commits.Single();
                selected.Add(commit with
                {
                    Metadata = metadata,
                    Rewrite = new ChangeRewriteAttribution
                    {
                        Evidence = evidence,
                        Role = role,
                        SupportOnly = !InWindow(timestamp),
                        OriginalAuthorTimestamp = original.AuthorTimestamp.ToUniversalTime(),
                        RewrittenCommitterTimestamp = rewritten.CommitterTimestamp.ToUniversalTime(),
                    },
                });
            }
        }
        return new AuthorPeriodManifestSelectionResult(
            [.. selected.OrderBy(value => value.SelectedTimestamp).ThenBy(value => value.Metadata.ObjectId, StringComparer.Ordinal)],
            diagnostics);

        bool InWindow(DateTimeOffset timestamp) => timestamp >= manifest.Selection.SinceInclusive &&
            timestamp < manifest.Selection.UntilExclusive;
        GitCommitMetadata Require(string objectId, string baseObjectId)
        {
            GitCommitMetadata? metadata = history.SingleOrDefault(value => value.ObjectId == objectId);
            if (metadata is null || metadata.ParentObjectIds.Count != 1 || metadata.ParentObjectIds[0] != baseObjectId)
                throw new InvalidOperationException("Declared rewrite evidence is unavailable, does not match the requested contributor, or is not an exact single-commit first-parent pair. Supply reachable before/after objects and their actual upstream bases; incomplete evidence cannot certify zero.");
            return metadata;
        }
    }
}
