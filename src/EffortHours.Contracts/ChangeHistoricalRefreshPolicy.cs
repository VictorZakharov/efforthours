using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static class ChangeHistoricalRefreshPolicy
{
    private const string Begin = "[EffortHours historical annotation]";
    private const string End = "[/EffortHours historical annotation]";
    public static ChangeHistoricalRefreshProposal Propose(ChangeWorkdayReviewReport review,
        ChangeHistoricalRefreshEntry entry, ChangeWorkdayReviewDay day, ChangeWorkRecordReview record, string fields)
    {
        string original = entry.Original.GetProperty("description").GetString()!;
        string noteStatus = Permission(fields != "ehe", entry.NotePermission, entry.Restriction);
        string eheStatus = Permission(fields != "notes", entry.EhePermission, entry.Restriction);
        string? description = null;
        if (noteStatus == "proposed")
        {
            string annotation = $"{Begin}\nRetained evidence: {day.RetainedEvidenceStatus}; review: {record.Status}.\n" +
                "Original workdays and intermediate history remain unresolved. EHE is experimental replacement effort; zero retained EHE is not zero labor.\n" +
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
