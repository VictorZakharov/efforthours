using System.Text;
using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed class SnapshotPortfolioTests
{
    [Fact]
    public void SelectionUsesFirstParentOrderStrictCutoffsAndFutureNulls()
    {
        SnapshotHistoryCommit[] history =
        [
            new(new string('a', 40), new string('b', 40), Instant("2026-03-01T00:00:00Z")),
            new(new string('c', 40), new string('d', 40), Instant("2026-01-15T00:00:00Z")),
        ];
        IReadOnlyList<SnapshotPeriodResult> result = SnapshotPortfolioSelection.Select(2026, TimeZoneInfo.Utc,
            Instant("2026-03-15T00:00:00Z"), history);
        Assert.Equal("baseline-zero", result[0].Status);
        Assert.Equal(history[1].ObjectId, result[2].CommitObjectId);
        Assert.Equal("partial", result[3].Status);
        Assert.Equal(history[0].ObjectId, result[3].CommitObjectId);
        Assert.Equal("future", result[4].Status);
        Assert.Null(result[4].Hours);
        Assert.Equal(13, result.Count);
        Assert.Equal("assumed-zero", SnapshotPortfolioSelection.Select(2026, TimeZoneInfo.Utc,
            Instant("2026-03-15T00:00:00Z"), [history[0]])[1].Status);
    }

    [Fact]
    public void CalendarBoundariesUseIanaDaylightSaving()
    {
        IReadOnlyList<SnapshotPeriodResult> result = SnapshotPortfolioSelection.Select(2026,
            TimeZoneInfo.FindSystemTimeZoneById("America/Toronto"), Instant("2026-05-01T05:00:00Z"), []);
        Assert.Equal(Instant("2026-03-01T05:00:00Z"), result[2].Cutoff);
        Assert.Equal(Instant("2026-04-01T04:00:00Z"), result[3].Cutoff);
        Assert.Equal("future", result[6].Status);
    }

    [Fact]
    public void OwnershipIncludesHiddenFilesAndAncestorControlsExactlyOnce()
    {
        GitArchiveSnapshot snapshot = Snapshot((".gitignore", "ignored.cs\n"), ("src/Main.cs", "class Main {}"),
            ("src/.hidden", "hidden"), ("README.md", "# Readme"));
        IReadOnlyList<SnapshotAreaInput> areas = SnapshotAreaPartition.Partition(snapshot,
            [new() { Id = "source", Include = ["src/**"] }, new() { Id = "other", Include = ["**"] }]);
        Assert.Equal(4, areas.Sum(a => a.OwnedFiles));
        Assert.Equal(2, areas[0].OwnedFiles);
        Assert.Equal(1, areas[0].ContextFiles);
        Assert.Contains(".gitignore", areas[0].Snapshot.Files.Keys);
        Assert.Contains("src/.hidden", areas[0].Snapshot.Files.Keys);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("/absolute")]
    [InlineData("C:/private")]
    [InlineData("src/./bad")]
    [InlineData("src\\bad")]
    public void RejectsUnsafeAreaSelectors(string selector) => Assert.Throws<InvalidDataException>(() =>
        SnapshotAreaPartition.ValidateDefinitions([new() { Id = "bad", Include = [selector] }, new() { Id = "rest", Include = ["**"] }]));

    [Fact]
    public void RejectsMissingSelectorsAndEmptyOwnedAreas()
    {
        GitArchiveSnapshot snapshot = Snapshot(("README.md", "# Readme"));
        Assert.Throws<SnapshotPlanningException>(() => SnapshotAreaPartition.Partition(snapshot,
            [new() { Id = "missing", Include = ["missing/**"] }, new() { Id = "rest", Include = ["**"] }]));
        Assert.Throws<SnapshotPlanningException>(() => SnapshotAreaPartition.Partition(snapshot,
            [new() { Id = "all", Include = ["**"] }, new() { Id = "rest", Include = ["**"] }]));
    }

    [Fact]
    public void LargestRemaindersPreserveExactProjectTotalAndStableAreaOrder()
    {
        Assert.Equal([0.34m, 0.33m, 0.33m], SnapshotAreaPartition.Allocate(1m, [1m, 1m, 1m]));
        Assert.Equal([0m, 0m], SnapshotAreaPartition.Allocate(0m, [0m, 0m]));
        Assert.Throws<InvalidDataException>(() => SnapshotAreaPartition.Allocate(1m, [0m]));
        for (int count = 1; count <= 40; count++)
            Assert.Equal(127.89m, SnapshotAreaPartition.Allocate(127.89m, [.. Enumerable.Range(1, count).Select(i => (decimal)i)]).Sum());
    }

    [Fact]
    public async Task SharedWholeAndAreaArtifactsMatchSeparateStandaloneRuns()
    {
        GitArchiveSnapshot snapshot = Snapshot(("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"/>"),
            ("src/Main.cs", "public class Main { public int Add(int a, int b) => a + b; }"),
            ("package.json", "{\"name\":\"demo\",\"dependencies\":{\"express\":\"5.0.0\"}}"),
            ("web/main.js", "import express from 'express'; const app = express(); app.get('/hello', (req, res) => res.send('hello'));"),
            (".gitignore", "ignored.js\n"), ("README.md", "# Synthetic app\n"));
        RepositoryAnalysisArtifactCache shared = new();
        GitArchiveSnapshot reusable = new(snapshot.Files, shared);
        _ = await new RepositoryAnalysisPipeline(reusable, analysisArtifactCache: shared).ScanAsync(reusable.RootPath);
        IReadOnlyList<SnapshotAreaInput> areas = SnapshotAreaPartition.Partition(reusable,
            [new() { Id = "web", Include = ["web/**"] }, new() { Id = "rest", Include = ["**"] }]);
        foreach (SnapshotAreaInput area in areas)
        {
            RepositoryEvidence warm = await new RepositoryAnalysisPipeline(area.Snapshot, analysisArtifactCache: shared).ScanAsync(area.Snapshot.RootPath);
            GitArchiveSnapshot cold = new(area.Snapshot.Files);
            RepositoryEvidence separate = await new RepositoryAnalysisPipeline(cold).ScanAsync(cold.RootPath);
            Assert.Equal(ContractJson.Serialize(separate), ContractJson.Serialize(warm));
            Assert.Equal(ContractJson.Serialize(new SeedEstimator().Estimate(separate, EstimationProfile.Implementation)),
                ContractJson.Serialize(new SeedEstimator().Estimate(warm, EstimationProfile.Implementation)));
        }
        Assert.True(shared.GetStatistics().Hits > 0);
    }

    [Fact]
    public void MeasurementIdentityIsContentBoundAndDoesNotContainProducerOrRate()
    {
        MeasurementIdentity identity = SnapshotMeasurementIdentity.Create(EstimationProfile.Implementation);
        Assert.StartsWith("sha256:", identity.ImplementationDigest, StringComparison.Ordinal);
        string json = ContractJson.Serialize(identity);
        Assert.DoesNotContain("producer", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rate", json, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(SnapshotMeasurementIdentity.Digest(identity), SnapshotMeasurementIdentity.Digest(identity with
        {
            Profile = EstimationProfile.Recreation,
        }));
        Assert.Equal(Enum.GetValues<EffortCategory>().Length,
            Enum.GetValues<EffortCategory>().Select(SnapshotCategoryGrouping.Group).Count());
    }

    [Fact]
    public void ReceiptIntegrityAndCategoryReconciliationAreValidated()
    {
        SnapshotMeasurementReceipt receipt = new()
        {
            Id = "",
            InputDigest = Digest,
            EvidenceDigest = Digest,
            Measurement = SnapshotMeasurementIdentity.Create(EstimationProfile.Implementation),
            ProducerVersion = "synthetic-producer",
            Hours = new() { Low = 1, Expected = 2, High = 3 },
            SelectedFileCount = 1,
            ContextFileCount = 0,
            Categories = [new() { Category = EffortCategory.ProductionImplementation, Hours = new() { Low = 1, Expected = 2, High = 3 } }],
        };
        receipt = receipt with { Id = SnapshotPortfolioValidation.ReceiptId(receipt) };
        SnapshotPortfolioValidation.Validate(receipt);
        Assert.True(ContractSchemaValidator.Validate("snapshot-measurement-receipt.schema.json", ContractJson.Serialize(receipt)).IsValid);
        Assert.Throws<InvalidDataException>(() => SnapshotPortfolioValidation.Validate(receipt with { ProducerVersion = "rewritten" }));
        SnapshotMeasurementReceipt wrong = receipt with { Categories = [] };
        wrong = wrong with { Id = SnapshotPortfolioValidation.ReceiptId(wrong) };
        Assert.Throws<InvalidDataException>(() => SnapshotPortfolioValidation.Validate(wrong));
    }

    private static readonly string Digest = "sha256:" + new string('a', 64);
    private static DateTimeOffset Instant(string value) => DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    private static GitArchiveSnapshot Snapshot(params (string Path, string Text)[] files) =>
        new(files.ToDictionary(f => f.Path, f => Encoding.UTF8.GetBytes(f.Text), StringComparer.Ordinal));
}
