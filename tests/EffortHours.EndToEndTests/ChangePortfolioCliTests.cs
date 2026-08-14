using System.Diagnostics;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    private static readonly string[] ContributorAAliases =
        ["Contributor A", "selected-a@example.invalid"];

    private static readonly string[] ContributorBAliases =
        ["selected-b@example.invalid", "Contributor B"];

    [Fact]
    public async Task AuthorPeriodManifestUnionsHeadsScopesObjectsAndKeepsAliasesPrivate()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        await repository.GitAsync("config", "user.name", "Baseline Author");
        await repository.GitAsync("config", "user.email", "baseline@example.invalid");
        repository.WriteText("Demo.csproj", ProjectFile);
        _ = await repository.CommitAsync("base");
        await repository.GitAsync("config", "user.name", "Contributor A");
        await repository.GitAsync("config", "user.email", "selected-a@example.invalid");
        repository.WriteText(
            "Shared.cs",
            "namespace Demo; public sealed class Shared { public int Value => 1; }\n");
        string shared = await repository.CommitAsync(
            "shared\n\nCo-authored-by: Contributor B <selected-b@example.invalid>");
        await repository.GitAsync("switch", "-c", "open-change");
        repository.WriteText(
            "Open.cs",
            "namespace Demo; public sealed class Open { public bool Enabled => true; }\n");
        string openHead = await repository.CommitAsync("open change");
        await repository.GitAsync("switch", "main");
        await repository.GitAsync("config", "user.name", "Contributor B");
        await repository.GitAsync("config", "user.email", "selected-b@example.invalid");
        repository.WriteText(
            "Default.cs",
            "namespace Demo; public sealed class Default { public string Name => \"main\"; }\n");
        string defaultHead = await repository.CommitAsync("default change");
        await repository.GitAsync("switch", "-c", "no-selected-change", shared);
        await repository.GitAsync("config", "user.name", "Baseline Author");
        await repository.GitAsync("config", "user.email", "baseline@example.invalid");
        repository.WriteText("Unselected.md", "# Unselected head\n");
        string noSelectedHead = await repository.CommitAsync("unselected head");
        await repository.GitAsync("switch", "main");
        string executionRoot = Path.Combine(
            Path.GetTempPath(),
            "efforthours-author-manifest-e2e",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(executionRoot);
        try
        {
            string clonedRepository = Path.Combine(executionRoot, "repository-b");
            await CloneAsync(repository.RootPath, clonedRepository);
            string manifestPath = Path.Combine(executionRoot, "portfolio.json");
            string repositoryARelativePath = Path.GetRelativePath(executionRoot, repository.RootPath);
            File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    schemaVersion = "1.0.0",
                    selection = new
                    {
                        sinceInclusive = "2020-01-01T00:00:00Z",
                        untilExclusive = "2030-01-01T00:00:00Z",
                        timeZone = "UTC",
                        dateField = "author",
                        mergePolicy = "exclude",
                        coauthorPolicy = "include",
                        intervalSemantics = "since-inclusive-until-exclusive",
                    },
                    contributors = new object[]
                    {
                        new { id = "contributor-b", aliases = ContributorBAliases },
                        new { id = "contributor-a", aliases = ContributorAAliases },
                    },
                    repositories = new object[]
                    {
                        new
                        {
                            id = "repository-b",
                            repositoryPath = "repository-b",
                            heads = new[] { new { id = "default", objectId = defaultHead } },
                        },
                        new
                        {
                            id = "repository-a",
                            repositoryPath = repositoryARelativePath,
                            heads = new[]
                            {
                                new { id = "open", objectId = openHead },
                                new { id = "default", objectId = defaultHead },
                                new { id = "shared-base", objectId = shared },
                                new { id = "no-selected", objectId = noSelectedHead },
                            },
                        },
                    },
                }));
            string sourceStatus = await repository.GitAsync("status", "--porcelain=v1");

            ProcessResult result = await RunCliAsync(
                "change",
                "portfolio",
                "--author-period-manifest", manifestPath,
                "--no-rate",
                "--compact");

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardError);
            Assert.Equal(sourceStatus, await repository.GitAsync("status", "--porcelain=v1"));
            Assert.DoesNotContain(repository.RootPath, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(clonedRepository, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(repositoryARelativePath, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("selected-a@example.invalid", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("selected-b@example.invalid", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            using JsonDocument report = JsonDocument.Parse(result.StandardOutput);
            JsonElement selection = report.RootElement.GetProperty("selection");
            Assert.Equal("author-period", selection.GetProperty("kind").GetString());
            Assert.True(selection.GetProperty("manifestBased").GetBoolean());
            JsonElement manifest = selection.GetProperty("authorPeriodManifest");
            Assert.Equal(
                ["contributor-a", "contributor-b"],
                manifest.GetProperty("contributorIds").EnumerateArray().Select(value => value.GetString()));
            Assert.Equal(
                ["repository-a", "repository-b"],
                manifest.GetProperty("repositories").EnumerateArray()
                    .Select(value => value.GetProperty("id").GetString()));
            JsonElement repositoryASelection = Assert.Single(
                manifest.GetProperty("repositories").EnumerateArray(),
                repositoryValue => repositoryValue.GetProperty("id").GetString() == "repository-a");
            Assert.Equal(
                ["default", "no-selected", "open", "shared-base"],
                repositoryASelection.GetProperty("heads").EnumerateArray()
                    .Select(value => value.GetProperty("id").GetString()));
            JsonElement[] items = [.. report.RootElement.GetProperty("items").EnumerateArray()];
            Assert.Equal(5, items.Length);
            Assert.Equal(5, items.Select(item => item.GetProperty("selectorId").GetString()).Distinct().Count());
            JsonElement sharedA = Assert.Single(items, item =>
                item.GetProperty("repositoryId").GetString() == "repository-a" &&
                item.GetProperty("selection").GetProperty("head").GetProperty("objectId").GetString() == shared);
            Assert.Equal(
                ["default", "no-selected", "open", "shared-base"],
                sharedA.GetProperty("attribution").GetProperty("headIds")
                    .EnumerateArray().Select(value => value.GetString()));
            Assert.Equal(
                ["contributor-a", "contributor-b"],
                sharedA.GetProperty("attribution").GetProperty("contributorMatches")
                    .EnumerateArray().Select(value => value.GetProperty("contributorId").GetString()));
            Assert.Contains(
                sharedA.GetProperty("attribution").GetProperty("ambiguityReasons").EnumerateArray(),
                reason => reason.GetString()!.Contains("counted once", StringComparison.Ordinal));
            _ = Assert.Single(items, item =>
                item.GetProperty("repositoryId").GetString() == "repository-b" &&
                item.GetProperty("selection").GetProperty("head").GetProperty("objectId").GetString() == shared);
            Assert.Contains(
                report.RootElement.GetProperty("diagnostics").EnumerateArray(),
                diagnostic => diagnostic.GetProperty("code").GetString() == "FB5322");
        }
        finally
        {
            DeleteDirectory(executionRoot);
        }
    }

    [Fact]
    public async Task AuthorPeriodPortfolioIsDeterministicOfflineAndDoesNotMutateWorktree()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        await repository.GitAsync("config", "user.name", "Baseline Author");
        await repository.GitAsync("config", "user.email", "baseline@example.invalid");
        repository.WriteText("Demo.csproj", ProjectFile);
        _ = await repository.CommitAsync("base");
        await repository.GitAsync("config", "user.name", "Selected Contributor");
        await repository.GitAsync("config", "user.email", "selected@example.invalid");
        repository.WriteText(
            "Alpha.cs",
            "namespace Demo; public sealed class Alpha { public int Value => 1; }\n");
        _ = await repository.CommitAsync("alpha");
        repository.WriteText(
            "Beta.cs",
            "namespace Demo; public sealed class Beta { public bool Enabled => true; }\n");
        _ = await repository.CommitAsync("beta");
        repository.WriteText("untracked.txt", "keep me\n");
        string statusBefore = await repository.GitAsync("status", "--porcelain=v1");
        string[] arguments =
        [
            "change",
            "portfolio",
            repository.RootPath,
            "--author", "selected@example.invalid",
            "--since", "2020-01-01T00:00:00Z",
            "--until", "2030-01-01T00:00:00Z",
            "--timezone", "UTC",
            "--no-rate",
            "--compact",
        ];

        ProcessResult first = await RunCliAsync(arguments);
        ProcessResult second = await RunCliAsync(arguments);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(string.Empty, first.StandardError);
        Assert.Equal(first.StandardOutput, second.StandardOutput);
        Assert.Equal(statusBefore, await repository.GitAsync("status", "--porcelain=v1"));
        Assert.DoesNotContain(repository.RootPath, first.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("public sealed class Alpha", first.StandardOutput, StringComparison.Ordinal);
        using JsonDocument report = JsonDocument.Parse(first.StandardOutput);
        Assert.Equal(
            "author-period",
            report.RootElement.GetProperty("selection").GetProperty("kind").GetString());
        JsonElement items = report.RootElement.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.All(items.EnumerateArray(), item =>
        {
            Assert.Equal("commit", item.GetProperty("selection").GetProperty("kind").GetString());
            Assert.Equal("direct-author", item.GetProperty("attribution").GetProperty("kind").GetString());
            Assert.True(item.GetProperty("allocatedExpectedHours").GetDecimal() >= 0m);
        });
        decimal allocated = items.EnumerateArray()
            .Sum(item => item.GetProperty("allocatedExpectedHours").GetDecimal());
        Assert.Equal(
            report.RootElement.GetProperty("totalEffort").GetProperty("expected").GetDecimal(),
            allocated);
        Assert.Contains(
            report.RootElement.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "FB5310");
    }

    [Fact]
    public async Task AuthorPeriodPortfolioSelectsStructuredCoauthorTrailerWithoutEmittingMessage()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Demo.csproj", ProjectFile);
        _ = await repository.CommitAsync("base");
        repository.WriteText(
            "Feature.cs",
            "namespace Demo; public sealed class Feature { public int Value => 1; }\n");
        const string message =
            "coauthored feature\n\nCo-authored-by: Selected Contributor <selected@example.invalid>";
        _ = await repository.CommitAsync(message);

        ProcessResult result = await RunCliAsync(
            "change",
            "portfolio",
            repository.RootPath,
            "--author", "selected@example.invalid",
            "--since", "2020-01-01T00:00:00Z",
            "--until", "2030-01-01T00:00:00Z",
            "--timezone", "UTC",
            "--no-rate",
            "--compact");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.DoesNotContain("coauthored feature", result.StandardOutput, StringComparison.Ordinal);
        using JsonDocument report = JsonDocument.Parse(result.StandardOutput);
        JsonElement item = Assert.Single(report.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("coauthor", item.GetProperty("attribution").GetProperty("kind").GetString());
        Assert.Contains(
            item.GetProperty("attribution").GetProperty("ambiguityReasons").EnumerateArray(),
            reason => reason.GetString()!.Contains("shared-credit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AuthorPeriodManifestPreflightFailuresDoNotLeakPathsOrAliases()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Demo.csproj", ProjectFile);
        string head = await repository.CommitAsync("base");
        string executionRoot = Path.Combine(
            Path.GetTempPath(),
            "efforthours-author-manifest-invalid",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(executionRoot);
        string manifestPath = Path.Combine(executionRoot, "portfolio.json");
        const string missingPath = "sensitive-missing-repository";
        string absentObject = new('f', 40);
        try
        {
            WriteManifest(
                manifestPath,
                new
                {
                    id = "repository-a",
                    repositoryPath = missingPath,
                    heads = new[] { new { id = "default", objectId = head } },
                });
            ProcessResult missing = await RunCliAsync(
                "change", "portfolio", "--author-period-manifest", manifestPath, "--no-rate");
            WriteManifest(
                manifestPath,
                new
                {
                    id = "repository-a",
                    repositoryPath = Path.GetPathRoot(repository.RootPath)!,
                    heads = new[] { new { id = "default", objectId = head } },
                });
            ProcessResult unsafeRoot = await RunCliAsync(
                "change", "portfolio", "--author-period-manifest", manifestPath, "--no-rate");
            WriteManifest(
                manifestPath,
                new
                {
                    id = "repository-a",
                    repositoryPath = repository.RootPath,
                    heads = new[] { new { id = "missing-head", objectId = absentObject } },
                });
            ProcessResult absent = await RunCliAsync(
                "change", "portfolio", "--author-period-manifest", manifestPath, "--no-rate");
            WriteManifest(
                manifestPath,
                new
                {
                    id = "repository-a",
                    repositoryPath = repository.RootPath,
                    heads = new[] { new { id = "default", objectId = head } },
                },
                new
                {
                    id = "repository-b",
                    repositoryPath = repository.RootPath,
                    heads = new[] { new { id = "default", objectId = head } },
                });
            ProcessResult duplicateRoot = await RunCliAsync(
                "change", "portfolio", "--author-period-manifest", manifestPath, "--no-rate");

            foreach (ProcessResult result in new[] { missing, unsafeRoot, absent, duplicateRoot })
            {
                Assert.Equal(3, result.ExitCode);
                Assert.Equal(string.Empty, result.StandardOutput);
                Assert.DoesNotContain(repository.RootPath, result.StandardError, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(
                    "selected-a@example.invalid",
                    result.StandardError,
                    StringComparison.OrdinalIgnoreCase);
            }

            Assert.DoesNotContain(missingPath, missing.StandardError, StringComparison.Ordinal);
            Assert.Contains("filesystem root", unsafeRoot.StandardError, StringComparison.Ordinal);
            Assert.Contains("does not contain pinned commit", absent.StandardError, StringComparison.Ordinal);
            Assert.Contains("does not fetch", absent.StandardError, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("same local Git repository", duplicateRoot.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(executionRoot);
        }
    }

    private static async Task CloneAsync(string sourcePath, string targetPath)
    {
        string workingDirectory = Path.GetDirectoryName(targetPath)!;
        ProcessStartInfo startInfo = StartInfo("git", workingDirectory);
        startInfo.ArgumentList.Add("clone");
        startInfo.ArgumentList.Add("--quiet");
        startInfo.ArgumentList.Add("--no-hardlinks");
        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add(targetPath);
        ProcessResult result = await RunAsync(startInfo);
        Assert.True(result.ExitCode == 0, $"git clone failed: {result.StandardError}");
    }

    private static void WriteManifest(string path, params object[] repositories)
    {
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(new
            {
                schemaVersion = "1.0.0",
                selection = new
                {
                    sinceInclusive = "2020-01-01T00:00:00Z",
                    untilExclusive = "2030-01-01T00:00:00Z",
                    timeZone = "UTC",
                    dateField = "author",
                    mergePolicy = "exclude",
                    coauthorPolicy = "include",
                    intervalSemantics = "since-inclusive-until-exclusive",
                },
                contributors = new[]
                {
                    new { id = "contributor-a", aliases = ContributorAAliases },
                },
                repositories,
            }));
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}
