using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class JupyterNotebookChangeTests
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [Fact]
    public async Task OutputExecutionAndSerializationOnlyChangesHaveZeroEffort()
    {
        string before = Notebook(
            Code("def value():\n    return 1\n", 1, [new { output_type = "stream", text = "old" }]),
            Markdown("# Result\nStable narrative"));
        string after = JsonSerializer.Serialize(
            JsonSerializer.Deserialize<JsonElement>(Notebook(
                Code("def value():\n    return 1\n", 99, [new { output_type = "stream", text = "new" }]),
                Markdown("# Result\nStable narrative"))),
            IndentedJson);

        ChangeEstimateReport report = await EstimateAsync(before, after);
        ChangePathEvidence path = Assert.Single(report.Evidence.Paths);

        Assert.Equal(ChangePathClassification.FormattingOnly, path.Classification);
        Assert.False(path.Represented);
        Assert.Equal(0m, report.TotalEffort.Expected);
        Assert.Empty(report.WorkItems);
    }

    [Fact]
    public async Task PythonCodeChangeIsRepresentedAsImplementation()
    {
        ChangeEstimateReport report = await EstimateAsync(
            Notebook(Code("def value():\n    return 1\n")),
            Notebook(Code("def value():\n    if ready:\n        return 2\n    return 1\n")));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.ProductionImplementation && category.Hours.Expected > 0m);
        Assert.Equal("change-seed/0.18.1+seed-rules/0.4.0", report.EstimatorVersion);
    }

    [Fact]
    public async Task MarkdownChangeRoutesToDocumentation()
    {
        ChangeEstimateReport report = await EstimateAsync(
            Notebook(Markdown("# Analysis\nShort.")),
            Notebook(Markdown("# Analysis\nExpanded explanation with an operational example.")));

        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.Documentation && category.Hours.Expected > 0m);
    }

    [Fact]
    public async Task UnsupportedCellBodyChangesRemainExcluded()
    {
        ChangeEstimateReport report = await EstimateAsync(
            NotebookFor("r", Code("value <- 1\n")),
            NotebookFor("r", Code("value <- 200\n")));

        Assert.Equal(ChangePathClassification.FormattingOnly, Assert.Single(report.Evidence.Paths).Classification);
        Assert.Equal(0m, report.TotalEffort.Expected);
    }

    [Fact]
    public async Task AddingExcludedRawAndNonPythonCellsRemainsZero()
    {
        string before = Notebook(Code("def value():\n    return 1\n"));
        string after = Notebook(
            Code("def value():\n    return 1\n"),
            Raw("private raw payload"),
            Code("%%bash\necho private-shell-payload\n"));

        ChangeEstimateReport report = await EstimateAsync(before, after);

        Assert.Equal(ChangePathClassification.FormattingOnly, Assert.Single(report.Evidence.Paths).Classification);
        Assert.Equal(0m, report.TotalEffort.Expected);
    }

    [Fact]
    public async Task ReorderingMaintainedCodeCellsRemainsMeaningful()
    {
        ChangeEstimateReport report = await EstimateAsync(
            Notebook(Code("first = load()\n"), Code("second = transform(first)\n")),
            Notebook(Code("second = transform(first)\n"), Code("first = load()\n")));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.ProductionImplementation && category.Hours.Expected > 0m);
    }

    [Fact]
    public async Task MaintainedCellTagChangeRoutesToConfiguration()
    {
        ChangeEstimateReport report = await EstimateAsync(
            Notebook(Code("value = 1\n", tags: ["parameters"])),
            Notebook(Code("value = 1\n", tags: ["parameters", "raises-exception"])));

        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.BuildConfigurationAndDeveloperTooling &&
            category.Hours.Expected > 0m);
    }

    private static Task<ChangeEstimateReport> EstimateAsync(string before, string after)
    {
        InMemoryChangeSnapshot baseSnapshot = new([("analysis.ipynb", before)]);
        InMemoryChangeSnapshot headSnapshot = new([("analysis.ipynb", after)]);
        return new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "in-memory-jupyter-change",
                Selection = new ChangeSelection
                {
                    Kind = ChangeSelectionKind.BaseHead,
                    Base = Reference("base", baseSnapshot.ObjectId),
                    Head = Reference("head", headSnapshot.ObjectId),
                },
                OpenBaseAsync = InMemoryChangeSnapshot.Factory(("analysis.ipynb", before)),
                OpenHeadAsync = InMemoryChangeSnapshot.Factory(("analysis.ipynb", after)),
            },
            EstimationProfile.Implementation);
    }

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.GitTree,
    };

    private static object Code(
        string source,
        int? executionCount = null,
        object[]? outputs = null,
        string[]? tags = null) => new
        {
            cell_type = "code",
            metadata = new { tags = tags ?? [] },
            source,
            execution_count = executionCount,
            outputs = outputs ?? [],
        };

    private static object Markdown(string source) => new
    {
        cell_type = "markdown",
        metadata = new { },
        source,
    };

    private static object Raw(string source) => new
    {
        cell_type = "raw",
        metadata = new { },
        source,
    };

    private static string Notebook(params object[] cells) => NotebookFor("python", cells);

    private static string NotebookFor(string language, params object[] cells) => JsonSerializer.Serialize(new
    {
        cells,
        metadata = new
        {
            kernelspec = new { name = language == "python" ? "python3" : language, language },
            language_info = new { name = language },
        },
        nbformat = 4,
        nbformat_minor = 5,
    });
}
