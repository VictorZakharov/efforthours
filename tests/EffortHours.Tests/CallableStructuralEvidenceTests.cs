using EffortHours.Analysis;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class CallableStructuralEvidenceTests
{
    [Fact]
    public void DistributionUsesFrozenNearestRankAndThresholdRules()
    {
        IReadOnlyList<EvidenceMeasurement> measurements =
            CallableStructuralMeasurements.Build(
                [
                    new CallableStructuralMetric(10, 1, 0),
                    new CallableStructuralMetric(200, 10, 4),
                    new CallableStructuralMetric(201, 11, 5),
                ],
                detectedCallables: 4,
                sourceFiles: 3,
                parserBackedFiles: 2);

        Assert.Equal(200m, Measurement(measurements, StructuralEvidenceMeasurementNames.CallableSizeP50));
        Assert.Equal(201m, Measurement(measurements, StructuralEvidenceMeasurementNames.CallableSizeP90));
        Assert.Equal(0.333333m, Measurement(measurements, StructuralEvidenceMeasurementNames.OversizedCallableShare));
        Assert.Equal(10m, Measurement(measurements, StructuralEvidenceMeasurementNames.DecisionComplexityP50));
        Assert.Equal(11m, Measurement(measurements, StructuralEvidenceMeasurementNames.DecisionComplexityMaximum));
        Assert.Equal(5m, Measurement(measurements, StructuralEvidenceMeasurementNames.NestingDepthP90));
        Assert.Equal(0.75m, Measurement(measurements, StructuralEvidenceMeasurementNames.CallableMeasurementCoverage));
        Assert.Equal(0.333333m, Measurement(measurements, StructuralEvidenceMeasurementNames.AnalyzerAmbiguityConcentration));
    }

    [Fact]
    public async Task DotNetAndJavaScriptEmitParserBackedLocalCallableDistributions()
    {
        InMemoryRepository repository = CreateMixedRepository();

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        EvidenceFact dotnet = Fact(evidence, "dotnet:source-structure:App.csproj");
        Assert.Contains(StructuralEvidenceVersions.CallableMetricsV1Tag, dotnet.Tags);
        Assert.Equal(2m, Measurement(dotnet, StructuralEvidenceMeasurementNames.DetectedCallables));
        Assert.Equal(2m, Measurement(dotnet, StructuralEvidenceMeasurementNames.MeasuredCallables));
        Assert.Equal(1m, Measurement(dotnet, StructuralEvidenceMeasurementNames.CallableMeasurementCoverage));
        Assert.Equal(0m, Measurement(dotnet, StructuralEvidenceMeasurementNames.AnalyzerAmbiguityConcentration));
        Assert.Equal(5m, Measurement(dotnet, StructuralEvidenceMeasurementNames.DecisionComplexityMaximum));
        Assert.Equal(3m, Measurement(dotnet, StructuralEvidenceMeasurementNames.NestingDepthMaximum));

        EvidenceFact javascript = Fact(evidence, "javascript:source-structure:web");
        Assert.Contains(StructuralEvidenceVersions.CallableMetricsV1Tag, javascript.Tags);
        Assert.True(Measurement(javascript, StructuralEvidenceMeasurementNames.MeasuredCallables) >= 2m);
        Assert.Equal(1m, Measurement(javascript, StructuralEvidenceMeasurementNames.CallableMeasurementCoverage));
        Assert.True(Measurement(javascript, StructuralEvidenceMeasurementNames.DecisionComplexityMaximum) >= 4m);
        Assert.True(Measurement(javascript, StructuralEvidenceMeasurementNames.NestingDepthMaximum) >= 2m);
        Assert.DoesNotContain("return <main>", ContractJson.Serialize(evidence), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FormattingAndCommentsDoNotChangeCallableDistributionsOrSeedEstimate()
    {
        InMemoryRepository compactRepository = CreateDotNetRepository(
            "public int Run(int x){if(x>0&&x<10){return x;}return 0;}");
        InMemoryRepository formattedRepository = CreateDotNetRepository(
            """
            // Layout and comments are deliberately different.
            public int Run(int x)
            {
                if (x > 0 && x < 10)
                {
                    return x;
                }

                return 0;
            }
            """);
        RepositoryEvidence compact = await new RepositoryAnalysisPipeline(compactRepository)
            .ScanAsync(compactRepository.RootPath);
        RepositoryEvidence formatted = await new RepositoryAnalysisPipeline(formattedRepository)
            .ScanAsync(formattedRepository.RootPath);
        EvidenceFact compactFact = Fact(compact, "dotnet:source-structure:App.csproj");
        EvidenceFact formattedFact = Fact(formatted, "dotnet:source-structure:App.csproj");

        Assert.Equal(
            StructuralMeasurements(compactFact),
            StructuralMeasurements(formattedFact));

        EstimateReport withStructure = new SeedEstimator().Estimate(
            compact,
            EstimationProfile.Implementation);
        RepositoryEvidence withoutStructuralEvidence = compact with
        {
            Facts =
            [
                .. compact.Facts.Select(fact => fact.Id == compactFact.Id
                    ? fact with
                    {
                        Measurements =
                        [
                            .. fact.Measurements.Where(measurement =>
                                !measurement.Name.StartsWith("structural-", StringComparison.Ordinal)),
                        ],
                        Tags =
                        [
                            .. fact.Tags.Where(tag =>
                                tag != StructuralEvidenceVersions.CallableMetricsV1Tag),
                        ],
                    }
                    : fact),
            ],
        };
        EstimateReport withoutStructuralEstimate = new SeedEstimator().Estimate(
            withoutStructuralEvidence,
            EstimationProfile.Implementation);

        Assert.Equal(
            ContractJson.Serialize(withoutStructuralEstimate),
            ContractJson.Serialize(withStructure));
    }

    [Fact]
    public async Task TypeScriptRemainsExplicitlyUnmeasuredInsteadOfReceivingGuessedComplexity()
    {
        InMemoryRepository repository = new();
        repository.WriteText("package.json", "{ \"name\": \"typescript-structure\" }\n");
        repository.WriteText(
            "src/index.ts",
            "export function choose(value: number): number { if (value > 0) return value; return 0; }\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        EvidenceFact structure = Fact(evidence, "javascript:source-structure:.");

        Assert.True(Measurement(structure, StructuralEvidenceMeasurementNames.DetectedCallables) > 0m);
        Assert.Equal(0m, Measurement(structure, StructuralEvidenceMeasurementNames.MeasuredCallables));
        Assert.Equal(0m, Measurement(structure, StructuralEvidenceMeasurementNames.CallableMeasurementCoverage));
        Assert.Equal(1m, Measurement(structure, StructuralEvidenceMeasurementNames.AnalyzerAmbiguityConcentration));
        Assert.DoesNotContain(structure.Measurements, measurement =>
            measurement.Name == StructuralEvidenceMeasurementNames.DecisionComplexityP90);
    }

    private static InMemoryRepository CreateMixedRepository()
    {
        InMemoryRepository repository = CreateDotNetRepository(
            """
            public int Complex(int x)
            {
                if (x > 0 && x < 10)
                {
                    while (x > 1)
                    {
                        if (x == 4) break;
                        x--;
                    }
                }

                return x;
            }

            public int Simple() => 1;
            """);
        repository.WriteText("web/package.json", "{ \"name\": \"web\" }\n");
        repository.WriteText(
            "web/index.js",
            "export function pick(x) { if (x > 0 && x < 10) { while (x > 1) x--; } return x; } const one = () => 1;\n");
        return repository;
    }

    private static InMemoryRepository CreateDotNetRepository(string methods)
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "App.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
        repository.WriteText("Service.cs", $"public sealed class Service {{ {methods} }}\n");
        return repository;
    }

    private static EvidenceFact Fact(RepositoryEvidence evidence, string id) =>
        Assert.Single(evidence.Facts, fact => fact.Id == id);

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Measurement(fact.Measurements, name);

    private static decimal Measurement(IEnumerable<EvidenceMeasurement> measurements, string name) =>
        Assert.Single(measurements, measurement => measurement.Name == name).Value;

    private static string StructuralMeasurements(EvidenceFact fact) => ContractJson.Serialize(
        fact.Measurements.Where(measurement =>
            measurement.Name.StartsWith("structural-", StringComparison.Ordinal)).ToArray());
}
