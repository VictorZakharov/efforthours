using System.Globalization;
using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Fact]
    public async Task ManifestPreflightMeasuresSelectionWithoutSnapshotAnalysis()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        await repository.GitAsync("config", "user.name", "Baseline Author");
        await repository.GitAsync("config", "user.email", "baseline@example.invalid");
        repository.WriteText("Demo.csproj", ProjectFile);
        _ = await repository.CommitAsync("base");
        await repository.GitAsync("config", "user.name", "Selected Contributor");
        await repository.GitAsync("config", "user.email", "selected@example.invalid");
        repository.WriteText("Feature.cs", "namespace Demo; public sealed class Feature { }\n");
        string head = await repository.CommitAsync("selected change");
        DateTimeOffset selectedAt = DateTimeOffset.Parse(
            await repository.GitAsync("show", "-s", "--format=%aI", head),
            CultureInfo.InvariantCulture);
        string manifestPath = Path.Combine(repository.RootPath, "preflight-manifest.json");
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(new
            {
                schemaVersion = "1.0.0",
                selection = new
                {
                    sinceInclusive = selectedAt.AddDays(-1),
                    untilExclusive = selectedAt.AddDays(1),
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
                        aliases = ComparisonContributorAliases,
                    },
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

        ProcessResult result = await RunCliAsync(
            [
                "change", "portfolio",
                "--author-period-manifest", manifestPath,
                "--preflight",
                "--format", "json",
                "--compact",
                "--no-rate",
            ]);

        Assert.Equal(0, result.ExitCode);
        ChangePortfolioPreflightReport report =
            ContractJson.Deserialize<ChangePortfolioPreflightReport>(result.StandardOutput);
        Assert.Equal(ChangePortfolioPreflightStatus.Ready, report.Status);
        Assert.Equal(1, report.Totals.SelectedChangeCount);
        Assert.Equal(2, report.Totals.ProjectedSnapshotRequests);
        Assert.Equal(ChangePortfolioPreflightAction.RunNormally, report.Recommendation.Action);
        Assert.DoesNotContain("snapshot-diff-construction", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("static-analysis", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain(repository.RootPath, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("selected@example.invalid", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioPreflightReport,
            result.StandardOutput).IsValid);
    }
}
