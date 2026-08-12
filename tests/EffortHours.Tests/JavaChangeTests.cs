using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class JavaChangeTests
{
    private const string Pom = "<project><groupId>example</groupId><artifactId>change</artifactId></project>";

    [Fact]
    public async Task JavaCodeFormattingOnlyChangeHasZeroEffort()
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("pom.xml", Pom), ("Greet.java", "class Greet { String hello(String name) { if (name.isEmpty()) { return \"\"; } return \"Hello, \" + name; } }")),
            State(("pom.xml", Pom), ("Greet.java", "class Greet\n{\n  String hello( String name )\n  {\n    if(name.isEmpty()){return \"\";}\n    return \"Hello, \"+name;\n  }\n}\n")));

        ChangePathEvidence path = Assert.Single(report.Evidence.Paths);
        Assert.Equal(ChangePathClassification.FormattingOnly, path.Classification);
        Assert.False(path.Represented);
        Assert.Equal(0m, report.TotalEffort.Expected);
    }

    [Fact]
    public async Task OrdinaryJavaCommentOnlyChangeHasZeroEffort()
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("Value.java", "class Value { // old explanation\n int get() { return 1; } }")),
            State(("Value.java", "class Value { // rewritten explanation\n int get() { return 1; } }")));

        Assert.Equal(ChangePathClassification.FormattingOnly,
            Assert.Single(report.Evidence.Paths).Classification);
        Assert.Equal(0m, report.TotalEffort.Expected);
    }

    [Theory]
    [InlineData("class Text { String value = \"one\"; }", "class Text { String value = \"two\"; }")]
    [InlineData("class Text { String value = \"\"\"one\"\"\"; }", "class Text { String value = \"\"\"two\"\"\"; }")]
    [InlineData("class Text { int value = one + +two; }", "class Text { int value = one++ + two; }")]
    [InlineData("class Text { double value = 1e+2; }", "class Text { double value = 1e + 2; }")]
    [InlineData("/** one */ class Text { }", "/** two */ class Text { }")]
    [InlineData("/// one\nclass Text { }", "/// two\nclass Text { }")]
    public async Task LiteralsOperatorsAndJavadocChangesRemainMeaningful(string before, string after)
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("pom.xml", Pom), ("Text.java", before)),
            State(("pom.xml", Pom), ("Text.java", after)));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
    }

    [Fact]
    public async Task UnicodeEscapesRemainConservativelyMeaningfulBeforeJavaLexing()
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("Escape.java", "class Escape { // \\u000a int value = 1;\n}")),
            State(("Escape.java", "class Escape { // \\u000a int value = 2;\n}")));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
    }

    [Theory]
    [MemberData(nameof(SemanticCases))]
    public async Task ImportQualifiedJavaChangesReachTheirIntendedCategory(
        string before,
        string after,
        EffortCategory category)
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("pom.xml", Pom), ("App.java", before)),
            State(("pom.xml", Pom), ("App.java", after)));

        Assert.Contains(report.Categories, candidate =>
            candidate.Category == category && candidate.Hours.Expected > 0m);
        Assert.Equal("change-seed/0.12.0+seed-rules/0.4.0", report.EstimatorVersion);
    }

    [Fact]
    public async Task AddingJUnitTestProducesUnitTestEffort()
    {
        ChangeState before = State(("pom.xml", Pom), ("Total.java", "class Total { int sum(int a, int b) { return a + b; } }"));
        ChangeState after = State(
            ("pom.xml", Pom),
            ("Total.java", "class Total { int sum(int a, int b) { return a + b; } }"),
            ("TotalTest.java", "import org.junit.jupiter.api.Test; import static org.junit.jupiter.api.Assertions.assertEquals; class TotalTest { @Test void sums() { assertEquals(3, new Total().sum(1, 2)); } }"));

        ChangeEstimateReport report = await EstimateAsync(before, after);

        Assert.Contains(report.Categories, candidate =>
            candidate.Category == EffortCategory.UnitTesting && candidate.Hours.Expected > 0m);
    }

    public static TheoryData<string, string, EffortCategory> SemanticCases => new()
    {
        {
            "import org.springframework.web.bind.annotation.GetMapping; class App { void route() { } }",
            "import org.springframework.web.bind.annotation.GetMapping; class App { @GetMapping(\"/status\") void route() { } }",
            EffortCategory.ProductionImplementation
        },
        {
            "import java.net.http.HttpClient; class App { void call() { } }",
            "import java.net.http.HttpClient; class App { void call() { HttpClient.newHttpClient().sendAsync(null, null); } }",
            EffortCategory.ExternalIntegrationsAndProtocols
        },
        {
            "import org.springframework.jdbc.core.JdbcTemplate; class App { JdbcTemplate jdbc; void load() { } }",
            "import org.springframework.jdbc.core.JdbcTemplate; class App { JdbcTemplate jdbc; void load() { jdbc.query(\"select 1\", null); } }",
            EffortCategory.DataModelingPersistenceAndMigrations
        },
        {
            "import org.springframework.security.access.prepost.PreAuthorize; class App { void load() { } }",
            "import org.springframework.security.access.prepost.PreAuthorize; class App { @PreAuthorize(\"hasRole('USER')\") void load() { } }",
            EffortCategory.SecurityAndAccessibility
        },
    };

    private static Task<ChangeEstimateReport> EstimateAsync(ChangeState before, ChangeState after) =>
        new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "in-memory-java-change",
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
