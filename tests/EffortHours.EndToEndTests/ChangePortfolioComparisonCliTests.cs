using System.Globalization;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    private static readonly string[] ComparisonContributorAliases = ["selected@example.invalid"];

    [Fact]
    public async Task OneCommandWritesVersionedJsonTrendMarkdownAndGenericFindings()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        await repository.GitAsync("config", "user.name", "Baseline Author");
        await repository.GitAsync("config", "user.email", "baseline@example.invalid");
        repository.WriteText("Demo.csproj", ProjectFile);
        _ = await repository.CommitAsync("base");
        await repository.GitAsync("config", "user.name", "Selected Contributor");
        await repository.GitAsync("config", "user.email", "selected@example.invalid");
        repository.WriteText(
            "Feature.cs",
            "namespace Demo; public sealed class Feature { public int Value => 1; }\n");
        string head = await repository.CommitAsync("selected feature");
        DateTimeOffset selectedAt = DateTimeOffset.Parse(
            await repository.GitAsync("show", "-s", "--format=%aI", head),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind).ToUniversalTime();
        DateTimeOffset currentMonth = new(
            selectedAt.Year,
            selectedAt.Month,
            1,
            0,
            0,
            0,
            TimeSpan.Zero);
        DateTimeOffset since = currentMonth.AddMonths(-1).AddDays(10);
        DateTimeOffset until = new(selectedAt.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        string previousBucket = currentMonth.AddMonths(-1).ToString("yyyy-MM", CultureInfo.InvariantCulture);
        string currentBucket = currentMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        string executionRoot = Path.Combine(
            Path.GetTempPath(),
            "efforthours-comparison-e2e",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(executionRoot);
        try
        {
            string manifestPath = Path.Combine(executionRoot, "manifest.json");
            string capacityPath = Path.Combine(executionRoot, "capacity.json");
            string checkpointPath = Path.Combine(executionRoot, "checkpoint");
            string jsonPath = Path.Combine(executionRoot, "requested", "report.json");
            string trendPath = Path.Combine(executionRoot, "requested", "trend.md");
            string findingsPath = Path.Combine(executionRoot, "requested", "findings.md");
            File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    schemaVersion = "1.0.0",
                    selection = new
                    {
                        sinceInclusive = since.ToString("O", CultureInfo.InvariantCulture),
                        untilExclusive = until.ToString("O", CultureInfo.InvariantCulture),
                        timeZone = "UTC",
                        dateField = "author",
                        mergePolicy = "exclude",
                        coauthorPolicy = "include",
                        intervalSemantics = "since-inclusive-until-exclusive",
                    },
                    contributors = new[]
                    {
                        new { id = "contributor-a", aliases = ComparisonContributorAliases },
                    },
                    repositories = new[]
                    {
                        new
                        {
                            id = "repository-a",
                            repositoryPath = repository.RootPath,
                            heads = new[] { new { id = "default", objectId = head } },
                        },
                    },
                }));
            File.WriteAllText(
                capacityPath,
                JsonSerializer.Serialize(new
                {
                    schemaVersion = "1.0.0",
                    calendarPolicy = "Synthetic reference capacity with a partial first and final month.",
                    entries = new[]
                    {
                        new { bucketId = previousBucket, contributorId = "contributor-a", hours = 80m },
                        new { bucketId = currentBucket, contributorId = "contributor-a", hours = 40m },
                    },
                }));
            string[] shared =
            [
                "change", "portfolio",
                "--author-period-manifest", manifestPath,
                "--bucket", "calendar-month",
                "--capacity-manifest", capacityPath,
                "--checkpoint", checkpointPath,
                "--generated-at", "2026-08-20T12:00:00.0000000+00:00",
                "--no-rate",
            ];

            ProcessResult json = await RunCliAsync(
                [.. shared, "--format", "json", "--compact", "--output", jsonPath]);
            ProcessResult trend = await RunCliAsync(
                [.. shared, "--format", "markdown", "--title", "Synthetic trend", "--output", trendPath]);
            ProcessResult findings = await RunCliAsync(
                [.. shared, "--format", "markdown", "--report-view", "findings", "--title", "Synthetic findings", "--output", findingsPath]);

            Assert.Equal(0, json.ExitCode);
            Assert.Equal(0, trend.ExitCode);
            Assert.Equal(0, findings.ExitCode);
            Assert.Equal(string.Empty, json.StandardOutput);
            Assert.Equal(string.Empty, trend.StandardOutput);
            Assert.Equal(string.Empty, findings.StandardOutput);
            Assert.True(File.Exists(jsonPath));
            Assert.True(File.Exists(trendPath));
            Assert.True(File.Exists(findingsPath));
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(jsonPath));
            Assert.Equal("complete", document.RootElement.GetProperty("status").GetString());
            Assert.Equal("trend", document.RootElement.GetProperty("view").GetString());
            Assert.Equal(2, document.RootElement.GetProperty("buckets").GetArrayLength());
            Assert.Equal(
                "2026-08-20T12:00:00+00:00",
                document.RootElement.GetProperty("generatedAt").GetString());
            string trendMarkdown = await File.ReadAllTextAsync(trendPath);
            string findingsMarkdown = await File.ReadAllTextAsync(findingsPath);
            Assert.Contains("# Synthetic trend", trendMarkdown, StringComparison.Ordinal);
            Assert.Contains("```mermaid", trendMarkdown, StringComparison.Ordinal);
            Assert.Contains("Numeric fallback:", trendMarkdown, StringComparison.Ordinal);
            Assert.Contains("Partial-period note", trendMarkdown, StringComparison.Ordinal);
            Assert.Contains("# Synthetic findings", findingsMarkdown, StringComparison.Ordinal);
            Assert.Contains("Version and environment boundary", findingsMarkdown, StringComparison.Ordinal);
            Assert.Contains("Sanitized reproduction shape", findingsMarkdown, StringComparison.Ordinal);
            Assert.Contains("hits/misses/writes/failures: 1/0/0/0", findingsMarkdown, StringComparison.Ordinal);
            foreach (string output in new[]
            {
                await File.ReadAllTextAsync(jsonPath),
                trendMarkdown,
                findingsMarkdown,
            })
            {
                Assert.DoesNotContain(repository.RootPath, output, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("selected@example.invalid", output, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("public sealed class Feature", output, StringComparison.Ordinal);
            }
        }
        finally
        {
            DeleteDirectory(executionRoot);
        }
    }
}
