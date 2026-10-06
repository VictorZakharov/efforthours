using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed class ChangeRewriteReviewer
{
    private readonly GitClient _git = new();
    private readonly ChangeEstimator _estimator = new();
    public const int MaximumRangeCommits = 1024;

    public async Task<ChangeRewriteReviewReport> ReviewAsync(ChangeRewriteReviewManifest manifest,
        EstimationProfile profile, bool engineeringScope = true, CancellationToken token = default)
    {
        IReadOnlyList<string> errors = ContractValidation.Validate(manifest);
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
        string root = await _git.ResolveRepositoryRootAsync(manifest.RepositoryPath, token).ConfigureAwait(false);
        EngineeringScopeProfile? scope = engineeringScope ? EngineeringScopeProfile.Load() : null;
        string inputDigest = InputDigest(manifest, profile, scope);
        List<string> missing = [];
        foreach (string id in new[] { manifest.OldBaseObjectId, manifest.OriginalObjectId, manifest.NewBaseObjectId,
            manifest.RewrittenObjectId, manifest.ReplayObjectId }.OfType<string>().Distinct(StringComparer.Ordinal))
            if (!await _git.CommitExistsAsync(root, id, token).ConfigureAwait(false))
                missing.Add(id);
        if (missing.Count > 0)
        {
            ChangeRewriteReviewReport unresolved = new()
            {
                Status = "unresolved-object-evidence",
                RepositoryId = manifest.RepositoryId,
                InputDigest = inputDigest,
                SinceInclusive = manifest.SinceInclusive,
                UntilExclusive = manifest.UntilExclusive,
                EventTimestamp = manifest.EventTimestamp,
                EventProvenanceId = manifest.EventProvenanceId,
                ReplayProvenanceId = manifest.ReplayProvenanceId,
                ReplayConfidence = "unavailable",
                EventAttributionStatus = "unresolved-object-evidence",
                MissingObjectIds = [.. missing.Order(StringComparer.Ordinal)],
            };
            errors = ContractValidation.Validate(unresolved);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
            return unresolved;
        }
        await _git.EnsureAncestorAsync(root, manifest.OldBaseObjectId, manifest.NewBaseObjectId, token).ConfigureAwait(false);
        int originalCount = await Count(manifest.OldBaseObjectId, manifest.OriginalObjectId);
        int rewrittenCount = await Count(manifest.NewBaseObjectId, manifest.RewrittenObjectId);
        ChangeReplayProof? proof = null;
        if (manifest.ReplayObjectId is { } replayId)
        {
            _ = await Count(manifest.NewBaseObjectId, replayId);
            await using IChangeSnapshot oldBase = await _git.OpenSnapshotAsync(root, manifest.OldBaseObjectId, token).ConfigureAwait(false);
            await using IChangeSnapshot original = await _git.OpenSnapshotAsync(root, manifest.OriginalObjectId, token).ConfigureAwait(false);
            await using IChangeSnapshot newBase = await _git.OpenSnapshotAsync(root, manifest.NewBaseObjectId, token).ConfigureAwait(false);
            await using IChangeSnapshot replaySnapshot = await _git.OpenSnapshotAsync(root, replayId, token).ConfigureAwait(false);
            proof = ChangeReplayProofBuilder.Verify(oldBase, original, newBase, replaySnapshot, token);
        }
        ChangePathAdmission? admission = scope?.CreateAdmission(manifest.ScopeRepository ?? manifest.RepositoryId);
        List<ChangeRewriteComparison> comparisons = [];
        await Compare("original-implementation", manifest.OldBaseObjectId, manifest.OriginalObjectId);
        await Compare("inherited-upstream", manifest.OldBaseObjectId, manifest.NewBaseObjectId);
        await Compare("retained-feature", manifest.NewBaseObjectId, manifest.RewrittenObjectId);
        if (manifest.ReplayObjectId is { } replay)
        {
            await Compare("replayed-implementation", manifest.NewBaseObjectId, replay);
            await Compare("novel-retained-delta", replay, manifest.RewrittenObjectId);
        }
        var (Author, Committer) = await _git.ReadRewriteTimestampsAsync(root, manifest.OriginalObjectId, token).ConfigureAwait(false);
        var rewrittenTimes = await _git.ReadRewriteTimestampsAsync(root, manifest.RewrittenObjectId, token).ConfigureAwait(false);
        string eventStatus = manifest.ReplayObjectId is null ? "unresolved-replay-evidence"
            : manifest.EventTimestamp is null ? "unresolved-event-date"
            : manifest.EventTimestamp < manifest.SinceInclusive || manifest.EventTimestamp >= manifest.UntilExclusive ? "event-outside-period"
            : "attributed-under-declared-replay";
        ChangeRewriteReviewReport report = new()
        {
            Status = manifest.ReplayObjectId is null ? "unresolved-replay-evidence" : "complete-evidence-review",
            RepositoryId = manifest.RepositoryId,
            InputDigest = inputDigest,
            SinceInclusive = manifest.SinceInclusive,
            UntilExclusive = manifest.UntilExclusive,
            OriginalAuthorTimestamp = Author,
            RewrittenCommitterTimestamp = rewrittenTimes.Committer,
            EventTimestamp = manifest.EventTimestamp,
            ReplayProvenanceId = manifest.ReplayProvenanceId,
            EventProvenanceId = manifest.EventProvenanceId,
            ReplayProof = proof,
            ReplayConfidence = proof is null ? "unavailable" : proof.DeclaredConflictPathCount > 0 ? "caller-declared-conflict-replay" : "exact-path-replay-verified",
            EventAttributionStatus = eventStatus,
            EventAttributedNovelEffort = eventStatus == "attributed-under-declared-replay" ? comparisons.Single(value => value.Role == "novel-retained-delta").Effort
                : eventStatus == "event-outside-period" ? new EffortRange { Low = 0, Expected = 0, High = 0 } : null,
            OriginalCommitCount = originalCount,
            RewrittenCommitCount = rewrittenCount,
            Comparisons = comparisons,
        };
        errors = ContractValidation.Validate(report);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
        return report;

        async Task<int> Count(string before, string after)
        {
            await _git.EnsureAncestorAsync(root, before, after, token).ConfigureAwait(false);
            IReadOnlyList<string> commits = await _git.ListRangeCommitsAsync(root, before, after, MaximumRangeCommits + 1, token).ConfigureAwait(false);
            if (commits.Count > MaximumRangeCommits) throw new InvalidOperationException("Rewrite range exceeded its 1,024-commit evidence bound; no history was truncated.");
            return commits.Count;
        }

        async Task Compare(string role, string before, string after)
        {
            token.ThrowIfCancellationRequested();
            GitChangePlan plan = await new GitChangePlanner().PlanBaseHeadAsync(root, before, after, token).ConfigureAwait(false);
            ChangeEstimateReport source = await _estimator.EstimateAsync(plan with { RepositoryName = manifest.RepositoryId, PathAdmission = admission },
                profile, rateCard: null, token).ConfigureAwait(false);
            comparisons.Add(new()
            {
                Role = role,
                Selection = source.Selection,
                Profile = source.Profile,
                EstimatorVersion = source.EstimatorVersion,
                Effort = source.TotalEffort,
                SourceReportDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(ContractJson.SerializeCompact(source)),
                RepresentedPathCount = source.Evidence.Paths.Count(path => path.Represented),
            });
        }
    }

    private static string InputDigest(ChangeRewriteReviewManifest manifest, EstimationProfile profile, EngineeringScopeProfile? scope) =>
        ChangePortfolioComparisonIdentity.ComputeTextDigest(ContractJson.SerializeCompact(manifest with { RepositoryPath = "<execution-only>" }) +
            "\n" + profile + "\n" + (scope?.Contract.Digest ?? "all"));
}
