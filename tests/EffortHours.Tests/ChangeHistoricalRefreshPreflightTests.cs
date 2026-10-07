using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Fact]
    public async Task RefreshPreflightDetectsConcurrentEditsAndAlreadyCurrentNotesWithoutMutatingSnapshots()
    {
        var source = await WorkdaySourceAsync();
        var review = ChangeWorkdayReviewer.Review(source, WorkRecords(source, Record(source, "a", 0)), ChangeWorkdayReviewPolicies.EqualEntries);
        var input = RefreshInput(review, RefreshEntry("a"));
        var plan = ChangeHistoricalRefreshPlanner.Plan(review, input, "both");
        var ready = ChangeHistoricalRefreshPreflight.Check(plan, input);
        Assert.Equal("ready-for-confirmation", ready.Status);
        Assert.Equal("original-match", ready.Entries[0].SnapshotStatus);
        Assert.Equal("ready-to-set", ready.Entries[0].NoteStatus);
        Assert.Equal(plan.Proposals[0].ProposedNoteRecordDigest, ready.Entries[0].ProposedNoteRecordDigest);
        Assert.NotEqual(plan.Proposals[0].OriginalRecordDigest, plan.Proposals[0].ProposedNoteRecordDigest);
        var already = input with { Entries = [input.Entries[0] with { Original = Snapshot(plan.Proposals[0].ProposedDescription!) }] };
        var repeated = ChangeHistoricalRefreshPreflight.Check(plan, already);
        Assert.Equal("planned-note-match", repeated.Entries[0].SnapshotStatus);
        Assert.Equal("already-current", repeated.Entries[0].NoteStatus);
        Assert.Equal("ready-to-set", repeated.Entries[0].EheStatus); // No inferred numeric field mapping.
        var changed = input with { Entries = [input.Entries[0] with { Original = Snapshot("User added another factual note") }] };
        var blocked = ChangeHistoricalRefreshPreflight.Check(plan, changed);
        Assert.Equal("blocked", blocked.Status);
        Assert.Equal("blocked-concurrent-edit", blocked.Entries[0].NoteStatus);
        Assert.Equal("blocked-concurrent-edit", blocked.Entries[0].EheStatus);
        Assert.Equal("Original task \u03a9\nTicket T-1", input.Entries[0].Original.GetProperty("description").GetString());
        foreach (var check in new[] { ready, repeated, blocked })
        {
            Assert.True(check.DryRun);
            Assert.True(check.RequiresEntryConfirmation);
            Assert.Empty(ContractValidation.Validate(check));
            AssertSchema(SchemaNames.ChangeHistoricalRefreshCheck, ContractJson.Serialize(check));
        }
        Assert.NotEmpty(ContractValidation.Validate(blocked with { Status = "ready-for-confirmation" }));
        Assert.NotEmpty(ContractValidation.Validate(ready with { Entries = [ready.Entries[0] with { CurrentRecordDigest = "sha256:" + new string('0', 64) }] }));
    }

    [Theory]
    [InlineData("date")]
    [InlineData("hours")]
    [InlineData("project")]
    [InlineData("task")]
    [InlineData("ticket")]
    [InlineData("billing")]
    [InlineData("provenance")]
    [InlineData("revision")]
    public async Task RefreshPreflightBindsEveryOpaqueRecordField(string field)
    {
        var source = await WorkdaySourceAsync();
        var review = ChangeWorkdayReviewer.Review(source, WorkRecords(source, Record(source, "a", 0)));
        var input = RefreshInput(review, RefreshEntry("a"));
        var plan = ChangeHistoricalRefreshPlanner.Plan(review, input);
        var snapshot = System.Text.Json.Nodes.JsonNode.Parse(input.Entries[0].Original.GetRawText())!.AsObject();
        snapshot[field] = "concurrent change";
        var current = input with { Entries = [input.Entries[0] with { Original = System.Text.Json.JsonSerializer.SerializeToElement(snapshot) }] };
        Assert.Equal("blocked-concurrent-edit", ChangeHistoricalRefreshPreflight.Check(plan, current).Entries[0].NoteStatus);
    }

    [Theory]
    [InlineData("denied", "allowed", "none", "blocked-permission-denied", "ready-to-set")]
    [InlineData("allowed", "unknown", "none", "ready-to-set", "blocked-permission-unknown")]
    [InlineData("allowed", "allowed", "locked", "blocked-locked", "blocked-locked")]
    [InlineData("allowed", "allowed", "invoiced", "blocked-invoiced", "blocked-invoiced")]
    [InlineData("allowed", "allowed", "unknown", "blocked-unknown", "blocked-unknown")]
    public async Task RefreshPreflightRechecksCurrentIndependentPermissionsAndRestrictions(string notes, string ehe, string restriction, string noteStatus, string eheStatus)
    {
        var source = await WorkdaySourceAsync();
        var review = ChangeWorkdayReviewer.Review(source, WorkRecords(source, Record(source, "a", 0)), ChangeWorkdayReviewPolicies.EqualEntries);
        var input = RefreshInput(review, RefreshEntry("a"));
        var current = input with { Entries = [input.Entries[0] with { NotePermission = notes, EhePermission = ehe, Restriction = restriction }] };
        var check = ChangeHistoricalRefreshPreflight.Check(ChangeHistoricalRefreshPlanner.Plan(review, input, "both"), current);
        Assert.Equal("blocked", check.Status);
        Assert.Equal(noteStatus, check.Entries[0].NoteStatus);
        Assert.Equal(eheStatus, check.Entries[0].EheStatus);
        var blockedPlan = ChangeHistoricalRefreshPlanner.Plan(review, current, "both");
        Assert.Equal("blocked", ChangeHistoricalRefreshPreflight.Check(blockedPlan, input).Status);
    }

    [Fact]
    public async Task RefreshPreflightRejectsChangedScopeTamperedPlanAndPartialOrExpandedSelections()
    {
        var source = await WorkdaySourceAsync();
        var review = ChangeWorkdayReviewer.Review(source, WorkRecords(source, Record(source, "a", 0), Record(source, "b", 0)));
        var input = RefreshInput(review, RefreshEntry("a"), RefreshEntry("b"));
        var plan = ChangeHistoricalRefreshPlanner.Plan(review, input);
        var deleted = ChangeHistoricalRefreshPreflight.Check(plan, input with { Entries = [] });
        Assert.Equal("blocked", deleted.Status);
        Assert.All(deleted.Entries, row => Assert.Equal("blocked-missing-record", row.NoteStatus));
        Assert.Empty(ContractValidation.Validate(deleted));
        AssertSchema(SchemaNames.ChangeHistoricalRefreshCheck, ContractJson.Serialize(deleted));
        var partial = ChangeHistoricalRefreshPreflight.Check(plan, input with { Entries = [input.Entries[0]] });
        Assert.Equal("blocked", partial.Status);
        Assert.Equal("blocked-missing-record", partial.Entries[1].NoteStatus);
        var expanded = ChangeHistoricalRefreshPreflight.Check(plan, input with { Entries = [.. input.Entries, RefreshEntry("extra")] });
        Assert.Equal("blocked", expanded.Status);
        Assert.Equal(["extra"], expanded.UnexpectedRecordIds);
        Assert.Throws<ArgumentException>(() => ChangeHistoricalRefreshPreflight.Check(plan, input with { UntilExclusiveDate = "2026-02-01" }));
        Assert.Throws<ArgumentException>(() => ChangeHistoricalRefreshPreflight.Check(plan with { Proposals = [plan.Proposals[0] with { ProposedDescription = "overwrite" }, plan.Proposals[1]] }, input));
    }

    [Fact]
    public async Task LegacyRefreshPlansRemainSchemaAndSemanticallyValidForPreflight()
    {
        var source = await WorkdaySourceAsync();
        var review = ChangeWorkdayReviewer.Review(source, WorkRecords(source, Record(source, "a", 0)));
        var input = RefreshInput(review, RefreshEntry("a"));
        var legacy = ChangeHistoricalRefreshPlanner.Plan(review, input) with
        {
            Policy = ChangeHistoricalRefreshPolicy.LegacyPlan,
            Proposals = [ChangeHistoricalRefreshPolicy.Propose(review, input.Entries[0], review.Days[0], review.Days[0].Records[0], "notes", ChangeHistoricalRefreshPolicy.LegacyPlan)],
        };
        Assert.Null(legacy.Proposals[0].PriorAnnotationStatus);
        Assert.Empty(ContractValidation.Validate(legacy));
        AssertSchema(SchemaNames.ChangeHistoricalRefreshPlan, ContractJson.Serialize(legacy));
        Assert.Equal("ready-for-confirmation", ChangeHistoricalRefreshPreflight.Check(legacy, input).Status);
    }
    [Fact]
    public async Task LegacyScopeUnresolvedProposalCannotPassNewPreflight()
    {
        var source = await WorkdaySourceAsync();
        var review = ChangeWorkdayReviewer.Review(source, WorkRecords(source, Record(source, "a", 0) with { RepositoryIds = ["wrong-scope"] }));
        var input = RefreshInput(review, RefreshEntry("a"));
        var legacy = ChangeHistoricalRefreshPlanner.Plan(review, input) with
        {
            Policy = ChangeHistoricalRefreshPolicy.LegacyPlan,
            Proposals = [ChangeHistoricalRefreshPolicy.Propose(review, input.Entries[0], review.Days[0], review.Days[0].Records[0], "notes", ChangeHistoricalRefreshPolicy.LegacyPlan)],
        };
        Assert.Equal("proposed", legacy.Proposals[0].NoteStatus);
        Assert.Empty(ContractValidation.Validate(legacy));
        Assert.Equal("blocked-unresolved-matching", ChangeHistoricalRefreshPreflight.Check(legacy, input).Entries[0].NoteStatus);
    }

}
