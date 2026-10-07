using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static class ChangeHistoricalRefreshPreflight
{
    public static ChangeHistoricalRefreshCheck Check(ChangeHistoricalRefreshPlan plan, ChangeHistoricalRefreshManifest current)
    {
        IReadOnlyList<string> errors = [.. ContractValidation.Validate(plan), .. ContractValidation.ValidateHistoricalRefreshManifest(current, allowEmpty: true)];
        if (errors.Count != 0) throw new ArgumentException("Invalid historical refresh preflight inputs.");
        if (current.SourceSemanticDigest != plan.Input.SourceSemanticDigest || current.WorkRecordInputDigest != plan.Input.WorkRecordInputDigest ||
            current.SinceInclusiveDate != plan.Input.SinceInclusiveDate || current.UntilExclusiveDate != plan.Input.UntilExclusiveDate)
            throw new ArgumentException("Current snapshots must bind the exact planned lineage and date range.");
        return Evaluate(plan, current);
    }

    internal static ChangeHistoricalRefreshCheck Evaluate(ChangeHistoricalRefreshPlan plan, ChangeHistoricalRefreshManifest current)
    {
        var originals = plan.Input.Entries.ToDictionary(value => value.RecordId, StringComparer.Ordinal);
        var snapshots = current.Entries.ToDictionary(value => value.RecordId, StringComparer.Ordinal);
        var reviewed = plan.Review.Days.SelectMany(day => day.Records).ToDictionary(value => value.RecordId, StringComparer.Ordinal);
        List<ChangeHistoricalRefreshCheckEntry> rows = [];
        foreach (ChangeHistoricalRefreshProposal proposal in plan.Proposals.OrderBy(value => value.RecordId, StringComparer.Ordinal))
        {
            ChangeHistoricalRefreshEntry original = originals[proposal.RecordId];
            snapshots.TryGetValue(proposal.RecordId, out ChangeHistoricalRefreshEntry? fresh);
            string? digest = fresh is null ? null : ChangeHistoricalSnapshot.Digest(fresh.Original);
            string? proposedDigest = proposal.ProposedDescription is null ? null : ChangeHistoricalSnapshot.NoteDigest(original.Original, proposal.ProposedDescription);
            string snapshot = fresh is null ? "missing-record" : digest == proposal.OriginalRecordDigest ? "original-match"
                : digest == proposedDigest ? "planned-note-match" : "concurrent-edit";
            bool unresolvedMatching = reviewed[proposal.RecordId].Kind == "mixed" || proposal.EvidenceStatus is "repository-scope-unresolved" or "mixed-work-records-unresolved";
            rows.Add(new()
            {
                RecordId = proposal.RecordId,
                OriginalRecordDigest = proposal.OriginalRecordDigest,
                CurrentRecordDigest = digest,
                ProposedNoteRecordDigest = proposedDigest,
                SnapshotStatus = snapshot,
                NoteStatus = Field(proposal.NoteStatus, fresh?.NotePermission, fresh?.Restriction, snapshot, note: true, unresolvedMatching),
                EheStatus = Field(proposal.EheStatus, fresh?.EhePermission, fresh?.Restriction, snapshot, note: false, unresolvedMatching),
            });
        }
        string[] extra = [.. snapshots.Keys.Except(originals.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        return new()
        {
            Status = extra.Length != 0 || rows.Any(row => row.NoteStatus.StartsWith("blocked-", StringComparison.Ordinal) || row.EheStatus.StartsWith("blocked-", StringComparison.Ordinal))
                ? "blocked" : "ready-for-confirmation",
            PlanDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(ContractJson.SerializeCompact(plan)),
            Plan = plan,
            Current = current with { Entries = [.. current.Entries.OrderBy(value => value.RecordId, StringComparer.Ordinal)] },
            UnexpectedRecordIds = extra,
            Entries = rows,
        };
    }

    private static string Field(string planned, string? permission, string? restriction, string snapshot, bool note, bool unresolvedMatching)
    {
        if (planned == "not-requested") return planned;
        if (snapshot == "missing-record") return "blocked-missing-record";
        if (restriction != "none") return "blocked-" + restriction;
        if (permission != "allowed") return "blocked-permission-" + permission;
        if (unresolvedMatching) return "blocked-unresolved-matching";
        // A blocked plan cannot be unblocked by changing only permission observations.
        if (planned.StartsWith("blocked-", StringComparison.Ordinal)) return planned;
        if (snapshot == "concurrent-edit") return "blocked-concurrent-edit";
        return note && (snapshot == "planned-note-match" || planned == "unchanged") ? "already-current" : "ready-to-set";
    }
}
