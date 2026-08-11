using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class SqlChangeTests
{
    [Fact]
    public async Task SqlFormattingOnlyChangeHasZeroEffort()
    {
        ChangeState before = State((
            "schema.sql",
            "CREATE TABLE orders (id INTEGER PRIMARY KEY, total DECIMAL(10,2));\n"));
        ChangeState after = State((
            "schema.sql",
            "CREATE   TABLE orders\n( id INTEGER PRIMARY KEY, total DECIMAL(10,2) );\n"));

        ChangeEstimateReport report = await EstimateAsync(before, after);
        ChangePathEvidence path = Assert.Single(report.Evidence.Paths);

        Assert.Equal(ChangePathClassification.FormattingOnly, path.Classification);
        Assert.False(path.Represented);
        Assert.Equal(0m, report.TotalEffort.Expected);
        Assert.Empty(report.WorkItems);
    }

    [Fact]
    public async Task ExactSqlMoveHasZeroEffort()
    {
        const string schema = "CREATE TABLE orders(id INTEGER PRIMARY KEY);\n";
        ChangeEstimateReport report = await EstimateAsync(
            State(("schema.sql", schema)),
            State(("migrations/001_orders.sql", schema)));
        ChangePathEvidence path = Assert.Single(report.Evidence.Paths);

        Assert.Equal(ChangePathStatus.Moved, path.Status);
        Assert.Equal(ChangePathClassification.ExactMove, path.Classification);
        Assert.False(path.Represented);
        Assert.Equal(0m, report.TotalEffort.Expected);
        Assert.Empty(report.WorkItems);
    }

    [Fact]
    public async Task SqlLiteralChangeRemainsMeaningfulDataWork()
    {
        ChangeState before = State(("query.sql", "SELECT * FROM orders WHERE status = 'in progress';\n"));
        ChangeState after = State(("query.sql", "SELECT * FROM orders WHERE status = 'inprogress';\n"));

        ChangeEstimateReport report = await EstimateAsync(before, after);
        ChangePathEvidence path = Assert.Single(report.Evidence.Paths);

        Assert.Equal(ChangePathClassification.Represented, path.Classification);
        Assert.True(path.Represented);
        AssertPositiveCategory(report, EffortCategory.DataModelingPersistenceAndMigrations);
    }

    [Fact]
    public async Task SqlSchemaQueryAndRemovalDeltasStayInTheDataCategory()
    {
        ChangeEstimateReport schema = await EstimateAsync(
            State(),
            State(("schema.sql", "CREATE TABLE orders(id INTEGER PRIMARY KEY);\n")));
        ChangeEstimateReport query = await EstimateAsync(
            State(("query.sql", "SELECT id FROM orders;\n")),
            State((
                "query.sql",
                "WITH active AS (SELECT id FROM orders WHERE active = 1) SELECT id FROM active;\n")));
        ChangeEstimateReport removal = await EstimateAsync(
            State(("migrations/001_orders.sql", "CREATE TABLE orders(id INTEGER PRIMARY KEY);\n")),
            State());

        AssertSpecializedCategory(schema, EffortCategory.DataModelingPersistenceAndMigrations);
        AssertSpecializedCategory(query, EffortCategory.DataModelingPersistenceAndMigrations);
        AssertSpecializedCategory(removal, EffortCategory.DataModelingPersistenceAndMigrations);
        Assert.All(removal.WorkItems.Where(item =>
            item.Category == EffortCategory.DataModelingPersistenceAndMigrations), item =>
        {
            Assert.Contains("removal", item.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(item.EvidenceIds);
        });
    }

    [Theory]
    [InlineData(
        "tests/fixtures/orders.sql",
        "INSERT INTO orders(id) VALUES (1);",
        EffortCategory.IntegrationContractAndComponentTesting)]
    [InlineData(
        "deploy/install.sql",
        "CREATE TABLE installed(id INTEGER PRIMARY KEY);",
        EffortCategory.PackagingDeploymentAndReleaseArtifacts)]
    [InlineData(
        "queries/remote.sql",
        "ATTACH DATABASE 'archive.db' AS archive; SELECT * FROM archive.orders;",
        EffortCategory.ExternalIntegrationsAndProtocols)]
    public async Task SqlRolesKeepChangeEffortInTheirIntendedCategory(
        string path,
        string content,
        EffortCategory category)
    {
        ChangeEstimateReport report = await EstimateAsync(State(), State((path, content)));

        AssertPositiveCategory(report, category);
        if (category is EffortCategory.IntegrationContractAndComponentTesting or
            EffortCategory.PackagingDeploymentAndReleaseArtifacts)
        {
            Assert.DoesNotContain(report.Categories, candidate =>
                candidate.Category == EffortCategory.DataModelingPersistenceAndMigrations);
        }
    }

    [Fact]
    public async Task SqlDumpAndExactCopyAreExcludedFromChangeEffort()
    {
        const string schema = "CREATE TABLE orders(id INTEGER PRIMARY KEY);\n";
        ChangeEstimateReport report = await EstimateAsync(
            State(("schema.sql", schema)),
            State(
                ("schema.sql", schema),
                ("copy.sql", schema),
                ("backups/archive.dump.sql", "-- PostgreSQL database dump\n" + schema),
                ("generated/derived.sql", "CREATE TABLE generated_copy(id INTEGER);\n")));

        Assert.Equal(3, report.Evidence.Paths.Count);
        Assert.Contains(report.Evidence.Paths, path =>
            path.Path == "copy.sql" &&
            path.Classification == ChangePathClassification.ExactDuplicate &&
            !path.Represented);
        Assert.Contains(report.Evidence.Paths, path =>
            path.Path == "backups/archive.dump.sql" &&
            path.Classification == ChangePathClassification.Generated &&
            !path.Represented);
        Assert.Contains(report.Evidence.Paths, path =>
            path.Path == "generated/derived.sql" &&
            path.Classification == ChangePathClassification.Generated &&
            !path.Represented);
        Assert.Equal(0m, report.TotalEffort.Expected);
        Assert.Empty(report.WorkItems);
    }

    private static void AssertSpecializedCategory(
        ChangeEstimateReport report,
        EffortCategory expected)
    {
        AssertPositiveCategory(report, expected);
        EffortCategory[] specialized =
        [
            EffortCategory.DataModelingPersistenceAndMigrations,
            EffortCategory.ExternalIntegrationsAndProtocols,
            EffortCategory.IntegrationContractAndComponentTesting,
            EffortCategory.PackagingDeploymentAndReleaseArtifacts,
        ];
        Assert.All(specialized.Where(category => category != expected), category =>
            Assert.DoesNotContain(report.Categories, candidate => candidate.Category == category));
    }

    private static void AssertPositiveCategory(ChangeEstimateReport report, EffortCategory expected)
    {
        CategoryEstimate category = Assert.Single(
            report.Categories,
            candidate => candidate.Category == expected);
        Assert.True(category.Hours.Low > 0m);
        Assert.True(category.Hours.Low <= category.Hours.Expected);
        Assert.True(category.Hours.Expected <= category.Hours.High);
        Assert.Contains(report.WorkItems, item => item.Category == expected);
        Assert.All(report.WorkItems, item =>
        {
            Assert.InRange(item.Hours.Expected, 0.01m, 8m);
            Assert.NotEmpty(item.EvidenceIds);
        });
    }

    private static Task<ChangeEstimateReport> EstimateAsync(
        ChangeState before,
        ChangeState after) => new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "in-memory-sql-change",
                Selection = new ChangeSelection
                {
                    Kind = ChangeSelectionKind.BaseHead,
                    Base = Reference("base", before.ObjectId),
                    Head = Reference("head", after.ObjectId),
                },
                OpenBaseAsync = before.OpenAsync,
                OpenHeadAsync = after.OpenAsync,
            },
            EstimationProfile.Implementation);

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.GitTree,
    };

    private static ChangeState State(params (string Path, string Content)[] files)
    {
        InMemoryChangeSnapshot snapshot = new(files);
        return new ChangeState(snapshot.ObjectId, InMemoryChangeSnapshot.Factory(files));
    }

    private sealed record ChangeState(
        string ObjectId,
        Func<CancellationToken, Task<IChangeSnapshot>> OpenAsync);
}
