using EffortHours.Contracts.V1;
using EffortHours.RepositoryCalibration;

namespace EffortHours.EndToEndTests;

public sealed partial class CandidatePreflightTests
{
    [Fact]
    public void LogicalCapabilityNormalizesExactDuplicateSourceAggregates()
    {
        const string sourceId = "dotnet:source-structure:src/App/App.csproj";
        WorkItem item = Item(
            "work:dotnet-source-backbone:logical:part-0001",
            "src/App/App.csproj",
            8m) with
        {
            EvidenceIds = [sourceId],
        };
        EstimateReport source = Report(item);
        EvidenceFact sourceFact = new()
        {
            Id = sourceId,
            Kind = "source-structure",
            Scope = item.Scope,
            Summary = "Synthetic aggregate source evidence.",
            Provenance = Provenance(),
            Measurements =
            [
                new EvidenceMeasurement { Name = "files", Value = 2m },
                new EvidenceMeasurement { Name = "types", Value = 20m },
                new EvidenceMeasurement { Name = "methods", Value = 100m },
                new EvidenceMeasurement { Name = "branch-points", Value = 100m },
            ],
        };
        RepositoryEvidence duplicates = Evidence(
            source,
            FileFact("common:file:one", "src/App/One.cs", 'a'),
            FileFact("common:file:two", "src/App/Two.cs", 'a'),
            sourceFact);
        RepositoryEvidence distinct = Evidence(
            source,
            FileFact("common:file:one", "src/App/One.cs", 'a'),
            FileFact("common:file:two", "src/App/Two.cs", 'b'),
            sourceFact);

        decimal duplicateHours = LogicalCandidateScorer.Build(source, duplicates).Single().LogicalExpected;
        decimal distinctHours = LogicalCandidateScorer.Build(source, distinct).Single().LogicalExpected;

        Assert.Equal(6.75m, duplicateHours);
        Assert.Equal(13.75m, distinctHours);
    }

    [Fact]
    public void LogicalCapabilityNormalizesExactDuplicateFrontendFacts()
    {
        WorkItem item = Item(
            "work:ui-surface:logical:part-0001",
            "src/app",
            4m) with
        {
            EvidenceIds = ["frontend:ui:one", "frontend:ui:two"],
        };
        EstimateReport source = Report(item);

        decimal duplicateHours = LogicalCandidateScorer.Build(
            source,
            FrontendEvidence(source, 'a', 'a')).Single().LogicalExpected;
        decimal distinctHours = LogicalCandidateScorer.Build(
            source,
            FrontendEvidence(source, 'a', 'b')).Single().LogicalExpected;

        Assert.Equal(2m, duplicateHours);
        Assert.Equal(3.5m, distinctHours);
    }

    [Fact]
    public void LogicalCapabilityRetainsMeaningfulApiMinimum()
    {
        const string evidenceId = "dotnet:api:src/App/Endpoint.cs";
        WorkItem item = Item(
            "work:api-surface:logical:part-0001",
            "src/App/App.csproj",
            2m) with
        {
            EvidenceIds = [evidenceId],
        };
        EstimateReport source = Report(item);
        RepositoryEvidence evidence = Evidence(
            source,
            new EvidenceFact
            {
                Id = evidenceId,
                Kind = "api",
                Scope = item.Scope,
                Summary = "Synthetic API boundary evidence.",
                Provenance = Provenance(),
            });

        LogicalCapabilityGroup capability = LogicalCandidateScorer.Build(source, evidence).Single();

        Assert.Equal(1.25m, capability.LogicalExpected);
        Assert.Equal("s", capability.LogicalSizeBand);
    }

    [Fact]
    public void LogicalCapabilitySmallSourceEvidenceRetainsSeedAnchor()
    {
        const string evidenceId = "dotnet:source-structure:src/Generated.cs";
        WorkItem item = Item(
            "work:dotnet-source-backbone:logical:part-0001",
            "src/Generated.cs",
            4m) with
        {
            EvidenceIds = [evidenceId],
        };
        EstimateReport source = Report(item);
        RepositoryEvidence evidence = Evidence(
            source,
            new EvidenceFact
            {
                Id = evidenceId,
                Kind = "source-structure",
                Scope = item.Scope,
                Summary = "Synthetic small maintained source projection.",
                Provenance = Provenance(),
            });
        LogicalCandidateModel model = Model(
            new LogicalCandidatePointFactor
            {
                WorkItemKind = "dotnet-source-backbone",
                LogicalSizeBand = "xs",
                SampleCount = 3,
                LogicalExpectedHours = 1.5m,
                SeedExpectedHours = 12m,
                ReviewedExpectedHours = 7.5m,
                Factor = 1m,
            },
            new LogicalCandidateRangeFactor
            {
                WorkItemKind = "dotnet-source-backbone",
                ExpectedSizeBand = "m",
                SampleSource = "exact-kind-and-size",
                SampleCount = 3,
                LowFactor = 1m,
                HighFactor = 1.01m,
            });

        EstimateReport candidate = LogicalCandidateTransformer.Transform(source, evidence, model);

        Assert.Equal(2.5m, candidate.TotalEffort.Expected);
        Assert.Contains("seed anchor 2", candidate.WorkItems[0].Reason);
    }

    [Fact]
    public void LogicalCapabilityExpandsDevelopmentEmptySizeBandsFromNearestSameKind()
    {
        LogicalCandidatePointFactor observed = new()
        {
            WorkItemKind = "project-setup",
            LogicalSizeBand = "s",
            SampleCount = 4,
            LogicalExpectedHours = 6m,
            ReviewedExpectedHours = 9m,
            Factor = 1.5m,
            SampleSource = "exact-kind-and-size",
        };

        LogicalCandidatePointFactor[] expanded =
            [.. LogicalCandidateModelFitter.ExpandPointBands([observed])];

        Assert.Equal(8, expanded.Length);
        Assert.Equal(
            LogicalCandidateScorer.SizeBands.Select(band => band.Split(':', 2)[0]),
            expanded.Select(factor => factor.LogicalSizeBand));
        Assert.All(expanded, factor => Assert.Equal(1.5m, factor.Factor));
        Assert.Equal("exact-kind-and-size", expanded.Single(factor => factor.LogicalSizeBand == "s").SampleSource);
        Assert.Equal("nearest-same-kind:s", expanded.Single(factor => factor.LogicalSizeBand == "4xl").SampleSource);
    }

    [Fact]
    public void LogicalCapabilityBoundsOperationOnlyDataByMaintainedEvidenceRatherThanRawVolume()
    {
        EstimateReport small = OperationOnlyDataReport(1m);
        EstimateReport bulk = OperationOnlyDataReport(10_000m);
        LogicalCandidateModel model = Model(
            new LogicalCandidatePointFactor
            {
                WorkItemKind = "data-persistence",
                LogicalSizeBand = "s",
                SampleCount = 3,
                LogicalExpectedHours = 4.5m,
                ReviewedExpectedHours = 4.5m,
                Factor = 1m,
            },
            new LogicalCandidateRangeFactor
            {
                WorkItemKind = "data-persistence",
                ExpectedSizeBand = "s",
                SampleSource = "exact-kind-and-size",
                SampleCount = 3,
                LowFactor = 0.1m,
                HighFactor = 1m,
            });

        EstimateReport smallCandidate = LogicalCandidateTransformer.Transform(
            small,
            OperationOnlyDataEvidence(small, 1m),
            model);
        EstimateReport bulkCandidate = LogicalCandidateTransformer.Transform(
            bulk,
            OperationOnlyDataEvidence(bulk, 10_000m),
            model);

        Assert.Equal(1.5m, smallCandidate.TotalEffort.Expected);
        Assert.Equal(0.5m, smallCandidate.TotalEffort.Low);
        Assert.Equal(smallCandidate.TotalEffort, bulkCandidate.TotalEffort);
        Assert.Contains("derived 1.5 logical hours", bulkCandidate.WorkItems[0].Reason);
    }

    [Fact]
    public void LogicalCapabilityExternalIntegrationLowRangeRetainsExplicitFloor()
    {
        const string evidenceId = "javascript:integration:src/client.js";
        WorkItem item = Item(
            "work:external-integration:logical:part-0001",
            "src/client.js",
            2m) with
        {
            EvidenceIds = [evidenceId],
        };
        EstimateReport source = Report(item);
        RepositoryEvidence evidence = Evidence(
            source,
            new EvidenceFact
            {
                Id = evidenceId,
                Kind = "integration",
                Scope = item.Scope,
                Summary = "Synthetic integration evidence.",
                Provenance = Provenance(),
                Measurements =
                [
                    new EvidenceMeasurement { Name = "integration-calls", Value = 1m },
                ],
            });
        LogicalCandidateModel model = Model(
            new LogicalCandidatePointFactor
            {
                WorkItemKind = "external-integration",
                LogicalSizeBand = "s",
                SampleCount = 3,
                LogicalExpectedHours = 4.5m,
                ReviewedExpectedHours = 4.5m,
                Factor = 1m,
            },
            new LogicalCandidateRangeFactor
            {
                WorkItemKind = "external-integration",
                ExpectedSizeBand = "s",
                SampleSource = "exact-kind-and-size",
                SampleCount = 3,
                LowFactor = 0.5m,
                HighFactor = 1m,
            });

        EstimateReport candidate = LogicalCandidateTransformer.Transform(source, evidence, model);

        Assert.Equal(new EffortRange { Low = 1m, Expected = 1.5m, High = 1.5m }, candidate.TotalEffort);
        Assert.Contains("low floor 1", candidate.WorkItems[0].Reason);
    }

    private static EstimateReport OperationOnlyDataReport(decimal operations)
    {
        const string evidenceId = "sql:data:src/data.sql";
        return Report(Item(
            "work:data-persistence:logical:part-0001",
            "src/data.sql",
            operations) with
        {
            EvidenceIds = [evidenceId],
        });
    }

    private static RepositoryEvidence OperationOnlyDataEvidence(
        EstimateReport report,
        decimal operations) => Evidence(
            report,
            new EvidenceFact
            {
                Id = "sql:data:src/data.sql",
                Kind = "data-access",
                Scope = "src/data.sql",
                Summary = "Synthetic operation-only data evidence.",
                Provenance = Provenance(),
                Measurements =
                [
                    new EvidenceMeasurement { Name = "data-calls", Value = operations },
                    new EvidenceMeasurement { Name = "queries", Value = operations },
                    new EvidenceMeasurement { Name = "statements", Value = operations },
                ],
            });

    private static RepositoryEvidence FrontendEvidence(
        EstimateReport report,
        char firstHash,
        char secondHash) => Evidence(
            report,
            FileFact("common:file:one", "src/app/one.html", firstHash, "html"),
            FileFact("common:file:two", "src/app/two.html", secondHash, "html"),
            FrontendFact("frontend:ui:one", "src/app/one.html"),
            FrontendFact("frontend:ui:two", "src/app/two.html"));

    private static EvidenceFact FrontendFact(string id, string path) => new()
    {
        Id = id,
        Kind = "ui-structure",
        Scope = "src/app",
        Summary = "Synthetic frontend semantics.",
        Provenance = Provenance(),
        Locations = [new EvidenceLocation { Path = path }],
        Measurements = [new EvidenceMeasurement { Name = "components", Value = 1m }],
    };

    private static EvidenceFact FileFact(
        string id,
        string path,
        char hash,
        string language = "csharp") => new()
        {
            Id = id,
            Kind = "file",
            Scope = path,
            Summary = "Synthetic file identity.",
            Provenance = Provenance(),
            Locations = [new EvidenceLocation { Path = path }],
            Tags =
            [
                "role:source",
                $"language:{language}",
                $"sha256:{new string(hash, 64)}",
            ],
        };
}
