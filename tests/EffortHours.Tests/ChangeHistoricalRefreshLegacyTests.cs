using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Alpha39SavedPlansValidateOnlyTheirExactHistoricalAnnotation(bool declared, bool unknownCompleteness)
    {
        var source = await WorkdaySourceAsync();
        var records = WorkRecords(source, Record(source, "a", 0));
        var review = declared ? DeclaredReview(source, records, Declare(records)) : ChangeWorkdayReviewer.Review(source, records);
        if (unknownCompleteness) review = review with { AttributionCompleteness = null };
        var input = RefreshInput(review, RefreshEntry("a"));
        var day = review.Days[0];
        var record = day.Records[0];
        string dateEvidence = "";
        if (declared)
        {
            dateEvidence = $"Workday date: externally declared record date {day.Date}; allocation policy: equal-declared-days/1.0.0.\n" +
                $"Declaration: {review.WorkdayResolution!.Allocation.WorkdayInputDigest}.\n";
            dateEvidence += unknownCompleteness ? "Source declared events: unknown.\n" :
                $"Source declared events: {review.AttributionCompleteness!.DeclaredEventStatus}; missing dates: {review.AttributionCompleteness.MissingEventDateCount}; missing replay baselines: {review.AttributionCompleteness.MissingReplayBaselineCount}.\n";
        }
        string uncertainty = declared ? "Original Git workdays and intermediate history remain unresolved." :
            "Original workdays and intermediate history remain unresolved.";
        // Frozen alpha.39 format: no original-workday source line, and no source completeness outside declared allocation.
        string description = "Original task \u03a9\nTicket T-1\n[EffortHours historical annotation]\n" +
            $"Retained evidence: {day.RetainedEvidenceStatus}; review: {record.Status}.\n" + dateEvidence + uncertainty +
            " EHE is experimental replacement effort; zero retained EHE is not zero labor.\n" +
            $"Source: {review.SourceSemanticDigest}; records: {review.WorkRecordInputDigest}.\n[/EffortHours historical annotation]";
        var proposal = ChangeHistoricalRefreshPolicy.Propose(review, input.Entries[0], day, record, "notes", ChangeHistoricalRefreshPolicy.LegacyPlan) with
        { ProposedDescription = description };
        var plan = ChangeHistoricalRefreshPlanner.Plan(review, input) with { Policy = ChangeHistoricalRefreshPolicy.LegacyPlan, Proposals = [proposal] };
        Assert.Empty(ContractValidation.Validate(plan));
        AssertSchema(SchemaNames.ChangeHistoricalRefreshPlan, ContractJson.Serialize(plan));
        var check = ChangeHistoricalRefreshPreflight.Check(plan, input);
        Assert.Equal("ready-for-confirmation", check.Status);
        Assert.Empty(ContractValidation.Validate(check));
        var current = input with { Entries = [input.Entries[0] with { Original = Snapshot(description) }] };
        Assert.Equal("already-current", ChangeHistoricalRefreshPreflight.Check(plan, current).Entries[0].NoteStatus);
        Assert.NotEmpty(ContractValidation.Validate(plan with { Proposals = [proposal with { ProposedDescription = description + "edited" }] }));
    }
}
