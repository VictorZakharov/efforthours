using System.Text;
using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests : ChangeCliTestSupport
{
    [Fact]
    public async Task AuthorPeriodExclusionKeepsExcludedPathsOutOfEveryChangeAndDay()
    {
        using GitFixture vendored = await ExclusionFixtureAsync(withVendor: true);
        using GitFixture authored = await ExclusionFixtureAsync(withVendor: false);
        string executionRoot = Path.Combine(Path.GetTempPath(), "efforthours-exclusion", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(executionRoot);
        try
        {
            ChangePortfolioComparisonReport plain = await IndependentDayAsync(executionRoot, "plain", vendored, null);
            Assert.DoesNotContain("Packs", ContractJson.Serialize(plain), StringComparison.Ordinal);
            ChangePortfolioComparisonReport excluded = await IndependentDayAsync(executionRoot, "excluded", vendored, ["Packs/**"]);
            ChangePortfolioComparisonReport own = await IndependentDayAsync(executionRoot, "own", authored, null);

            Assert.Equal(own.SourcePortfolio!.TotalEffort, excluded.SourcePortfolio!.TotalEffort);
            Assert.Equal(ContractJson.Serialize(own.SourcePortfolio.Categories), ContractJson.Serialize(excluded.SourcePortfolio.Categories));
            Assert.True(plain.SourcePortfolio!.TotalEffort.Expected > excluded.SourcePortfolio.TotalEffort.Expected);
            Assert.NotEqual(plain.Verification.SemanticDigest, excluded.Verification.SemanticDigest);
            Assert.Empty(ContractValidation.Validate(excluded));
            SchemaValidationResult schema = ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioComparisonReport,
                ContractJson.Serialize(excluded));
            Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
            Assert.DoesNotContain("Packs/", ContractJson.Serialize(excluded), StringComparison.Ordinal);

            ChangePortfolioComparisonReport warm = await IndependentDayAsync(executionRoot, "excluded", vendored, ["Packs/**"]);
            Assert.Equal(excluded.Verification.SemanticDigest, warm.Verification.SemanticDigest);
            Assert.Equal(ChangePortfolioRepositoryExecutionStatus.Reused, Assert.Single(warm.Execution.Repositories).Status);

            string manifest = await WriteExclusionManifestAsync(executionRoot, "everything", vendored,
                ["Demo.csproj", "*.cs", "Packs"]);
            ProcessResult everything = await RunCliAsync("change", "portfolio", "--author-period-manifest", manifest,
                "--bucket", "independent-day", "--no-checkpoint", "--output", Path.Combine(executionRoot, "failed.json"), "--no-rate");
            Assert.NotEqual(0, everything.ExitCode);
            Assert.Contains("admits no file", everything.StandardError, StringComparison.Ordinal);
            manifest = await WriteExclusionManifestAsync(executionRoot, "unsafe", vendored, ["/Packs"]);
            ProcessResult rejected = await RunCliAsync("change", "portfolio", "--author-period-manifest", manifest,
                "--bucket", "independent-day", "--no-checkpoint", "--output", Path.Combine(executionRoot, "failed.json"), "--no-rate");
            Assert.NotEqual(0, rejected.ExitCode);
            Assert.Contains("exclude", rejected.StandardError, StringComparison.Ordinal);
        }
        finally { DeleteDirectory(executionRoot); }
    }

    private static async Task<GitFixture> ExclusionFixtureAsync(bool withVendor)
    {
        GitFixture repository = await GitFixture.CreateAsync();
        await repository.GitAsync("config", "user.email", "baseline@example.invalid");
        repository.WriteText("Demo.csproj", ProjectFile);
        repository.WriteText("Own.cs", FinalDeltaSource("Own", 3));
        if (withVendor) repository.WriteText("Packs/Forest/Pack.cs", FinalDeltaSource("Pack", 40));
        _ = await repository.CommitAsync("opening");
        await repository.GitAsync("config", "user.email", "selected@example.invalid");
        repository.WriteText("Own.cs", FinalDeltaSource("Own", 12));
        if (withVendor) repository.WriteText("Packs/Forest/Pack.cs", FinalDeltaSource("Pack", 90));
        _ = await HistoricalCommitAsync(repository, "edit own and pack", "2026-01-01T09:00:00Z", "2026-01-01T09:00:00Z");
        repository.WriteText("Own.cs", FinalDeltaSource("Own", 20));
        if (withVendor) repository.WriteText("Packs/Forest/Pack.cs", FinalDeltaSource("Pack", 140));
        _ = await HistoricalCommitAsync(repository, "next day", "2026-01-02T09:00:00Z", "2026-01-02T09:00:00Z");
        return repository;
    }

    private static async Task<ChangePortfolioComparisonReport> IndependentDayAsync(string root, string name,
        GitFixture repository, IReadOnlyList<string>? exclude)
    {
        string manifest = await WriteExclusionManifestAsync(root, name, repository, exclude);
        string output = Path.Combine(root, name + ".json");
        ProcessResult run = await RunCliAsync("change", "portfolio", "--author-period-manifest", manifest,
            "--bucket", "independent-day", "--normalization", "joint", "--checkpoint", Path.Combine(root, name + "-checkpoint"),
            "--output", output, "--no-rate");
        Assert.True(run.ExitCode == 0, run.StandardError);
        return ContractJson.Deserialize<ChangePortfolioComparisonReport>(await File.ReadAllTextAsync(output, Encoding.UTF8));
    }

    private static async Task<string> WriteExclusionManifestAsync(string root, string name, GitFixture repository,
        IReadOnlyList<string>? exclude)
    {
        string head = await repository.GitAsync("rev-parse", "HEAD");
        string path = Path.Combine(root, name + "-manifest.json");
        Dictionary<string, object> entry = new()
        {
            ["id"] = "repository",
            ["repositoryPath"] = repository.RootPath,
            ["heads"] = new[] { new { id = "default", objectId = head } },
        };
        if (exclude is not null) entry["exclude"] = exclude;
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0.0",
            selection = new
            {
                sinceInclusive = "2026-01-01T00:00:00Z",
                untilExclusive = "2026-01-03T00:00:00Z",
                timeZone = "UTC",
                dateField = "committer",
                mergePolicy = "first-parent",
                coauthorPolicy = "exclude",
                intervalSemantics = "since-inclusive-until-exclusive",
            },
            contributors = new[] { new { id = "contributor", aliases = FinalDeltaAliases } },
            repositories = new[] { entry },
        }), new UTF8Encoding(false));
        return path;
    }
}
