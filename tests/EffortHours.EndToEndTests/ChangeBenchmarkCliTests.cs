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
            "8",
            "--max-seconds",
            "30",
            "--max-peak-mib",
            "1024");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Dictionary<string, string> values = Parse(result.StandardOutput);
        Assert.Equal("large-tree", values["mode"]);
        Assert.Equal("2", values["repository-estimator-invocations"]);
        Assert.Equal("false", values["range-audit-bounded"]);
        AssertReadOnlyAndBounded(values);
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
            "2",
            "--max-seconds",
            "30",
            "--max-peak-mib",
            "1024");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Dictionary<string, string> values = Parse(result.StandardOutput);
        Assert.Equal("long-range", values["mode"]);
        Assert.Equal("1", values["planned-components"]);
        Assert.Equal("true", values["range-audit-bounded"]);
        Assert.Equal("2", values["repository-estimator-invocations"]);
        AssertReadOnlyAndBounded(values);
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
            "--compare-independent",
            "--max-seconds",
            "30",
            "--max-peak-mib",
            "1024");

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
        Assert.Equal("true", values["combined-faster-than-independent"]);
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
        AssertReadOnlyAndBounded(values);
    }

    private static void AssertReadOnlyAndBounded(Dictionary<string, string> values)
    {
        Assert.Equal("change/1.2.0", values["benchmark"]);
        Assert.Equal("true", values["worktree-unchanged"]);
        Assert.Equal("true", values["git-state-unchanged"]);
        Assert.Equal("not-performed", values["target-execution"]);
        Assert.Equal("not-performed", values["dependency-installation"]);
        Assert.Equal("not-performed", values["network-access"]);
        Assert.Equal("true", values["seconds-threshold-passed"]);
        Assert.Equal("true", values["peak-mib-threshold-passed"]);
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
