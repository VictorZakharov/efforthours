using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class SeedEstimatorTests
{
    private readonly SeedEstimator _estimator = new();

    [Fact]
    public void ImplementationProfileCreatesDeterministicTraceableLedger()
    {
        RepositoryEvidence evidence = TestRepositoryEvidence.Create();

        EstimateReport first = _estimator.Estimate(evidence, EstimationProfile.Implementation);
        EstimateReport second = _estimator.Estimate(evidence, EstimationProfile.Implementation);

        Assert.Equal(SeedEstimator.Version, first.EstimatorVersion);
        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(second));
        Assert.True(first.TotalEffort.Expected > 0m);
        Assert.NotEmpty(first.WorkItems);
        Assert.All(first.WorkItems, item =>
        {
            Assert.NotEmpty(item.EvidenceIds);
            Assert.InRange(item.Hours.Expected, 0.5m, 8m);
            Assert.Contains(EstimationProfile.Implementation, item.Profiles);
            Assert.StartsWith("seed-rule:", item.Estimator.Id, StringComparison.Ordinal);
            Assert.Equal("0.4.0", item.Estimator.Version);
            Assert.Contains("Rule '", item.Reason, StringComparison.Ordinal);
        });
        Assert.Equal(
            first.TotalEffort,
            ContractValidation.Sum(first.Categories.Select(category => category.Hours)));
        Assert.Contains(first.Diagnostics, diagnostic => diagnostic.Code == "FB1000");
        Assert.Contains(first.Diagnostics, diagnostic => diagnostic.Code == "FB1005");
        Assert.Empty(ContractValidation.Validate(first));
    }

    [Fact]
    public void RecreationProfileAddsExplicitDecisionWork()
    {
        RepositoryEvidence evidence = TestRepositoryEvidence.CreateStructuredDotNet(endpoints: 3);
        EstimateReport implementation = _estimator.Estimate(
            evidence,
            EstimationProfile.Implementation);
        EstimateReport recreation = _estimator.Estimate(
            evidence,
            EstimationProfile.Recreation);

        Assert.True(recreation.TotalEffort.Expected > implementation.TotalEffort.Expected);
        WorkItem[] recreationOnly = [.. recreation.WorkItems.Where(item =>
            item.Profiles.SequenceEqual([EstimationProfile.Recreation]))];
        Assert.NotEmpty(recreationOnly);
        Assert.All(recreationOnly, item =>
            Assert.Equal(EffortCategory.ArchitectureAndTechnicalDesign, item.Category));
        Assert.DoesNotContain(
            implementation.WorkItems,
            item => item.Profiles.SequenceEqual([EstimationProfile.Recreation]));
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
        Assert.Equal(withRate.TotalEffort.Expected * 100m, withRate.TotalCost.Expected);
    }

    [Fact]
    public void UnknownEvidenceIsReportedWithoutInventingEffort()
    {
        RepositoryEvidence baselineEvidence = TestRepositoryEvidence.Create();
        EstimateReport baseline = _estimator.Estimate(
            baselineEvidence,
            EstimationProfile.Implementation);
        EvidenceProvenance provenance = baselineEvidence.Facts[0].Provenance;
        RepositoryEvidence extendedEvidence = baselineEvidence with
        {
            Facts =
            [
                .. baselineEvidence.Facts,
                TestRepositoryEvidence.Fact(
                    "unknown:item",
                    "future-ecosystem-fact",
                    "src/Future",
                    "unsupported construct",
                    provenance),
            ],
        };

        EstimateReport extended = _estimator.Estimate(
            extendedEvidence,
            EstimationProfile.Implementation);

        Assert.Equal(baseline.TotalEffort, extended.TotalEffort);
        Diagnostic diagnostic = Assert.Single(
            extended.Diagnostics,
            diagnostic => diagnostic.Code == "FB1001");
        Assert.Contains("unknown:item", diagnostic.EvidenceIds);
    }

    [Fact]
    public void ExactDuplicateSourceDoesNotIncreaseRepresentedEffort()
    {
        EstimateReport single = _estimator.Estimate(
            TestRepositoryEvidence.CreateStructuredDotNet(sourceCopies: 1, endpoints: 3),
            EstimationProfile.Implementation);
        EstimateReport duplicated = _estimator.Estimate(
            TestRepositoryEvidence.CreateStructuredDotNet(sourceCopies: 2, endpoints: 3),
            EstimationProfile.Implementation);

        Assert.Equal(single.TotalEffort.Expected, duplicated.TotalEffort.Expected);
        Assert.Contains(duplicated.Diagnostics, diagnostic => diagnostic.Code == "FB1003");
        Assert.Contains(
            duplicated.WorkItems,
            item => item.Reason.Contains("Exact duplicate source was normalized", StringComparison.Ordinal) ||
                item.Assumptions.Any(assumption => assumption.Contains(
                    "Exact duplicate source was normalized",
                    StringComparison.Ordinal)));
    }

    [Fact]
    public void ExactDuplicateTypeScriptSourceDoesNotIncreaseRepresentedEffort()
    {
        EstimateReport single = _estimator.Estimate(
            TestRepositoryEvidence.CreateStructuredTypeScript(sourceCopies: 1),
            EstimationProfile.Implementation);
        EstimateReport duplicated = _estimator.Estimate(
            TestRepositoryEvidence.CreateStructuredTypeScript(sourceCopies: 2),
            EstimationProfile.Implementation);

        Assert.Equal(single.TotalEffort, duplicated.TotalEffort);
        Assert.Contains(duplicated.Diagnostics, diagnostic => diagnostic.Code == "FB1003");
        Assert.Contains(
            duplicated.WorkItems,
            item => item.Assumptions.Any(assumption => assumption.Contains(
                "Exact duplicate source was normalized",
                StringComparison.Ordinal)));
    }

    [Fact]
    public void GeneratedFileMetadataDoesNotIncreaseRepresentedEffort()
    {
        EstimateReport baseline = _estimator.Estimate(
            TestRepositoryEvidence.CreateStructuredDotNet(),
            EstimationProfile.Implementation);
        EstimateReport withGenerated = _estimator.Estimate(
            TestRepositoryEvidence.CreateStructuredDotNet(includeGeneratedFile: true),
            EstimationProfile.Implementation);

        Assert.Equal(baseline.TotalEffort, withGenerated.TotalEffort);
    }

    [Fact]
    public void MeaningfulApiBehaviorIncreasesAppropriateEffort()
    {
        EstimateReport withoutApi = _estimator.Estimate(
            TestRepositoryEvidence.CreateStructuredDotNet(),
            EstimationProfile.Implementation);
        EstimateReport withApi = _estimator.Estimate(
            TestRepositoryEvidence.CreateStructuredDotNet(endpoints: 6),
            EstimationProfile.Implementation);

        Assert.True(withApi.TotalEffort.Expected > withoutApi.TotalEffort.Expected);
        Assert.Contains(
            withApi.WorkItems,
            item => item.Estimator.Id == "seed-rule:api-surface" &&
                item.Category == EffortCategory.ProductionImplementation);
    }

    [Fact]
    public void HigherDeclaredCoverageRepresentsMoreTestingEffort()
    {
        EstimateReport eightyPercent = _estimator.Estimate(
            TestRepositoryEvidence.CreateStructuredDotNet(
                testCases: 12,
                declaredCoverage: 80m),
            EstimationProfile.Implementation);
        EstimateReport fullCoverage = _estimator.Estimate(
            TestRepositoryEvidence.CreateStructuredDotNet(
                testCases: 12,
                declaredCoverage: 100m),
            EstimationProfile.Implementation);

        decimal eightyTesting = TestingHours(eightyPercent);
        decimal fullTesting = TestingHours(fullCoverage);
        Assert.True(fullTesting > eightyTesting);
        WorkItem coverage = Assert.Single(
            fullCoverage.WorkItems,
            item => item.Estimator.Id == "seed-rule:coverage-achievement");
        Assert.Contains(
            coverage.Assumptions,
            assumption => assumption.Contains("declared", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            coverage.UncertaintyReasons,
            reason => reason.Contains("not executed or measured", StringComparison.Ordinal));
    }

    [Fact]
    public void ProfessionalizationGapIsExcludedFromEffortAndCost()
    {
        RateCard rate = new()
        {
            Id = "gap-test-rate",
            Name = "Gap test rate",
            Currency = "USD",
            HourlyRate = 100m,
            Methodology = "Unit test override",
        };
        EstimateReport report = _estimator.Estimate(
            TestRepositoryEvidence.CreateStructuredDotNet(
                includeDocumentation: false,
                includeCi: false),
            EstimationProfile.Implementation,
            rate);

        Assert.NotEmpty(report.ProfessionalizationGap);
        Assert.Contains(
            report.ProfessionalizationGap,
            item => item.Estimator.Id == "seed-rule:gap-automated-tests");
        Assert.Contains(
            report.ProfessionalizationGap,
            item => item.Estimator.Id == "seed-rule:gap-documentation");
        Assert.Contains(
            report.ProfessionalizationGap,
            item => item.Estimator.Id == "seed-rule:gap-ci");
        Assert.Equal(
            report.TotalEffort,
            ContractValidation.Sum(report.WorkItems.Select(item => item.Hours)));
        Assert.NotNull(report.TotalCost);
        Assert.Equal(report.TotalEffort.Expected * 100m, report.TotalCost.Expected);
        HashSet<string> representedIds = report.WorkItems
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(
            report.ProfessionalizationGap,
            item => Assert.DoesNotContain(item.Id, representedIds));
    }

    private static decimal TestingHours(EstimateReport report) => report.Categories
        .Where(category => category.Category is
            EffortCategory.UnitTesting or
            EffortCategory.IntegrationContractAndComponentTesting or
            EffortCategory.EndToEndAndUiTesting)
        .Sum(category => category.Hours.Expected);
}
