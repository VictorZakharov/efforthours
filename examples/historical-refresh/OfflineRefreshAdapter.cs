using System.Text.Json;
using System.Text.Json.Nodes;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Examples;

// Credential-free demonstration: no transport, disk writes or external field mapping.
public static class OfflineRefreshAdapter
{
    public sealed record NoteMutation(string RecordId, string ExpectedSnapshotDigest, string TargetSnapshotDigest, string Description);
    public sealed record EheProposal(string RecordId, string ExpectedSnapshotDigest, decimal Contribution);
    public sealed record Handoff(IReadOnlyList<NoteMutation> Notes, IReadOnlyList<EheProposal> Ehe);

    public static Handoff Prepare(ChangeHistoricalRefreshCheck receipt, string confirmedPlanDigest)
    {
        if (ContractValidation.Validate(receipt).Count != 0 || receipt.Status != "ready-for-confirmation" ||
            receipt.PlanDigest != confirmedPlanDigest || !receipt.DryRun || !receipt.RequiresEntryConfirmation ||
            receipt.UnexpectedRecordIds.Count != 0)
            throw new ArgumentException("Require a valid ready receipt and confirmation of its exact plan digest.");
        List<NoteMutation> notes = [];
        List<EheProposal> ehe = [];
        foreach (var row in receipt.Entries)
        {
            var proposal = receipt.Plan.Proposals.Single(value => value.RecordId == row.RecordId);
            if (row.NoteStatus == "ready-to-set")
                notes.Add(new(row.RecordId, row.CurrentRecordDigest!, row.ProposedNoteRecordDigest!, proposal.ProposedDescription!));
            else if (row.NoteStatus is not ("already-current" or "not-requested"))
                throw new ArgumentException("Unsupported note state.");
            if (row.EheStatus == "ready-to-set")
                ehe.Add(new(row.RecordId, row.CurrentRecordDigest!, proposal.ProposedMultiplierContribution!.Value));
            else if (row.EheStatus != "not-requested")
                throw new ArgumentException("Unsupported analytics state.");
        }
        return new(notes, ehe);
    }

    // The lock models one server-side conditional mutation, not a read-then-write API.
    // This store has no numeric writer: the consumer must explicitly map that field.
    public sealed class MemoryNoteStore(ChangeHistoricalRefreshEntry initial)
    {
        private readonly Lock gate = new();
        private ChangeHistoricalRefreshEntry current = initial;
        public ChangeHistoricalRefreshEntry Read() { lock (gate) return current; }
        public void ObserveExternalChange(ChangeHistoricalRefreshEntry fresh) { lock (gate) current = fresh; }
        public string TrySetNote(NoteMutation mutation)
        {
            lock (gate)
            {
                if (current.RecordId != mutation.RecordId || current.Restriction != "none" || current.NotePermission != "allowed")
                    return "blocked-permission-or-restriction";
                string digest = ChangeHistoricalSnapshot.Digest(current.Original);
                if (digest == mutation.TargetSnapshotDigest) return "already-current";
                if (digest != mutation.ExpectedSnapshotDigest) return "blocked-concurrent-edit";
                JsonObject snapshot = JsonNode.Parse(current.Original.GetRawText())!.AsObject();
                snapshot["description"] = mutation.Description;
                JsonElement target = JsonSerializer.SerializeToElement(snapshot);
                if (ChangeHistoricalSnapshot.Digest(target) != mutation.TargetSnapshotDigest)
                    return "blocked-target-mismatch";
                current = current with { Original = target };
                return "applied";
            }
        }
    }
}
