using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Fact]
    public async Task AuthorPeriodManifestAcceptsSiblingRepositoryFromManifestInsideWorktree()
    {
        using GitFixture repositoryA = await GitFixture.CreateAsync();
        await repositoryA.GitAsync("config", "user.name", "Selected Contributor");
        await repositoryA.GitAsync("config", "user.email", "selected@example.invalid");
        repositoryA.WriteText("Demo.csproj", ProjectFile);
        repositoryA.WriteText(
            "Feature.cs",
            "namespace Demo; public sealed class Feature { public bool Enabled => true; }\n");
        string head = await repositoryA.CommitAsync("selected change");
        string sibling = Path.Combine(
            Path.GetDirectoryName(repositoryA.RootPath)!,
            $"efforthours-sibling-{Guid.NewGuid():N}");
        string manifestPath = Path.Combine(repositoryA.RootPath, "portfolio.json");
        try
        {
            await CloneAsync(repositoryA.RootPath, sibling);
            string[] selectedAliases = ["selected@example.invalid"];
            await File.WriteAllTextAsync(
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
                    contributors = new[]
                    {
                        new
                        {
                            id = "contributor-a",
                            aliases = selectedAliases,
                        },
                    },
                    repositories = new[]
                    {
                        new
                        {
                            id = "repository-a",
                            repositoryPath = ".",
                            heads = new[] { new { id = "default", objectId = head } },
                        },
                        new
                        {
                            id = "repository-b",
                            repositoryPath = Path.GetRelativePath(repositoryA.RootPath, sibling),
                            heads = new[] { new { id = "default", objectId = head } },
                        },
                    },
                }));

            ProcessResult result = await RunCliAsync(
                "change",
                "portfolio",
                "--author-period-manifest", manifestPath,
                "--no-rate",
                "--compact");

            Assert.Equal(0, result.ExitCode);
            using JsonDocument report = JsonDocument.Parse(result.StandardOutput);
            Assert.Equal(2, report.RootElement.GetProperty("items").GetArrayLength());
            Assert.DoesNotContain(sibling, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(sibling, result.StandardError, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(manifestPath);
            DeleteDirectory(sibling);
        }
    }
}
