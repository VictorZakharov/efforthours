using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(ChangeHistoricalRefreshManifest input)
    {
        List<string> errors = [];
        RequireVersion(input.SchemaVersion, "historical refresh manifest", errors);
        ValidateDigest(input.SourceSemanticDigest, "sourceSemanticDigest", errors);
        ValidateDigest(input.WorkRecordInputDigest, "workRecordInputDigest", errors);
        ValidateWorkRecordDate(input.SinceInclusiveDate, errors);
        ValidateWorkRecordDate(input.UntilExclusiveDate, errors);
        if (string.CompareOrdinal(input.SinceInclusiveDate, input.UntilExclusiveDate) >= 0) errors.Add("Refresh range must be nonempty.");
        if (input.Entries.Count is < 1 or > 4096) errors.Add("Supply 1-4096 explicitly selected entries.");
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (ChangeHistoricalRefreshEntry entry in input.Entries)
        {
            ValidatePublicId(entry.RecordId, "recordId", errors);
            if (!ids.Add(entry.RecordId)) errors.Add("Refresh entry IDs must be unique.");
            if (entry.NotePermission is not ("allowed" or "denied" or "unknown") || entry.EhePermission is not ("allowed" or "denied" or "unknown") ||
                entry.Restriction is not ("none" or "locked" or "invoiced" or "unknown")) errors.Add("Explicit independent permissions and restriction state are required.");
            if (entry.Original.ValueKind != JsonValueKind.Object ||
                !entry.Original.TryGetProperty("description", out JsonElement description) || description.ValueKind != JsonValueKind.String ||
                description.GetString()!.Length > 8192 || entry.Original.GetRawText().Length > 65536)
                errors.Add("Original entry must be a bounded snapshot with a string description.");
        }
        return errors;
    }

    public static IReadOnlyList<string> Validate(ChangeHistoricalRefreshPlan plan)
    {
        List<string> errors = [.. Validate(plan.Input), .. Validate(plan.Review)];
        RequireVersion(plan.SchemaVersion, "historical refresh plan", errors);
        if (plan.Policy != "historical-note-refresh-plan/1.0.0" || !plan.DryRun || !plan.RequiresEntryConfirmation ||
            plan.RequestedFields is not ("notes" or "ehe" or "both")) errors.Add("Refresh plans must be read-only and require separate entry confirmation.");
        if (plan.Input.SourceSemanticDigest != plan.Review.SourceSemanticDigest || plan.Input.WorkRecordInputDigest != plan.Review.WorkRecordInputDigest)
            errors.Add("Refresh must bind exact source/review provenance.");
        if (plan.Proposals.Count != plan.Input.Entries.Count || plan.Proposals.Select(value => value.RecordId).Distinct(StringComparer.Ordinal).Count() != plan.Proposals.Count)
            errors.Add("Every selected entry requires exactly one proposal.");
        var entries = plan.Input.Entries.GroupBy(value => value.RecordId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var records = plan.Review.Days.SelectMany(day => day.Records.Select(record => (day, record)))
            .GroupBy(value => value.record.RecordId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        if (plan.RequestedFields != "notes")
            foreach (ChangeWorkdayReviewDay day in plan.Review.Days.Where(day => day.Records.Any(record => entries.ContainsKey(record.RecordId) && record.AllocatedMultiplierContribution is not null)))
                if (day.Records.Any(record => record.AllocatedMultiplierContribution is not null && !entries.ContainsKey(record.RecordId)))
                    errors.Add("EHE refresh must select all matched entries on an affected day.");
        foreach (ChangeHistoricalRefreshProposal proposal in plan.Proposals)
        {
            ChangeHistoricalRefreshEntry? entry = entries.GetValueOrDefault(proposal.RecordId);
            var (day, record) = records.GetValueOrDefault(proposal.RecordId);
            if (entry is null || record is null) { errors.Add("Proposal must match an explicit reviewed entry."); continue; }
            if (proposal.Date != day.Date || proposal.EvidenceStatus != record.Status ||
                string.CompareOrdinal(proposal.Date, plan.Input.SinceInclusiveDate) < 0 || string.CompareOrdinal(proposal.Date, plan.Input.UntilExclusiveDate) >= 0)
                errors.Add("Proposal must retain exact reviewed date, scope and evidence state.");
            if (entry.Original.ValueKind != JsonValueKind.Object) continue;
            if (proposal.OriginalRecordDigest != ChangePortfolioComparisonIdentity.ComputeTextDigest(ContractJson.SerializeCompact(entry.Original)))
                errors.Add("Proposal must bind the complete untouched original snapshot.");
            if (errors.Count == 0 && proposal != ChangeHistoricalRefreshPolicy.Propose(plan.Review, entry, day, record, plan.RequestedFields))
                errors.Add("Proposal must preserve the exact annotation, permission/restriction state and conserved reviewed contribution.");
        }
        return errors;
    }
}
