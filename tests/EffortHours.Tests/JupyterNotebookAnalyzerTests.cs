using System.Text;
using System.Text.Json;
using EffortHours.Analysis;
using EffortHours.Analyzers.Python;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class JupyterNotebookAnalyzerTests
{
    [Fact]
    public async Task PythonCellsNarrativeAndQualifiedSemanticsProduceBoundedEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText("analysis.ipynb", Notebook(
            "python",
            Code(
                "import pandas as pd\nimport matplotlib.pyplot as plt\nimport httpx\n\ndef summarize(values):\n    frame = pd.DataFrame(values)\n    plt.plot(frame)\n    return httpx.get('private-notebook-source-marker')\n",
                outputs: [new { output_type = "stream", text = "private-output-marker" }],
                executionCount: 17),
            Markdown("# Analysis\n[guide](private-attachment-marker)", attachments: new { secret = "private-attachment-payload" })));

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);
        string serialized = ContractJson.Serialize(evidence);
        EvidenceFact notebook = Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.JupyterNotebook);

        Assert.Contains("language:jupyter", evidence.Facts.Single(fact => fact.Kind == EvidenceKinds.Language).Tags);
        Assert.Contains("analysis-status:analyzed", evidence.Facts.Single(fact => fact.Kind == EvidenceKinds.Language).Tags);
        Assert.Equal(1m, Measurement(notebook, "output-bearing-cells"));
        Assert.Equal(1m, Measurement(notebook, "execution-count-cells"));
        Assert.Equal(1m, Measurement(notebook, "attachment-cells"));
        Assert.Equal(1m, Measurement(notebook, "widget-state-containers"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure &&
            fact.Tags.Contains("format:jupyter-notebook", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Documentation);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.UserInterface &&
            fact.Tags.Contains("technology:matplotlib", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.DataAccess &&
            fact.Tags.Contains("technology:pandas", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Integration &&
            fact.Tags.Contains("format:jupyter-notebook", StringComparer.Ordinal));
        Assert.Contains(estimate.Categories, category => category.Category == EffortCategory.UiImplementationAndRepresentedUxDecisions);
        Assert.Contains(estimate.Categories, category => category.Category == EffortCategory.Documentation);
        Assert.DoesNotContain("private-notebook-source-marker", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-output-marker", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-attachment-marker", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-attachment-payload", serialized, StringComparison.Ordinal);
        Assert.Empty(ContractValidation.Validate(evidence));
        Assert.Empty(ContractValidation.Validate(estimate));
    }

    [Fact]
    public async Task MixedKernelMagicsAndShellEscapesRemainExplicitUncertainty()
    {
        InMemoryRepository repository = new();
        repository.WriteText("mixed.ipynb", Notebook(
            "r",
            Code("value <- 1\n"),
            Code("%%python\ndef admitted():\n    return 1\n"),
            Code("%%bash\necho excluded\n"),
            Code("!pip install excluded\n%matplotlib inline\ndef kept():\n    return 2\n")));

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact notebook = Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.JupyterNotebook);

        Assert.Equal(3m, Measurement(notebook, "unsupported-code-cells"));
        Assert.True(Measurement(notebook, "magic-lines") >= 2m);
        Assert.Contains("declared-language:r", notebook.Tags);
        Assert.Contains("mixed-language:uncertain", notebook.Tags);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB7013");
        EvidenceFact structure = Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure &&
            fact.Tags.Contains("format:jupyter-notebook", StringComparer.Ordinal));
        Assert.Equal(1m, Measurement(structure, "functions"));
    }

    [Fact]
    public async Task DuplicateCellsAndOutputVariantNotebooksAreValuedOnce()
    {
        string code = "def calculate(value):\n    return value * 2\n";
        string first = Notebook("python", Code(code), Code(code));
        string second = Notebook("python", Code(code, outputs: [new { data = new { text = "ignored" } }]));
        EstimateReport single = await EstimateAsync(("one.ipynb", first));
        EstimateReport duplicates = await EstimateAsync(("one.ipynb", first), ("two.ipynb", second));

        Assert.Equal(single.TotalEffort.Expected, duplicates.TotalEffort.Expected);
        Assert.Equal(
            Category(single, EffortCategory.ProductionImplementation).Expected,
            Category(duplicates, EffortCategory.ProductionImplementation).Expected);
    }

    [Fact]
    public async Task InvalidNotebookAndCheckpointAreNotSemanticallyValued()
    {
        InMemoryRepository repository = new();
        repository.WriteText("broken.ipynb", "{ not-json");
        repository.WriteText(".ipynb_checkpoints/private-checkpoint.ipynb", Notebook("python", Code("def hidden(): pass")));

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB7012");
        Assert.DoesNotContain(evidence.Facts, fact => fact.Locations.Any(location =>
            location.Path.Contains("private-checkpoint", StringComparison.Ordinal)));
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
    }

    [Fact]
    public async Task Utf16NotebookIsRejectedByTheStrictReader()
    {
        InMemoryRepository repository = new();
        repository.WriteBytes("analysis.ipynb", [.. Encoding.Unicode.GetPreamble()
, .. Encoding.Unicode.GetBytes("{\"cells\":[]}")]);
        RepositoryEvidence common = await new EffortHours.Analysis.RepositoryScanner(repository)
            .ScanAsync(repository.RootPath);
        EvidenceFact file = Assert.Single(common.Facts, fact => fact.Id == "file:analysis.ipynb");

        EvidenceFact admitted = file with
        {
            Tags = [.. file.Tags.Where(tag => tag is not "content:binary" &&
                !tag.StartsWith("role:", StringComparison.Ordinal)), "role:source"],
        };
        RepositoryEvidence adjusted = common with
        {
            Facts = [.. common.Facts.Select(fact => fact.Id == file.Id ? admitted : fact)],
        };
        RepositoryAnalysisContribution contribution = await new PythonRepositoryAnalyzer(repository)
            .AnalyzeAsync(repository.RootPath, adjusted);

        Diagnostic diagnostic = Assert.Single(contribution.Diagnostics, item => item.Code == "FB7011");
        Assert.Contains("valid UTF-8", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(contribution.Facts, fact => fact.Kind == EvidenceKinds.JupyterNotebook);
    }

    [Fact]
    public async Task DigestMutationAfterScanningIsRejectedWithoutDisclosure()
    {
        InMemoryRepository repository = new();
        repository.WriteText("analysis.ipynb", Notebook("python", Code("def safe(): return 1\n")));
        RepositoryEvidence common = await new EffortHours.Analysis.RepositoryScanner(repository)
            .ScanAsync(repository.RootPath);
        repository.WriteText(
            "analysis.ipynb",
            Notebook("python", Code("private-post-scan-marker = 'must-not-leak'\n")));

        RepositoryAnalysisContribution contribution = await new PythonRepositoryAnalyzer(repository)
            .AnalyzeAsync(repository.RootPath, common);
        string serialized = JsonSerializer.Serialize(contribution);

        Assert.Contains(contribution.Diagnostics, diagnostic => diagnostic.Code == "FB7011");
        Assert.DoesNotContain(contribution.Facts, fact => fact.Kind == EvidenceKinds.JupyterNotebook);
        Assert.DoesNotContain("private-post-scan-marker", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("must-not-leak", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OversizedCellSourceIsInventoriedButNotSemanticallyValued()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "analysis.ipynb",
            Notebook("python", Code(new string('x', 1 * 1024 * 1024 + 1))));

        RepositoryEvidence evidence = await ScanAsync(repository);

        EvidenceFact notebook = Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.JupyterNotebook);
        Assert.Contains("analysis:bounded-incomplete", notebook.Tags);
        Assert.Equal(0m, Measurement(notebook, "python-code-cells"));
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB7014");
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
    }

    [Fact]
    public async Task TestPathNotebookIsAnalyzedAndOutputMarkersCannotGenerateIt()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "tests/validation.ipynb",
            Notebook(
                "python",
                Code(
                    "import pytest\n\ndef test_normalize():\n    assert True\n",
                    outputs: [new { text = "<auto-generated> excluded output" }])));

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact file = Assert.Single(evidence.Facts, fact => fact.Id == "file:tests/validation.ipynb");

        Assert.Contains("role:test", file.Tags);
        Assert.DoesNotContain("classification:generated", file.Tags);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemTest &&
            fact.Locations.Any(location => location.Path == "tests/validation.ipynb"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.JupyterNotebook);
    }

    private static object Code(string source, object[]? outputs = null, int? executionCount = null) => new
    {
        cell_type = "code",
        metadata = new { },
        source,
        execution_count = executionCount,
        outputs = outputs ?? [],
    };

    private static object Markdown(string source, object? attachments = null) => new
    {
        cell_type = "markdown",
        metadata = new { },
        source,
        attachments = attachments ?? new { },
    };

    private static string Notebook(string language, params object[] cells) => JsonSerializer.Serialize(new
    {
        cells,
        metadata = new
        {
            kernelspec = new { name = language == "python" ? "python3" : language, language },
            language_info = new { name = language },
            widgets = new { secret = "private-widget-state" },
        },
        nbformat = 4,
        nbformat_minor = 5,
    });

    private static async Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        await new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static async Task<EstimateReport> EstimateAsync(params (string Path, string Content)[] files)
    {
        InMemoryRepository repository = new();
        foreach ((string path, string content) in files) repository.WriteText(path, content);
        return new SeedEstimator().Estimate(await ScanAsync(repository), EstimationProfile.Implementation);
    }

    private static decimal Measurement(EvidenceFact fact, string name) =>
        fact.Measurements.Single(measurement => measurement.Name == name).Value;

    private static EffortRange Category(EstimateReport report, EffortCategory category) =>
        report.Categories.SingleOrDefault(item => item.Category == category)?.Hours ?? new EffortRange
        {
            Low = 0m,
            Expected = 0m,
            High = 0m,
        };
}
