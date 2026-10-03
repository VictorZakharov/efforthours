using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class RustSynchronizationTests
{
    [Theory]
    [InlineData("use std::sync::Arc; pub fn value() { let _ = Arc::new(1); }")]
    [InlineData("use std::sync::{Arc, Weak}; pub fn value() { let _ = Arc::new(1); }")]
    [InlineData("use std::sync::Arc as Mutex; pub fn value() { let _ = Mutex::new(1); }")]
    [InlineData("use std::sync::{Arc as Mutex}; pub fn value() { let _ = Mutex::new(1); }")]
    [InlineData("use std::sync::atomic::Ordering; pub fn value() { let _ = Ordering::Relaxed; }")]
    [InlineData("use std::sync::Arc; pub struct Mutex; pub fn value() { let _ = Arc::new(1); }")]
    [InlineData("pub struct AtomicBool; pub fn value() { let _ = AtomicBool; }")]
    [InlineData("mod std { pub mod sync { pub struct Mutex; } } use std::sync::Mutex;")]
    [InlineData("pub async fn value() -> i32 { 1 }")]
    public async Task OwnershipNamespaceNamesakesAndAsyncSyntaxDoNotCreateBackgroundWork(string source)
    {
        RepositoryEvidence evidence = await ScanAsync(source);
        EstimateReport report = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);

        Assert.DoesNotContain(evidence.Facts, IsRustBackground);
        Assert.DoesNotContain(report.WorkItems, item => item.Estimator.Id == "seed-rule:background-work");
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
        Assert.Empty(ContractValidation.Validate(evidence));
        Assert.Empty(ContractValidation.Validate(report));
    }

    [Theory]
    [InlineData("use std::sync::Mutex; pub fn value() { let _ = Mutex::new(1); }")]
    [InlineData("use std::sync::{Arc, RwLock}; pub fn value() { let _ = Arc::new(RwLock::new(1)); }")]
    [InlineData("use std::sync::{atomic::{AtomicBool, Ordering}, Arc}; pub fn value() { let _ = AtomicBool::new(false); }")]
    [InlineData("pub fn value() { let _ = std::sync::atomic::AtomicUsize::new(1); }")]
    [InlineData("pub fn value() { let _ = std::sync::mpsc::channel::<i32>(); }")]
    [InlineData("use std::sync::Barrier as Gate; pub fn value() { let _ = Gate::new(2); }")]
    [InlineData("pub fn value() { std::thread::spawn(|| 1); }")]
    public async Task QualifiedCoordinationAndThreadSpawningRetainBackgroundWork(string source)
    {
        RepositoryEvidence evidence = await ScanAsync(source);
        EstimateReport report = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);

        Assert.Contains(evidence.Facts, IsRustBackground);
        Assert.Contains(report.WorkItems, item => item.Estimator.Id == "seed-rule:background-work");
        Assert.Empty(ContractValidation.Validate(evidence));
        Assert.Empty(ContractValidation.Validate(report));
    }

    [Fact]
    public async Task OversizedGroupedImportDoesNotSearchUnboundedlyForCoordination()
    {
        string ownership = string.Join(", ", Enumerable.Range(0, 100).Select(index => $"Arc as Owned{index}"));
        RepositoryEvidence evidence = await ScanAsync($"use std::sync::{{{ownership}, Mutex}};");

        Assert.DoesNotContain(evidence.Facts, IsRustBackground);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
    }

    private static bool IsRustBackground(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.BackgroundWork &&
        fact.Provenance.Analyzer == "efforthours.rust-analyzer";

    private static Task<RepositoryEvidence> ScanAsync(string source)
    {
        InMemoryRepository repository = new();
        repository.WriteText("Cargo.toml", "[package]\nname = \"coordination\"\nversion = \"1.0.0\"\n");
        repository.WriteText("src/lib.rs", source + "\n");
        return new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);
    }
}
