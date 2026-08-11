using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed partial class GoAnalyzerTests
{
    private const string GoMod =
        """
        module example.com/orders

        go 1.24

        require (
            github.com/gin-gonic/gin v1.10.0
            github.com/golang-jwt/jwt/v5 v5.2.0
            github.com/robfig/cron/v3 v3.0.0
            github.com/go-playground/validator/v10 v10.20.0
        )
        """;

    [Fact]
    public async Task ModuleStructureFrameworksBuildDirectivesAndTestsProduceEvidenceAndEffort()
    {
        InMemoryRepository repository = new();
        repository.WriteText("go.mod", GoMod);
        repository.WriteText(
            "cmd/orders/main_linux.go",
            """
            //go:build linux

            package main

            import (
                "database/sql"
                "embed"
                "net/http"
                "sync"

                "github.com/gin-gonic/gin"
                "github.com/go-playground/validator/v10"
                "github.com/golang-jwt/jwt/v5"
                "github.com/robfig/cron/v3"
            )

            //go:embed static/*
            //go:generate stringer -type=Request
            var assets embed.FS

            type Request[T any] struct { Value T }
            type Store interface { Load(int) (Request[string], error) }

            func main() {
                router := gin.Default()
                router.GET("/orders/:id", getOrder)
                db, _ := sql.Open("sqlite", "orders.db")
                db.Query("select 1")
                http.Get("https://example.invalid")
                jwt.New(jwt.SigningMethodHS256)
                scheduler := cron.New()
                scheduler.AddFunc("@hourly", refresh)
                validate := validator.New()
                validate.Struct(Request[int]{Value: 1})
                var wait sync.WaitGroup
                wait.Add(1)
                ch := make(chan int)
                go func() { ch <- 1 }()
            }

            func getOrder() {}
            func refresh() {}
            """);
        repository.WriteText(
            "cmd/orders/main_test.go",
            """
            package main

            import (
                "testing"
                "github.com/stretchr/testify/require"
            )

            func TestOrders(t *testing.T) {
                cases := []int{1, 2}
                for _, value := range cases {
                    t.Run("case", func(t *testing.T) {
                        require.Equal(t, value, value)
                        if value == 0 { t.Fatalf("invalid") }
                    })
                }
            }

            func BenchmarkOrders(b *testing.B) {}
            func ExampleOrders() {}
            func FuzzOrders(f *testing.F) {}
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);
        EvidenceFact module = ModuleFact(evidence);
        EvidenceFact structure = FactOfKind(evidence, EvidenceKinds.SourceStructure);
        EvidenceFact test = FactOfKind(evidence, EvidenceKinds.EcosystemTest);
        EvidenceFact build = evidence.Facts.Single(fact => fact.Kind == EvidenceKinds.BuildConfiguration &&
            fact.Provenance.Analyzer == "efforthours.go-analyzer");

        Assert.Equal("0.1.0", module.Provenance.AnalyzerVersion);
        Assert.Contains("package-role:server", module.Tags);
        Assert.Contains("package:cli-bin", module.Tags);
        Assert.Contains("package:library-exports", module.Tags);
        Assert.Contains("syntax:token-backed", structure.Tags);
        Assert.Contains("parser-confidence:medium", structure.Tags);
        Assert.True(Measurement(structure, "functions") >= 3m);
        Assert.True(Measurement(structure, "types") >= 2m);
        Assert.True(Measurement(structure, "interfaces") >= 1m);
        Assert.True(Measurement(structure, "generic-declarations") >= 1m);
        Assert.True(Measurement(structure, "goroutines") >= 1m);
        Assert.True(Measurement(structure, "channel-usages") >= 1m);
        Assert.True(Measurement(build, "build-constraints") >= 1m);
        Assert.True(Measurement(build, "platform-files") >= 1m);
        Assert.True(Measurement(build, "embed-directives") >= 1m);
        Assert.True(Measurement(build, "code-generation-directives") >= 1m);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ApiSurface &&
            fact.Tags.Contains("technology:gin", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.DataAccess &&
            fact.Tags.Contains("technology:database/sql", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Integration &&
            fact.Tags.Contains("technology:net/http", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SecurityConfiguration);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.BackgroundWork);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Validation);
        Assert.Equal(1m, Measurement(test, "test-cases"));
        Assert.Equal(1m, Measurement(test, "benchmarks"));
        Assert.Equal(1m, Measurement(test, "examples"));
        Assert.Equal(1m, Measurement(test, "fuzz-tests"));
        Assert.True(Measurement(test, "parameterized-cases") >= 1m);
        Assert.True(Measurement(test, "assertions") >= 1m);
        Assert.Contains("analysis-status:analyzed", LanguageFact(evidence).Tags);
        Assert.Contains("analysis-depth:token-backed", LanguageFact(evidence).Tags);
        Assert.DoesNotContain(estimate.Diagnostics, diagnostic => diagnostic.Code == "FB1002");
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:polyglot-source-backbone");
        Assert.Contains(estimate.WorkItems, item => item.Category == EffortCategory.UnitTesting);
        Assert.Contains(estimate.WorkItems, item => item.Category == EffortCategory.BuildConfigurationAndDeveloperTooling);
        Assert.Contains(estimate.WorkItems, item => item.Category == EffortCategory.PackagingDeploymentAndReleaseArtifacts);
        Assert.Empty(ContractValidation.Validate(evidence));
        Assert.Empty(ContractValidation.Validate(estimate));
    }

    [Fact]
    public async Task LocalFrameworkNamesWithoutImportsDoNotProduceSemanticEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "main.go",
            """
            package local

            type router struct{}
            func (router) GET(path string, handler any) {}
            type db struct{}
            func (db) Query(query string) {}
            type jwt struct{}
            func (jwt) New() {}

            func localOnly() {
                router{}.GET("/not-a-route", nil)
                db{}.Query("not persistence")
                jwt{}.New()
            }

            func TestSupport() {}
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains("go-module:implicit-fallback", ModuleFact(evidence).Tags);
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind is
            EvidenceKinds.ApiSurface or EvidenceKinds.DataAccess or
            EvidenceKinds.Integration or EvidenceKinds.SecurityConfiguration);
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemTest);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
    }

    [Fact]
    public async Task SimilarButNonCanonicalImportPathsDoNotProduceFrameworkEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "main.go",
            """
            package local

            import (
                gin "example.com/gin-gonic/gin"
                jwt "example.com/golang-jwt/jwt/v5"
            )

            func localOnly() {
                gin.GET("/not-a-route", nil)
                jwt.New()
            }
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind is
            EvidenceKinds.ApiSurface or EvidenceKinds.SecurityConfiguration);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
    }

    [Fact]
    public async Task ExactDuplicateGoBodiesDoNotIncreaseEstimatedEffort()
    {
        const string source = "package health\nfunc Check() bool { return true }\n";
        EstimateReport single = await EstimateAsync(("health.go", source));
        EstimateReport duplicate = await EstimateAsync(("health.go", source), ("copy.go", source));

        Assert.Equal(single.TotalEffort.Expected, duplicate.TotalEffort.Expected);
        Assert.Equal(
            Category(single, EffortCategory.ProductionImplementation).Expected,
            Category(duplicate, EffortCategory.ProductionImplementation).Expected);
        Assert.Contains(duplicate.Diagnostics, diagnostic => diagnostic.Code == "FB1003");
    }

    private static async Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        await new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static async Task<EstimateReport> EstimateAsync(params (string Path, string Content)[] files)
    {
        InMemoryRepository repository = new();
        foreach ((string path, string content) in files) repository.WriteText(path, content);
        RepositoryEvidence evidence = await ScanAsync(repository);
        return new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);
    }

    private static EvidenceFact ModuleFact(RepositoryEvidence evidence) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemPackage &&
            fact.Tags.Contains("scope:analyzed", StringComparer.Ordinal) &&
            fact.Tags.Contains("ecosystem:go", StringComparer.Ordinal));

    private static EvidenceFact LanguageFact(RepositoryEvidence evidence) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.Language &&
            fact.Tags.Contains("language:go", StringComparer.Ordinal));

    private static EvidenceFact FactOfKind(RepositoryEvidence evidence, string kind) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == kind);

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
