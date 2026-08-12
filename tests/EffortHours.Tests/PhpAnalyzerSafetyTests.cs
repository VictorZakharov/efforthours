using EffortHours.Analysis;
using EffortHours.Analyzers.Php;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.Tests;

public sealed class PhpAnalyzerSafetyTests
{
    private const string Analyzer = "efforthours.php-analyzer";

    [Fact]
    public async Task VendorLocksFrameworkCachesAndGeneratedProxiesAreNotAnalyzed()
    {
        InMemoryRepository repository = new();
        repository.WriteText("src/App.php", "<?php\nnamespace App;\nclass App {}\n");
        repository.WriteText("vendor/acme/private/Secret.php", "<?php\nconst VALUE = 'vendor-secret';\n");
        repository.WriteText("composer.lock", "{\"packages\":[{\"name\":\"private/secret-marker\"}]}");
        repository.WriteText("bootstrap/cache/services.php", "<?php\nreturn ['cache-secret'];\n");
        repository.WriteText("var/cache/DoctrineProxy.php", "<?php\nclass GeneratedSecretProxy {}\n");
        repository.WriteText("generated/Proxy.php", "<?php // @generated\nclass GeneratedProxy {}\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        string json = ContractJson.Serialize(evidence);

        Assert.DoesNotContain(evidence.Facts, fact => fact.Provenance.Analyzer == Analyzer &&
            fact.Locations.Any(location => location.Path.StartsWith("vendor/", StringComparison.Ordinal) ||
                location.Path.StartsWith("bootstrap/cache/", StringComparison.Ordinal) ||
                location.Path.StartsWith("var/cache/", StringComparison.Ordinal) ||
                location.Path.StartsWith("generated/", StringComparison.Ordinal)));
        Assert.DoesNotContain("vendor-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private/secret-marker", json, StringComparison.Ordinal);
        Assert.DoesNotContain("cache-secret", json, StringComparison.Ordinal);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ExcludedContent);
    }

    [Fact]
    public async Task InvalidComposerAndDynamicPhpStayBoundedWithoutSourceDisclosure()
    {
        InMemoryRepository repository = new();
        repository.WriteText("composer.json", "{\"name\":\"private-name\", invalid}");
        repository.WriteText(
            "src/Dynamic.php",
            "<?php\nrequire $privateTarget;\nclass Dynamic { public function __call($name, $args) { call_user_func($name); } }\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        string json = ContractJson.Serialize(evidence);

        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8703");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8705");
        Assert.DoesNotContain("private-name", json, StringComparison.Ordinal);
        Assert.DoesNotContain("privateTarget", json, StringComparison.Ordinal);
        Assert.Empty(ContractValidation.Validate(evidence));
    }

    [Fact]
    public async Task DigestChangesAfterScanningAreRejected()
    {
        InMemoryRepository repository = new();
        repository.WriteText("app.php", "<?php\nclass Before {}\n");
        RepositoryEvidence common = await new RepositoryScanner(repository).ScanAsync(repository.RootPath);
        repository.WriteText("app.php", "<?php\nclass After {}\n");

        RepositoryAnalysisContribution contribution = await new PhpRepositoryAnalyzer(repository)
            .AnalyzeAsync(repository.RootPath, common);

        Assert.Contains(contribution.Diagnostics, diagnostic => diagnostic.Code == "FB8701" &&
            diagnostic.Message.Contains("changed after common scanning", StringComparison.Ordinal));
        Assert.DoesNotContain(contribution.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
    }

    [Fact]
    public void TokenizerSafeguardsReturnLowConfidenceInputs()
    {
        string oversized = string.Join(' ', Enumerable.Repeat("identifier", PhpTokenizer.MaximumTokens + 1));

        PhpTokenizationResult truncated = PhpTokenizer.Tokenize(oversized);
        PhpTokenizationResult incomplete = PhpTokenizer.Tokenize("<?php\nfunction run() {\n");
        PhpTokenizationResult heredoc = PhpTokenizer.Tokenize("<?php\n$value = <<<SECRET\nmissing\n");

        Assert.True(truncated.Truncated);
        Assert.False(incomplete.StructurallyBalanced);
        Assert.False(heredoc.StructurallyBalanced);
    }

    private static Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);
}
