using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class CalibrationUncertaintyGraphFeatureTests
{
    [Fact]
    public async Task DotNetGraphProjectionIsDeterministicSchemaValidAndDiagnosticOnly()
    {
        (EstimateReport estimate, RepositoryEvidence evidence) = await CreateDotNetGraphAsync();
        string estimateBefore = ContractJson.SerializeCompact(estimate);

        CalibrationUncertaintyGraphFeatureReport first =
            CalibrationUncertaintyGraphFeatureProjector.Project(estimate, evidence);
        CalibrationUncertaintyGraphFeatureReport second =
            CalibrationUncertaintyGraphFeatureProjector.Project(estimate, evidence);
        string json = ContractJson.Serialize(first);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintyGraphFeatures,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(first));
        Assert.Equal(json, ContractJson.Serialize(second));
        Assert.Equal(estimateBefore, ContractJson.SerializeCompact(estimate));
        Assert.True(first.FeatureContract.LabelIndependent);
        Assert.Equal(14, first.FeatureContract.Features.Count);
        Assert.All(first.FeatureContract.Features, feature => Assert.Equal(
            CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly,
            feature.Monotonicity));
        Assert.Equal(5, first.Summary.NodeCount);
        Assert.Equal(5, first.Summary.EdgeCount);
        Assert.Equal(2, first.Summary.CyclicNodeCount);
        Assert.True(first.Summary.MappedWorkItemCount > 0);
        Assert.Equal(4m, Feature(first, CalibrationUncertaintyGraphFeatureIds.FanOutMaximum));
        Assert.Equal(0.2m, Feature(first, CalibrationUncertaintyGraphFeatureIds.HighFanOutShare));
        Assert.Equal(0.4m, Feature(first, CalibrationUncertaintyGraphFeatureIds.CyclicNodeShare));
        Assert.Equal(
            0.4m,
            Feature(first, CalibrationUncertaintyGraphFeatureIds.LargestCyclicComponentShare));
        Assert.Equal(
            0.8m,
            Feature(first, CalibrationUncertaintyGraphFeatureIds.HighPublicInterfaceShare));
    }

    [Fact]
    public async Task DuplicateReferenceFactsDoNotMultiplyGraphDegrees()
    {
        (EstimateReport estimate, RepositoryEvidence source) = await CreateDotNetGraphAsync();
        EvidenceFact reference = source.Facts.First(fact =>
            fact.Id.StartsWith("dotnet:project-reference:A/A.csproj:", StringComparison.Ordinal));
        RepositoryEvidence evidence = source with
        {
            Facts =
            [
                .. source.Facts,
                reference with { Id = reference.Id + ":duplicate-proof" },
            ],
        };

        CalibrationUncertaintyGraphFeatureReport report =
            CalibrationUncertaintyGraphFeatureProjector.Project(estimate, evidence);
        CalibrationUncertaintyGraphEdge edge = report.Edges.Single(value =>
            value.SourceNodeId == "dotnet:project:A/A.csproj" &&
            value.TargetNodeId == "dotnet:project:B/B.csproj");

        Assert.Equal(5, report.Summary.EdgeCount);
        Assert.Equal(6, report.Summary.ResolvedLocalReferenceFactCount);
        Assert.Equal(2, edge.EvidenceIds.Count);
        Assert.Equal(4, report.Nodes.Single(node =>
            node.NodeId == "dotnet:project:A/A.csproj").FanOut);
        Assert.Empty(ContractValidation.Validate(report));
    }

    [Fact]
    public async Task DirectedAcyclicCrossEdgeDoesNotBecomeACycle()
    {
        InMemoryRepository repository = new();
        repository.WriteText("A/A.csproj", Project("../B/B.csproj", "../C/C.csproj"));
        repository.WriteText("B/B.csproj", Project("../C/C.csproj"));
        repository.WriteText("C/C.csproj", Project());
        repository.WriteText("A/A.cs", "public class A {}");
        repository.WriteText("B/B.cs", "public class B {}");
        repository.WriteText("C/C.cs", "public class C {}");
        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        EstimateReport estimate = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Implementation);

        CalibrationUncertaintyGraphFeatureReport report =
            CalibrationUncertaintyGraphFeatureProjector.Project(estimate, evidence);

        Assert.Equal(3, report.Summary.EdgeCount);
        Assert.Equal(0, report.Summary.CyclicNodeCount);
        Assert.Equal(0m, Feature(
            report,
            CalibrationUncertaintyGraphFeatureIds.CyclicNodeShare));
        Assert.Empty(ContractValidation.Validate(report));
    }

    [Fact]
    public async Task JavaScriptLocalPackagesAndExportsUseTheSameStaticBoundary()
    {
        (EstimateReport estimate, RepositoryEvidence evidence) = await CreateJavaScriptGraphAsync();
        EvidenceFact aToB = evidence.Facts.Single(fact => fact.Id ==
            "javascript:project-reference:packages/a/package.json:" +
            "packages/b/package.json:runtime");
        Assert.Equal("packages/a", aToB.Scope);
        Assert.Contains("target:packages/b/package.json", aToB.Tags);
        Assert.Equal(
            "packages/a",
            evidence.Facts.Single(fact =>
                fact.Id == "javascript:package:packages/a/package.json").Scope);

        CalibrationUncertaintyGraphFeatureReport report =
            CalibrationUncertaintyGraphFeatureProjector.Project(estimate, evidence);

        Assert.Equal(3, report.Summary.NodeCount);
        Assert.Equal(2, report.Summary.EdgeCount);
        Assert.Equal(2, report.Summary.CyclicNodeCount);
        Assert.All(report.Nodes, node => Assert.Equal("javascript", node.Ecosystem));
        Assert.All(report.Nodes, node => Assert.NotEqual(
            CalibrationUncertaintyFeatureAvailability.Unavailable,
            node.PublicInterfaceAvailability));
        Assert.Equal(
            CalibrationUncertaintyFeatureAvailability.Available,
            FeatureValue(report, CalibrationUncertaintyGraphFeatureIds.PublicInterfaceMaximum)
                .Availability);
        Assert.Empty(ContractValidation.Validate(report));
    }

    [Fact]
    public async Task IncompatibleInterfaceEvidenceFailsVisibleInsteadOfLookingPrivate()
    {
        (EstimateReport estimate, RepositoryEvidence source) = await CreateJavaScriptGraphAsync();
        EvidenceFact structure = source.Facts.First(fact =>
            fact.Id.StartsWith("javascript:source-structure:", StringComparison.Ordinal));
        RepositoryEvidence evidence = source with
        {
            Facts =
            [
                .. source.Facts.Select(fact => fact.Id == structure.Id
                    ? fact with
                    {
                        Measurements =
                        [
                            .. fact.Measurements.Where(measurement =>
                                measurement.Name != "exports"),
                        ],
                    }
                    : fact),
            ],
        };

        CalibrationUncertaintyGraphFeatureReport report =
            CalibrationUncertaintyGraphFeatureProjector.Project(estimate, evidence);

        Assert.Equal(1, report.Summary.PublicInterfaceUnavailableNodeCount);
        Assert.Equal(
            CalibrationUncertaintyFeatureAvailability.Unavailable,
            FeatureValue(report, CalibrationUncertaintyGraphFeatureIds.PublicInterfaceP50)
                .Availability);
        Assert.Empty(ContractValidation.Validate(report));
    }

    [Fact]
    public async Task UnsupportedEcosystemFactDoesNotMapByCoincidentalScope()
    {
        (EstimateReport sourceEstimate, RepositoryEvidence sourceEvidence) =
            await CreateJavaScriptGraphAsync();
        EvidenceFact template = sourceEvidence.Facts.First(fact => fact.Scope == ".");
        EvidenceFact unsupported = template with
        {
            Id = "python:synthetic-shared-scope",
        };
        RepositoryEvidence evidence = sourceEvidence with
        {
            Facts = [.. sourceEvidence.Facts, unsupported],
        };
        WorkItem selected = sourceEstimate.WorkItems[0];
        EstimateReport estimate = sourceEstimate with
        {
            WorkItems =
            [
                selected with { EvidenceIds = [unsupported.Id] },
                .. sourceEstimate.WorkItems.Skip(1),
            ],
        };

        CalibrationUncertaintyGraphFeatureReport report =
            CalibrationUncertaintyGraphFeatureProjector.Project(estimate, evidence);

        Assert.Empty(report.WorkItems.Single(item => item.WorkItemId == selected.Id).NodeIds);
        Assert.Empty(ContractValidation.Validate(report));
    }

    [Fact]
    public async Task RepositoryWithoutSupportedGraphNodesIsExplicitlyNotApplicable()
    {
        InMemoryRepository repository = new();
        repository.WriteText("app.py", "def run():\n    return 1\n");
        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        EstimateReport estimate = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Implementation);

        CalibrationUncertaintyGraphFeatureReport report =
            CalibrationUncertaintyGraphFeatureProjector.Project(estimate, evidence);

        Assert.Empty(report.Nodes);
        Assert.Empty(report.Edges);
        Assert.All(report.Features, feature => Assert.Equal(
            CalibrationUncertaintyFeatureAvailability.NotApplicable,
            feature.Availability));
        Assert.Equal(report.Summary.WorkItemCount, report.Summary.UnmappedWorkItemCount);
        Assert.Empty(ContractValidation.Validate(report));
    }

    [Fact]
    public void CanonicalGraphFeatureContractDigestIsPinned()
    {
        Assert.Equal(
            CalibrationUncertaintyVersions.GraphFeatureContractDigestV1,
            CalibrationDigest.Compute(CalibrationUncertaintyGraphFeatureCatalog.Current));
    }

    private static async Task<(EstimateReport Estimate, RepositoryEvidence Evidence)>
        CreateDotNetGraphAsync()
    {
        InMemoryRepository repository = new();
        repository.WriteText("A/A.csproj", Project("../B/B.csproj", "../C/C.csproj",
            "../D/D.csproj", "../E/E.csproj"));
        repository.WriteText("B/B.csproj", Project("../A/A.csproj"));
        repository.WriteText("C/C.csproj", Project());
        repository.WriteText("D/D.csproj", Project());
        repository.WriteText("E/E.csproj", Project());
        repository.WriteText("A/A.cs", "public class A { public void Run() {} private void Hide() {} }");
        repository.WriteText("B/B.cs", "public class B { public void Run() {} }");
        repository.WriteText("C/C.cs", "public class C {}");
        repository.WriteText("D/D.cs", "internal class D { private void Hide() {} }");
        repository.WriteText("E/E.cs", "public class E { private void Hide() {} }");
        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        return (
            new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation),
            evidence);
    }

    private static async Task<(EstimateReport Estimate, RepositoryEvidence Evidence)>
        CreateJavaScriptGraphAsync()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "package.json",
            "{\"name\":\"root\",\"private\":true,\"workspaces\":[\"packages/*\"]}");
        repository.WriteText(
            "packages/a/package.json",
            "{\"name\":\"pkg-a\",\"dependencies\":{\"pkg-b\":\"workspace:*\"}}");
        repository.WriteText(
            "packages/b/package.json",
            "{\"name\":\"pkg-b\",\"dependencies\":{\"pkg-a\":\"workspace:*\"}}");
        repository.WriteText("index.js", "export function root() { return 1; }");
        repository.WriteText("packages/a/index.js", "export function a() { return 1; }");
        repository.WriteText("packages/b/index.js", "export function b() { return 1; }");
        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        return (
            new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation),
            evidence);
    }

    private static string Project(params string[] references)
    {
        string items = string.Join(
            string.Empty,
            references.Select(reference => $"<ProjectReference Include=\"{reference}\" />"));
        return $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<TargetFramework>net10.0</TargetFramework></PropertyGroup>" +
            $"<ItemGroup>{items}</ItemGroup></Project>";
    }

    private static decimal? Feature(
        CalibrationUncertaintyGraphFeatureReport report,
        string id) => FeatureValue(report, id).Value;

    private static CalibrationUncertaintyFeatureValue FeatureValue(
        CalibrationUncertaintyGraphFeatureReport report,
        string id) => report.Features.Single(feature => feature.FeatureId == id);
}
