using System.Diagnostics;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class JupyterNotebookCliTests
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [Fact]
    public async Task ScanAndEstimateAnalyzeMaintainedCellsWithoutDisclosingNotebookContent()
    {
        using JupyterRepository repository = new();
        repository.WriteText(
            "analysis.ipynb",
            Notebook(
                Code(
                    "import pandas as pd\nimport matplotlib.pyplot as plt\n\n" +
                    "def summarize(values):\n    frame = pd.DataFrame(values)\n    plt.plot(frame)\n    return frame\n",
                    12,
                    [new { output_type = "stream", text = "private-output-marker" }]),
                Markdown("# Private notebook source marker\nMaintained analysis narrative.")));

        ProcessResult scan = await RunCliAsync("scan", repository.RootPath);
        ProcessResult estimate = await RunCliAsync(
            "estimate",
            repository.RootPath,
            "--no-rate",
            "--compact");

        Assert.Equal(0, scan.ExitCode);
        Assert.Equal(0, estimate.ExitCode);
        Assert.Equal(string.Empty, scan.StandardError);
        Assert.Equal(string.Empty, estimate.StandardError);
        Assert.DoesNotContain("Private notebook source marker", scan.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("private-output-marker", scan.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(repository.RootPath, scan.StandardOutput, StringComparison.OrdinalIgnoreCase);

        using JsonDocument scanDocument = JsonDocument.Parse(scan.StandardOutput);
        JsonElement notebook = Assert.Single(
            scanDocument.RootElement.GetProperty("facts").EnumerateArray(),
            fact => fact.GetProperty("kind").GetString() == "jupyter-notebook");
        Assert.Equal(
            "0.2.0",
            notebook.GetProperty("provenance").GetProperty("analyzerVersion").GetString());
        Assert.Contains(
            scanDocument.RootElement.GetProperty("facts").EnumerateArray(),
            fact => fact.GetProperty("kind").GetString() == "source-structure" &&
                fact.GetProperty("tags").EnumerateArray().Any(tag =>
                    tag.GetString() == "format:jupyter-notebook"));

        using JsonDocument estimateDocument = JsonDocument.Parse(estimate.StandardOutput);
        Assert.Equal(
            "seed-rules/0.4.0",
            estimateDocument.RootElement.GetProperty("estimatorVersion").GetString());
        Assert.Contains(
            estimateDocument.RootElement.GetProperty("categories").EnumerateArray(),
            category => category.GetProperty("category").GetString() == "production-implementation");
        Assert.Contains(
            estimateDocument.RootElement.GetProperty("categories").EnumerateArray(),
            category => category.GetProperty("category").GetString() == "documentation");
    }

    [Fact]
    public async Task ChangeTreatsOutputExecutionAndSerializationDifferencesAsZero()
    {
        using JupyterRepository repository = new(createPair: true);
        repository.WriteBase(
            "analysis.ipynb",
            Notebook(Code("def value():\n    return 1\n", 1, [new { text = "old-output" }])));
        repository.WriteHead(
            "analysis.ipynb",
            JsonSerializer.Serialize(
                JsonSerializer.Deserialize<JsonElement>(Notebook(
                    Code("def value():\n    return 1\n", 99, [new { text = "private-new-output" }]))),
                IndentedJson));

        ProcessResult change = await RunCliAsync(
            "change",
            "--base-path",
            repository.BasePath,
            "--head-path",
            repository.HeadPath,
            "--no-rate",
            "--compact");

        Assert.Equal(0, change.ExitCode);
        Assert.Equal(string.Empty, change.StandardError);
        Assert.DoesNotContain("private-new-output", change.StandardOutput, StringComparison.Ordinal);
        using JsonDocument report = JsonDocument.Parse(change.StandardOutput);
        Assert.Equal(
            "change-seed/0.17.0+seed-rules/0.4.0",
            report.RootElement.GetProperty("estimatorVersion").GetString());
        Assert.Equal(0m, report.RootElement.GetProperty("totalEffort").GetProperty("expected").GetDecimal());
        Assert.Empty(report.RootElement.GetProperty("workItems").EnumerateArray());
    }

    private static object Code(string source, int? executionCount = null, object[]? outputs = null) => new
    {
        cell_type = "code",
        metadata = new { },
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

    private static string Notebook(params object[] cells) => JsonSerializer.Serialize(new
    {
        cells,
        metadata = new
        {
            kernelspec = new { name = "python3", language = "python" },
            language_info = new { name = "python" },
            widgets = new { state = "private-widget-marker" },
        },
        nbformat = 4,
        nbformat_minor = 5,
    });

    private static async Task<ProcessResult> RunCliAsync(params string[] arguments)
    {
        string repositoryRoot = FindRepositoryRoot();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine the test build configuration.");
        string cliAssembly = Path.Combine(
            repositoryRoot,
            "src",
            "EffortHours.Cli",
            "bin",
            configuration,
            "net10.0",
            "efforthours.dll");
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.ArgumentList.Add(cliAssembly);
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);

        using Process process = Process.Start(startInfo)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        return new ProcessResult(
            process.ExitCode,
            (await stdout).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd(),
            (await stderr).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd());
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the EffortHours repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class JupyterRepository : IDisposable
    {
        public JupyterRepository(bool createPair = false)
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "efforthours-jupyter-e2e",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
            BasePath = createPair ? Path.Combine(RootPath, "base") : RootPath;
            HeadPath = createPair ? Path.Combine(RootPath, "head") : RootPath;
            Directory.CreateDirectory(BasePath);
            Directory.CreateDirectory(HeadPath);
        }

        public string RootPath { get; }

        public string BasePath { get; }

        public string HeadPath { get; }

        public void WriteText(string relativePath, string content) => Write(RootPath, relativePath, content);

        public void WriteBase(string relativePath, string content) => Write(BasePath, relativePath, content);

        public void WriteHead(string relativePath, string content) => Write(HeadPath, relativePath, content);

        public void Dispose() => Directory.Delete(RootPath, recursive: true);

        private static void Write(string root, string relativePath, string content)
        {
            string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }
}
