using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class PhpScannerClassificationTests
{
    [Fact]
    public async Task ScannerClassifiesComposerPhpTestsAndFrameworkCaches()
    {
        InMemoryRepository repository = new();
        repository.WriteText("composer.json", "{\"name\":\"acme/app\"}");
        repository.WriteText("src/App.php", "<?php class App {}\n");
        repository.WriteText("src/AppTest.php", "<?php class AppTest {}\n");
        repository.WriteText("bootstrap/cache/services.php", "<?php return [];\n");
        repository.WriteText("phpunit.xml.dist", "<phpunit />\n");

        RepositoryEvidence evidence = await new RepositoryScanner(repository).ScanAsync(repository.RootPath);

        Assert.Contains("php", evidence.Repository.Ecosystems);
        AssertFileTag(evidence, "composer.json", "role:package-manifest");
        Assert.Contains(evidence.Facts, fact => fact.Id == "component:composer.json");
        AssertFileTag(evidence, "src/App.php", "language:php");
        AssertFileTag(evidence, "src/AppTest.php", "classification:test");
        AssertFileTag(evidence, "bootstrap/cache/services.php", "classification:generated");
        AssertFileTag(evidence, "phpunit.xml.dist", "role:build-configuration");
        Assert.Equal("0.2.11", RepositoryScanner.AnalyzerVersion);
    }

    private static void AssertFileTag(RepositoryEvidence evidence, string path, string tag)
    {
        EvidenceFact file = Assert.Single(evidence.Facts, fact => fact.Id == $"file:{path}");
        Assert.Contains(tag, file.Tags);
    }
}
