using System.Diagnostics;
using System.Text;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed class CliDocumentationTests : ChangeCliTestSupport
{
    [Fact]
    public async Task ExportOutsideCheckoutContainsExactBundledSourceBytesAndUnicode()
    {
        using var temp = new DocumentationDirectory();
        var exported = await OutsideCliAsync(temp.Path, "docs", "export", "offline");
        Assert.Equal(0, exported.ExitCode);
        Assert.Empty(exported.StandardError);
        string target = Path.Combine(temp.Path, "offline");
        const string prefix = "EffortHours.Cli.Documentation.";
        string[] expected = [.. typeof(EffortHoursApplication).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).Select(name => name[prefix.Length..])
            .Order(StringComparer.Ordinal)];
        string[] actual = [.. Directory.GetFiles(target, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(target, path).Replace('\\', '/')).Order(StringComparer.Ordinal)];
        Assert.Equal(expected, actual);
        Assert.Equal(Directory.GetFiles(Path.Combine(FindRepositoryRoot(), "docs"), "*.md").Length,
            actual.Count(path => path.StartsWith("docs/", StringComparison.Ordinal)));
        foreach (string path in expected)
        {
            Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(FindRepositoryRoot(), path)),
                await File.ReadAllBytesAsync(Path.Combine(target, path)));
        }

        Assert.Contains("LICENSE", actual);
        Assert.Contains("examples/historical-refresh/OfflineRefreshAdapter.cs", actual);
        var shown = await OutsideCliAsync(temp.Path, "docs", "show", "historical-refresh-integration");
        Assert.Equal(0, shown.ExitCode);
        Assert.Equal((await File.ReadAllTextAsync(Path.Combine(target, "docs", "HISTORICAL_REFRESH_INTEGRATION.md"),
            Encoding.UTF8)).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd(), shown.StandardOutput);
        Assert.Empty(Directory.GetDirectories(temp.Path, ".eh-docs-*"));
    }

    [Fact]
    public async Task ExportedExampleReproducesSchemaValidReviewPlanAndReadyReceiptOffline()
    {
        using var temp = new DocumentationDirectory();
        Assert.Equal(0, (await OutsideCliAsync(temp.Path, "docs", "export", "offline")).ExitCode);
        string example = Path.Combine(temp.Path, "offline", "examples", "historical-refresh");
        string[] originals = Directory.GetFiles(example);
        byte[][] before = [.. originals.Select(File.ReadAllBytes)];
        string[] policy = ["--workdays", "declared-days.json", "--workday-policy", ChangeWorkdayPolicies.EqualDeclaredDaysV1,
            "--entry-policy", ChangeDeclaredWorkdayReviewPolicies.EqualEntries];
        var review = await OutsideCliAsync(example, ["change", "review-days", "comparison.json", "--work-records", "work-records.json", .. policy, "--compact"]);
        Assert.Equal(0, review.ExitCode);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeWorkdayReviewReport, review.StandardOutput).IsValid);
        Assert.Equal(ContractJson.SerializeCompact(ContractJson.Deserialize<ChangeWorkdayReviewReport>(
            await File.ReadAllTextAsync(Path.Combine(example, "review.json"), Encoding.UTF8))), review.StandardOutput);
        var plan = await OutsideCliAsync(example, ["change", "plan-refresh", "comparison.json", "--work-records", "work-records.json",
            "--entries", "entries.json", .. policy, "--fields", "both", "--output", "new-plan.json"]);
        Assert.Equal(0, plan.ExitCode);
        string planText = await File.ReadAllTextAsync(Path.Combine(example, "new-plan.json"), Encoding.UTF8);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeHistoricalRefreshPlan, planText).IsValid);
        Assert.Equal(ContractJson.SerializeCompact(ContractJson.Deserialize<ChangeHistoricalRefreshPlan>(
            await File.ReadAllTextAsync(Path.Combine(example, "plan.json"), Encoding.UTF8))),
            ContractJson.SerializeCompact(ContractJson.Deserialize<ChangeHistoricalRefreshPlan>(planText)));
        var check = await OutsideCliAsync(example, "change", "check-refresh", "new-plan.json", "--entries", "entries.json", "--compact");
        Assert.Equal(0, check.ExitCode);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeHistoricalRefreshCheck, check.StandardOutput).IsValid);
        Assert.Equal(ContractJson.SerializeCompact(ContractJson.Deserialize<ChangeHistoricalRefreshCheck>(
            await File.ReadAllTextAsync(Path.Combine(example, "check-ready.json"), Encoding.UTF8))), check.StandardOutput);
        Assert.Equal(before, originals.Select(File.ReadAllBytes));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExportRejectsExistingDestinationsWithoutChangingAnyBytes(bool directory)
    {
        using var temp = new DocumentationDirectory();
        string target = Path.Combine(temp.Path, "existing");
        if (directory) Directory.CreateDirectory(target);
        string sentinel = directory ? Path.Combine(target, "sentinel.txt") : target;
        await File.WriteAllTextAsync(sentinel, "Retain this original \u2014 content", new UTF8Encoding(false));
        byte[] before = await File.ReadAllBytesAsync(sentinel);
        var result = await OutsideCliAsync(temp.Path, "docs", "export", "existing");
        Assert.Equal(3, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Contains("already exists", result.StandardError, StringComparison.Ordinal);
        Assert.Equal(before, await File.ReadAllBytesAsync(sentinel));
        Assert.Empty(Directory.GetDirectories(temp.Path, ".eh-docs-*"));
        if (directory) Assert.Single(Directory.GetFiles(target));
    }

    [Fact]
    public async Task ExportDoesNotCreateAMissingParentOrWriteAfterPreCancellation()
    {
        using var temp = new DocumentationDirectory();
        var result = await OutsideCliAsync(temp.Path, "docs", "export", Path.Combine("missing-parent", "offline"));
        Assert.Equal(3, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Empty(Directory.GetFileSystemEntries(temp.Path));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        int code = await new EffortHoursApplication().RunAsync(["docs", "export", Path.Combine(temp.Path, "offline")],
            stdout, stderr, cancellation.Token);
        Assert.Equal(130, code);
        Assert.Empty(Directory.GetFileSystemEntries(temp.Path));
    }

    private static Task<ProcessResult> OutsideCliAsync(string directory, params string[] args)
    {
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        ProcessStartInfo start = StartInfo("dotnet", directory);
        start.StandardOutputEncoding = new UTF8Encoding(false, true);
        start.StandardErrorEncoding = new UTF8Encoding(false, true);
        start.ArgumentList.Add(Path.Combine(FindRepositoryRoot(), "src", "EffortHours.Cli", "bin", configuration, "net10.0", "efforthours.dll"));
        foreach (string argument in args) start.ArgumentList.Add(argument);
        return RunAsync(start);
    }

    private sealed class DocumentationDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "efforthours-docs-e2e", Guid.NewGuid().ToString("N"));

        public DocumentationDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
