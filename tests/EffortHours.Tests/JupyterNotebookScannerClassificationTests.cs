using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class JupyterNotebookScannerClassificationTests
{
    [Fact]
    public async Task ScannerClassifiesMaintainedNotebooksAndExcludesCheckpointCopies()
    {
        InMemoryRepository repository = new();
        repository.WriteText("analysis.ipynb", "{\"cells\":[],\"metadata\":{},\"nbformat\":4}");
        repository.WriteText(
            "reports/.ipynb_checkpoints/analysis-checkpoint.ipynb",
            "{\"cells\":[],\"metadata\":{},\"nbformat\":4}");

        RepositoryEvidence evidence = await new RepositoryScanner(repository).ScanAsync(repository.RootPath);

        EvidenceFact notebook = Assert.Single(evidence.Facts, fact => fact.Id == "file:analysis.ipynb");
        Assert.Contains("role:source", notebook.Tags);
        Assert.Contains("language:jupyter", notebook.Tags);
        Assert.Contains("ecosystem:python", notebook.Tags);
        Assert.Contains("python", evidence.Repository.Ecosystems);
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.File &&
            fact.Id.Contains("checkpoint", StringComparison.Ordinal));
        Assert.Equal("0.2.12", RepositoryScanner.AnalyzerVersion);
    }
}
