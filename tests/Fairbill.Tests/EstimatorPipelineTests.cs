using Fairbill.Contracts;
using Fairbill.Contracts.V1;
using Fairbill.Core;
using Fairbill.Estimation;

namespace Fairbill.Tests;

public sealed class EstimatorPipelineTests
{
    [Fact]
    public async Task DotNetAnalyzerEvidenceProducesGranularSchemaValidEstimate()
    {
        DotNetAnalyzerTests.DotNetFixtureRepository repository =
            DotNetAnalyzerTests.DotNetFixtureRepository.Create();
        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        EstimateReport report = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Implementation);
        string json = ContractJson.Serialize(report);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.EstimateReport,
            json);
        HashSet<string> evidenceIds = evidence.Facts
            .Select(fact => fact.Id)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.ProductionImplementation);
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.DataModelingPersistenceAndMigrations);
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.ExternalIntegrationsAndProtocols);
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.IntegrationContractAndComponentTesting);
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.SecurityAndAccessibility);
        Assert.All(report.WorkItems, item =>
        {
            Assert.InRange(item.Hours.Expected, 0.5m, 8m);
            Assert.All(item.EvidenceIds, id => Assert.Contains(id, evidenceIds));
        });
        Assert.DoesNotContain(report.Diagnostics, diagnostic => diagnostic.Code == "FB1001");
        Assert.DoesNotContain(
            report.WorkItems.Where(item => item.Estimator.Id.Contains("tests", StringComparison.Ordinal)),
            item => item.EvidenceIds.Contains("tests:repository", StringComparer.Ordinal));
    }

    [Fact]
    public async Task JavaScriptAndTypeScriptEvidenceProducesBoundaryCoverageAndTokenUncertainty()
    {
        JavaScriptAnalyzerTests.JavaScriptFixtureRepository repository =
            JavaScriptAnalyzerTests.JavaScriptFixtureRepository.Create();
        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        SeedEstimator estimator = new();
        EstimateReport first = estimator.Estimate(evidence, EstimationProfile.Implementation);
        EstimateReport second = estimator.Estimate(evidence, EstimationProfile.Implementation);

        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(second));
        Assert.Contains(first.WorkItems, item => item.Estimator.Id == "seed-rule:api-surface");
        Assert.Contains(first.WorkItems, item => item.Estimator.Id == "seed-rule:ui-surface");
        Assert.Contains(first.WorkItems, item => item.Estimator.Id == "seed-rule:data-persistence");
        Assert.Contains(first.WorkItems, item => item.Estimator.Id == "seed-rule:external-integration");
        Assert.Contains(first.WorkItems, item => item.Estimator.Id == "seed-rule:coverage-achievement");
        Assert.Contains(first.WorkItems, item =>
            item.UncertaintyReasons.Any(reason => reason.Contains(
                "token-backed",
                StringComparison.Ordinal)));
        Assert.Contains(first.Categories, category =>
            category.Category == EffortCategory.EndToEndAndUiTesting);
        Assert.DoesNotContain(first.Diagnostics, diagnostic => diagnostic.Code == "FB1001");
    }

    [Fact]
    public async Task MixedRepositoryProducesBothEcosystemBackbonesWithoutFilesystemFixtures()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "Api.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
        repository.WriteText(
            "Program.cs",
            "var app = WebApplication.Create(); app.MapGet(\"/status\", () => true); app.Run();\n");
        repository.WriteText(
            "web/package.json",
            "{ \"name\": \"web-fixture\", \"dependencies\": { \"react\": \"19.0.0\" } }\n");
        repository.WriteText(
            "web/index.jsx",
            "export function App() { return <main>Status</main>; }\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        EstimateReport report = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Implementation);

        Assert.Contains(report.WorkItems, item =>
            item.Estimator.Id == "seed-rule:dotnet-source-backbone");
        Assert.Contains(report.WorkItems, item =>
            item.Estimator.Id == "seed-rule:javascript-source-backbone");
        Assert.Contains(report.WorkItems, item => item.Estimator.Id == "seed-rule:api-surface");
        Assert.Contains(report.WorkItems, item => item.Estimator.Id == "seed-rule:ui-surface");
        Assert.Contains(report.ProfessionalizationGap, item =>
            item.Estimator.Id == "seed-rule:gap-automated-tests");
        Assert.Empty(ContractValidation.Validate(report));
    }

    [Fact]
    public async Task ManifestFreeTypeScriptRepositoryReceivesFallbackEstimationScope()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "tsconfig.json",
            "{ \"compilerOptions\": { \"strict\": true } }\n");
        repository.WriteText(
            "src/index.ts",
            "export function calculate(value: number) { return value > 0 ? value * 2 : 0; }\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        EstimateReport report = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Implementation);

        Assert.Contains(report.WorkItems, item =>
            item.Estimator.Id == "seed-rule:javascript-source-backbone");
        Assert.Contains(report.WorkItems, item => item.Scope == ".");
        Assert.True(report.TotalEffort.Expected > 0m);
        Assert.Empty(ContractValidation.Validate(report));
    }
}
