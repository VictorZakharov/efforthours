using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class GoChangeTests
{
    private const string GoMod = "module example.com/change\ngo 1.24\n";

    [Fact]
    public async Task FormattingAndOrdinaryCommentOnlyGoChangeHasZeroEffort()
    {
        ChangeState before = State(
            ("go.mod", GoMod),
            ("greet.go", "package greet\nfunc Hello(name string) string {\n\t// old explanation\n\tif\n                name == \"\" { return \"\" }\n\treturn \"Hello, \" + name\n}\n"));
        ChangeState after = State(
            ("go.mod", GoMod),
            ("greet.go", "package greet\n\nfunc Hello( name string ) string { // rewritten explanation\n  if name == \"\" { return \"\" }\n  return \"Hello, \"+name\n}\n"));

        ChangeEstimateReport report = await EstimateAsync(before, after);
        ChangePathEvidence path = Assert.Single(report.Evidence.Paths);

        Assert.Equal(ChangePathClassification.FormattingOnly, path.Classification);
        Assert.False(path.Represented);
        Assert.Equal(0m, report.TotalEffort.Expected);
        Assert.Empty(report.WorkItems);
    }

    [Theory]
    [InlineData(
        "package work\n//go:build linux\nfunc Value() int { return 1 }\n",
        "package work\n//go:build windows\nfunc Value() int { return 1 }\n")]
    [InlineData(
        "package work\nfunc Value() int { return 1 +\n2 }\n",
        "package work\nfunc Value() int { return 1\n+ 2 }\n")]
    [InlineData(
        "package work\n/* #cgo LDFLAGS: -lone */\nimport (\n    \"C\"\n)\n",
        "package work\n/* #cgo LDFLAGS: -ltwo */\nimport (\n    \"C\"\n)\n")]
    public async Task CompilerDirectiveSemicolonAndCgoChangesRemainMeaningful(
        string beforeSource,
        string afterSource)
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("go.mod", GoMod), ("work.go", beforeSource)),
            State(("go.mod", GoMod), ("work.go", afterSource)));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
    }

    [Theory]
    [MemberData(nameof(SemanticCases))]
    public async Task ImportQualifiedGoChangesReachTheirIntendedCategory(
        string beforeSource,
        string afterSource,
        EffortCategory category)
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("go.mod", GoMod), ("app.go", beforeSource)),
            State(("go.mod", GoMod), ("app.go", afterSource)));

        CategoryEstimate estimate = Assert.Single(
            report.Categories,
            candidate => candidate.Category == category);
        Assert.True(estimate.Hours.Expected > 0m);
        Assert.Contains(report.WorkItems, item => item.Category == category);
        Assert.Equal("change-seed/0.13.0+seed-rules/0.4.0", report.EstimatorVersion);
    }

    [Fact]
    public async Task AddingGoTestsProducesTestEffort()
    {
        ChangeState before = State(
            ("go.mod", GoMod),
            ("total.go", "package total\nfunc Sum(a, b int) int { return a + b }\n"));
        ChangeState after = State(
            ("go.mod", GoMod),
            ("total.go", "package total\nfunc Sum(a, b int) int { return a + b }\n"),
            ("total_test.go", "package total\nimport \"testing\"\nfunc TestSum(t *testing.T) { if Sum(1, 2) != 3 { t.Fatal(\"bad\") } }\n"));

        ChangeEstimateReport report = await EstimateAsync(before, after);

        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.UnitTesting && category.Hours.Expected > 0m);
    }

    [Fact]
    public async Task AddingGoEmbedWiringProducesBuildConfigurationEffort()
    {
        ChangeState before = State(
            ("go.mod", GoMod),
            ("assets.go", "package assets\nfunc Name() string { return \"assets\" }\n"));
        ChangeState after = State(
            ("go.mod", GoMod),
            ("assets.go", "package assets\nimport \"embed\"\n//go:embed static/*\nvar Files embed.FS\nfunc Name() string { return \"assets\" }\n"));

        ChangeEstimateReport report = await EstimateAsync(before, after);

        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.BuildConfigurationAndDeveloperTooling &&
            category.Hours.Expected > 0m);
    }

    public static TheoryData<string, string, EffortCategory> SemanticCases => new()
    {
        {
            "package app\nimport \"net/http\"\nfunc route() {}\n",
            "package app\nimport \"net/http\"\nfunc route() { http.HandleFunc(\"/status\", func(http.ResponseWriter, *http.Request) {}) }\n",
            EffortCategory.ProductionImplementation
        },
        {
            "package app\nimport \"net/http\"\nfunc load() {}\n",
            "package app\nimport \"net/http\"\nfunc load() { http.Get(\"https://example.invalid\") }\n",
            EffortCategory.ExternalIntegrationsAndProtocols
        },
        {
            "package app\nimport \"database/sql\"\nfunc load() {}\n",
            "package app\nimport \"database/sql\"\nfunc load() { db, _ := sql.Open(\"sqlite\", \"db\"); db.Query(\"select 1\") }\n",
            EffortCategory.DataModelingPersistenceAndMigrations
        },
        {
            "package app\nimport \"github.com/golang-jwt/jwt/v5\"\nfunc token() {}\n",
            "package app\nimport \"github.com/golang-jwt/jwt/v5\"\nfunc token() { jwt.New(jwt.SigningMethodHS256) }\n",
            EffortCategory.SecurityAndAccessibility
        },
        {
            "package app\nimport \"github.com/robfig/cron/v3\"\nfunc work() {}\n",
            "package app\nimport \"github.com/robfig/cron/v3\"\nfunc work() { scheduler := cron.New(); scheduler.AddFunc(\"@hourly\", work) }\n",
            EffortCategory.ProductionImplementation
        },
    };

    private static Task<ChangeEstimateReport> EstimateAsync(
        ChangeState before,
        ChangeState after) => new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "in-memory-go-change",
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
