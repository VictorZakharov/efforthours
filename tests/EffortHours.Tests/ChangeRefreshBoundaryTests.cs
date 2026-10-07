using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Fact]
    public async Task DeclaredMultiRepositoryRecordsRequireExactJointScopeAndUnambiguousAnchors()
    {
        var source = await WorkdaySourceAsync(multipleRepositories: true);
        var records = WorkRecords(source, Record(source, "a", 0), Record(source, "b", 1));
        var days = Declare(records);
        var review = DeclaredReview(source, records, days);
        Assert.Equal(["repository-a", "repository-b"], review.RepositoryIds);
        Assert.Equal(source.SourcePortfolio!.TotalEffort, review.WorkdayResolution!.Allocation.TotalEffort);
        Assert.All(review.Days, day => Assert.Equal(ChangeWorkdayPolicies.Unresolved, day.OriginalWorkdayStatus));
        Assert.Equal(review.WorkdayResolution.ExpectedMultiplierTotal, review.Days.SelectMany(day => day.Records).Sum(record => record.AllocatedMultiplierContribution));
        Assert.Empty(ContractValidation.Validate(review));
        var partial = records with { Records = [records.Records[0] with { RepositoryIds = ["repository-a"] }, records.Records[1]] };
        Assert.Throws<ArgumentException>(() => DeclaredReview(source, partial, days));
        Assert.Throws<ArgumentException>(() => DeclaredReview(source, records with { Records = [records.Records[0], records.Records[0], records.Records[1]] }, days));
        Assert.Throws<ArgumentException>(() => DeclaredReview(source, records with { Records = [records.Records[0], records.Records[1] with { Date = "2026-01-24" }] }, days));
        Assert.Throws<ArgumentException>(() => DeclaredReview(source, records, days with { Workdays = [days.Workdays[0], days.Workdays[1] with { RecordId = "a" }] }));
        Assert.Throws<ArgumentException>(() => ChangeDeclaredWorkdayReviewPolicy.Apply(review, days, review.WorkdayResolution.Allocation,
            ChangeDeclaredWorkdayReviewPolicies.EqualEntries)); // An allocated view cannot be allocated again.
        var plan = ChangeHistoricalRefreshPlanner.Plan(review, RefreshInput(review, RefreshEntry("a"), RefreshEntry("b")), "both");
        Assert.Throws<ArgumentException>(() => ChangeHistoricalRefreshPreflight.Check(plan, plan.Input with { WorkRecordInputDigest = "sha256:" + new string('0', 64) }));
    }

    [Theory]
    [InlineData("[EffortHours historical annotation]unfinished")]
    [InlineData("[/EffortHours historical annotation]orphan end")]
    [InlineData("[EffortHours historical annotation]one[/EffortHours historical annotation][EffortHours historical annotation]two[/EffortHours historical annotation]")]
    [InlineData("[EffortHours historical annotation]nested[EffortHours historical annotation]one[/EffortHours historical annotation]")]
    public async Task AmbiguousManagedBlocksStayStructuredAndCannotProduceReadyNotes(string description)
    {
        var source = await WorkdaySourceAsync();
        var review = ChangeWorkdayReviewer.Review(source, WorkRecords(source, Record(source, "a", 0)));
        var plan = ChangeHistoricalRefreshPlanner.Plan(review, RefreshInput(review, RefreshEntry("a") with { Original = Snapshot(description) }));
        Assert.StartsWith("blocked-", plan.Proposals[0].NoteStatus, StringComparison.Ordinal);
        Assert.Null(plan.Proposals[0].ProposedDescription);
        Assert.Equal(description, plan.Input.Entries[0].Original.GetProperty("description").GetString());
        var check = ChangeHistoricalRefreshPreflight.Check(plan, plan.Input);
        Assert.Equal("blocked", check.Status);
        Assert.Empty(ContractValidation.Validate(check));
        AssertSchema(SchemaNames.ChangeHistoricalRefreshCheck, ContractJson.Serialize(check));
    }

    [Fact]
    public async Task ChangedManagedNoteRemainsConflictAndIncompleteEvidenceCannotBePlanned()
    {
        var source = await WorkdaySourceAsync();
        var records = WorkRecords(source, Record(source, "a", 0));
        var review = ChangeWorkdayReviewer.Review(source, records);
        var input = RefreshInput(review, RefreshEntry("a"));
        var plan = ChangeHistoricalRefreshPlanner.Plan(review, input);
        string target = plan.Proposals[0].ProposedDescription!;
        var changed = input with { Entries = [input.Entries[0] with { Original = Snapshot(target.Replace("Retained", "Edited", StringComparison.Ordinal) + " user edit") }] };
        Assert.Equal("blocked-concurrent-edit", ChangeHistoricalRefreshPreflight.Check(plan, changed).Entries[0].NoteStatus);
        Assert.Throws<ArgumentException>(() => ChangeWorkdayReviewer.Review(source with { Status = ChangePortfolioComparisonStatus.Incomplete, SourcePortfolio = null }, records));
    }
}
