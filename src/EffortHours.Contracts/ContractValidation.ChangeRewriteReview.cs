using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(ChangeRewriteReviewManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        List<string> errors = [];
        RequireVersion(manifest.SchemaVersion, "rewrite review manifest", errors);
        if (manifest.Policy != ChangeRewriteReviewPolicy.Version) errors.Add("Unknown immutable replay review policy.");
        ValidatePublicId(manifest.RepositoryId, "repositoryId", errors);
        RequireCanonicalText(manifest.RepositoryPath, "repositoryPath", 4096, errors);
        if (manifest.ScopeRepository is not null) RequireCanonicalText(manifest.ScopeRepository, "scopeRepository", 256, errors);
        foreach (string id in new[] { manifest.OriginalObjectId, manifest.OldBaseObjectId, manifest.RewrittenObjectId, manifest.NewBaseObjectId }.Concat(manifest.ReplayObjectId is null ? [] : new[] { manifest.ReplayObjectId }))
            if (id is null || !IsObjectId(id)) errors.Add("Rewrite review requires full immutable Git object IDs.");
        if (manifest.OriginalObjectId == manifest.OldBaseObjectId || manifest.RewrittenObjectId == manifest.NewBaseObjectId)
            errors.Add("Original and retained feature ranges must contain commits.");
        ValidateReplayPeriod(manifest.SinceInclusive, manifest.UntilExclusive, errors);
        ValidateReplayProvenance(manifest.ReplayObjectId is not null, manifest.ReplayProvenanceId,
            manifest.EventTimestamp, manifest.EventProvenanceId, errors);
        return errors;
    }

    public static IReadOnlyList<string> Validate(ChangeRewriteReviewReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "rewrite review report", errors);
        if (report.Policy != ChangeRewriteReviewPolicy.Version || report.Boundary != ChangeRewriteReviewPolicy.Boundary)
            errors.Add("Rewrite review must preserve its policy and non-additive counterfactual boundary.");
        ValidatePublicId(report.RepositoryId, "repositoryId", errors);
        ValidateDigest(report.InputDigest, "inputDigest", errors);
        ValidateReplayPeriod(report.SinceInclusive, report.UntilExclusive, errors);
        if (report.SinceInclusive.Offset != TimeSpan.Zero || report.UntilExclusive.Offset != TimeSpan.Zero ||
            report.EventTimestamp is { Offset: var offset } && offset != TimeSpan.Zero)
            errors.Add("Replay reports require canonical UTC period and event instants.");
        if (report.Status == "unresolved-object-evidence")
        {
            if (report.MissingObjectIds is not { Count: >= 1 and <= 5 } ids || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count || ids.Any(id => !IsObjectId(id)) ||
                report.Comparisons.Count != 0 || report.OriginalCommitCount != 0 || report.RewrittenCommitCount != 0 ||
                report.OriginalAuthorTimestamp is not null || report.RewrittenCommitterTimestamp is not null ||
                report.ReplayProof is not null || report.ReplayConfidence != "unavailable" ||
                report.EventAttributionStatus != "unresolved-object-evidence" || report.EventAttributedNovelEffort is not null)
                errors.Add("Missing immutable objects require unresolved attribution without comparisons, dates or zero-valued effort.");
            ValidateReplayProvenance(report.ReplayProvenanceId is not null, report.ReplayProvenanceId, report.EventTimestamp, report.EventProvenanceId, errors);
            return errors;
        }
        if (report.MissingObjectIds is not null) errors.Add("Completed object evidence cannot retain missing-object declarations.");
        bool hasReplay = report.ReplayProof is not null;
        ValidateReplayProvenance(hasReplay, report.ReplayProvenanceId, report.EventTimestamp, report.EventProvenanceId, errors);
        if (report.OriginalCommitCount is < 1 or > 1024 || report.RewrittenCommitCount is < 1 or > 1024 ||
            report.OriginalAuthorTimestamp is null || report.RewrittenCommitterTimestamp is null ||
            report.OriginalAuthorTimestamp.Value.Offset != TimeSpan.Zero || report.RewrittenCommitterTimestamp.Value.Offset != TimeSpan.Zero)
            errors.Add("Rewrite review requires bounded ranges and distinct preserved UTC timestamp fields.");
        if (report.ReplayProof is { } proof && (proof.ExactReplayPathCount < 0 || proof.InheritedUpstreamPathCount < 0 ||
            proof.DeclaredConflictPathCount < 0 || (long)proof.ExactReplayPathCount + proof.InheritedUpstreamPathCount + proof.DeclaredConflictPathCount > 65536))
            errors.Add("Replay path proof must retain its bounded exact, inherited and caller-declared conflict counts.");
        string confidence = !hasReplay ? "unavailable" : report.ReplayProof!.DeclaredConflictPathCount > 0 ? "caller-declared-conflict-replay" : "exact-path-replay-verified";
        if (report.ReplayConfidence != confidence || report.Status != (hasReplay ? "complete-evidence-review" : "unresolved-replay-evidence"))
            errors.Add("Replay review confidence must not promote caller-declared conflicts into verified causation.");
        string[] roles = hasReplay ? ["original-implementation", "inherited-upstream", "retained-feature", "replayed-implementation", "novel-retained-delta"]
            : ["original-implementation", "inherited-upstream", "retained-feature"];
        if (report.Comparisons.Count != roles.Length || !report.Comparisons.Select(value => value.Role).Order(StringComparer.Ordinal).SequenceEqual(roles.Order(StringComparer.Ordinal)))
            errors.Add("Rewrite review must preserve exactly its distinct non-additive comparisons.");
        foreach (ChangeRewriteComparison comparison in report.Comparisons)
        {
            ValidateChangeSelection(comparison.Selection, errors);
            ValidateRange(comparison.Effort, "rewrite comparison effort", errors);
            ValidateDigest(comparison.SourceReportDigest, "sourceReportDigest", errors);
            RequireCanonicalText(comparison.EstimatorVersion, "estimatorVersion", 256, errors);
            if (comparison.Selection.Kind != ChangeSelectionKind.BaseHead || !Enum.IsDefined(comparison.Profile) || comparison.RepresentedPathCount < 0)
                errors.Add("Rewrite comparisons require immutable normalized base/head source estimates.");
        }
        if (report.Comparisons.Select(value => (value.Profile, value.EstimatorVersion)).Distinct().Count() != 1)
            errors.Add("All rewrite comparisons must use the same profile and estimator.");
        if (errors.Count == 0)
        {
            var byRole = report.Comparisons.ToDictionary(value => value.Role, StringComparer.Ordinal);
            ChangeSelection original = byRole["original-implementation"].Selection, upstream = byRole["inherited-upstream"].Selection,
                retained = byRole["retained-feature"].Selection;
            if (original.Base.ObjectId != upstream.Base.ObjectId || retained.Base.ObjectId != upstream.Head.ObjectId ||
                hasReplay && (byRole["replayed-implementation"].Selection.Base.ObjectId != retained.Base.ObjectId ||
                    byRole["novel-retained-delta"].Selection.Base.ObjectId != byRole["replayed-implementation"].Selection.Head.ObjectId ||
                    byRole["novel-retained-delta"].Selection.Head.ObjectId != retained.Head.ObjectId))
                errors.Add("Replay comparisons must bind the same immutable original, upstream, replay and retained endpoints.");
            string status = !hasReplay ? "unresolved-replay-evidence" : report.EventTimestamp is null ? "unresolved-event-date"
                : report.EventTimestamp < report.SinceInclusive || report.EventTimestamp >= report.UntilExclusive ? "event-outside-period" : "attributed-under-declared-replay";
            EffortRange? expected = status == "attributed-under-declared-replay" ? byRole["novel-retained-delta"].Effort
                : status == "event-outside-period" ? new EffortRange { Low = 0, Expected = 0, High = 0 } : null;
            if (report.EventAttributionStatus != status || report.EventAttributedNovelEffort != expected)
                errors.Add("Only proven availability under an explicit replay/date declaration can attribute the novel comparison; unresolved values stay unavailable.");
        }
        return errors;
    }

    private static void ValidateReplayPeriod(DateTimeOffset since, DateTimeOffset until, List<string> errors)
    {
        if (since >= until)
            errors.Add("Replay attribution requires an explicit non-empty inclusive/exclusive period.");
    }

    private static void ValidateReplayProvenance(bool hasReplay, string? replayProvenance, DateTimeOffset? date,
        string? eventProvenance, List<string> errors)
    {
        if (hasReplay != (replayProvenance is not null) || (date is not null) != (eventProvenance is not null))
            errors.Add("Every supplied replay snapshot and event date requires a separate explicit public provenance ID.");
        if (replayProvenance is not null) ValidatePublicId(replayProvenance, "replayProvenanceId", errors);
        if (eventProvenance is not null) ValidatePublicId(eventProvenance, "eventProvenanceId", errors);
    }
}
