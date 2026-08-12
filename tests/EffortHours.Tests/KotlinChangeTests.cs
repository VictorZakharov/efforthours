using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class KotlinChangeTests
{
    private const string Build = "plugins { kotlin(\"jvm\") version \"2.2.0\" }";

    [Fact]
    public async Task KotlinFormattingAndOrdinaryCommentsHaveZeroEffort()
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("build.gradle.kts", Build), ("Greet.kt", "data class Greet(val name: String); fun hello(value: Greet): String { /* old */ return \"Hello, ${value.name}\"; }")),
            State(("build.gradle.kts", Build), ("Greet.kt", """
                data class Greet(
                    val name: String,
                )

                fun hello(value: Greet): String {
                    // rewritten explanation
                    return "Hello, ${value.name}"
                }
                """)));

        ChangePathEvidence path = Assert.Single(report.Evidence.Paths);
        Assert.Equal(ChangePathClassification.FormattingOnly, path.Classification);
        Assert.False(path.Represented);
        Assert.Equal(0m, report.TotalEffort.Expected);
    }

    [Theory]
    [InlineData("fun text() = \"one\"", "fun text() = \"two\"")]
    [InlineData("fun text() = \"\"\"one\"\"\"", "fun text() = \"\"\"two\"\"\"")]
    [InlineData("/** one */ fun value() = 1", "/** two */ fun value() = 1")]
    [InlineData("fun run() { return\nprintln(\"x\") }", "fun run() { return println(\"x\") }")]
    [InlineData("fun `one name`() = 1", "fun `two name`() = 1")]
    public async Task LiteralsKdocBackticksAndSemanticNewlinesRemainMeaningful(string before, string after)
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("Value.kt", before)),
            State(("Value.kt", after)));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
    }

    [Theory]
    [MemberData(nameof(SemanticCases))]
    public async Task ImportQualifiedKotlinChangesReachTheirIntendedCategory(
        string before,
        string after,
        EffortCategory category)
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("build.gradle.kts", Build), ("App.kt", before)),
            State(("build.gradle.kts", Build), ("App.kt", after)));

        Assert.Contains(report.Categories, candidate =>
            candidate.Category == category && candidate.Hours.Expected > 0m);
        Assert.Equal("change-seed/0.18.0+seed-rules/0.4.0", report.EstimatorVersion);
    }

    [Fact]
    public async Task AddingKotlinTestProducesUnitTestEffort()
    {
        ChangeState before = State(
            ("build.gradle.kts", Build),
            ("Total.kt", "fun sum(a: Int, b: Int) = a + b"));
        ChangeState after = State(
            ("build.gradle.kts", Build),
            ("Total.kt", "fun sum(a: Int, b: Int) = a + b"),
            ("TotalTest.kt", "import kotlin.test.Test\nimport kotlin.test.assertEquals\nclass TotalTest { @Test fun sums() { assertEquals(3, sum(1, 2)) } }"));

        ChangeEstimateReport report = await EstimateAsync(before, after);

        Assert.Contains(report.Categories, candidate =>
            candidate.Category == EffortCategory.UnitTesting && candidate.Hours.Expected > 0m);
    }

    public static TheoryData<string, string, EffortCategory> SemanticCases => new()
    {
        {
            "import io.ktor.server.routing.get\nfun routes() = Unit",
            "import io.ktor.server.routing.get\nfun routes() { get(\"/status\") { } }",
            EffortCategory.ProductionImplementation
        },
        {
            "import okhttp3.OkHttpClient\nfun call(client: OkHttpClient) = Unit",
            "import okhttp3.OkHttpClient\nfun call(client: OkHttpClient) { client.newCall(null) }",
            EffortCategory.ExternalIntegrationsAndProtocols
        },
        {
            "import androidx.room.Query\ninterface Store",
            "import androidx.room.Query\ninterface Store { @Query(\"select 1\") fun load(): Int }",
            EffortCategory.DataModelingPersistenceAndMigrations
        },
        {
            "import org.springframework.security.access.prepost.PreAuthorize\nfun load() = Unit",
            "import org.springframework.security.access.prepost.PreAuthorize\n@PreAuthorize(\"hasRole('USER')\") fun load() = Unit",
            EffortCategory.SecurityAndAccessibility
        },
    };

    private static Task<ChangeEstimateReport> EstimateAsync(ChangeState before, ChangeState after) =>
        new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "in-memory-kotlin-change",
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
