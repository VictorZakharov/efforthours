using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Fact]
    public async Task ExternalImplementationOnBlankRetainedDateIsAnExplicitDiscrepancy()
    {
        ChangePortfolioComparisonReport source = await WorkdaySourceAsync();
        ChangeWorkdayReviewReport result = ChangeWorkdayReviewer.Review(source, WorkRecords(source,
            Record(source, "lost", 1)));
        Assert.Equal("unresolved", result.Status);
        Assert.Equal("missing-work-record", result.Days[0].Status);
        Assert.Equal("unresolved-workday", result.Days[1].Status);
        Assert.Equal("no-retained-change", result.Days[1].RetainedEvidenceStatus);
        Assert.Null(result.Days[1].MatchedDailyMultiplier);
        Assert.Null(Assert.Single(result.Days[1].Records).AllocatedMultiplierContribution);
        Assert.All(result.Days, day => Assert.Equal(ChangeWorkdayPolicies.Unresolved, day.OriginalWorkdayStatus));
        Assert.Empty(ContractValidation.Validate(result));
        AssertSchema(SchemaNames.ChangeWorkRecordManifest, ContractJson.Serialize(WorkRecords(source, Record(source, "lost", 1))));
        AssertSchema(SchemaNames.ChangeWorkdayReviewReport, ContractJson.Serialize(result));
        Assert.Contains("unavailable", ChangeWorkdayReviewMarkdownRenderer.Render(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitEntryAllocationConservesRoundedDailyMultiplierWithFixedEightAndNoLoggedWeights()
    {
        ChangePortfolioComparisonReport source = await WorkdaySourceAsync(referenceHours: 12m);
        ChangeWorkRecordManifest manifest = WorkRecords(source,
            Record(source, "c", 0) with { LoggedHours = 12m }, Record(source, "a", 0) with { LoggedHours = 4m },
            Record(source, "b", 0), Record(source, "meeting", 0) with { Kind = "meeting", RepositoryIds = [] },
            Record(source, "pto", 2) with { Kind = "pto", RepositoryIds = [] });
        ChangeWorkdayReviewReport result = ChangeWorkdayReviewer.Review(source, manifest, ChangeWorkdayReviewPolicies.EqualEntries);
        Assert.Equal("reviewed-retained-attribution", result.Status);
        Assert.Equal(8m, result.ReferenceHoursPerDay);
        decimal expected = decimal.Round(result.Days[0].SourceAttributedExpectedHours / 8m, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(expected, result.Days[0].MatchedDailyMultiplier);
        Assert.Equal(expected, result.Days[0].Records.Sum(record => record.AllocatedMultiplierContribution ?? 0m));
        Assert.All(result.Days.SelectMany(day => day.Records).Where(record => record.Kind == "implementation"),
            record => Assert.Equal(decimal.Round(record.AllocatedMultiplierContribution!.Value, 2), record.AllocatedMultiplierContribution));
        Assert.Null(result.Days[0].Records.Single(record => record.Kind == "meeting").AllocatedMultiplierContribution);
        Assert.Equal("excluded-non-implementation", Assert.Single(result.Days[2].Records).Status);
        Assert.Empty(ContractValidation.Validate(result));
        AssertSchema(SchemaNames.ChangeWorkdayReviewReport, ContractJson.Serialize(result));
        ChangeWorkdayReviewReport reordered = ChangeWorkdayReviewer.Review(source,
            manifest with { Records = [.. manifest.Records.Reverse()] }, ChangeWorkdayReviewPolicies.EqualEntries);
        Assert.Equal(ContractJson.SerializeCompact(result), ContractJson.SerializeCompact(reordered));
        ChangeWorkdayReviewReport hours = ChangeWorkdayReviewer.Review(source,
            manifest with { Records = [.. manifest.Records.Select(record => record with { LoggedHours = 1m })] }, ChangeWorkdayReviewPolicies.EqualEntries);
        Assert.NotEqual(result.WorkRecordInputDigest, hours.WorkRecordInputDigest);
        Assert.Equal(ContractJson.SerializeCompact(result.Days), ContractJson.SerializeCompact(hours.Days));
        ChangeWorkdayReviewReport noPolicy = ChangeWorkdayReviewer.Review(source, manifest);
        Assert.Null(noPolicy.Days[0].MatchedDailyMultiplier);
        Assert.All(noPolicy.Days.SelectMany(day => day.Records), record => Assert.Null(record.AllocatedMultiplierContribution));
        Assert.NotEmpty(ContractValidation.Validate(result with { ReferenceHoursPerDay = 12m }));
        Assert.NotEmpty(ContractValidation.Validate(result with { Days = [result.Days[0] with { MatchedDailyMultiplier = expected + .01m }, .. result.Days.Skip(1)] }));
    }

    [Theory]
    [InlineData("mixed", "mixed-work-records-unresolved")]
    [InlineData("implementation", "repository-scope-unresolved")]
    public async Task MixedAndCrossRepositoryRecordsCannotReceiveFabricatedContributions(string kind, string status)
    {
        ChangePortfolioComparisonReport source = await WorkdaySourceAsync();
        ChangeWorkdayReviewReport result = ChangeWorkdayReviewer.Review(source,
            WorkRecords(source, Record(source, "ambiguous", 0) with { Kind = kind, RepositoryIds = ["outside-source"] }),
            ChangeWorkdayReviewPolicies.EqualEntries);
        Assert.Equal(status, result.Days[0].Status);
        Assert.Equal("unresolved", result.Status);
        Assert.Null(result.Days[0].MatchedDailyMultiplier);
        Assert.Null(Assert.Single(result.Days[0].Records).AllocatedMultiplierContribution);
        Assert.Empty(ContractValidation.Validate(result));
        Assert.NotEmpty(ContractValidation.Validate(result with
        { Days = [result.Days[0] with { Records = [result.Days[0].Records[0] with { AllocatedMultiplierContribution = 0m }] }, .. result.Days.Skip(1)] }));
    }

    [Fact]
    public async Task CompleteEmptyEvidenceWithImplementationRecordRemainsUnresolvedAndWrongInputsFailClosed()
    {
        ChangePortfolioComparisonReport source = await WorkdaySourceAsync(dst: true, empty: true);
        ChangeWorkRecordManifest manifest = WorkRecords(source, Record(source, "lost", 1));
        ChangeWorkdayReviewReport result = ChangeWorkdayReviewer.Review(source, manifest, ChangeWorkdayReviewPolicies.EqualEntries);
        Assert.Equal("unresolved-workday", result.Days[1].Status);
        Assert.Null(Assert.Single(result.Days[1].Records).AllocatedMultiplierContribution);
        Assert.Throws<ArgumentException>(() => ChangeWorkdayReviewer.Review(source, manifest, "automatic"));
        Assert.Throws<ArgumentException>(() => ChangeWorkdayReviewer.Review(source, manifest with { SourceSemanticDigest = "sha256:" + new string('0', 64) }));
        Assert.Throws<ArgumentException>(() => ChangeWorkdayReviewer.Review(source, manifest with { Records = [manifest.Records[0], manifest.Records[0]] }));
        Assert.Throws<ArgumentException>(() => ChangeWorkdayReviewer.Review(source, manifest with { Records = [manifest.Records[0] with { Date = "2026-02-30" }] }));
        Assert.Throws<ArgumentException>(() => ChangeWorkdayReviewer.Review(source, manifest with { Records = [manifest.Records[0] with { Date = "2026-02-01" }] }));
        Assert.Throws<ArgumentException>(() => ChangeWorkdayReviewer.Review(source with { Status = ChangePortfolioComparisonStatus.Incomplete, SourcePortfolio = null }, manifest));
        Assert.NotEmpty(ContractValidation.Validate(manifest with { Records = [manifest.Records[0] with { RepositoryIds = [] }] }));
    }

    private static ChangeWorkRecordManifest WorkRecords(ChangePortfolioComparisonReport source, params ChangeWorkRecord[] records) => new()
    { SourceSemanticDigest = source.Verification.SemanticDigest, Records = records };

    private static ChangeWorkRecord Record(ChangePortfolioComparisonReport source, string id, int day) => new()
    {
        RecordId = id,
        Date = source.Buckets[day].Label,
        Kind = "implementation",
        RepositoryIds = [.. source.Selection.AuthorPeriodManifest!.Repositories.Select(repository => repository.Id)],
    };
}
