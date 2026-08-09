using EffortHours.Analysis;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class CoverageReportAnalyzerTests
{
    [Fact]
    public async Task BoundedReadStreamRejectsContentBeyondLimit()
    {
        await using MemoryStream source = new([1, 2, 3, 4]);
        await using BoundedReadStream bounded = new(source, 3);
        byte[] buffer = new byte[4];

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await bounded.ReadExactlyAsync(buffer));
    }

    [Fact]
    public async Task LcovMeasurementsAreScopedAndDoNotExposeReportedSourcePaths()
    {
        InMemoryRepository repository = CreateJavaScriptRepository(includeDeclaredCoverage: false);
        repository.WriteText(
            "coverage/lcov.info",
            """
            TN:
            SF:C:\private-client\checkout\src\math.js
            FNF:1
            FNH:1
            BRF:2
            BRH:1
            LF:10
            LH:8
            end_of_record
            """);

        RepositoryAnalysisPipeline pipeline = new(repository);
        RepositoryEvidence first = await pipeline.ScanAsync(repository.RootPath);
        RepositoryEvidence second = await pipeline.ScanAsync(repository.RootPath);

        EvidenceFact coverage = Assert.Single(first.Facts, IsMeasuredCoverage);
        Assert.Equal(EvidenceSourceKind.Measured, coverage.Provenance.SourceKind);
        Assert.Equal(".", coverage.Scope);
        Assert.Equal(80m, Measurement(coverage, "lines"));
        Assert.Equal(50m, Measurement(coverage, "branches"));
        Assert.Equal(100m, Measurement(coverage, "functions"));
        Assert.Equal(8m, Measurement(coverage, "lines-covered"));
        Assert.Contains("coverage-format:lcov", coverage.Tags);
        Assert.Contains("ecosystem:javascript", coverage.Tags);
        Assert.Contains(first.Diagnostics, diagnostic => diagnostic.Code == "FB2500");
        Assert.DoesNotContain("private-client", ContractJson.Serialize(coverage), StringComparison.Ordinal);
        Assert.Empty(ContractValidation.Validate(first));
        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(second));
    }

    [Fact]
    public async Task CoberturaMeasurementsUseDotNetProjectScope()
    {
        InMemoryRepository repository = new();
        repository.WriteText("src/App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        repository.WriteText(
            "src/App/Calculator.cs",
            "namespace Demo; public static class Calculator { public static int Add(int a, int b) => a + b; }\n");
        repository.WriteText(
            "coverage/coverage.cobertura.xml",
            """
            <?xml version="1.0"?>
            <coverage line-rate="0.8" branch-rate="0.5" lines-covered="8" lines-valid="10" branches-covered="1" branches-valid="2">
              <packages>
                <package name="Demo">
                  <classes>
                    <class name="Calculator" filename="src/App/Calculator.cs">
                      <methods>
                        <method name="Add"><lines><line number="1" hits="1" /></lines></method>
                      </methods>
                      <lines>
                        <line number="1" hits="1" branch="true" condition-coverage="50% (1/2)" />
                        <line number="2" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        EvidenceFact coverage = Assert.Single(evidence.Facts, IsMeasuredCoverage);
        Assert.Equal("src/App/App.csproj", coverage.Scope);
        Assert.Equal(80m, Measurement(coverage, "lines"));
        Assert.Equal(50m, Measurement(coverage, "branches"));
        Assert.Equal(100m, Measurement(coverage, "functions"));
        Assert.Contains("coverage-format:cobertura", coverage.Tags);
        Assert.Contains("ecosystem:dotnet", coverage.Tags);
    }

    [Fact]
    public async Task MeasuredCoverageSupersedesDeclaredThresholdForEstimation()
    {
        InMemoryRepository declaredRepository = CreateJavaScriptRepository(includeDeclaredCoverage: true);
        InMemoryRepository measuredOnlyRepository = CreateJavaScriptRepository(includeDeclaredCoverage: false);
        const string lcov = "SF:src/math.js\nLF:10\nLH:5\nend_of_record\n";
        declaredRepository.WriteText("coverage/lcov.info", lcov);
        measuredOnlyRepository.WriteText("coverage/lcov.info", lcov);

        RepositoryEvidence declaredEvidence = await new RepositoryAnalysisPipeline(declaredRepository)
            .ScanAsync(declaredRepository.RootPath);
        RepositoryEvidence measuredOnlyEvidence = await new RepositoryAnalysisPipeline(measuredOnlyRepository)
            .ScanAsync(measuredOnlyRepository.RootPath);
        SeedEstimator estimator = new();
        EstimateReport declaredEstimate = estimator.Estimate(
            declaredEvidence,
            EstimationProfile.Implementation);
        EstimateReport measuredOnlyEstimate = estimator.Estimate(
            measuredOnlyEvidence,
            EstimationProfile.Implementation);

        Assert.Contains(declaredEvidence.Facts, fact =>
            fact.Kind == EvidenceKinds.Coverage &&
            fact.Provenance.SourceKind == EvidenceSourceKind.DeclaredAssumed);
        WorkItem declaredCoverage = Assert.Single(
            declaredEstimate.WorkItems,
            item => item.Estimator.Id == "seed-rule:coverage-achievement");
        WorkItem measuredOnlyCoverage = Assert.Single(
            measuredOnlyEstimate.WorkItems,
            item => item.Estimator.Id == "seed-rule:coverage-achievement");
        Assert.Contains("measured", declaredCoverage.Title, StringComparison.OrdinalIgnoreCase);
        Assert.All(declaredCoverage.EvidenceIds, id => Assert.StartsWith("coverage:measured:", id));
        Assert.Contains(
            declaredCoverage.Assumptions,
            assumption => assumption.Contains("supersedes", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(measuredOnlyCoverage.Hours, declaredCoverage.Hours);
    }

    [Fact]
    public async Task ChangedCoverageArtifactIsDiscarded()
    {
        InMemoryRepository repository = CreateJavaScriptRepository(includeDeclaredCoverage: false);
        repository.WriteText("coverage/lcov.info", "SF:src/math.js\nLF:2\nLH:1\nend_of_record\n");
        RepositoryEvidence inventory = await new RepositoryScanner(repository)
            .ScanAsync(repository.RootPath);
        repository.WriteText("coverage/lcov.info", "SF:src/math.js\nLF:2\nLH:2\nend_of_record\n");

        RepositoryAnalysisContribution contribution = await new CoverageReportAnalyzer(repository)
            .AnalyzeAsync(repository.RootPath, inventory);

        Assert.Empty(contribution.Facts);
        Assert.Contains(contribution.Diagnostics, diagnostic => diagnostic.Code == "FB2502");
    }

    [Fact]
    public async Task UnmatchedCoverageSourceIsNotValued()
    {
        InMemoryRepository repository = CreateJavaScriptRepository(includeDeclaredCoverage: false);
        repository.WriteText(
            "coverage/lcov.info",
            "SF:/outside/private/unknown.js\nLF:10\nLH:10\nend_of_record\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.DoesNotContain(evidence.Facts, IsMeasuredCoverage);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB2504");
    }

    [Fact]
    public async Task UnmatchedCoverageSourceDoesNotInflateMatchedScope()
    {
        InMemoryRepository repository = CreateJavaScriptRepository(includeDeclaredCoverage: false);
        repository.WriteText(
            "coverage/lcov.info",
            "SF:src/math.js\nLF:10\nLH:5\nend_of_record\n" +
            "SF:/outside/private/unknown.js\nLF:90\nLH:90\nend_of_record\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        EvidenceFact coverage = Assert.Single(evidence.Facts, IsMeasuredCoverage);
        Assert.Equal(50m, Measurement(coverage, "lines"));
        Assert.Equal(5m, Measurement(coverage, "lines-covered"));
        Assert.Equal(10m, Measurement(coverage, "lines-total"));
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB2504");
    }

    [Fact]
    public async Task CoberturaDtdIsRejectedWithoutStructuredMeasurements()
    {
        InMemoryRepository repository = new();
        repository.WriteText("src/App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        repository.WriteText("src/App/App.cs", "public sealed class App { }\n");
        repository.WriteText(
            "coverage/coverage.xml",
            """
            <?xml version="1.0"?>
            <!DOCTYPE coverage [<!ENTITY unsafe SYSTEM "file:///private/client.txt">]>
            <coverage lines-covered="1" lines-valid="1">
              <packages><package name="&unsafe;" /></packages>
            </coverage>
            """);

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.DoesNotContain(evidence.Facts, IsMeasuredCoverage);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB2503");
    }

    private static InMemoryRepository CreateJavaScriptRepository(bool includeDeclaredCoverage)
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "package.json",
            includeDeclaredCoverage
                ? """
                  {
                    "name": "coverage-demo",
                    "scripts": { "test": "jest" },
                    "devDependencies": { "jest": "30.0.0" },
                    "jest": { "coverageThreshold": { "global": { "lines": 100 } } }
                  }
                  """
                : """
                  {
                    "name": "coverage-demo",
                    "scripts": { "test": "jest" },
                    "devDependencies": { "jest": "30.0.0" }
                  }
                  """);
        repository.WriteText(
            "src/math.js",
            "export function add(a, b) { return a + b; }\n");
        repository.WriteText(
            "test/math.test.js",
            "import { add } from '../src/math.js'; test('adds', () => expect(add(1, 2)).toBe(3));\n");
        return repository;
    }

    private static bool IsMeasuredCoverage(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.Coverage &&
        fact.Tags.Contains("coverage:measured", StringComparer.Ordinal);

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;
}
