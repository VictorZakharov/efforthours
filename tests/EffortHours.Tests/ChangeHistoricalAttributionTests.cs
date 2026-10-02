using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Fact]
    public async Task HistoricalDailyEvidenceDoesNotCertifyBlankDatesAsWorkZeros()
    {
        DateTimeOffset since = new(2026, 1, 19, 5, 0, 0, TimeSpan.Zero);
        DateTimeOffset until = since.AddDays(5);
        DateTimeOffset asOf = until.AddMonths(2);
        ChangeAuthorPeriodManifest manifest = TodayManifest(since, until);
        ChangePortfolioCandidate candidate = await CandidateAsync("retained", since.AddHours(12),
            [Match("me", ChangePortfolioContributorMatchKind.DirectAuthor)]);
        ChangePortfolioReport source = ChangePortfolioReconciler.Reconcile(Selection(manifest), [candidate], EstimationProfile.Implementation);
        ChangePortfolioComparisonInputs inputs = ChangePortfolioComparisonInputLoader.CreateNamedPeriod(
            source.Selection.AuthorPeriodManifest!, ChangePortfolioNativePeriodKind.CustomRange,
            ChangePortfolioNativeBreakdown.CalendarDay, 8m);
        ChangePortfolioComparisonBuildOptions options = TodayBuildOptions(manifest, inputs, asOf, 1) with
        {
            Discovery = TodayBuildOptions(manifest, inputs, asOf, 1).Discovery! with
            {
                HistoricalPullRequestCount = 0,
                HistoricalPullRequestHeadCount = 0,
            },
            NativePeriod = new ChangePortfolioNativePeriod
            {
                Kind = ChangePortfolioNativePeriodKind.CustomRange,
                Breakdown = ChangePortfolioNativeBreakdown.CalendarDay,
                CapacityHoursPerDay = 8m,
                RetainedHistory = true,
                ContributorSelection = new ChangePortfolioContributorSelection
                {
                    Mode = ChangePortfolioContributorSelectionMode.SingleContributor,
                    Complete = true,
                    EligiblePopulationCount = 1,
                    IncludedContributorIds = ["me"],
                    InputDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest("single"),
                },
            },
        };
        ChangePortfolioComparisonReport report = ChangePortfolioComparisonBuilder.Build(source, options);
        Assert.Equal("measured-retained-change", report.NativePeriod!.DailyEvidence![0].State);
        Assert.All(report.NativePeriod.DailyEvidence.Skip(1), day => Assert.Equal("no-retained-change", day.State));
        Assert.Equal(40m, report.Series.Single(series => series.Kind == ChangePortfolioSeriesKind.Portfolio).TotalCapacityHours);
        string json = new ChangePortfolioComparisonJsonRenderer().Render(report);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioComparisonReport, json);
        Assert.True(schema.IsValid, string.Join('\n', schema.Errors));
        Assert.Contains("not proof that no work occurred", ChangePortfolioPeriodMarkdownRenderer.Render(report));
        Assert.DoesNotContain("verified-zero", json, StringComparison.Ordinal);
    }
}
