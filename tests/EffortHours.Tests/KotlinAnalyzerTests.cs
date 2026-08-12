using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class KotlinAnalyzerTests
{
    private const string GradleBuild =
        """
        plugins {
            kotlin("jvm") version "2.2.0"
            id("com.google.devtools.ksp") version "2.2.0-2.0.2"
        }
        dependencies {
            implementation("io.ktor:ktor-server-core:3.2.3")
            implementation("org.jetbrains.kotlinx:kotlinx-coroutines-core:1.10.2")
            testImplementation("org.jetbrains.kotlin:kotlin-test")
        }
        """;

    [Fact]
    public async Task KotlinStructureFrameworksTestsAndBuildProduceEvidenceAndEffort()
    {
        InMemoryRepository repository = new();
        repository.WriteText("settings.gradle.kts", "rootProject.name = \"orders\"");
        repository.WriteText("build.gradle.kts", GradleBuild);
        repository.WriteText(
            "src/main/kotlin/example/orders/App.kt",
            """
            package example.orders

            import androidx.compose.runtime.Composable
            import androidx.room.Database
            import androidx.room.Entity
            import androidx.room.RoomDatabase
            import io.ktor.server.routing.get
            import io.ktor.server.routing.routing
            import jakarta.validation.constraints.NotBlank
            import kotlinx.coroutines.async
            import kotlinx.coroutines.flow.Flow
            import kotlinx.coroutines.flow.flow
            import okhttp3.OkHttpClient
            import org.springframework.security.access.prepost.PreAuthorize

            @Entity data class Order<T>(val id: T, @NotBlank val name: String?)
            sealed interface OrderResult
            enum class OrderState { Open, Closed }
            object Empty : OrderResult
            class OrderFactory { companion object { fun create() = Order("1", "new") } }
            @Database(entities = [Order::class], version = 1)
            abstract class OrdersDatabase : RoomDatabase()

            fun String.slug(): String = trim().lowercase()
            suspend fun loadOrders(client: OkHttpClient): Flow<Order<String>> {
                val request = client.newCall(null)
                return flow { if (request != null) async { request } }
            }

            @PreAuthorize("hasRole('USER')")
            fun routes() = routing { get("/orders") { } }

            @Composable fun OrdersScreen() { }
            fun main() { }
            """);
        repository.WriteText(
            "src/test/kotlin/example/orders/AppTest.kt",
            """
            package example.orders
            import kotlin.test.Test
            import kotlin.test.assertEquals
            import io.mockk.mockk
            class AppTest {
                @Test fun slugIsStable() { val value = mockk<Order<String>>(); assertEquals("a", "A".slug()) }
            }
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);
        EvidenceFact project = Assert.Single(evidence.Facts, fact => fact.Id.StartsWith("kotlin:project:", StringComparison.Ordinal));
        EvidenceFact structure = KotlinFact(evidence, EvidenceKinds.SourceStructure);
        EvidenceFact build = KotlinFact(evidence, EvidenceKinds.BuildConfiguration);
        EvidenceFact test = KotlinFact(evidence, EvidenceKinds.EcosystemTest);

        Assert.Equal("0.1.0", project.Provenance.AnalyzerVersion);
        Assert.Contains("scope:analyzed", project.Tags);
        Assert.Contains("ecosystem:java", project.Tags);
        Assert.Contains("ecosystem:kotlin", project.Tags);
        Assert.Contains("package-role:application", project.Tags);
        Assert.Contains("analysis-status:analyzed", LanguageFact(evidence).Tags);
        Assert.Contains("analysis-depth:token-backed", LanguageFact(evidence).Tags);
        Assert.True(Measurement(structure, "types") >= 7m);
        Assert.True(Measurement(structure, "data-classes") >= 1m);
        Assert.True(Measurement(structure, "sealed-types") >= 1m);
        Assert.True(Measurement(structure, "interfaces") >= 1m);
        Assert.True(Measurement(structure, "objects") >= 2m);
        Assert.True(Measurement(structure, "extension-functions") >= 1m);
        Assert.True(Measurement(structure, "suspend-functions") >= 1m);
        Assert.True(Measurement(structure, "nullability-usages") >= 1m);
        Assert.True(Measurement(structure, "coroutine-usages") >= 1m);
        Assert.True(Measurement(structure, "flow-usages") >= 1m);
        Assert.True(Measurement(build, "plugins") >= 1m);
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == KotlinEvidenceAnalyzer &&
            fact.Kind == EvidenceKinds.ApiSurface && fact.Tags.Contains("technology:ktor-server", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == KotlinEvidenceAnalyzer &&
            fact.Kind == EvidenceKinds.UserInterface && fact.Tags.Contains("technology:android-compose", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == KotlinEvidenceAnalyzer &&
            fact.Kind == EvidenceKinds.DataAccess);
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == KotlinEvidenceAnalyzer &&
            fact.Kind == EvidenceKinds.Integration);
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == KotlinEvidenceAnalyzer &&
            fact.Kind == EvidenceKinds.SecurityConfiguration);
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == KotlinEvidenceAnalyzer &&
            fact.Kind == EvidenceKinds.BackgroundWork);
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == KotlinEvidenceAnalyzer &&
            fact.Kind == EvidenceKinds.Validation);
        Assert.True(Measurement(test, "test-cases") >= 1m);
        Assert.True(Measurement(test, "assertions") >= 1m);
        Assert.True(Measurement(test, "mock-usages") >= 1m);
        Assert.DoesNotContain(estimate.Diagnostics, diagnostic => diagnostic.Code == "FB1002");
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:polyglot-source-backbone");
        Assert.Contains(estimate.WorkItems, item => item.Category == EffortCategory.UnitTesting);
        Assert.Empty(ContractValidation.Validate(evidence));
        Assert.Empty(ContractValidation.Validate(estimate));
    }

    [Fact]
    public async Task FrameworkNamesWithoutCanonicalImportsRemainOrdinaryStructure()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "Local.kt",
            """
            annotation class RestController
            annotation class GetMapping
            annotation class Composable
            class HttpClient { fun send() = Unit }
            @RestController class Local {
                @GetMapping @Composable fun route() { HttpClient().send(); launch { }; query() }
                fun launch(block: () -> Unit) = block()
                fun query() = Unit
            }
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains("kotlin-project:implicit-fallback",
            Assert.Single(evidence.Facts, fact => fact.Id.StartsWith("kotlin:project:", StringComparison.Ordinal)).Tags);
        Assert.DoesNotContain(evidence.Facts, fact => fact.Provenance.Analyzer == KotlinEvidenceAnalyzer && fact.Kind is
            EvidenceKinds.ApiSurface or EvidenceKinds.DataAccess or EvidenceKinds.Integration or
            EvidenceKinds.UserInterface or EvidenceKinds.BackgroundWork);
    }

    [Fact]
    public async Task MixedJavaAndKotlinReuseOneJvmProjectAndBuildScope()
    {
        InMemoryRepository repository = new();
        repository.WriteText("build.gradle.kts", GradleBuild);
        repository.WriteText("src/main/java/example/JavaApi.java", "package example; public class JavaApi { public int value() { return 1; } }");
        repository.WriteText("src/main/kotlin/example/KotlinApi.kt", "package example\ndata class KotlinApi(val value: Int)");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);

        Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemPackage &&
            fact.Tags.Contains("scope:analyzed", StringComparer.Ordinal) &&
            fact.Tags.Contains("ecosystem:java", StringComparer.Ordinal));
        Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.BuildConfiguration &&
            fact.Scope == "." && fact.Provenance.Analyzer is "efforthours.java-analyzer" or KotlinEvidenceAnalyzer);
        Assert.Contains(evidence.Facts, fact => fact.Id.StartsWith("java:source:", StringComparison.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Id.StartsWith("kotlin:source:", StringComparison.Ordinal));
        Assert.True(estimate.TotalEffort.Expected > 0m);
        Assert.Empty(ContractValidation.Validate(estimate));
    }

    [Fact]
    public async Task KotlinOnlyGradleModulesRetainLiteralJvmProjectEdges()
    {
        InMemoryRepository repository = new();
        repository.WriteText("settings.gradle.kts", "rootProject.name = \"suite\"\ninclude(\":api\", \":domain\")");
        repository.WriteText("build.gradle.kts", "plugins { kotlin(\"jvm\") version \"2.2.0\" }");
        repository.WriteText("api/build.gradle.kts", "dependencies { implementation(project(\":domain\")) }");
        repository.WriteText("domain/build.gradle.kts", "plugins { kotlin(\"jvm\") }");
        repository.WriteText("domain/src/main/kotlin/example/domain/Order.kt", "package example.domain\ndata class Order(val id: String)");
        repository.WriteText("api/src/main/kotlin/example/api/Api.kt", "package example.api\nimport example.domain.Order\nfun load(): Order = Order(\"1\")");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Scope == "api" &&
            fact.Tags.Contains("target-scope:domain", StringComparer.Ordinal) &&
            fact.Tags.Contains("reference-kind:build", StringComparer.Ordinal));
        Assert.Empty(ContractValidation.Validate(evidence));
    }

    [Fact]
    public async Task MaintainedScriptAndGradleKotlinDslHaveDistinctBoundaries()
    {
        InMemoryRepository repository = new();
        repository.WriteText("build.gradle.kts", GradleBuild);
        repository.WriteText("tools/release.main.kts", "import kotlinx.coroutines.runBlocking\nrunBlocking { println(\"release\") }");
        repository.WriteText("tools/SmokeTest.kts", "import kotlin.test.*\n@Test fun smoke() { assertEquals(1, 1) }");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact structure = KotlinFact(evidence, EvidenceKinds.SourceStructure);

        Assert.Equal(2m, Measurement(structure, "scripts"));
        Assert.DoesNotContain(structure.Locations, location => location.Path == "build.gradle.kts");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8203");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8205");
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.EntryPoint &&
            fact.Locations.Any(location => location.Path == "tools/release.main.kts"));
        EvidenceFact scriptTest = Assert.Single(evidence.Facts, fact =>
            fact.Kind == EvidenceKinds.EcosystemTest &&
            fact.Locations.Any(location => location.Path == "tools/SmokeTest.kts"));
        Assert.True(Measurement(scriptTest, "test-cases") >= 1m);
        Assert.True(Measurement(scriptTest, "assertions") >= 1m);
    }

    [Fact]
    public async Task MalformedKotlinLowersConfidenceWithoutDisclosingSource()
    {
        InMemoryRepository repository = new();
        repository.WriteText("Broken.kt", "package safe\nclass Broken { val privateMarker = \"DO_NOT_DISCLOSE_91827\"");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact structure = KotlinFact(evidence, EvidenceKinds.SourceStructure);
        string serialized = System.Text.Json.JsonSerializer.Serialize(evidence);

        Assert.Contains("parser-confidence:low", structure.Tags);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8202");
        Assert.DoesNotContain("DO_NOT_DISCLOSE_91827", serialized, StringComparison.Ordinal);
        Assert.Empty(ContractValidation.Validate(evidence));
    }

    [Fact]
    public async Task GeneratedKotlinIsExcludedAndSourceSummariesDoNotDiscloseContent()
    {
        InMemoryRepository repository = new();
        repository.WriteText("src/main/kotlin/App.kt", "package safe\nclass App { val secret = \"DO_NOT_DISCLOSE_48291\" }");
        repository.WriteText("generated/Api.kt", "package generated\nclass GeneratedApi { fun many() = 1 }");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact structure = KotlinFact(evidence, EvidenceKinds.SourceStructure);
        string serialized = System.Text.Json.JsonSerializer.Serialize(evidence.Facts
            .Where(fact => fact.Provenance.Analyzer == KotlinEvidenceAnalyzer));

        Assert.Equal(1m, Measurement(structure, "files"));
        Assert.DoesNotContain("DO_NOT_DISCLOSE_48291", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(structure.Locations, location => location.Path == "generated/Api.kt");
    }

    private const string KotlinEvidenceAnalyzer = "efforthours.kotlin-analyzer";

    private static async Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        await new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static EvidenceFact KotlinFact(RepositoryEvidence evidence, string kind) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == kind &&
            fact.Provenance.Analyzer == KotlinEvidenceAnalyzer);

    private static EvidenceFact LanguageFact(RepositoryEvidence evidence) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.Language &&
            fact.Tags.Contains("language:kotlin", StringComparer.Ordinal));

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;
}
