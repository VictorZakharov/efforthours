using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Fact]
    public async Task HistoricalRefreshPreservesSnapshotsAndIsIdempotentWithoutEditingFields()
    {
        ChangePortfolioComparisonReport source = await WorkdaySourceAsync();
        var work = WorkRecords(source, Record(source, "a", 0), Record(source, "b", 0));
        var review = ChangeWorkdayReviewer.Review(source, work, ChangeWorkdayReviewPolicies.EqualEntries);
        var input = RefreshInput(review, RefreshEntry("a"), RefreshEntry("b"));
        var plan = ChangeHistoricalRefreshPlanner.Plan(review, input, "both");
        Assert.True(plan.DryRun);
        Assert.True(plan.RequiresEntryConfirmation);
        Assert.Equal(input.Entries[0].Original.GetRawText(), plan.Input.Entries[0].Original.GetRawText());
        Assert.Equal(review.Days[0].MatchedDailyMultiplier, plan.Proposals.Sum(value => value.ProposedMultiplierContribution));
        Assert.All(plan.Proposals, value =>
        {
            Assert.StartsWith("Original task \u03a9\nTicket T-1\n", value.ProposedDescription, StringComparison.Ordinal);
            Assert.True(value.ProposedMultiplierContribution > 0);
        });
        var repeated = input with
        {
            Entries = [.. input.Entries.Select(entry => entry with
        { Original = Snapshot(plan.Proposals.Single(value => value.RecordId == entry.RecordId).ProposedDescription!) })]
        };
        var again = ChangeHistoricalRefreshPlanner.Plan(review, repeated, "both");
        Assert.All(again.Proposals, proposal => Assert.Equal("unchanged", proposal.NoteStatus));
        Assert.Equal(plan.Proposals[0].ProposedDescription, again.Proposals[0].ProposedDescription);
        Assert.Equal(1, again.Proposals[0].ProposedDescription!.Split("[EffortHours historical annotation]").Length - 1);
        Assert.Empty(ContractValidation.Validate(plan));
        AssertSchema(SchemaNames.ChangeHistoricalRefreshManifest, ContractJson.Serialize(input));
        AssertSchema(SchemaNames.ChangeHistoricalRefreshPlan, ContractJson.Serialize(plan));
        Assert.NotEmpty(ContractValidation.Validate(plan with { DryRun = false }));
        Assert.NotEmpty(ContractValidation.Validate(plan with { Proposals = [plan.Proposals[0] with { ProposedDescription = "Lost original task" }, plan.Proposals[1]] }));
        Assert.Throws<ArgumentException>(() => ChangeHistoricalRefreshPlanner.Plan(review, input with { Entries = [input.Entries[0]] }, "ehe"));
        Assert.Throws<ArgumentException>(() => ChangeHistoricalRefreshPlanner.Plan(review, input with { SourceSemanticDigest = "sha256:" + new string('0', 64) }));
        Assert.Throws<ArgumentException>(() => ChangeHistoricalRefreshPlanner.Plan(review, input with { SinceInclusiveDate = "2026-01-20" }));
    }

    [Fact]
    public async Task HistoricalRefreshKeepsPermissionsIndependentAndRestrictionsExplicit()
    {
        var source = await WorkdaySourceAsync();
        var review = ChangeWorkdayReviewer.Review(source, WorkRecords(source, Record(source, "a", 0)), ChangeWorkdayReviewPolicies.EqualEntries);
        var input = RefreshInput(review, RefreshEntry("a") with { EhePermission = "denied" });
        var notes = ChangeHistoricalRefreshPlanner.Plan(review, input, "both");
        Assert.Equal("proposed", notes.Proposals[0].NoteStatus);
        Assert.Equal("blocked-permission-denied", notes.Proposals[0].EheStatus);
        Assert.Null(notes.Proposals[0].ProposedMultiplierContribution);
        foreach (string restriction in new[] { "locked", "invoiced", "unknown" })
        {
            var blocked = ChangeHistoricalRefreshPlanner.Plan(review, input with { Entries = [RefreshEntry("a") with { Restriction = restriction }] }, "both");
            Assert.Equal("blocked-" + restriction, blocked.Proposals[0].NoteStatus);
            Assert.Equal("blocked-" + restriction, blocked.Proposals[0].EheStatus);
            Assert.Null(blocked.Proposals[0].ProposedDescription);
            Assert.Null(blocked.Proposals[0].ProposedMultiplierContribution);
        }
        var numericOnly = ChangeHistoricalRefreshPlanner.Plan(review, RefreshInput(review, RefreshEntry("a") with { NotePermission = "denied" }), "ehe");
        Assert.Equal("not-requested", numericOnly.Proposals[0].NoteStatus);
        Assert.Equal("proposed", numericOnly.Proposals[0].EheStatus);
    }

    [Fact]
    public async Task HistoricalRefreshNeverTurnsLostHistoryIntoZeroWorkAndRefusesAmbiguousAnnotationBlocks()
    {
        var source = await WorkdaySourceAsync(empty: true, dst: true);
        var review = ChangeWorkdayReviewer.Review(source, WorkRecords(source, Record(source, "lost", 1)), ChangeWorkdayReviewPolicies.EqualEntries);
        var input = RefreshInput(review, RefreshEntry("lost"));
        var result = ChangeHistoricalRefreshPlanner.Plan(review, input, "both");
        Assert.Equal("unresolved-workday", result.Proposals[0].EvidenceStatus);
        Assert.Null(result.Proposals[0].ProposedMultiplierContribution);
        Assert.Equal("blocked-unresolved-attribution", result.Proposals[0].EheStatus);
        Assert.Contains("not zero labor", result.Proposals[0].ProposedDescription, StringComparison.Ordinal);
        var malformed = input with { Entries = [input.Entries[0] with { Original = Snapshot("Original [EffortHours historical annotation] incomplete") }] };
        var blocked = ChangeHistoricalRefreshPlanner.Plan(review, malformed);
        Assert.Equal("blocked-managed-annotation", blocked.Proposals[0].NoteStatus);
        Assert.Null(blocked.Proposals[0].ProposedDescription);
        var fullDescription = input with { Entries = [input.Entries[0] with { Original = Snapshot(new string('x', 8192)) }] };
        Assert.Equal("blocked-description-bound", ChangeHistoricalRefreshPlanner.Plan(review, fullDescription).Proposals[0].NoteStatus);
        Assert.NotEmpty(ContractValidation.Validate(result with { Input = input with { Entries = [input.Entries[0] with { Original = default }] } }));
    }

    private static ChangeHistoricalRefreshManifest RefreshInput(ChangeWorkdayReviewReport review, params ChangeHistoricalRefreshEntry[] entries) => new()
    {
        SourceSemanticDigest = review.SourceSemanticDigest,
        WorkRecordInputDigest = review.WorkRecordInputDigest,
        SinceInclusiveDate = review.Days[0].Date,
        UntilExclusiveDate = DateOnly.Parse(review.Days[^1].Date, System.Globalization.CultureInfo.InvariantCulture).AddDays(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        Entries = entries,
    };
    private static ChangeHistoricalRefreshEntry RefreshEntry(string id) => new()
    { RecordId = id, Original = Snapshot("Original task \u03a9\nTicket T-1"), NotePermission = "allowed", EhePermission = "allowed", Restriction = "none" };
    private static JsonElement Snapshot(string description) => JsonSerializer.SerializeToElement(new
    { description, loggedHours = 12m, project = "project", task = "task", ticket = "T-1", billing = "unchanged", provenance = new { externalId = "original-record" } });
}
