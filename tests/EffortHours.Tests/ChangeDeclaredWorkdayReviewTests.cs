using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Fact]
    public async Task DeclaredRecordsOnLostDatesReceiveConservedPeriodValuesWithoutChangingSourceEvidence()
    {
        var source = await WorkdaySourceAsync(referenceHours: 12m);
        string before = ContractJson.SerializeCompact(source);
        var records = WorkRecords(source, Record(source, "a", 1) with { LoggedHours = 4 }, Record(source, "b", 1) with { LoggedHours = 12 },
            Record(source, "c", 2), Record(source, "d", 3), Record(source, "e", 4), Record(source, "meeting", 0) with { Kind = "meeting", RepositoryIds = [] });
        var days = Declare(records);
        var result = DeclaredReview(source, records, days);
        Assert.Equal("reviewed-declared-workdays", result.Status);
        Assert.Equal(ChangeDeclaredWorkdayReviewPolicies.Review, result.Policy);
        decimal total = decimal.Round(source.SourcePortfolio!.TotalEffort.Expected / 8m, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(total, result.WorkdayResolution!.ExpectedMultiplierTotal);
        Assert.Equal(total, result.Days.Sum(day => day.MatchedDailyMultiplier ?? 0));
        Assert.Equal(total, result.Days.SelectMany(day => day.Records).Sum(record => record.AllocatedMultiplierContribution ?? 0));
        Assert.True(result.Days[1].AllocatedExpectedHours > 0);
        Assert.True(result.Days[1].Records.All(record => record.AllocatedMultiplierContribution > 0));
        Assert.Equal(0, result.Days[1].SourceAttributedExpectedHours);
        Assert.Equal("no-retained-change", result.Days[1].RetainedEvidenceStatus);
        Assert.Equal("external-work-record", result.Days[1].WorkdayEvidenceBasis);
        Assert.All(result.Days, day => Assert.Equal(ChangeWorkdayPolicies.Unresolved, day.OriginalWorkdayStatus));
        Assert.Equal(source.SourcePortfolio.TotalEffort, result.WorkdayResolution.Allocation.TotalEffort);
        Assert.Null(Assert.Single(result.Days[0].Records).AllocatedMultiplierContribution);
        Assert.Equal("no-declared-workday", result.Days[0].Status);
        Assert.Equal(before, ContractJson.SerializeCompact(source));
        Assert.Empty(ContractValidation.Validate(result));
        AssertSchema(SchemaNames.ChangeWorkdayReviewReport, ContractJson.Serialize(result));
        Assert.Contains("Externally allocated EHE", ChangeWorkdayReviewMarkdownRenderer.Render(result), StringComparison.Ordinal);
        var reversed = DeclaredReview(source, records with { Records = [.. records.Records.Reverse()] }, days with { Workdays = [.. days.Workdays.Reverse()] });
        Assert.Equal(ContractJson.SerializeCompact(result), ContractJson.SerializeCompact(reversed));
        var hours = DeclaredReview(source, records with { Records = [.. records.Records.Select(record => record with { LoggedHours = 1 })] }, days with { Workdays = [.. days.Workdays.Select(day => day with { LoggedHours = 24 })] });
        Assert.Equal(ContractJson.SerializeCompact(result.Days), ContractJson.SerializeCompact(hours.Days));
        Assert.NotEqual(result.WorkRecordInputDigest, hours.WorkRecordInputDigest);
        Assert.NotEqual(result.WorkdayResolution.Allocation.WorkdayInputDigest, hours.WorkdayResolution!.Allocation.WorkdayInputDigest);
        Assert.NotEmpty(ContractValidation.Validate(result with { Days = [result.Days[0], result.Days[1] with { MatchedDailyMultiplier = result.Days[1].MatchedDailyMultiplier + .01m }, .. result.Days.Skip(2)] }));
        Assert.NotEmpty(ContractValidation.Validate(result with { WorkdayResolution = result.WorkdayResolution with { ExpectedMultiplierTotal = total + .01m } }));
        Assert.NotEmpty(ContractValidation.Validate(result with { WorkdayResolution = null }));
        Assert.False(ContractSchemaValidator.Validate(SchemaNames.ChangeWorkdayReviewReport, ContractJson.Serialize(result with { WorkdayResolution = null })).IsValid);
        Assert.False(ContractSchemaValidator.Validate(SchemaNames.ChangeWorkdayReviewReport, ContractJson.Serialize(result with { EntryPolicy = ChangeWorkdayReviewPolicies.EqualEntries })).IsValid);
        var retained = ChangeWorkdayReviewer.Review(source, records, ChangeWorkdayReviewPolicies.EqualEntries);
        Assert.Equal("unresolved-workday", retained.Days[1].Status);
        Assert.Null(retained.Days[1].MatchedDailyMultiplier);
    }

    [Theory]
    [InlineData(9875)]
    [InlineData(9876)]
    public async Task DeclaredEntryRoundingConservesOnePeriodMultiplierRatherThanFiveRoundedDays(int totalCents)
    {
        var source = await WorkdaySourceAsync();
        var records = WorkRecords(source, [.. Enumerable.Range(0, 5).Select(index => Record(source, "record-" + index, index)),
            Record(source, "extra", 0) with { LoggedHours = 12 }, Record(source, "meeting", 0) with { Kind = "meeting", RepositoryIds = [] },
            Record(source, "pto", 0) with { Kind = "pto", RepositoryIds = [] }]);
        var manifest = Declare(records);
        var retained = ChangeWorkdayReviewer.Review(source, records);
        var allocation = ChangeWorkdayAllocator.Allocate(source, manifest, ChangeWorkdayPolicies.EqualDeclaredDaysV1);
        // A synthetic saved-report pair fixes the rounding input independently of estimator priors.
        EffortRange total = new() { Low = totalCents / 100m, Expected = totalCents / 100m, High = totalCents / 100m };
        EffortRange share = new() { Low = 19.75m, Expected = 19.75m, High = 19.75m };
        EffortRange zero = new() { Low = 0, Expected = 0, High = 0 };
        CategoryEstimate category = allocation.Categories[0] with { Hours = total };
        retained = retained with { Days = [.. retained.Days.Select((day, index) => day with { SourceAttributedExpectedHours = index == 0 ? total.Expected : 0 })] };
        allocation = allocation with
        {
            TotalEffort = total,
            TotalCapacityHours = null,
            Categories = [category],
            Days = [.. allocation.Days.Select((day, index) => day with { SourceAttributedEffort = index == 0 ? total : zero,
                AllocatedEffort = index == 0 && totalCents == 9876 ? share with { Low = 19.76m, Expected = 19.76m, High = 19.76m } : share,
                CapacityHours = null, CapacityRatio = null, Categories = [category with { Hours = index == 0 && totalCents == 9876
                    ? share with { Low = 19.76m, Expected = 19.76m, High = 19.76m } : share }] })],
        };
        Assert.Empty(ContractValidation.Validate(retained));
        Assert.Empty(ContractValidation.Validate(allocation));
        var result = ChangeDeclaredWorkdayReviewPolicy.Apply(retained, manifest, allocation, ChangeDeclaredWorkdayReviewPolicies.EqualEntries);
        decimal expected = totalCents == 9875 ? 12.34m : 12.35m;
        Assert.Equal(expected, result.WorkdayResolution!.ExpectedMultiplierTotal);
        Assert.Equal([2.47m, 2.47m, 2.47m, 2.47m, totalCents == 9875 ? 2.46m : 2.47m], result.Days.Select(day => day.MatchedDailyMultiplier));
        Assert.Equal(expected, result.Days.SelectMany(day => day.Records).Sum(record => record.AllocatedMultiplierContribution));
        Assert.Equal(1.24m, result.Days[0].Records.Single(record => record.RecordId == "extra").AllocatedMultiplierContribution);
        Assert.Equal(1.23m, result.Days[0].Records.Single(record => record.RecordId == "record-0").AllocatedMultiplierContribution);
        Assert.All(result.Days[0].Records.Where(record => record.Kind is "meeting" or "pto"), record => Assert.Null(record.AllocatedMultiplierContribution));
        Assert.Equal(12.35m, result.Days.Sum(day => decimal.Round(day.AllocatedExpectedHours!.Value / 8m, 2, MidpointRounding.AwayFromZero)));
        Assert.Empty(ContractValidation.Validate(result));
        AssertSchema(SchemaNames.ChangeWorkdayReviewReport, ContractJson.Serialize(result));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclaredDstDatesNeedExplicitEntryPolicyAndEmptySourcesNeverFabricateValues(bool empty)
    {
        var source = await WorkdaySourceAsync(dst: true, empty: empty);
        var records = WorkRecords(source, Record(source, "a", 1), Record(source, "b", 2));
        var days = Declare(records);
        var noValues = ChangeDeclaredWorkdayReviewer.Review(source, records, days, ChangeWorkdayPolicies.EqualDeclaredDaysV1);
        Assert.All(noValues.Days, day => Assert.Null(day.MatchedDailyMultiplier));
        var result = DeclaredReview(source, records, days);
        Assert.Equal(23, (source.Buckets[1].UntilExclusive - source.Buckets[1].SinceInclusive).TotalHours);
        Assert.Equal("America/Toronto", result.TimeZone);
        Assert.Empty(ContractValidation.Validate(result));
        AssertSchema(SchemaNames.ChangeWorkdayReviewReport, ContractJson.Serialize(result));
        if (empty)
        {
            Assert.Equal("declared-workdays-no-retained-effort", result.Status);
            Assert.Null(result.WorkdayResolution!.ExpectedMultiplierTotal);
            Assert.All(result.Days.SelectMany(day => day.Records), record => Assert.Null(record.AllocatedMultiplierContribution));
        }
        else Assert.True(result.Days[1].MatchedDailyMultiplier > 0);
    }

    [Fact]
    public async Task DeclaredReviewRejectsPartialUnknownMixedOutOfScopeAndStaleEvidence()
    {
        var source = await WorkdaySourceAsync();
        var records = WorkRecords(source, Record(source, "a", 1), Record(source, "b", 2));
        var days = Declare(records);
        Assert.Throws<ArgumentException>(() => DeclaredReview(source, records, days with { Workdays = [days.Workdays[0]] }));
        Assert.Throws<ArgumentException>(() => DeclaredReview(source, records, days with { Workdays = [days.Workdays[0] with { RecordId = "unknown" }, days.Workdays[1]] }));
        Assert.Throws<ArgumentException>(() => DeclaredReview(source, records, days with { Workdays = [days.Workdays[0] with { Date = source.Buckets[0].Label }, days.Workdays[1]] }));
        Assert.Throws<ArgumentException>(() => DeclaredReview(source, records, days with { SourceSemanticDigest = "sha256:" + new string('0', 64) }));
        Assert.Throws<ArgumentException>(() => DeclaredReview(source, records with { Records = [records.Records[0] with { Kind = "mixed" }, records.Records[1]] }, days));
        Assert.Throws<ArgumentException>(() => DeclaredReview(source, records with { Records = [records.Records[0] with { RepositoryIds = ["other"] }, records.Records[1]] }, days));
        Assert.Throws<ArgumentException>(() => ChangeDeclaredWorkdayReviewer.Review(source, records, days, "automatic"));
        Assert.Throws<ArgumentException>(() => ChangeDeclaredWorkdayReviewer.Review(source, records, days, ChangeWorkdayPolicies.EqualDeclaredDaysV1, ChangeWorkdayReviewPolicies.EqualEntries));
        Assert.Throws<ArgumentException>(() => DeclaredReview(source with { Status = ChangePortfolioComparisonStatus.Incomplete, SourcePortfolio = null }, records, days));
    }

    [Fact]
    public async Task DeclaredRefreshPreservesDescriptionsAndRequiresCompletePeriodForNumericProposals()
    {
        var source = await WorkdaySourceAsync();
        var records = WorkRecords(source, Record(source, "a", 1), Record(source, "b", 2), Record(source, "meeting", 0) with { Kind = "meeting", RepositoryIds = [] });
        var review = DeclaredReview(source, records, Declare(records));
        var input = RefreshInput(review, RefreshEntry("a"), RefreshEntry("b"));
        var plan = ChangeHistoricalRefreshPlanner.Plan(review, input, "both");
        Assert.All(plan.Proposals, proposal =>
        {
            Assert.StartsWith("Original task \u03a9", proposal.ProposedDescription, StringComparison.Ordinal);
            Assert.Contains("externally declared record date", proposal.ProposedDescription, StringComparison.Ordinal);
            Assert.Contains(review.WorkdayResolution!.Allocation.WorkdayInputDigest, proposal.ProposedDescription, StringComparison.Ordinal);
            Assert.True(proposal.ProposedMultiplierContribution > 0);
        });
        Assert.Equal(review.WorkdayResolution!.ExpectedMultiplierTotal, plan.Proposals.Sum(proposal => proposal.ProposedMultiplierContribution));
        AssertSchema(SchemaNames.ChangeHistoricalRefreshPlan, ContractJson.Serialize(plan));
        var meeting = ChangeHistoricalRefreshPlanner.Plan(review, RefreshInput(review, RefreshEntry("meeting")));
        Assert.Contains("Workday date: not declared for allocation", meeting.Proposals[0].ProposedDescription, StringComparison.Ordinal);
        Assert.DoesNotContain("externally declared record date", meeting.Proposals[0].ProposedDescription, StringComparison.Ordinal);
        var subset = input with { Entries = [input.Entries[0]], UntilExclusiveDate = review.Days[2].Date };
        Assert.Throws<ArgumentException>(() => ChangeHistoricalRefreshPlanner.Plan(review, subset, "both"));
        Assert.Single(ChangeHistoricalRefreshPlanner.Plan(review, subset).Proposals);
        Assert.NotEmpty(ContractValidation.Validate(plan with { Input = subset, Proposals = [plan.Proposals[0]] }));
        var blocked = ChangeHistoricalRefreshPlanner.Plan(review, input with { Entries = [input.Entries[0] with { Restriction = "locked" }, input.Entries[1] with { EhePermission = "denied" }] }, "both");
        Assert.Equal("blocked-locked", blocked.Proposals[0].NoteStatus);
        Assert.Equal("blocked-locked", blocked.Proposals[0].EheStatus);
        Assert.Equal("blocked-permission-denied", blocked.Proposals[1].EheStatus);
        Assert.All(blocked.Proposals, proposal => Assert.Null(proposal.ProposedMultiplierContribution));
        var again = ChangeHistoricalRefreshPlanner.Plan(review, input with
        {
            Entries = [.. input.Entries.Select(entry => entry with
        { Original = Snapshot(plan.Proposals.Single(proposal => proposal.RecordId == entry.RecordId).ProposedDescription!) })]
        }, "both");
        Assert.All(again.Proposals, proposal => Assert.Equal("unchanged", proposal.NoteStatus));
    }

    private static ChangeWorkdayReviewReport DeclaredReview(ChangePortfolioComparisonReport source, ChangeWorkRecordManifest records, ChangeWorkdayManifest days) =>
        ChangeDeclaredWorkdayReviewer.Review(source, records, days, ChangeWorkdayPolicies.EqualDeclaredDaysV1, ChangeDeclaredWorkdayReviewPolicies.EqualEntries);
    private static ChangeWorkdayManifest Declare(ChangeWorkRecordManifest records) => new()
    {
        SourceSemanticDigest = records.SourceSemanticDigest,
        Workdays = [.. records.Records.Where(record => record.Kind == "implementation")
        .GroupBy(record => record.Date, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => new ChangeDeclaredWorkday { Date = group.Key, RecordId = group.OrderBy(record => record.RecordId, StringComparer.Ordinal).First().RecordId })]
    };
}
