using System.Text;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class SnapshotPortfolioCompatibilityTests
{
    [Fact]
    public void CompiledSelectorsAreReusedAndRemainBoundToTheirDefinition()
    {
        GitArchiveSnapshot snapshot = new(new Dictionary<string, byte[]>
        {
            ["src/Main.cs"] = Encoding.UTF8.GetBytes("public class Main {}"),
            ["README.md"] = Encoding.UTF8.GetBytes("# Synthetic"),
        });
        SnapshotAreaDefinition[] definitions = [new() { Id = "source", Include = ["src/**"] }, new() { Id = "rest", Include = ["**"] }];
        int compiled = 0;
        SnapshotAreaSelectors selectors = new(definitions, () => compiled++);
        for (int i = 0; i < 20; i++)
        {
            IReadOnlyList<SnapshotAreaInput> areas = SnapshotAreaPartition.Partition(snapshot, definitions, selectors: selectors);
            Assert.Equal([1, 1], areas.Select(a => a.OwnedFiles));
        }
        Assert.Equal(2, compiled);
        SnapshotPlanningException mismatch = Assert.Throws<SnapshotPlanningException>(() => SnapshotAreaPartition.Partition(snapshot,
            [definitions[0] with { Include = ["README.md"] }, definitions[1]], selectors: selectors));
        Assert.Equal("invalid-area-definition", mismatch.Category);
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => SnapshotAreaPartition.Partition(snapshot, definitions,
            selectors: selectors, cancellationToken: cancelled.Token));
    }

    [Fact]
    public void PrivateStudySchemaAcceptsNullUrlButPublicAndUnknownInputsFail()
    {
        const string privateStudy = """
            {"schemaVersion":"1.0.0","projects":[{"id":"demo","sourceVisibility":"closed-source",
            "publicRepositoryUrl":null,"areasDigest":"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "areas":[{"id":"source","reviewedCommit":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}]}]}
            """;
        Assert.True(ContractSchemaValidator.Validate("snapshot-dashboard-studies.schema.json", privateStudy).IsValid);
        Assert.False(ContractSchemaValidator.Validate("snapshot-dashboard-studies.schema.json",
            privateStudy.Replace("closed-source", "public", StringComparison.Ordinal)).IsValid);
        Assert.False(ContractSchemaValidator.Validate("snapshot-dashboard-studies.schema.json",
            privateStudy.Replace("\"publicRepositoryUrl\":null", "\"privateUrl\":\"hidden\"", StringComparison.Ordinal)).IsValid);
    }

    [Fact]
    public void AddedOptionalDefaultsPreserveLegacyTransportForDigestRecomputation()
    {
        string telemetry = ContractJson.Serialize(new SnapshotPortfolioTelemetry());
        Assert.DoesNotContain("inventoryReads", telemetry, StringComparison.Ordinal);
        Assert.DoesNotContain("areaPlanningCalls", telemetry, StringComparison.Ordinal);
        Assert.DoesNotContain("selectorCompilations", telemetry, StringComparison.Ordinal);
        Assert.DoesNotContain("planningReuseHits", telemetry, StringComparison.Ordinal);
        SnapshotProjectDefinition definition = new() { Id = "demo", Ref = "main", Areas = [new() { Id = "all", Include = ["**"] }] };
        Assert.DoesNotContain("areaMeasurementMode", ContractJson.Serialize(definition), StringComparison.Ordinal);
        Assert.DoesNotContain("areaRevisions", ContractJson.Serialize(definition), StringComparison.Ordinal);
    }
}
