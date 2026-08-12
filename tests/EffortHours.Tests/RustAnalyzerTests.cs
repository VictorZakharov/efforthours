using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class RustAnalyzerTests
{
    private const string Analyzer = "efforthours.rust-analyzer";

    [Fact]
    public async Task CargoWorkspaceRustSemanticsTargetsAndTestsProduceTraceableEvidenceAndEffort()
    {
        InMemoryRepository repository = RichRepository();

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);
        EvidenceFact source = AnalyzerFact(evidence, EvidenceKinds.SourceStructure, "crates/app");

        Assert.Contains("analysis-status:analyzed", Language(evidence).Tags);
        Assert.Contains("analysis-depth:token-backed", Language(evidence).Tags);
        Assert.Equal("0.1.0", source.Provenance.AnalyzerVersion);
        Assert.True(Measurement(source, "types") >= 3m);
        Assert.True(Measurement(source, "traits") >= 1m);
        Assert.True(Measurement(source, "impls") >= 1m);
        Assert.True(Measurement(source, "functions") + Measurement(source, "methods") >= 4m);
        Assert.True(Measurement(source, "public-symbols") >= 3m);
        Assert.True(Measurement(source, "generic-declarations") >= 1m);
        Assert.True(Measurement(source, "lifetime-usages") >= 1m);
        Assert.True(Measurement(source, "async-units") >= 1m);
        Assert.True(Measurement(source, "unsafe-blocks") >= 1m);
        Assert.True(Measurement(source, "error-paths") >= 1m);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ApiSurface &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("technology:axum"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.DataAccess &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("technology:sqlx"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Integration &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("technology:reqwest"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SecurityConfiguration &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("technology:argon2"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.BackgroundWork &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("technology:tokio"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Validation &&
            fact.Provenance.Analyzer == Analyzer);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Integration &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("integration-kind:ffi"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemTest &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("test-type:integration"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemTest &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("test-type:benchmark"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemTest &&
            fact.Provenance.Analyzer == Analyzer &&
            Measurement(fact, "documentation-tests") > 0m);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Provenance.Analyzer == Analyzer && fact.Scope == "crates/app" &&
            fact.Tags.Contains("target-scope:crates/domain"));
        EvidenceFact build = AnalyzerFact(evidence, EvidenceKinds.BuildConfiguration, "crates/app");
        Assert.True(Measurement(build, "targets") >= 4m);
        Assert.Equal(1m, Measurement(build, "features"));
        Assert.Equal(1m, Measurement(build, "build-scripts"));
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8805");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8806");
        Assert.Contains(estimate.Categories, item => item.Category == EffortCategory.ProductionImplementation);
        Assert.Contains(estimate.Categories, item => item.Category == EffortCategory.DataModelingPersistenceAndMigrations);
        Assert.Contains(estimate.Categories, item => item.Category == EffortCategory.ExternalIntegrationsAndProtocols);
        Assert.Contains(estimate.Categories, item => item.Category == EffortCategory.SecurityAndAccessibility);
        Assert.Contains(estimate.Categories, item => item.Category == EffortCategory.IntegrationContractAndComponentTesting);
        Assert.DoesNotContain(estimate.Diagnostics, diagnostic => diagnostic.Code is "FB1001" or "FB1002");
        Assert.Empty(ContractValidation.Validate(evidence));
        Assert.Empty(ContractValidation.Validate(estimate));
    }

    [Fact]
    public async Task LocalFrameworkNamesAndCratesDoNotCreateSemanticEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "Cargo.toml",
            "[package]\nname = \"namesakes\"\nversion = \"1.0.0\"\n" +
            "[dependencies]\naxum = \"0.8\"\nsqlx = \"0.8\"\nreqwest = \"0.12\"\n" +
            "local_web = { package = \"axum\", path = \"crates/local-web\" }\n");
        repository.WriteText(
            "src/lib.rs",
            "mod axum { pub struct Router; } use axum::Router; " +
            "use local_web::Router as LocalRouter; pub struct Client; " +
            "impl Router { pub fn route(&self) {} }\npub fn query() {}\npub fn send() {}\n");
        repository.WriteText(
            "crates/local-web/Cargo.toml",
            "[package]\nname = \"axum\"\nversion = \"1.0.0\"\n");
        repository.WriteText("crates/local-web/src/lib.rs", "pub struct Router;\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.DoesNotContain(evidence.Facts, fact =>
            fact.Provenance.Analyzer == Analyzer && fact.Kind is
                EvidenceKinds.ApiSurface or EvidenceKinds.DataAccess);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure &&
            fact.Provenance.Analyzer == Analyzer);
    }

    [Fact]
    public async Task ExactDuplicateRustBodiesDoNotIncreaseEstimatedEffort()
    {
        const string source = "pub struct Service; impl Service { pub fn run(&self) -> bool { true } }\n";
        EstimateReport single = await EstimateAsync(("src/service.rs", source));
        EstimateReport duplicate = await EstimateAsync(("src/service.rs", source), ("src/copy.rs", source));

        Assert.Equal(single.TotalEffort.Expected, duplicate.TotalEffort.Expected);
        Assert.Equal(
            Category(single, EffortCategory.ProductionImplementation).Expected,
            Category(duplicate, EffortCategory.ProductionImplementation).Expected);
        Assert.Contains(duplicate.Diagnostics, diagnostic => diagnostic.Code == "FB1003");
    }

    [Fact]
    public async Task MixedRustJavaScriptAndSqlRepositoryKeepsAnalyzerOwnershipSeparate()
    {
        InMemoryRepository repository = new();
        repository.WriteText("Cargo.toml", "[package]\nname = \"mixed\"\nversion = \"1.0.0\"\n");
        repository.WriteText("src/lib.rs", "pub struct Order { pub id: i64 }\n");
        repository.WriteText("web/order.js", "export function showOrder(order) { return order.id; }\n");
        repository.WriteText("database/schema.sql", "create table orders (id integer primary key);\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == Analyzer &&
            fact.Locations.Any(location => location.Path == "src/lib.rs"));
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == "efforthours.javascript-analyzer" &&
            fact.Locations.Any(location => location.Path == "web/order.js"));
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == "efforthours.sql-analyzer" &&
            fact.Locations.Any(location => location.Path == "database/schema.sql"));
        Assert.DoesNotContain(evidence.Facts.Where(fact => fact.Provenance.Analyzer == Analyzer)
            .SelectMany(fact => fact.Locations), location =>
                location.Path is "web/order.js" or "database/schema.sql");
        Assert.Empty(ContractValidation.Validate(evidence));
    }

    [Fact]
    public async Task DottedDependencyTablesCreateLocalEdgesAndQualifiedSemantics()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "Cargo.toml",
            "[package]\nname = \"app\"\nversion = \"1.0.0\"\n" +
            "[dependencies.domain]\npath = \"crates/domain\"\n" +
            "[dependencies.web]\npackage = \"axum\"\nversion = \"0.8\"\n" +
            "[dependencies.missing]\npath = \"crates/missing\"\n" +
            "[[test]]\nname = \"protocol\"\npath = \"spec/protocol.rs\"\n");

        repository.WriteText("src/lib.rs", "use web::Router; pub fn routes() { let _ = Router::new(); }\n");
        repository.WriteText("crates/domain/Cargo.toml", "[package]\nname = \"domain\"\nversion = \"1.0.0\"\n");
        repository.WriteText("crates/domain/src/lib.rs", "pub struct Order;\n");
        repository.WriteText("spec/protocol.rs", "fn protocol_contract() {}\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Scope == "." && fact.Tags.Contains("ecosystem:rust", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ApiSurface &&
            fact.Provenance.Analyzer == Analyzer);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.PackageReference &&
            fact.Tags.Contains("dependency:web", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.BuildConfiguration &&
            fact.Scope == "." && fact.Provenance.Analyzer == Analyzer &&
            Measurement(fact, "unresolved-values") >= 1m);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemTest &&
            fact.Locations.Any(location => location.Path == "spec/protocol.rs") &&
            fact.Tags.Contains("test-type:integration", StringComparer.Ordinal));
        Assert.DoesNotContain(AnalyzerFact(evidence, EvidenceKinds.SourceStructure, ".").Locations,
            location => location.Path == "spec/protocol.rs");
    }

    [Fact]
    public async Task RustOutsideNestedCargoPackageGetsAnImplicitRootScope()
    {
        InMemoryRepository repository = new();
        repository.WriteText("tools.rs", "pub fn generate() {}\n");
        repository.WriteText("crates/domain/Cargo.toml", "[package]\nname = \"domain\"\nversion = \"1.0.0\"\n");
        repository.WriteText("crates/domain/src/lib.rs", "pub struct Order;\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Facts, fact => fact.Id == "rust:package:." && fact.Scope == ".");
        Assert.Contains(evidence.Facts, fact => fact.Id == "rust:source:." &&
            fact.Locations.Any(location => location.Path == "tools.rs"));
        Assert.Contains(evidence.Facts, fact => fact.Id == "rust:package:crates~domain");
        Assert.Empty(ContractValidation.Validate(evidence));
    }

    private static InMemoryRepository RichRepository()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "Cargo.toml",
            "[workspace]\nmembers = [\"crates/*\"]\nresolver = \"3\"\n");
        repository.WriteText(
            "crates/domain/Cargo.toml",
            "[package]\nname = \"domain\"\nversion = \"1.0.0\"\n[lib]\ncrate-type = [\"rlib\"]\n");
        repository.WriteText(
            "crates/domain/src/lib.rs",
            "pub struct Order<'a, T> { pub id: &'a T }\npub trait Repository<T> { fn save(&self, value: T); }\n");
        repository.WriteText(
            "crates/app/Cargo.toml",
            """
            [package]
            name = "store"
            version = "1.0.0"
            build = "build.rs"

            [features]
            telemetry = []

            [dependencies]
            domain = { path = "../domain" }
            axum = "0.8"
            sqlx = "0.8"
            reqwest = "0.12"
            argon2 = "0.5"
            clap = "4.5"
            tokio = "1.0"
            validator = "0.20"

            [build-dependencies]
            bindgen = "0.72"

            [dev-dependencies]
            criterion = "0.6"

            [[bin]]
            name = "store"
            path = "src/main.rs"
            """);
        repository.WriteText(
            "crates/app/src/main.rs",
            """
            /// Runs the service.
            /// ```rust
            /// let _ready = true;
            /// ```
            use axum::{routing::get, Router};
            use sqlx::query;
            use reqwest::Client;
            use argon2::Argon2;
            use clap::Parser;
            use tokio::task::JoinSet;
            use validator::Validate;
            use domain::Order;

            #[derive(Parser, Validate)]
            pub struct Options<'a, T> { pub value: &'a T }
            pub enum State { Ready, Stopped }
            pub trait Runner { fn run(&self) -> Result<(), Error>; }
            pub struct Error;
            impl Runner for State {
                fn run(&self) -> Result<(), Error> {
                    match self { State::Ready => Ok(()), State::Stopped => Err(Error)? }
                }
            }
            extern "C" { fn native_call(value: i32) -> i32; }
            pub async fn serve() {
                let _router = Router::new().route("/", get(serve));
                let _rows = query("select 1");
                let _client = Client::new();
                let _hash = Argon2::default();
                let mut tasks = JoinSet::new();
                tasks.spawn(async move {});
                unsafe { native_call(1); }
            }
            fn main() {}
            """);
        repository.WriteText(
            "crates/app/build.rs",
            "fn main() { println!(\"cargo:rerun-if-changed=wrapper.h\"); }\n");
        repository.WriteText(
            "crates/app/tests/api.rs",
            "#[test]\nfn health() { assert_eq!(2 + 2, 4); }\n");
        repository.WriteText(
            "crates/app/benches/throughput.rs",
            "use criterion::{criterion_group, Criterion};\nfn bench(c: &mut Criterion) { c.bench_function(\"run\", |b| b.iter(|| 1)); }\ncriterion_group!(benches, bench);\n");
        repository.WriteText(
            "crates/app/examples/client.rs",
            "fn main() { println!(\"example\"); }\n");
        return repository;
    }

    private static Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static async Task<EstimateReport> EstimateAsync(params (string Path, string Content)[] files)
    {
        InMemoryRepository repository = new();
        foreach ((string path, string content) in files) repository.WriteText(path, content);
        return new SeedEstimator().Estimate(
            await ScanAsync(repository),
            EstimationProfile.Implementation);
    }

    private static EvidenceFact AnalyzerFact(RepositoryEvidence evidence, string kind, string scope) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == kind && fact.Scope == scope &&
            fact.Provenance.Analyzer == Analyzer);

    private static EvidenceFact Language(RepositoryEvidence evidence) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.Language &&
            fact.Tags.Contains("language:rust", StringComparer.Ordinal));

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;

    private static EffortRange Category(EstimateReport report, EffortCategory category) =>
        Assert.Single(report.Categories, item => item.Category == category).Hours;
}
