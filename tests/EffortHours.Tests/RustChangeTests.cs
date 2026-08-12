using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class RustChangeTests
{
    [Fact]
    public async Task WhitespaceAndOrdinaryCommentOnlyRustChangeHasZeroEffort()
    {
        const string before =
            "// old note\npub struct Service;\nimpl Service { pub fn run(&self) -> bool { true } }\n";
        const string after =
            "/* new note */\npub  struct Service ;\nimpl Service\n{\n    pub fn run( &self )->bool\n    {\n        true\n    }\n}\n";

        ChangeEstimateReport report = await EstimateAsync(
            State(("src/service.rs", before)),
            State(("src/service.rs", after)));

        Assert.Equal(ChangePathClassification.FormattingOnly, Assert.Single(report.Evidence.Paths).Classification);
        Assert.Equal(0m, report.TotalEffort.Expected);
    }

    [Theory]
    [InlineData("pub const VALUE: &str = r#\"before\"#;\n", "pub const VALUE: &str = r#\"after\"#;\n")]
    [InlineData("/// Before contract.\npub fn run() {}\n", "/// After contract.\npub fn run() {}\n")]
    [InlineData("pub fn value<'a>(x: &'a str) {}\n", "pub fn value<'b>(x: &'b str) {}\n")]
    [InlineData("#[cfg(feature = \"one\")]\npub fn run() {}\n", "#[cfg(feature = \"two\")]\npub fn run() {}\n")]
    public async Task RawStringsRustdocLifetimesAndAttributesRemainMeaningful(string before, string after)
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("src/value.rs", before)),
            State(("src/value.rs", after)));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
    }

    [Fact]
    public async Task UnterminatedRustRawStringFailsClosed()
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("src/value.rs", "pub fn value() { let item = r#\"open;\n")),
            State(("src/value.rs", "pub fn value(){let item=r#\"open;\n")));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
    }

    [Fact]
    public async Task AddedCargoRustSurfacesReachSemanticCategories()
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(),
            State(
                ("Cargo.toml",
                    "[package]\nname=\"app\"\nversion=\"1.0.0\"\n" +
                    "[dependencies]\naxum=\"0.8\"\nsqlx=\"0.8\"\nreqwest=\"0.12\"\nargon2=\"0.5\"\ntokio=\"1\"\n" +
                    "[dev-dependencies]\ncriterion=\"0.6\"\n"),
                ("src/main.rs",
                    "use axum::{Router, routing::get}; use sqlx::query; use reqwest::Client; " +
                    "use argon2::Argon2; use tokio::task::JoinSet;\n" +
                    "pub async fn serve() { let _ = Router::new().route(\"/\", get(serve)); " +
                    "let _ = query(\"select 1\"); let _ = Client::new(); let _ = Argon2::default(); " +
                    "let mut tasks = JoinSet::new(); tasks.spawn(async move {}); }\nfn main() {}\n"),
                ("tests/api.rs", "#[test]\nfn health() { assert!(true); }\n"),
                ("benches/run.rs",
                    "use criterion::{criterion_group, Criterion}; fn bench(c: &mut Criterion) { " +
                    "c.bench_function(\"run\", |b| b.iter(|| 1)); } criterion_group!(benches, bench);\n")));

        Assert.Contains(report.Categories, item =>
            item.Category == EffortCategory.ProductionImplementation && item.Hours.Expected > 0m);
        Assert.Contains(report.Categories, item =>
            item.Category == EffortCategory.DataModelingPersistenceAndMigrations && item.Hours.Expected > 0m);
        Assert.Contains(report.Categories, item =>
            item.Category == EffortCategory.ExternalIntegrationsAndProtocols && item.Hours.Expected > 0m);
        Assert.Contains(report.Categories, item =>
            item.Category == EffortCategory.SecurityAndAccessibility && item.Hours.Expected > 0m);
        Assert.Contains(report.Categories, item =>
            item.Category == EffortCategory.IntegrationContractAndComponentTesting && item.Hours.Expected > 0m);
        Assert.Equal("change-seed/0.18.0+seed-rules/0.4.0", report.EstimatorVersion);
    }

    private static Task<ChangeEstimateReport> EstimateAsync(ChangeState before, ChangeState after) =>
        new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "in-memory-rust-change",
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
