using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class SqlAnalyzerTests
{
    private const string ProjectFile =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework>" +
        "</PropertyGroup></Project>";

    [Fact]
    public async Task SchemaMigrationQueryAndProgramsProduceTraceableBoundedEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText("src/App/App.csproj", ProjectFile);
        repository.WriteText(
            "src/App/migrations/V001__orders.sql",
            """
            -- private-sql-marker must never appear in emitted evidence
            CREATE TYPE order_status AS ENUM ('pending', 'paid');
            CREATE SEQUENCE order_number_seq;
            CREATE TABLE orders (
                id BIGSERIAL PRIMARY KEY,
                customer_id BIGINT NOT NULL,
                status order_status NOT NULL,
                payload JSONB,
                CONSTRAINT fk_orders_customer FOREIGN KEY (customer_id) REFERENCES customers(id),
                CONSTRAINT valid_payload CHECK (payload IS NOT NULL)
            );
            CREATE UNIQUE INDEX ix_orders_customer ON orders(customer_id);
            CREATE VIEW active_orders AS SELECT id FROM orders WHERE status::text ILIKE 'paid';
            CREATE FUNCTION order_count() RETURNS BIGINT LANGUAGE SQL AS $$
              SELECT count(*) FROM orders
            $$;
            CREATE PROCEDURE mark_paid() LANGUAGE SQL AS $body$
              UPDATE orders SET status = 'paid'
            $body$;
            CREATE TRIGGER orders_audit AFTER UPDATE ON orders
              EXECUTE FUNCTION order_count();
            WITH ranked AS (
              SELECT o.id, row_number() OVER (PARTITION BY o.customer_id ORDER BY o.id) AS rank
              FROM orders o JOIN customers c ON c.id = o.customer_id
            )
            SELECT * FROM ranked WHERE id IN (SELECT id FROM orders);
            BEGIN TRANSACTION;
            UPDATE orders SET status = 'paid' WHERE id = 1;
            COMMIT;
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);
        string json = ContractJson.Serialize(evidence);
        EvidenceFact artifact = Fact(evidence, "sql:artifact:src~app~migrations~v001__orders.sql");
        EvidenceFact data = Fact(evidence, "sql:data:src~app~migrations~v001__orders.sql");

        Assert.Equal("0.1.0", artifact.Provenance.AnalyzerVersion);
        Assert.Equal("src/App/App.csproj", artifact.Scope);
        Assert.Contains("sql", evidence.Repository.Ecosystems);
        Assert.Contains("sql-role:migration", artifact.Tags);
        Assert.Contains("migration-order:version-prefix", artifact.Tags);
        Assert.Contains("dialect:postgresql", artifact.Tags);
        Assert.Contains("dialect-confidence:high", artifact.Tags);
        Assert.Contains("syntax:token-backed", artifact.Tags);
        Assert.Contains("source-excerpts:not-emitted", artifact.Tags);
        Assert.Equal(1m, Measurement(artifact, "tables"));
        Assert.Equal(1m, Measurement(artifact, "views"));
        Assert.Equal(1m, Measurement(artifact, "indexes"));
        Assert.True(Measurement(artifact, "constraints") >= 3m);
        Assert.Equal(1m, Measurement(artifact, "sequences"));
        Assert.Equal(1m, Measurement(artifact, "types"));
        Assert.Equal(1m, Measurement(artifact, "functions"));
        Assert.Equal(1m, Measurement(artifact, "procedures"));
        Assert.Equal(1m, Measurement(artifact, "triggers"));
        Assert.True(Measurement(artifact, "queries") >= 3m);
        Assert.True(Measurement(artifact, "joins") >= 1m);
        Assert.True(Measurement(artifact, "ctes") >= 1m);
        Assert.True(Measurement(artifact, "subqueries") >= 1m);
        Assert.True(Measurement(artifact, "window-functions") >= 1m);
        Assert.Equal(1m, Measurement(artifact, "transactions"));
        Assert.True(Measurement(artifact, "data-modification-statements") >= 1m);
        Assert.Equal(1m, Measurement(data, "db-sets"));
        Assert.Equal(1m, Measurement(data, "migrations"));
        Assert.True(Measurement(data, "entity-configurations") >= 3m);
        Assert.True(Measurement(data, "repository-types") >= 3m);
        Assert.True(Measurement(data, "data-calls") >= 1m);
        Assert.DoesNotContain("private-sql-marker", json, StringComparison.Ordinal);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB6000");
        Assert.Empty(ContractValidation.Validate(evidence));
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.RepositoryEvidence,
            json);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
    }

    [Theory]
    [InlineData(
        "CREATE TABLE demo (payload JSONB); SELECT * FROM demo WHERE name ILIKE 'a%';",
        "dialect:postgresql")]
    [InlineData(
        "CREATE TABLE [demo] ([name] NVARCHAR(30)); GO\nRAISERROR ('failed', 16, 1);",
        "dialect:sql-server")]
    [InlineData(
        "CREATE TABLE `demo` (`id` BIGINT UNSIGNED AUTO_INCREMENT); DELIMITER //",
        "dialect:mysql-mariadb")]
    [InlineData(
        "PRAGMA foreign_keys=ON; CREATE TABLE demo(id INTEGER PRIMARY KEY AUTOINCREMENT); VACUUM;",
        "dialect:sqlite")]
    public async Task CommonDialectsAreDetectedWithoutChoosingAConcreteDatabase(
        string sql,
        string expectedDialect)
    {
        InMemoryRepository repository = new();
        repository.WriteText("schema.sql", sql);

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact artifact = Fact(evidence, "sql:artifact:schema.sql");

        Assert.Contains(expectedDialect, artifact.Tags);
        Assert.DoesNotContain("dialect-confidence:low", artifact.Tags);
        Assert.Contains(evidence.Diagnostics, diagnostic =>
            diagnostic.Code == "FB6000" &&
            diagnostic.Message.Contains("did not connect", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AmbiguousAndUnknownSqlRemainVisibleWithoutInventedDataSemantics()
    {
        InMemoryRepository repository = new();
        repository.WriteText("App.csproj", ProjectFile);
        repository.WriteText("package.json", "{ \"name\": \"mixed-root\" }");
        repository.WriteText("vendor-extension.sql", "REINDEXISH EVERYTHING NOW;");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact artifact = Fact(evidence, "sql:artifact:vendor-extension.sql");

        Assert.Equal(".", artifact.Scope);
        Assert.Contains("sql-role:unknown", artifact.Tags);
        Assert.Contains("dialect:standard-or-unknown", artifact.Tags);
        Assert.Contains("dialect-confidence:low", artifact.Tags);
        Assert.DoesNotContain(evidence.Facts, fact => fact.Id == "sql:data:vendor-extension.sql");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB6003");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB6005");
    }

    [Fact]
    public async Task TestDeliveryAndCrossDatabaseRolesMapOnlyToSupportedEvidenceKinds()
    {
        InMemoryRepository repository = new();
        repository.WriteText("tests/fixtures/orders.sql", "INSERT INTO orders(id) VALUES (1);");
        repository.WriteText("deploy/install.sql", "CREATE TABLE installed(id INTEGER PRIMARY KEY);");
        repository.WriteText(
            "queries/remote.sql",
            "ATTACH DATABASE 'archive.db' AS archive; SELECT * FROM archive.orders;");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport report = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Implementation);

        Assert.Contains(evidence.Facts, fact =>
            fact.Id == "sql:test:tests~fixtures~orders.sql" &&
            fact.Kind == EvidenceKinds.SqlTest);
        Assert.Contains(evidence.Facts, fact =>
            fact.Id == "sql:delivery:deploy~install.sql" &&
            fact.Kind == EvidenceKinds.SqlDelivery);
        Assert.Contains(evidence.Facts, fact =>
            fact.Id == "sql:integration:queries~remote.sql" &&
            fact.Kind == EvidenceKinds.Integration);
        Assert.DoesNotContain(evidence.Facts, fact => fact.Id == "sql:data:tests~fixtures~orders.sql");
        Assert.DoesNotContain(evidence.Facts, fact => fact.Id == "sql:data:deploy~install.sql");
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.IntegrationContractAndComponentTesting);
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.PackagingDeploymentAndReleaseArtifacts);
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.ExternalIntegrationsAndProtocols);
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.DataModelingPersistenceAndMigrations);
        Assert.DoesNotContain(report.Diagnostics, diagnostic => diagnostic.Code is "FB1001" or "FB1002");
        Assert.Contains(report.WorkItems, item => item.UncertaintyReasons.Contains(
            "Some SQL structure is token-backed rather than grammar-parser-backed.",
            StringComparer.Ordinal));
        Assert.DoesNotContain(report.WorkItems, item => item.UncertaintyReasons.Any(reason =>
            reason.Contains("TypeScript", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task FormattingDuplicatesDumpsAndSeedRowVolumeDoNotInflateRepositoryEffort()
    {
        const string schema = "CREATE TABLE orders (id INTEGER PRIMARY KEY, total DECIMAL(10,2));";
        EstimateReport baseline = await EstimateAsync(("schema.sql", schema));
        EstimateReport formatted = await EstimateAsync((
            "schema.sql",
            "CREATE   TABLE orders\n( id INTEGER PRIMARY KEY, total DECIMAL(10,2) );"));
        EstimateReport duplicate = await EstimateAsync(
            ("schema.sql", schema),
            ("copy.sql", schema));
        EstimateReport dump = await EstimateAsync(
            ("schema.sql", schema),
            ("backups/archive.sql", "-- PostgreSQL database dump\n" + schema));
        EstimateReport generated = await EstimateAsync(
            ("schema.sql", schema),
            ("generated/derived.sql", "CREATE TABLE generated_copy(id INTEGER);"));
        EstimateReport vendored = await EstimateAsync(
            ("schema.sql", schema),
            ("vendor/third-party.sql", "CREATE TABLE vendor_copy(id INTEGER);"));
        EstimateReport oneSeedRow = await EstimateAsync(
            ("seeds/orders.sql", "INSERT INTO orders(id) VALUES (1);"));
        EstimateReport manySeedRows = await EstimateAsync((
            "seeds/orders.sql",
            string.Join('\n', Enumerable.Range(1, 120).Select(index =>
                $"INSERT INTO orders(id) VALUES ({index});"))));

        Assert.Equal(baseline.TotalEffort, formatted.TotalEffort);
        foreach (WorkItem baselineItem in baseline.WorkItems)
        {
            WorkItem duplicateItem = Assert.Single(duplicate.WorkItems, item =>
                item.Id == baselineItem.Id);
            Assert.True(
                baselineItem.Hours == duplicateItem.Hours,
                $"Duplicate normalization changed {baselineItem.Id}: " +
                $"{baselineItem.Hours} -> {duplicateItem.Hours}.");
        }

        Assert.Equal(baseline.TotalEffort, duplicate.TotalEffort);
        Assert.Equal(baseline.TotalEffort, dump.TotalEffort);
        Assert.Equal(baseline.TotalEffort, generated.TotalEffort);
        Assert.Equal(baseline.TotalEffort, vendored.TotalEffort);
        Assert.Equal(oneSeedRow.TotalEffort, manySeedRows.TotalEffort);
        Assert.Equal(
            Category(oneSeedRow, EffortCategory.DataModelingPersistenceAndMigrations),
            Category(manySeedRows, EffortCategory.DataModelingPersistenceAndMigrations));
    }

    [Fact]
    public async Task DumpsAreScannerClassifiedAndExcludedFromSqlEffort()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "exports/customer.snapshot.sql",
            "-- PostgreSQL database dump\nCREATE TABLE customers(id INTEGER);\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact file = Fact(evidence, "file:exports/customer.snapshot.sql");

        Assert.Contains("language:sql", file.Tags);
        Assert.Contains("classification:generated", file.Tags);
        Assert.DoesNotContain(evidence.Facts, fact => fact.Id.StartsWith(
            "sql:data:",
            StringComparison.Ordinal));
        Assert.Equal(1m, Measurement(Fact(evidence, "sql:repository"), "excluded-or-skipped-files"));
    }

    [Fact]
    public async Task TokenLimitLowersConfidenceAndKeepsMeasurementsBounded()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "large.sql",
            string.Concat(Enumerable.Repeat("SELECT (1);\n", 40_100)));

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact artifact = Fact(evidence, "sql:artifact:large.sql");

        Assert.Contains("analysis:token-limit-reached", artifact.Tags);
        Assert.Contains("parser-confidence:low", artifact.Tags);
        Assert.InRange(Measurement(artifact, "statements"), 1m, 10_000m);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB6002");
        Assert.Empty(ContractValidation.Validate(evidence));
    }

    [Fact]
    public async Task InvalidUtf8FailsClosedWithoutSemanticEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteBytes("invalid.sql", [0xc3, 0x28]);

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.DoesNotContain(evidence.Facts, fact => fact.Id.StartsWith(
            "sql:artifact:",
            StringComparison.Ordinal));
        Assert.Contains(evidence.Diagnostics, diagnostic =>
            diagnostic.Code == "FB6001" &&
            diagnostic.Locations.Any(location => location.Path == "invalid.sql"));
    }

    private static async Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        await new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static async Task<EstimateReport> EstimateAsync(
        params (string Path, string Content)[] files)
    {
        InMemoryRepository repository = new();
        foreach ((string path, string content) in files)
        {
            repository.WriteText(path, content);
        }

        RepositoryEvidence evidence = await ScanAsync(repository);
        return new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);
    }

    private static EvidenceFact Fact(RepositoryEvidence evidence, string id) =>
        Assert.Single(evidence.Facts, fact => fact.Id == id);

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;

    private static EffortRange Category(EstimateReport report, EffortCategory category) =>
        report.Categories.SingleOrDefault(item => item.Category == category)?.Hours ?? new EffortRange
        {
            Low = 0m,
            Expected = 0m,
            High = 0m,
        };
}
