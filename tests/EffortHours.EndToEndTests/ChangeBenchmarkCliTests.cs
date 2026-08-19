using System.Diagnostics;
using System.Globalization;

namespace EffortHours.EndToEndTests;

public sealed class ChangeBenchmarkCliTests
{
    [Fact]
    public async Task LargeTreeBenchmarkMeasuresUniqueSnapshotsAndReadOnlyState()
    {
        ProcessResult result = await RunBenchmarkAsync(
            "--tree",
            "--files",
            "8",
            "--context-projects",
            "0",
            "--lines-per-file",
            "8");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Dictionary<string, string> values = Parse(result.StandardOutput);
        Assert.Equal("large-tree", values["mode"]);
        Assert.Equal("2", values["repository-estimator-invocations"]);
        Assert.Equal("false", values["range-audit-bounded"]);
        AssertReadOnlyAndMeasured(values);
    }

    [Fact]
    public async Task LongRangeBenchmarkExercisesBoundedAuditDiagnostic()
    {
        ProcessResult result = await RunBenchmarkAsync(
            "--range",
            "--files",
            "3",
            "--lines-per-file",
            "8",
            "--commits",
            "3",
            "--maximum-range-components",
            "2");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Dictionary<string, string> values = Parse(result.StandardOutput);
        Assert.Equal("long-range", values["mode"]);
        Assert.Equal("1", values["planned-components"]);
        Assert.Equal("true", values["range-audit-bounded"]);
        Assert.Equal("2", values["repository-estimator-invocations"]);
        AssertReadOnlyAndMeasured(values);
    }

    [Fact]
    public async Task AuthorPeriodBenchmarkPreservesReuseAndEquivalenceInCiFixture()
    {
        ProcessResult result = await RunBenchmarkAsync(
            "--author-period",
            "--files",
            "8",
            "--lines-per-file",
            "8",
            "--commits",
            "3",
            "--compare-independent");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Dictionary<string, string> values = Parse(result.StandardOutput);
        Assert.Equal("author-period", values["mode"]);
        Assert.Equal("3", values["selected-changes"]);
        Assert.Equal("4", values["repository-estimator-invocations"]);
        Assert.Equal("1", values["portfolio-repositories"]);
        Assert.Equal("1", values["maximum-active-repositories"]);
        Assert.Equal("6", values["snapshot-analysis-requests"]);
        Assert.Equal("2", values["snapshot-analysis-hits"]);
        Assert.Equal("6", values["snapshot-inventory-requests"]);
        Assert.Equal("2", values["snapshot-inventory-hits"]);
        Assert.Equal("1", values["git-object-readers"]);
        Assert.True(
            int.Parse(values["blob-cache-hits"], CultureInfo.InvariantCulture) > 0);
        Assert.Equal("6", values["independent-snapshot-analyses"]);
        Assert.Equal("true", values["independent-reports-equivalent"]);
        Assert.True(bool.TryParse(values["combined-faster-than-independent"], out _));
        AssertPositive(values, "combined-estimate-seconds");
        AssertPositive(values, "independent-estimate-seconds");
        Assert.Equal("false", values["range-audit-bounded"]);
        Assert.Equal("true", values["changed-scope-analysis"]);
        Assert.True(
            long.Parse(values["head-directories"], CultureInfo.InvariantCulture) >
            long.Parse(values["head-files"], CultureInfo.InvariantCulture));
        Assert.True(
            long.Parse(
                values["estimated-legacy-entry-comparisons-per-snapshot"],
                CultureInfo.InvariantCulture) > 0);
        AssertReadOnlyAndMeasured(values);
    }

    [Fact]
    public async Task AuthorPeriodManifestBenchmarkFreezesRegressionAndReuseMatrix()
    {
        ProcessResult result = await RunBenchmarkAsync(
            "--author-period-manifest",
            "--files",
            "8",
            "--context-projects",
            "0",
            "--lines-per-file",
            "8",
            "--commits",
            "3",
            "--compare-independent");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Dictionary<string, string> values = Parse(result.StandardOutput);
        Assert.Equal("author-period-manifest", values["mode"]);
        Assert.Equal("change/1.9.0", values["benchmark"]);
        Assert.Equal("2", values["portfolio-repositories"]);
        Assert.Equal("generated", values["fixture-source"]);
        AssertPositive(values, "fixture-preparation-seconds");
        Assert.Equal("2", values["maximum-active-repositories"]);
        Assert.Equal("8", values["portfolio-heads"]);
        Assert.Equal("3", values["portfolio-contributors"]);
        Assert.Equal("6", values["selected-changes"]);
        Assert.Equal("2", values["isolated-manifest-invocations"]);
        Assert.Equal("6", values["isolated-manifest-selected-rows"]);
        Assert.Equal("6", values["isolated-manifest-unique-changes"]);
        Assert.Equal("2", values["combined-git-object-readers"]);
        Assert.Equal("2", values["combined-git-metadata-readers"]);
        AssertPositive(values, "object-metadata-requests");
        AssertPositive(values, "object-metadata-cache-hits");
        AssertPositive(values, "unique-object-metadata-objects");
        Assert.Equal("0", values["object-metadata-cache-evictions"]);
        Assert.Equal("16384", values["object-metadata-cache-entry-limit"]);
        Assert.True(
            int.Parse(values["combined-snapshot-analyses"], CultureInfo.InvariantCulture) <
            int.Parse(
                values["isolated-manifest-snapshot-analyses"],
                CultureInfo.InvariantCulture));
        Assert.True(
            int.Parse(values["combined-git-object-readers"], CultureInfo.InvariantCulture) <
            int.Parse(
                values["isolated-manifest-git-object-readers"],
                CultureInfo.InvariantCulture));
        Assert.Equal("6", values["batched-incremental-snapshot-inventory-loads"]);
        Assert.Equal("0", values["snapshot-inventory-evictions"]);
        Assert.Equal("1", values["peak-retained-snapshot-inventory-roots"]);
        Assert.Equal("10000", values["snapshot-inventory-retention-limit"]);
        Assert.Equal("16", values["snapshot-inventory-root-retention-limit"]);
        Assert.Equal("64.00", values["snapshot-delta-batch-output-limit-mib"]);
        Assert.Equal("true", values["fewer-snapshot-analyses-than-isolated-manifests"]);
        Assert.Equal("true", values["fewer-blob-cache-misses-than-isolated-manifests"]);
        Assert.Equal("true", values["fewer-unique-analysis-artifacts-than-isolated-manifests"]);
        Assert.Equal("true", values["isolated-manifest-reports-equivalent"]);
        Assert.Equal("true", values["manual-baseline-equivalent"]);
        Assert.Equal("true", values["reordered-report-bytes-equivalent"]);
        Assert.Equal("true", values["repository-scoped-shared-object"]);
        Assert.Equal("true", values["fully-overlapping-heads-preserved"]);
        Assert.Equal("true", values["empty-contributor-preserved"]);
        Assert.Equal("true", values["privacy-boundary-preserved"]);
        Assert.Equal(64, values["report-sha256"].Length);
        Assert.Equal(64, values["estimate-semantics-sha256"].Length);
        Assert.Equal(
            "initial-combined-warmup,isolated-manifests,measured-reordered-combined",
            values["timing-order"]);
        Assert.True(bool.TryParse(values["combined-faster-than-isolated-manifests"], out _));
        AssertPositive(values, "initial-combined-warmup-seconds");
        AssertPositive(values, "combined-estimate-seconds");
        AssertPositive(values, "combined-managed-average-processor-equivalents");
        AssertPositive(values, "combined-managed-logical-processor-utilization-percent");
        AssertPositive(values, "isolated-manifest-estimate-seconds");
        AssertReadOnlyAndMeasured(values);
    }

    [Fact]
    public async Task AuthorPeriodManifestBenchmarkReusesPreparedFixture()
    {
        ProcessResult preparation = await RunBenchmarkAsync(
            "--author-period-manifest",
            "--files",
            "8",
            "--context-projects",
            "0",
            "--lines-per-file",
            "8",
            "--commits",
            "3",
            "--prepare-only");

        Assert.Equal(0, preparation.ExitCode);
        Assert.Equal(string.Empty, preparation.StandardError);
        Dictionary<string, string> prepared = Parse(preparation.StandardOutput);
        Assert.Equal("change-fixture/1.0.0", prepared["benchmark"]);
        Assert.Equal(
            "author-period-manifest-fixture-preparation",
            prepared["mode"]);
        AssertPositive(prepared, "fixture-preparation-seconds");
        string root = prepared["repository-container-path"];
        string descriptor = prepared["fixture-descriptor-path"];
        try
        {
            Assert.True(Directory.Exists(root));
            Assert.True(File.Exists(descriptor));

            ProcessResult measurement = await RunBenchmarkAsync(
                "--author-period-manifest",
                "--prepared-fixture",
                descriptor,
                "--compare-independent");

            Assert.Equal(0, measurement.ExitCode);
            Assert.Equal(string.Empty, measurement.StandardError);
            Dictionary<string, string> values = Parse(measurement.StandardOutput);
            Assert.Equal("prepared", values["fixture-source"]);
            Assert.Equal("6", values["selected-changes"]);
            AssertPositive(values, "fixture-preparation-seconds");
            AssertPositive(values, "combined-managed-average-processor-equivalents");
            AssertPositive(values, "combined-managed-logical-processor-utilization-percent");
            AssertReadOnlyAndMeasured(values);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                foreach (string file in Directory.EnumerateFiles(
                    root,
                    "*",
                    SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AuthorPeriodManifestBenchmarkRequiresAnIndependentBaseline()
    {
        ProcessResult result = await RunBenchmarkAsync("--author-period-manifest");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(
            "requires '--compare-independent'",
            result.StandardError,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorPeriodProcessMatrixSharesObjectDatabaseWithoutTimingGates()
    {
        ProcessResult result = await RunBenchmarkAsync(
            "--author-period",
            "--files",
            "8",
            "--lines-per-file",
            "8",
            "--commits",
            "1",
            "--process-matrix");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Dictionary<string, string> values = Parse(result.StandardOutput);
        Assert.Equal("author-period-process-matrix", values["mode"]);
        Assert.Equal("1,2,3", values["process-matrix"]);
        Assert.Equal("6", values["process-matrix-workers"]);
        Assert.Equal("true", values["process-matrix-reports-equivalent"]);
        Assert.Equal("true", values["shared-object-database"]);
        Assert.Equal("1", values["process-1-workers"]);
        Assert.Equal("2", values["process-2-workers"]);
        Assert.Equal("3", values["process-3-workers"]);
        AssertPositive(values, "process-1-group-wall-seconds");
        AssertPositive(values, "process-2-group-wall-seconds");
        AssertPositive(values, "process-3-group-wall-seconds");
        AssertPositive(values, "process-3-max-worker-peak-working-set-mib");
        AssertReadOnlyAndMeasured(values, requireOverallMeasurements: false);
    }

    [Fact]
    public async Task AuthorPeriodBenchmarkCopiesCallerSourceTreeWithoutMutatingIt()
    {
        string sourceTree = Path.Combine(
            Path.GetTempPath(),
            "efforthours-change-source-tree",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(sourceTree, "src"));
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(sourceTree, "Demo.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
            await File.WriteAllTextAsync(
                Path.Combine(sourceTree, "src", "Demo.cs"),
                "namespace Demo; public sealed class Example { }\n");

            ProcessResult result = await RunBenchmarkAsync(
                "--author-period",
                "--source-tree",
                sourceTree,
                "--lines-per-file",
                "8",
                "--commits",
                "1");

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardError);
            Dictionary<string, string> values = Parse(result.StandardOutput);
            Assert.Equal("caller-supplied", values["fixture-source"]);
            Assert.Equal("2", values["source-tree-files"]);
            Assert.Equal("2", values["source-tree-directories"]);
            Assert.Equal("0", values["source-tree-skipped-links"]);
            Assert.Equal("true", values["source-tree-unchanged"]);
            Assert.StartsWith("sha256:", values["source-tree-digest"], StringComparison.Ordinal);
            Assert.DoesNotContain(sourceTree, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            AssertReadOnlyAndMeasured(values);
        }
        finally
        {
            Directory.Delete(sourceTree, recursive: true);
        }
    }

    [Fact]
    public async Task AuthorPeriodProcessMatrixRejectsMachineDependentThresholds()
    {
        ProcessResult result = await RunBenchmarkAsync(
            "--author-period",
            "--process-matrix",
            "--max-seconds",
            "1");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(
            "observations and cannot be threshold gates",
            result.StandardError,
            StringComparison.Ordinal);
    }

    private static void AssertReadOnlyAndMeasured(
        Dictionary<string, string> values,
        bool requireOverallMeasurements = true)
    {
        Assert.Equal(
            values["mode"] == "author-period-manifest" ? "change/1.9.0" : "change/1.4.0",
            values["benchmark"]);
        Assert.Equal("true", values["worktree-unchanged"]);
        Assert.Equal("true", values["git-state-unchanged"]);
        Assert.Equal("not-performed", values["target-execution"]);
        Assert.Equal("not-performed", values["dependency-installation"]);
        Assert.Equal("not-performed", values["network-access"]);
        Assert.Equal("not-set", values["seconds-threshold"]);
        Assert.Equal("not-set", values["peak-mib-threshold"]);
        Assert.StartsWith("sha256:", values["worktree-digest"], StringComparison.Ordinal);
        Assert.StartsWith("sha256:", values["git-state-digest"], StringComparison.Ordinal);
        if (requireOverallMeasurements)
        {
            AssertPositive(values, "change-seconds");
            AssertPositive(values, "change-peak-working-set-mib");
        }
    }

    private static void AssertPositive(Dictionary<string, string> values, string name)
    {
        Assert.True(decimal.TryParse(
            values[name],
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out decimal value));
        Assert.True(value > 0m, $"Expected '{name}' to be positive, but it was {value}.");
    }

    private static Dictionary<string, string> Parse(string output) => output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Split('=', 2))
        .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

    private static async Task<ProcessResult> RunBenchmarkAsync(params string[] arguments)
    {
        string root = FindRepositoryRoot();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine test configuration.");
        string benchmark = Path.Combine(
            root,
            "benchmarks",
            "EffortHours.ChangeBenchmarks",
            "bin",
            configuration,
            "net10.0",
            "EffortHours.ChangeBenchmarks.dll");
        Assert.True(File.Exists(benchmark), $"Change benchmark assembly was not built: {benchmark}");

        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.ArgumentList.Add(benchmark);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start Change benchmark.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(timeout.Token);
        return new ProcessResult(
            process.ExitCode,
            (await stdout).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd(),
            (await stderr).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd());
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EffortHours.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
