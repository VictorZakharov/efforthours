using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Fact]
    public async Task CumulativePhaseLabelsAreCompatibleOperationalMetadataAndNeverChangeSemanticDigest()
    {
        ChangeAuthorPeriodManifest manifest = Manifest();
        ChangePortfolioComparisonReport report = ChangePortfolioComparisonBuilder.Build(await SourceReportAsync(manifest), BuildOptions(manifest));
        ChangePortfolioComparisonReport legacy = report with
        {
            Execution = report.Execution with
            {
                PhaseTimings = [.. report.Execution.PhaseTimings.Select(value => value with { ElapsedKind = null })],
                Repositories = [.. report.Execution.Repositories.Select(repository => repository with
                { PhaseTimings = [.. repository.PhaseTimings.Select(value => value with { ElapsedKind = null })] })],
            },
        };
        ChangePortfolioComparisonReport labeled = report;
        Assert.Empty(ContractValidation.Validate(legacy));
        AssertSchema(SchemaNames.ChangePortfolioComparisonReport, ContractJson.Serialize(legacy));
        Assert.Empty(ContractValidation.Validate(labeled));
        AssertSchema(SchemaNames.ChangePortfolioComparisonReport, ContractJson.Serialize(labeled));
        Assert.Equal(ChangePortfolioComparisonIdentity.ComputeSemanticDigest(legacy.SourcePortfolio!, legacy.BucketPolicy, legacy.Buckets, legacy.Series, legacy.ScopeProfile), ChangePortfolioComparisonIdentity.ComputeSemanticDigest(labeled.SourcePortfolio!, labeled.BucketPolicy, labeled.Buckets, labeled.Series, labeled.ScopeProfile));
        Assert.Contains("Cumulative work", ChangePortfolioComparisonMarkdownRenderer.Render(labeled with { View = ChangePortfolioComparisonView.Findings }), StringComparison.Ordinal);
        Assert.DoesNotContain("| Phase | Wall time |", ChangePortfolioComparisonMarkdownRenderer.Render(labeled with { View = ChangePortfolioComparisonView.Findings }), StringComparison.Ordinal);
        Assert.NotEmpty(ContractValidation.Validate(labeled with
        {
            Execution = labeled.Execution with
            { PhaseTimings = [labeled.Execution.PhaseTimings[0] with { ElapsedKind = "wall" }] }
        }));
    }
}
