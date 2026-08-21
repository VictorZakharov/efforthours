using System.Globalization;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Fact]
    public async Task ComparisonCheckpointsSuccessfulRepositoriesAndSelectivelyInvalidatesChangedHeads()
    {
        using GitFixture repositoryA = await GitFixture.CreateAsync();
        using GitFixture repositoryB = await GitFixture.CreateAsync();
        string headA = await CreateSelectedComparisonHeadAsync(repositoryA, "FeatureA.cs");
        string headB = await CreateSelectedComparisonHeadAsync(repositoryB, "FeatureB.cs");
        DateTimeOffset selectedA = await AuthorTimestampAsync(repositoryA, headA);
        DateTimeOffset selectedB = await AuthorTimestampAsync(repositoryB, headB);
        DateTimeOffset since = (selectedA < selectedB ? selectedA : selectedB).AddDays(-1);
        DateTimeOffset until = (selectedA > selectedB ? selectedA : selectedB).AddDays(2);
        string executionRoot = Path.Combine(
            Path.GetTempPath(),
            "efforthours-comparison-reliability-e2e",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(executionRoot);
        try
        {
            string manifestPath = Path.Combine(executionRoot, "manifest.json");
            string checkpointPath = Path.Combine(executionRoot, "checkpoint");
            string failedPath = Path.Combine(executionRoot, "failed.json");
            string resumedPath = Path.Combine(executionRoot, "resumed.json");
            string invalidatedPath = Path.Combine(executionRoot, "invalidated.json");
            WriteComparisonManifest(
                manifestPath,
                repositoryA.RootPath,
                headA,
                repositoryB.RootPath,
                new string('f', 40),
                since,
                until);

            ProcessResult failed = await RunComparisonAsync(
                manifestPath,
                checkpointPath,
                failedPath);

            Assert.Equal(3, failed.ExitCode);
            Assert.Equal(string.Empty, failed.StandardOutput);
            Assert.True(File.Exists(failedPath));
            using (JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(failedPath)))
            {
                JsonElement root = document.RootElement;
                Assert.Equal("incomplete", root.GetProperty("status").GetString());
                Assert.Empty(root.GetProperty("series").EnumerateArray());
                Assert.False(root.GetProperty("verification").GetProperty("completeAggregates").GetBoolean());
                Assert.True(
                    !root.TryGetProperty("sourcePortfolio", out JsonElement source) ||
                    source.ValueKind == JsonValueKind.Null);
                JsonElement execution = root.GetProperty("execution");
                JsonElement failure = Assert.Single(execution.GetProperty("failures").EnumerateArray());
                Assert.Equal("repository-b", failure.GetProperty("repositoryId").GetString());
                Assert.False(string.IsNullOrWhiteSpace(failure.GetProperty("phase").GetString()));
                Assert.False(string.IsNullOrWhiteSpace(failure.GetProperty("category").GetString()));
                Assert.StartsWith("sha256:", failure.GetProperty("messageDigest").GetString());
                JsonElement checkpoint = execution.GetProperty("checkpoint");
                Assert.Equal(0, checkpoint.GetProperty("hitCount").GetInt32());
                Assert.Equal(2, checkpoint.GetProperty("missCount").GetInt32());
                Assert.Equal(1, checkpoint.GetProperty("writeCount").GetInt32());
                Assert.Equal(1, checkpoint.GetProperty("failureCount").GetInt32());
            }

            Assert.Single(Directory.EnumerateFiles(checkpointPath, "*.json"));
            WriteComparisonManifest(
                manifestPath,
                repositoryA.RootPath,
                headA,
                repositoryB.RootPath,
                headB,
                since,
                until);

            ProcessResult resumed = await RunComparisonAsync(
                manifestPath,
                checkpointPath,
                resumedPath);

            Assert.Equal(0, resumed.ExitCode);
            using (JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(resumedPath)))
            {
                JsonElement root = document.RootElement;
                Assert.Equal("complete", root.GetProperty("status").GetString());
                JsonElement execution = root.GetProperty("execution");
                Assert.Equal(1, execution.GetProperty("checkpoint").GetProperty("hitCount").GetInt32());
                Assert.Equal(1, execution.GetProperty("checkpoint").GetProperty("missCount").GetInt32());
                Assert.Equal(
                    "hit",
                    RepositoryExecution(execution, "repository-a")
                        .GetProperty("checkpointDisposition").GetString());
                Assert.Equal(
                    "miss-written",
                    RepositoryExecution(execution, "repository-b")
                        .GetProperty("checkpointDisposition").GetString());
            }

            repositoryB.WriteText(
                "FeatureB2.cs",
                "namespace Demo; public sealed class FeatureB2 { public int Value => 2; }\n");
            string changedHeadB = await repositoryB.CommitAsync("second selected feature");
            WriteComparisonManifest(
                manifestPath,
                repositoryA.RootPath,
                headA,
                repositoryB.RootPath,
                changedHeadB,
                since,
                until);

            ProcessResult invalidated = await RunComparisonAsync(
                manifestPath,
                checkpointPath,
                invalidatedPath);

            Assert.Equal(0, invalidated.ExitCode);
            using (JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(invalidatedPath)))
            {
                JsonElement execution = document.RootElement.GetProperty("execution");
                Assert.Equal(
                    "hit",
                    RepositoryExecution(execution, "repository-a")
                        .GetProperty("checkpointDisposition").GetString());
                Assert.Equal(
                    "miss-written",
                    RepositoryExecution(execution, "repository-b")
                        .GetProperty("checkpointDisposition").GetString());
            }

            foreach (string output in new[]
            {
                await File.ReadAllTextAsync(failedPath),
                await File.ReadAllTextAsync(resumedPath),
                await File.ReadAllTextAsync(invalidatedPath),
            })
            {
                Assert.DoesNotContain(repositoryA.RootPath, output, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(repositoryB.RootPath, output, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("selected@example.invalid", output, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            DeleteDirectory(executionRoot);
        }
    }

    private static async Task<string> CreateSelectedComparisonHeadAsync(
        GitFixture repository,
        string featurePath)
    {
        await repository.GitAsync("config", "user.name", "Baseline Author");
        await repository.GitAsync("config", "user.email", "baseline@example.invalid");
        repository.WriteText("Demo.csproj", ProjectFile);
        _ = await repository.CommitAsync("base");
        await repository.GitAsync("config", "user.name", "Selected Contributor");
        await repository.GitAsync("config", "user.email", "selected@example.invalid");
        repository.WriteText(
            featurePath,
            "namespace Demo; public sealed class SelectedFeature { public int Value => 1; }\n");
        return await repository.CommitAsync("selected feature");
    }

    private static async Task<DateTimeOffset> AuthorTimestampAsync(
        GitFixture repository,
        string objectId) => DateTimeOffset.Parse(
            await repository.GitAsync("show", "-s", "--format=%aI", objectId),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind).ToUniversalTime();

    private static void WriteComparisonManifest(
        string path,
        string repositoryA,
        string headA,
        string repositoryB,
        string headB,
        DateTimeOffset since,
        DateTimeOffset until) => File.WriteAllText(
            path,
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
                        repositoryPath = repositoryA,
                        heads = new[] { new { id = "default", objectId = headA } },
                    },
                    new
                    {
                        id = "repository-b",
                        repositoryPath = repositoryB,
                        heads = new[] { new { id = "default", objectId = headB } },
                    },
                },
            }));

    private static Task<ProcessResult> RunComparisonAsync(
        string manifestPath,
        string checkpointPath,
        string outputPath) => RunCliAsync(
            "change", "portfolio",
            "--author-period-manifest", manifestPath,
            "--bucket", "calendar-month",
            "--checkpoint", checkpointPath,
            "--generated-at", "2026-08-20T12:00:00.0000000+00:00",
            "--format", "json",
            "--compact",
            "--no-rate",
            "--output", outputPath);

    private static JsonElement RepositoryExecution(JsonElement execution, string repositoryId) =>
        execution.GetProperty("repositories").EnumerateArray().Single(repository =>
            repository.GetProperty("repositoryId").GetString() == repositoryId);
}
