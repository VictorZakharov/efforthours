using EffortHours.Analysis;
using EffortHours.Analyzers.Rust;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.Tests;

public sealed class RustAnalyzerSafetyTests
{
    private const string Analyzer = "efforthours.rust-analyzer";

    [Fact]
    public async Task VendorTargetsLocksAndGeneratedBindingsAreNotAnalyzedOrDisclosed()
    {
        InMemoryRepository repository = new();
        repository.WriteText("src/lib.rs", "pub struct App;\n");
        repository.WriteText("vendor/private/src/lib.rs", "pub const VALUE: &str = \"vendor-secret\";\n");
        repository.WriteText("target/debug/build/output.rs", "pub const VALUE: &str = \"target-secret\";\n");
        repository.WriteText("Cargo.lock", "[[package]]\nname = \"private-lock-marker\"\n");
        repository.WriteText("generated/bindings.rs", "// @generated\npub const SECRET: &str = \"binding-secret\";\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        string json = ContractJson.Serialize(evidence);

        Assert.DoesNotContain(evidence.Facts, fact => fact.Provenance.Analyzer == Analyzer &&
            fact.Locations.Any(location => location.Path.StartsWith("vendor/", StringComparison.Ordinal) ||
                location.Path.StartsWith("target/", StringComparison.Ordinal) ||
                location.Path.StartsWith("generated/", StringComparison.Ordinal)));
        Assert.DoesNotContain("vendor-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("target-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-lock-marker", json, StringComparison.Ordinal);
        Assert.DoesNotContain("binding-secret", json, StringComparison.Ordinal);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ExcludedContent);
    }

    [Fact]
    public async Task MalformedCargoAndMacroHeavyRustStayBoundedWithoutSourceDisclosure()
    {
        InMemoryRepository repository = new();
        repository.WriteText("Cargo.toml", "[package\nname = \"private-name\"\n");
        repository.WriteText(
            "src/lib.rs",
            "#[private_macro]\ninclude!(concat!(env!(\"OUT_DIR\"), \"/private.rs\"));\nmacro_rules! private_value { () => { 1 } }\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        string json = ContractJson.Serialize(evidence);

        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8803");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8805");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8806");
        Assert.DoesNotContain("private-name", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private_value", json, StringComparison.Ordinal);
        Assert.Empty(ContractValidation.Validate(evidence));
    }

    [Fact]
    public async Task DigestChangesAfterScanningAreRejected()
    {
        InMemoryRepository repository = new();
        repository.WriteText("src/lib.rs", "pub struct Before;\n");
        RepositoryEvidence common = await new RepositoryScanner(repository).ScanAsync(repository.RootPath);
        repository.WriteText("src/lib.rs", "pub struct After;\n");

        RepositoryAnalysisContribution contribution = await new RustRepositoryAnalyzer(repository)
            .AnalyzeAsync(repository.RootPath, common);

        Assert.Contains(contribution.Diagnostics, diagnostic => diagnostic.Code == "FB8801" &&
            diagnostic.Message.Contains("changed after common scanning", StringComparison.Ordinal));
        Assert.DoesNotContain(contribution.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
    }

    [Fact]
    public void TokenizerHandlesNestedCommentsRawStringsLifetimesAndFailsClosed()
    {
        RustTokenization valid = RustTokenizer.Tokenize(
            "/* outer /* nested */ end */ pub fn r#value<'a>(x: &'a str) { let raw = r#\"value\"#; }\n");
        RustTokenization truncated = RustTokenizer.Tokenize(
            string.Join(' ', Enumerable.Repeat("identifier", RustTokenizer.MaximumTokens + 1)));
        RustTokenization incomplete = RustTokenizer.Tokenize("pub fn run() { let value = r#\"open");

        Assert.Equal("medium", valid.Confidence);
        Assert.Contains(valid.Tokens, token => token.Kind == RustTokenKind.Lifetime);
        Assert.Contains(valid.Tokens, token => token.Kind == RustTokenKind.String);
        Assert.Contains(valid.Tokens, token => token.Text == "r#value");
        Assert.True(truncated.Truncated);
        Assert.Equal("low", incomplete.Confidence);
    }

    private static Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);
}
