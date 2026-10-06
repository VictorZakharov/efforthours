using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task DeclaredDaysConserveEveryRangeCategoryAndCapacityWithStableCentRemainders(int count)
    {
        ChangePortfolioComparisonReport source = await WorkdaySourceAsync();
        ChangeWorkdayManifest manifest = Workdays(source, count);
        ChangeWorkdayAllocationReport result = ChangeWorkdayAllocator.Allocate(source, manifest, ChangeWorkdayPolicies.EqualDeclaredDaysV1);
        Assert.Empty(ContractValidation.Validate(result));
        AssertSchema(SchemaNames.ChangeWorkdayManifest, ContractJson.Serialize(manifest));
        AssertSchema(SchemaNames.ChangeWorkdayAllocationReport, ContractJson.Serialize(result));
        Assert.Equal(source.SourcePortfolio!.TotalEffort, Sum(result.Days.Select(day => day.AllocatedEffort)));
        Assert.Equal(40, result.TotalCapacityHours);
        Assert.All(result.Categories, category => Assert.Equal(category.Hours,
            Sum(result.Days.SelectMany(day => day.Categories).Where(value => value.Category == category.Category).Select(value => value.Hours))));
        Assert.All(result.Days.Where(day => day.RecordId is null), day => Assert.Equal(0, day.AllocatedEffort.High));
        ChangeWorkdayAllocationReport reversed = ChangeWorkdayAllocator.Allocate(source,
            manifest with { Workdays = [.. manifest.Workdays.Reverse()] }, ChangeWorkdayPolicies.EqualDeclaredDaysV1);
        Assert.Equal(ContractJson.SerializeCompact(result), ContractJson.SerializeCompact(reversed));
        string markdown = ChangeWorkdayAllocationMarkdownRenderer.Render(result);
        Assert.Contains("**allocated**", markdown, StringComparison.Ordinal);
        Assert.Contains("unresolved", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', markdown);
        Assert.Contains(result.Days, day => day.SourceAttributedEffort.Expected == 0 && day.OriginalWorkdayStatus == ChangeWorkdayPolicies.Unresolved);
        Assert.NotEmpty(ContractValidation.Validate(result with { TotalEffort = result.TotalEffort with { Expected = result.TotalEffort.Expected + .01m } }));
    }

    [Fact]
    public async Task AllocationRejectsUnknownPolicyWrongDigestOutsideDatesDuplicateClaimsAndTamperedSource()
    {
        ChangePortfolioComparisonReport source = await WorkdaySourceAsync();
        ChangeWorkdayManifest manifest = Workdays(source, 3);
        Assert.Throws<ArgumentException>(() => ChangeWorkdayAllocator.Allocate(source, manifest, "automatic"));
        Assert.Throws<ArgumentException>(() => ChangeWorkdayAllocator.Allocate(source, manifest with { SourceSemanticDigest = "sha256:" + new string('0', 64) }, ChangeWorkdayPolicies.EqualDeclaredDaysV1));
        Assert.Throws<ArgumentException>(() => ChangeWorkdayAllocator.Allocate(source, manifest with
        { Workdays = [manifest.Workdays[0] with { Date = "2026-02-01" }] }, ChangeWorkdayPolicies.EqualDeclaredDaysV1));
        Assert.Throws<ArgumentException>(() => ChangeWorkdayAllocator.Allocate(source, manifest with
        { Workdays = [manifest.Workdays[0], manifest.Workdays[0]] }, ChangeWorkdayPolicies.EqualDeclaredDaysV1));
        Assert.Throws<ArgumentException>(() => ChangeWorkdayAllocator.Allocate(source with
        { Series = [.. source.Series.Select(series => series with { TotalEffort = series.TotalEffort with { Expected = 0 } })] }, manifest, ChangeWorkdayPolicies.EqualDeclaredDaysV1));
        Assert.NotEmpty(ContractValidation.Validate(manifest with { Workdays = [manifest.Workdays[0] with { Date = "2026-02-30" }] }));
    }

    [Fact]
    public async Task AllocationHandlesDstDaysAndZeroCompleteSourcesWithoutInventingLabor()
    {
        ChangePortfolioComparisonReport source = await WorkdaySourceAsync(dst: true, empty: true);
        ChangeWorkdayAllocationReport result = ChangeWorkdayAllocator.Allocate(source, Workdays(source, 5), ChangeWorkdayPolicies.EqualDeclaredDaysV1);
        Assert.Equal(23, (source.Buckets[1].UntilExclusive - source.Buckets[1].SinceInclusive).TotalHours);
        Assert.Equal(0, result.TotalEffort.High);
        Assert.Equal(40, result.TotalCapacityHours);
        Assert.All(result.Days, day => Assert.Equal(ChangeWorkdayPolicies.Unresolved, day.OriginalWorkdayStatus));
    }

    [Fact]
    public async Task UnavailableSourceTimezoneFailsAsInputWithoutAnAllocation()
    {
        ChangePortfolioComparisonReport source = await WorkdaySourceAsync();
        ChangePortfolioSelection selection = source.Selection with
        { AuthorPeriodManifest = source.Selection.AuthorPeriodManifest! with { TimeZone = "Unavailable/DeclaredZone" } };
        ChangePortfolioReport portfolio = source.SourcePortfolio! with { Selection = selection };
        source = source with
        {
            Selection = selection,
            SourcePortfolio = portfolio,
            Verification = source.Verification with
            {
                SourcePortfolioDigest = ChangePortfolioComparisonIdentity.ComputePortfolioDigest(portfolio),
                SemanticDigest = ChangePortfolioComparisonIdentity.ComputeSemanticDigest(portfolio, source.BucketPolicy,
                    source.Buckets, source.Series, source.ScopeProfile),
            },
        };
        ArgumentException failure = Assert.Throws<ArgumentException>(() => ChangeWorkdayAllocator.Allocate(source,
            Workdays(source, 3), ChangeWorkdayPolicies.EqualDeclaredDaysV1));
        Assert.Contains("timezone is unavailable", failure.Message, StringComparison.Ordinal);
    }

    private static ChangeWorkdayManifest Workdays(ChangePortfolioComparisonReport source, int count) => new()
    {
        SourceSemanticDigest = source.Verification.SemanticDigest,
        Workdays = [.. source.Buckets.Take(count).Select((bucket, index) => new ChangeDeclaredWorkday
        { RecordId = "record-" + index, Date = bucket.Label, LoggedHours = index % 2 == 0 ? 4 : 12 })],
    };

    private static async Task<ChangePortfolioComparisonReport> WorkdaySourceAsync(bool dst = false, bool empty = false, decimal referenceHours = 8m)
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(dst ? "America/Toronto" : "UTC");
        DateTime start = new(2026, dst ? 3 : 1, dst ? 7 : 19, 0, 0, 0, DateTimeKind.Unspecified);
        DateTimeOffset Instant(DateTime local) => new(local, zone.GetUtcOffset(local));
        ChangeAuthorPeriodManifest manifest = Manifest();
        manifest = manifest with
        {
            Selection = manifest.Selection with { SinceInclusive = Instant(start), UntilExclusive = Instant(start.AddDays(5)), TimeZone = zone.Id },
            Contributors = [manifest.Contributors[0]],
        };
        ChangePortfolioCandidate[] candidates = empty ? [] : [await CandidateAsync("retained", Instant(start.AddHours(12)),
            [Match("contributor-a", ChangePortfolioContributorMatchKind.DirectAuthor)])];
        ChangePortfolioReport portfolio = ChangePortfolioReconciler.Reconcile(Selection(manifest), candidates, EstimationProfile.Implementation);
        ChangePortfolioBucketManifest buckets = new()
        {
            Buckets = [.. Enumerable.Range(0, 5).Select(index => new ChangePortfolioBucketDefinition
            { Id = "day-" + index, Label = start.AddDays(index).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                SinceInclusive = Instant(start.AddDays(index)), UntilExclusive = Instant(start.AddDays(index + 1)) })],
        };
        return ChangePortfolioComparisonBuilder.Build(portfolio, BuildOptions(manifest) with
        {
            ExecutionOverride = null,
            BucketKind = ChangePortfolioBucketPolicyKind.CalendarDay,
            BucketPolicy = ChangePortfolioComparisonPolicies.CalendarDayV1,
            BucketManifest = buckets,
            Buckets = [.. buckets.Buckets.Select(bucket => new ChangePortfolioComparisonBucket
            { Id = bucket.Id, Label = bucket.Label, SinceInclusive = bucket.SinceInclusive, UntilExclusive = bucket.UntilExclusive })],
            CapacityManifest = new()
            {
                CalendarPolicy = "fixed-eight",
                Entries = [.. buckets.Buckets.Select(bucket => new ChangePortfolioCapacityEntry
            { BucketId = bucket.Id, ContributorId = "contributor-a", Hours = referenceHours })]
            },
        });
    }
}
