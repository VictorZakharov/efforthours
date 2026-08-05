using Fairbill.Contracts;
using Fairbill.Contracts.V1;
using Fairbill.Estimation;

namespace Fairbill.Tests;

public sealed class SeedEstimatorTests
{
    private readonly SeedEstimator _estimator = new();

    [Fact]
    public void ImplementationProfileCreatesTraceableCategoryRollup()
    {
        EstimateReport report = _estimator.Estimate(
            TestRepositoryEvidence.Create(),
            EstimationProfile.Implementation);

        Assert.Equal(new EffortRange { Low = 8m, Expected = 14m, High = 24.5m }, report.TotalEffort);
        Assert.Equal(6, report.WorkItems.Count);
        Assert.All(report.WorkItems, item => Assert.NotEmpty(item.EvidenceIds));
        Assert.All(report.WorkItems, item => Assert.InRange(item.Hours.Expected, 0.5m, 8m));
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "FB1000");
        Assert.Empty(ContractValidation.Validate(report));
    }

    [Fact]
    public void RecreationProfileAddsExplicitDesignWork()
    {
        EstimateReport report = _estimator.Estimate(
            TestRepositoryEvidence.Create(),
            EstimationProfile.Recreation);

        Assert.Equal(15.5m, report.TotalEffort.Expected);
        WorkItem design = Assert.Single(
            report.WorkItems,
            item => item.Category == EffortCategory.ArchitectureAndTechnicalDesign);
        Assert.Equal(1.5m, design.Hours.Expected);
        Assert.Equal([EstimationProfile.Recreation], design.Profiles);
    }

    [Fact]
    public void UserRateChangesCostButNotEffort()
    {
        RateCard rate = new()
        {
            Id = "test-rate",
            Name = "Test rate",
            Currency = "USD",
            HourlyRate = 100m,
            Methodology = "Unit test override",
        };

        EstimateReport withoutRate = _estimator.Estimate(
            TestRepositoryEvidence.Create(),
            EstimationProfile.Implementation);
        EstimateReport withRate = _estimator.Estimate(
            TestRepositoryEvidence.Create(),
            EstimationProfile.Implementation,
            rate);

        Assert.Equal(withoutRate.TotalEffort, withRate.TotalEffort);
        Assert.NotNull(withRate.TotalCost);
        Assert.Equal(1_400m, withRate.TotalCost.Expected);
    }

    [Fact]
    public void UnsupportedEvidenceIsReportedWithoutInventingEffort()
    {
        RepositoryEvidence evidence = TestRepositoryEvidence.Create();
        EvidenceProvenance provenance = evidence.Facts[0].Provenance;
        evidence = evidence with
        {
            Facts =
            [
                .. evidence.Facts,
                TestRepositoryEvidence.Fact(
                    "unknown:item",
                    "future-ecosystem-fact",
                    "src/Future",
                    "unsupported construct",
                    provenance),
            ],
        };

        EstimateReport report = _estimator.Estimate(evidence, EstimationProfile.Implementation);

        Assert.Equal(14m, report.TotalEffort.Expected);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "FB1001");
    }
}
