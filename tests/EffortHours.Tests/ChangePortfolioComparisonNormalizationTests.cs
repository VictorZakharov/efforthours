using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Fact]
    public async Task IsolatedContributorSeriesStayStableAcrossManifestMembership()
    {
        ChangeAuthorPeriodManifest combinedManifest = Manifest();
        ChangeAuthorPeriodManifest singleManifest = combinedManifest with
        {
            Contributors = [combinedManifest.Contributors[0]],
        };
        ChangePortfolioCandidate alpha = await CandidateAsync(
            "alpha",
            Since.AddDays(10),
            [Match("contributor-a", ChangePortfolioContributorMatchKind.DirectAuthor)]);
        ChangePortfolioCandidate sharedSingle = await CandidateAsync(
            "shared",
            new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero),
            [Match("contributor-a", ChangePortfolioContributorMatchKind.DirectAuthor)]);
        ChangePortfolioCandidate beta = await CandidateAsync(
            "beta",
            new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero),
            [Match("contributor-b", ChangePortfolioContributorMatchKind.DirectAuthor)]);
        ChangePortfolioCandidate sharedCombined = sharedSingle with
        {
            Attribution = sharedSingle.Attribution with
            {
                ContributorMatches =
                [
                    Match("contributor-a", ChangePortfolioContributorMatchKind.DirectAuthor),
                    Match("contributor-b", ChangePortfolioContributorMatchKind.Coauthor),
                ],
            },
        };
        ChangePortfolioReport singleSource = ChangePortfolioReconciler.Reconcile(
            Selection(singleManifest),
            [alpha, sharedSingle],
            EstimationProfile.Implementation);
        ChangePortfolioReport combinedSource = ChangePortfolioReconciler.Reconcile(
            Selection(combinedManifest),
            [alpha, beta, sharedCombined],
            EstimationProfile.Implementation);
        ChangePortfolioComparisonBuildOptions singleOptions = BuildOptions(singleManifest) with
        {
            CapacityManifest = null,
            ContributorNormalization = ChangePortfolioContributorNormalization.Isolated,
            ExecutionOverride = null,
        };
        ChangePortfolioComparisonBuildOptions combinedOptions = BuildOptions(combinedManifest) with
        {
            CapacityManifest = null,
            ContributorNormalization = ChangePortfolioContributorNormalization.Isolated,
            ExecutionOverride = null,
        };

        ChangePortfolioComparisonReport single = ChangePortfolioComparisonBuilder.Build(
            singleSource,
            singleOptions);
        ChangePortfolioComparisonReport combined = ChangePortfolioComparisonBuilder.Build(
            combinedSource,
            combinedOptions);
        ChangePortfolioComparisonSeries singleA = Assert.Single(
            single.Series,
            series => series.Id == "contributor-contributor-a");
        ChangePortfolioComparisonSeries combinedA = Assert.Single(
            combined.Series,
            series => series.Id == "contributor-contributor-a");

        Assert.Equal(ChangePortfolioSeriesKind.ContributorIsolated, combinedA.Kind);
        Assert.False(combinedA.AdditiveToPortfolio);
        Assert.Equal(singleA.TotalEffort, combinedA.TotalEffort);
        Assert.Equal(
            [.. singleA.Points.Select(point => point.Effort)],
            [.. combinedA.Points.Select(point => point.Effort)]);
        Assert.Equal(combinedSource.TotalEffort, combined.Series.Single(
            series => series.Kind == ChangePortfolioSeriesKind.Portfolio).TotalEffort);
        Assert.Empty(ContractValidation.Validate(combined));
        Assert.Equal(
            ChangePortfolioComparisonPolicies.IsolatedContributorSeriesV1,
            combined.Verification.BucketAllocationPolicy);

        string markdown = ChangePortfolioComparisonMarkdownRenderer.Render(combined);
        Assert.Contains("membership-stable isolated expected EHE", markdown, StringComparison.Ordinal);
        Assert.Contains("can overlap", markdown, StringComparison.Ordinal);
        Assert.Equal(3, combinedSource.Items.Count);
        Assert.Equal(
            4,
            combined.Series
                .Where(series => series.Kind == ChangePortfolioSeriesKind.ContributorIsolated)
                .Sum(series => series.Points.Sum(point => point.SelectedChangeCount)));
        string json = new ChangePortfolioComparisonJsonRenderer().Render(combined);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioComparisonReport,
            json);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
    }
}
