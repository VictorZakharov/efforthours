using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static class ChangeHistoricalRefreshPolicy
{
    public const string LegacyPlan = "historical-note-refresh-plan/1.0.0";
    public const string CurrentPlan = "historical-note-refresh-plan/1.1.0";
    private const string Begin = "[EffortHours historical annotation]";
    private const string End = "[/EffortHours historical annotation]";
    public static ChangeHistoricalRefreshProposal Propose(ChangeWorkdayReviewReport review,
        ChangeHistoricalRefreshEntry entry, ChangeWorkdayReviewDay day, ChangeWorkRecordReview record, string fields, string policy = CurrentPlan) =>
        ProposeCore(review, entry, day, record, fields, policy, alpha39Annotation: false);

    internal static ChangeHistoricalRefreshProposal ProposeAlpha39Legacy(ChangeWorkdayReviewReport review,
        ChangeHistoricalRefreshEntry entry, ChangeWorkdayReviewDay day, ChangeWorkRecordReview record, string fields) =>
        ProposeCore(review, entry, day, record, fields, LegacyPlan, alpha39Annotation: true);

    private static ChangeHistoricalRefreshProposal ProposeCore(ChangeWorkdayReviewReport review,
        ChangeHistoricalRefreshEntry entry, ChangeWorkdayReviewDay day, ChangeWorkRecordReview record, string fields, string policy, bool alpha39Annotation)
    {
        string original = entry.Original.GetProperty("description").GetString()!;
        string noteStatus = Permission(fields != "ehe", entry.NotePermission, entry.Restriction);
        string eheStatus = Permission(fields != "notes", entry.EhePermission, entry.Restriction);
        bool current = policy == CurrentPlan;
        if (current && noteStatus == "proposed" && (record.Kind == "mixed" || record.Status is "repository-scope-unresolved" or "mixed-work-records-unresolved"))
            noteStatus = "blocked-unresolved-matching";
        string? description = null;
        if (noteStatus == "proposed")
        {
            string dateEvidence = "";
            if (review.WorkdayResolution is { } resolution)
            {
                string basis = day.WorkdayEvidenceBasis == "external-work-record" ? $"externally declared record date {day.Date}" : "not declared for allocation";
                dateEvidence = $"Workday date: {basis}; allocation policy: {resolution.Allocation.Policy}.\nDeclaration: {resolution.Allocation.WorkdayInputDigest}.\n";
            }
            if (!alpha39Annotation || review.WorkdayResolution is not null)
            {
                dateEvidence += review.AttributionCompleteness is { } completeness
                    ? $"Source declared events: {completeness.DeclaredEventStatus}; missing dates: {completeness.MissingEventDateCount}; missing replay baselines: {completeness.MissingReplayBaselineCount}.\n" +
                      (alpha39Annotation ? "" : $"Source original workdays: {completeness.OriginalWorkdayStatus}; intermediate history: {completeness.IntermediateHistoryStatus}.\n")
                    : alpha39Annotation ? "Source declared events: unknown.\n" : "Source attribution completeness: unknown.\n";
            }
            string uncertainty = review.WorkdayResolution is null ? "Original workdays and intermediate history remain unresolved."
                : "Original Git workdays and intermediate history remain unresolved.";
            string explanation = current
                ? (record.Status == "unresolved-workday"
                    ? "External implementation record exists on this date without positive retained artifact-date attribution. Original daily attribution is unresolved.\n"
                    : "Retained-date attribution and externally logged activity are separate evidence.\n") +
                  "Review, coordination, testing execution, debugging and integration labor without a retained artifact delta are not measured by Change EHE.\n"
                : "";
            string annotation = $"{Begin}\nRetained evidence: {day.RetainedEvidenceStatus}; review: {record.Status}.\n" +
                explanation + dateEvidence + uncertainty + " EHE is experimental replacement effort; zero retained EHE is not zero labor.\n" +
                $"Source: {review.SourceSemanticDigest}; records: {review.WorkRecordInputDigest}.\n{End}";
            description = Annotate(original, annotation);
            if (description is null) noteStatus = "blocked-managed-annotation";
            else if (description.Length > 8192) { description = null; noteStatus = "blocked-description-bound"; }
            else if (description == original) noteStatus = "unchanged";
        }
        decimal? contribution = null;
        if (eheStatus == "proposed")
        {
            contribution = record.AllocatedMultiplierContribution;
            if (contribution is null) eheStatus = "blocked-unresolved-attribution";
        }
        return new()
        {
            RecordId = entry.RecordId,
            Date = day.Date,
            EvidenceStatus = record.Status,
            OriginalRecordDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(ContractJson.SerializeCompact(entry.Original)),
            PriorAnnotationStatus = current ? ChangeHistoricalAnnotation.Classify(original) : null,
            ProposedNoteRecordDigest = current && description is not null ? ChangeHistoricalSnapshot.NoteDigest(entry.Original, description) : null,
            NoteStatus = noteStatus,
            ProposedDescription = description,
            EheStatus = eheStatus,
            ProposedMultiplierContribution = contribution,
        };
    }

    private static string Permission(bool requested, string permission, string restriction) => !requested ? "not-requested"
        : restriction != "none" ? "blocked-" + restriction : permission != "allowed" ? "blocked-permission-" + permission : "proposed";

    private static string? Annotate(string original, string annotation)
    {
        int begin = original.IndexOf(Begin, StringComparison.Ordinal), end = original.IndexOf(End, StringComparison.Ordinal);
        if (begin < 0 && end < 0) return original + (original.EndsWith('\n') || original.Length == 0 ? "" : "\n") + annotation;
        if (begin < 0 || end < begin || original.IndexOf(Begin, begin + Begin.Length, StringComparison.Ordinal) >= 0 ||
            original.IndexOf(End, end + End.Length, StringComparison.Ordinal) >= 0) return null;
        return original[..begin] + annotation + original[(end + End.Length)..];
    }

}
