using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class RustScannerClassificationTests
{
    [Fact]
    public async Task ScannerClassifiesCargoRustTestsBuildScriptsAndToolConfiguration()
    {
        InMemoryRepository repository = new();
        repository.WriteText("Cargo.toml", "[package]\nname = \"app\"\n");
        repository.WriteText("Cargo.lock", "version = 4\n");
        repository.WriteText("src/lib.rs", "pub struct App;\n");
        repository.WriteText("tests/api.rs", "#[test]\nfn api() {}\n");
        repository.WriteText("build.rs", "fn main() {}\n");
        repository.WriteText(".cargo/config.toml", "[build]\ntarget-dir = \"target\"\n");

        RepositoryEvidence evidence = await new RepositoryScanner(repository).ScanAsync(repository.RootPath);

        Assert.Contains("rust", evidence.Repository.Ecosystems);
        AssertFileTag(evidence, "Cargo.toml", "role:package-manifest");
        Assert.Contains(evidence.Facts, fact => fact.Id == "component:Cargo.toml");
        AssertFileTag(evidence, "Cargo.lock", "role:dependency-lock");
        AssertFileTag(evidence, "src/lib.rs", "language:rust");
        AssertFileTag(evidence, "tests/api.rs", "classification:test");
        AssertFileTag(evidence, "build.rs", "role:build-configuration");
        AssertFileTag(evidence, ".cargo/config.toml", "role:build-configuration");
        Assert.Equal("0.2.13", RepositoryScanner.AnalyzerVersion);
    }

    private static void AssertFileTag(RepositoryEvidence evidence, string path, string tag)
    {
        EvidenceFact file = Assert.Single(evidence.Facts, fact => fact.Id == $"file:{path}");
        Assert.Contains(tag, file.Tags);
    }
}
