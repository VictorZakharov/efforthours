using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed partial class JavaAnalyzerTests
{
    private const string MavenProject =
        """
        <project xmlns="http://maven.apache.org/POM/4.0.0">
          <modelVersion>4.0.0</modelVersion>
          <groupId>com.example</groupId><artifactId>orders</artifactId><version>1.0.0</version>
          <dependencies>
            <dependency><groupId>org.springframework.boot</groupId><artifactId>spring-boot-starter-web</artifactId></dependency>
            <dependency><groupId>org.springframework.boot</groupId><artifactId>spring-boot-starter-data-jpa</artifactId></dependency>
            <dependency><groupId>org.junit.jupiter</groupId><artifactId>junit-jupiter</artifactId></dependency>
          </dependencies>
          <profiles><profile><id>production</id></profile></profiles>
          <build><plugins><plugin><artifactId>maven-compiler-plugin</artifactId><configuration>
            <annotationProcessorPaths><path><groupId>org.mapstruct</groupId><artifactId>mapstruct-processor</artifactId></path></annotationProcessorPaths>
          </configuration></plugin></plugins></build>
        </project>
        """;

    [Fact]
    public async Task MavenFrameworkStructureTestsAndBuildMetadataProduceEvidenceAndEffort()
    {
        InMemoryRepository repository = new();
        repository.WriteText("pom.xml", MavenProject);
        repository.WriteText(
            "src/main/java/com/example/orders/OrderController.java",
            """
            package com.example.orders;

            import java.net.http.HttpClient;
            import java.util.concurrent.CompletableFuture;
            import jakarta.persistence.Entity;
            import jakarta.validation.Valid;
            import org.springframework.data.jpa.repository.JpaRepository;
            import org.springframework.scheduling.annotation.Scheduled;
            import org.springframework.security.access.prepost.PreAuthorize;
            import org.springframework.web.bind.annotation.GetMapping;
            import org.springframework.web.bind.annotation.RestController;

            @Entity record OrderRecord<T>(T value) { }
            interface OrderRepository extends JpaRepository<OrderRecord<String>, Long> { }

            @RestController
            public final class OrderController {
                @GetMapping("/orders") @PreAuthorize("hasRole('USER')")
                public OrderRecord<String> load(@Valid String id) throws Exception {
                    HttpClient.newHttpClient().sendAsync(null, null);
                    return CompletableFuture.completedFuture(new OrderRecord<>(id)).get();
                }

                @Scheduled(cron = "0 * * * * *") public void refresh() { }
                public static void main(String[] args) { }
            }
            """);
        repository.WriteText(
            "src/test/java/com/example/orders/OrderControllerTest.java",
            """
            package com.example.orders;
            import org.junit.jupiter.api.Test;
            import org.junit.jupiter.params.ParameterizedTest;
            import org.junit.jupiter.params.provider.ValueSource;
            import static org.junit.jupiter.api.Assertions.assertEquals;
            import org.mockito.Mock;

            class OrderControllerTest {
                @Mock OrderRepository repository;
                @Test void loads() { assertEquals(1, 1); }
                @ParameterizedTest @ValueSource(strings = {"a", "b"}) void accepts(String value) { assertEquals(value, value); }
            }
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);
        EvidenceFact project = ProjectFact(evidence);
        EvidenceFact structure = FactOfKind(evidence, EvidenceKinds.SourceStructure);
        EvidenceFact build = Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.BuildConfiguration &&
            fact.Provenance.Analyzer == "efforthours.java-analyzer");
        EvidenceFact test = FactOfKind(evidence, EvidenceKinds.EcosystemTest);

        Assert.Equal("0.1.0", project.Provenance.AnalyzerVersion);
        Assert.Contains("package-role:server", project.Tags);
        Assert.Contains("package:cli-bin", project.Tags);
        Assert.Contains("package:library-exports", project.Tags);
        Assert.Contains("build-system:maven", project.Tags);
        Assert.Contains("syntax:token-backed", structure.Tags);
        Assert.True(Measurement(structure, "types") >= 3m);
        Assert.True(Measurement(structure, "methods") >= 3m);
        Assert.True(Measurement(structure, "records") >= 1m);
        Assert.True(Measurement(structure, "interfaces") >= 1m);
        Assert.True(Measurement(structure, "generic-declarations") >= 2m);
        Assert.True(Measurement(structure, "exception-paths") >= 1m);
        Assert.True(Measurement(build, "plugins") >= 1m);
        Assert.Equal(1m, Measurement(build, "maven-profiles"));
        Assert.Equal(1m, Measurement(build, "annotation-processors"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ApiSurface &&
            fact.Tags.Contains("technology:spring-web", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.DataAccess);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Integration);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SecurityConfiguration);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.BackgroundWork);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Validation);
        Assert.Equal(2m, Measurement(test, "test-cases"));
        Assert.True(Measurement(test, "parameterized-cases") >= 1m);
        Assert.True(Measurement(test, "assertions") >= 2m);
        Assert.True(Measurement(test, "mock-usages") >= 1m);
        Assert.Contains("analysis-status:analyzed", LanguageFact(evidence).Tags);
        Assert.Contains("analysis-depth:token-backed", LanguageFact(evidence).Tags);
        Assert.DoesNotContain(estimate.Diagnostics, diagnostic => diagnostic.Code == "FB1002");
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:polyglot-source-backbone");
        Assert.Contains(estimate.WorkItems, item => item.Category == EffortCategory.UnitTesting);
        Assert.Contains(estimate.WorkItems, item => item.Category == EffortCategory.BuildConfigurationAndDeveloperTooling);
        Assert.Empty(ContractValidation.Validate(evidence));
        Assert.Empty(ContractValidation.Validate(estimate));
    }

    [Fact]
    public async Task FrameworkNamesWithoutCanonicalImportsDoNotProduceSemanticEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "Local.java",
            """
            @RestController class Local {
                @GetMapping void route() { }
                void run() { JdbcTemplate.query(); HttpClient.send(); }
            }
            @interface RestController { }
            @interface GetMapping { }
            class JdbcTemplate { static void query() { } }
            class HttpClient { static void send() { } }
            interface JavaMigration { }
            interface Job { }
            interface Runnable { }
            class LocalWorker implements JavaMigration, Job, Runnable { }
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains("java-project:implicit-fallback", ProjectFact(evidence).Tags);
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind is
            EvidenceKinds.ApiSurface or EvidenceKinds.DataAccess or EvidenceKinds.Integration or
            EvidenceKinds.BackgroundWork);
        Assert.Equal(0m, Measurement(FactOfKind(evidence, EvidenceKinds.SourceStructure), "concurrency-usages"));
    }

    [Fact]
    public async Task SourceOutsideDeclaredSubprojectUsesRepositoryFallbackOwnership()
    {
        InMemoryRepository repository = new();
        repository.WriteText("module/pom.xml", MavenProject);
        repository.WriteText(
            "unowned/Loose.java",
            "package unowned; public final class Loose { public boolean ready() { return true; } }");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact[] projects = [.. evidence.Facts.Where(fact =>
            fact.Kind == EvidenceKinds.EcosystemPackage &&
            fact.Tags.Contains("ecosystem:java", StringComparer.Ordinal) &&
            fact.Provenance.Analyzer == "efforthours.java-analyzer")];

        Assert.Contains(projects, project =>
            project.Scope == "." &&
            project.Tags.Contains("java-project:implicit-fallback", StringComparer.Ordinal));
        Assert.Contains(projects, project =>
            project.Scope == "module" &&
            project.Tags.Contains("java-project:declared", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact =>
            fact.Kind == EvidenceKinds.SourceStructure &&
            fact.Provenance.Analyzer == "efforthours.java-analyzer");
        Assert.Empty(ContractValidation.Validate(evidence));
    }

    [Fact]
    public async Task FullyQualifiedCanonicalCallsProduceSemanticEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "Call.java",
            "class Call { void send() { java.net.http.HttpClient.newHttpClient().sendAsync(null, null); } }");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Integration &&
            fact.Tags.Contains("technology:java-http", StringComparer.Ordinal));
    }

    [Fact]
    public async Task ExactDuplicateJavaBodiesDoNotIncreaseEstimatedEffort()
    {
        const string source = "package health; public final class Health { public boolean check() { return true; } }";
        EstimateReport single = await EstimateAsync(("src/Health.java", source));
        EstimateReport duplicate = await EstimateAsync(("src/Health.java", source), ("src/HealthCopy.java", source));

        Assert.Equal(single.TotalEffort.Expected, duplicate.TotalEffort.Expected);
        Assert.Equal(Category(single, EffortCategory.ProductionImplementation).Expected,
            Category(duplicate, EffortCategory.ProductionImplementation).Expected);
        Assert.Contains(duplicate.Diagnostics, diagnostic => diagnostic.Code == "FB1003");
    }

    private static async Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        await new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static async Task<EstimateReport> EstimateAsync(params (string Path, string Content)[] files)
    {
        InMemoryRepository repository = new();
        foreach ((string path, string content) in files) repository.WriteText(path, content);
        return new SeedEstimator().Estimate(await ScanAsync(repository), EstimationProfile.Implementation);
    }

    private static EvidenceFact ProjectFact(RepositoryEvidence evidence) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemPackage &&
            fact.Tags.Contains("scope:analyzed", StringComparer.Ordinal) &&
            fact.Tags.Contains("ecosystem:java", StringComparer.Ordinal));

    private static EvidenceFact LanguageFact(RepositoryEvidence evidence) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.Language &&
            fact.Tags.Contains("language:java", StringComparer.Ordinal));

    private static EvidenceFact FactOfKind(RepositoryEvidence evidence, string kind) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == kind &&
            fact.Provenance.Analyzer == "efforthours.java-analyzer");

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
