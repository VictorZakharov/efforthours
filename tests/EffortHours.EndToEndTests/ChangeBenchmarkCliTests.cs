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
    public async Task AuthorPeriodBenchmarkUsesNestedMergeHeavyTreeAndBoundedSnapshotReuse()
    {
        ProcessResult result = await RunBenchmarkAsync(
            "--author-period",
            "--files",
            "1025",
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
            "--lines-per-file",
            "8",
            "--commits",
            "3",
            "--compare-independent");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Dictionary<string, string> values = Parse(result.StandardOutput);
        Assert.Equal("author-period-manifest", values["mode"]);
        Assert.Equal("2", values["portfolio-repositories"]);
        Assert.Equal("8", values["portfolio-heads"]);
        Assert.Equal("3", values["portfolio-contributors"]);
        Assert.Equal("6", values["selected-changes"]);
        Assert.Equal("24", values["independent-invocations"]);
        Assert.Equal("8", values["independent-empty-invocations"]);
        Assert.Equal("6", values["independent-unique-changes"]);
        Assert.Equal("2", values["combined-git-object-readers"]);
        Assert.True(
            int.Parse(values["combined-snapshot-analyses"], CultureInfo.InvariantCulture) <
            int.Parse(values["independent-snapshot-analyses"], CultureInfo.InvariantCulture));
        Assert.True(
            int.Parse(values["combined-git-object-readers"], CultureInfo.InvariantCulture) <
            int.Parse(values["independent-git-object-readers"], CultureInfo.InvariantCulture));
        Assert.Equal("true", values["less-repeated-analysis"]);
        Assert.Equal("true", values["independent-reports-equivalent"]);
        Assert.Equal("true", values["manual-baseline-equivalent"]);
        Assert.Equal("true", values["reordered-report-bytes-equivalent"]);
        Assert.Equal("true", values["repository-scoped-shared-object"]);
        Assert.Equal("true", values["fully-overlapping-heads-preserved"]);
        Assert.Equal("true", values["empty-contributor-preserved"]);
        Assert.Equal("true", values["privacy-boundary-preserved"]);
        Assert.True(bool.TryParse(values["combined-faster-than-independent"], out _));
        AssertPositive(values, "combined-estimate-seconds");
        AssertPositive(values, "independent-estimate-seconds");
        AssertReadOnlyAndMeasured(values);
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

    private static void AssertReadOnlyAndMeasured(Dictionary<string, string> values)
    {
        Assert.Equal("change/1.3.0", values["benchmark"]);
        Assert.Equal("true", values["worktree-unchanged"]);
        Assert.Equal("true", values["git-state-unchanged"]);
        Assert.Equal("not-performed", values["target-execution"]);
        Assert.Equal("not-performed", values["dependency-installation"]);
        Assert.Equal("not-performed", values["network-access"]);
        Assert.Equal("not-set", values["seconds-threshold"]);
        Assert.Equal("not-set", values["peak-mib-threshold"]);
        Assert.StartsWith("sha256:", values["worktree-digest"], StringComparison.Ordinal);
        Assert.StartsWith("sha256:", values["git-state-digest"], StringComparison.Ordinal);
        AssertPositive(values, "change-seconds");
        AssertPositive(values, "change-peak-working-set-mib");
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
