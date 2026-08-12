using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.Tests;

public sealed class RustCargoBoundaryTests
{
    private const string Analyzer = "efforthours.rust-analyzer";

    [Fact]
    public async Task VirtualWorkspaceDoesNotAbsorbMemberTargetsOrInferRegistryEdges()
    {
        InMemoryRepository repository = new();
        repository.WriteText("Cargo.toml", "[workspace]\nmembers = [\"crates/*\"]\n");
        repository.WriteText(
            "crates/app/Cargo.toml",
            "[package]\nname = \"app\"\nversion = \"1.0.0\"\n[dependencies]\nhelper = \"1\"\n");
        repository.WriteText("crates/app/src/main.rs", "use helper::Value; fn main() {}\n");
        repository.WriteText(
            "crates/helper/Cargo.toml",
            "[package]\nname = \"helper\"\nversion = \"1.0.0\"\n");
        repository.WriteText("crates/helper/src/lib.rs", "pub struct Value;\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact workspace = AnalyzerFact(evidence, EvidenceKinds.BuildConfiguration, ".");

        Assert.Equal(0m, Measurement(workspace, "targets"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Scope == "." && fact.Tags.Contains("target-scope:crates/app"));
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Scope == "crates/app" && fact.Tags.Contains("target-scope:crates/helper"));
    }

    [Fact]
    public async Task CargoAutoFlagsBuildDisableAndDottedKeysStayConservative()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "Cargo.toml",
            "[package]\nname = \"app\"\nversion = \"1.0.0\"\n" +
            "autobins = false\nautoexamples = false\nautotests = false\nautobenches = false\nbuild = false\n" +
            "[dependencies]\ndomain.path = \"crates/domain\"\n" +
            "[[test]]\nname = \"explicit\"\npath = \"./spec/explicit.rs\"\n");
        repository.WriteText("src/main.rs", "fn main() {}\n");
        repository.WriteText("build.rs", "fn main() {}\n");
        repository.WriteText("tests/automatic.rs", "#[test] fn automatic() {}\n");
        repository.WriteText("spec/explicit.rs", "#[test] fn explicit() {}\n");
        repository.WriteText(
            "crates/domain/Cargo.toml",
            "[package]\nname = \"domain\"\nversion = \"1.0.0\"\n");
        repository.WriteText("crates/domain/src/lib.rs", "pub struct Order;\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact build = AnalyzerFact(evidence, EvidenceKinds.BuildConfiguration, ".");

        Assert.Equal(1m, Measurement(build, "targets"));
        Assert.Equal(0m, Measurement(build, "build-scripts"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Scope == "." && fact.Tags.Contains("target-scope:crates/domain"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemTest &&
            fact.Locations.Any(location => location.Path == "spec/explicit.rs"));
    }

    [Fact]
    public async Task DeriveAndRuntimeAttributeMacrosRemainVisibleAsUnexpandedUncertainty()
    {
        InMemoryRepository repository = new();
        repository.WriteText("Cargo.toml", "[package]\nname = \"macros\"\nversion = \"1.0.0\"\n");
        repository.WriteText(
            "src/lib.rs",
            "#[derive(Clone, Debug)] pub struct Value; #[tokio::test] async fn runs() {}\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8805" &&
            diagnostic.Message.Contains("macro signal", StringComparison.Ordinal));
    }

    private static Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static EvidenceFact AnalyzerFact(RepositoryEvidence evidence, string kind, string scope) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == kind && fact.Scope == scope &&
            fact.Provenance.Analyzer == Analyzer);

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;
}
