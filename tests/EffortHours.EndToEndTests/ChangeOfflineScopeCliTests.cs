using System.Globalization;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests : ChangeCliTestSupport
{
    [Fact]
    public async Task OfflineEngineeringScopeAppliesRepositoryOverridesToRowsAndFinalEndpoint()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Demo.csproj", ProjectFile);
        _ = await repository.CommitAsync("base");
        repository.WriteText("Feature.cs", "public class Feature { public int Value => 1; }\n");
        repository.WriteText("calibration/Hidden.cs", "public class Hidden { public string Name => \"hidden\"; }\n");
        _ = await HistoricalCommitAsync(repository, "feature", "2026-01-19T12:00:00Z", "2026-01-19T12:00:00Z");
        repository.WriteText("Feature.cs", "public class Feature { public int Value => 1; public bool Ready => true; }\n");
        string head = (await HistoricalCommitAsync(repository, "follow-up", "2026-01-20T12:00:00Z", "2026-01-20T12:00:00Z")).Trim();
        string input = Path.Combine(repository.RootPath, ".git", "scope-manifest.json");
        string output = Path.Combine(repository.RootPath, ".git", "scope-report.json");
        ChangeAuthorPeriodManifest manifest = new()
        {
            Selection = new()
            {
                SinceInclusive = DateTimeOffset.Parse("2026-01-19T00:00:00Z", CultureInfo.InvariantCulture),
                UntilExclusive = DateTimeOffset.Parse("2026-01-21T00:00:00Z", CultureInfo.InvariantCulture),
                TimeZone = "UTC",
                DateField = ChangePortfolioDateField.Author,
                MergePolicy = ChangePortfolioMergePolicy.Exclude,
                CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include
            },
            Contributors = [new() { Id = "selected", Aliases = ["selected@example.invalid"] }],
            Repositories = [new() { Id = "repository", RepositoryPath = repository.RootPath,
                ScopeRepository = "victorzakharov/efforthours", Heads = [new() { Id = "default", ObjectId = head }] }],
        };
        await File.WriteAllTextAsync(input, ContractJson.Serialize(manifest), new UTF8Encoding(false));
        ProcessResult result = await RunCliAsync("change", "portfolio", "--author-period-manifest", input,
            "--bucket", "calendar-day", "--scope", "engineering", "--output", output, "--no-checkpoint", "--no-rate", "--compact");
        Assert.True(result.ExitCode == 0, result.StandardError);
        string json = await File.ReadAllTextAsync(output);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioComparisonReport, json).IsValid);
        ChangePortfolioComparisonReport report = ContractJson.Deserialize<ChangePortfolioComparisonReport>(json);
        Assert.Empty(ContractValidation.Validate(report));
        Assert.Null(report.Discovery);
        Assert.NotNull(report.ScopeProfile);
        Assert.Equal(2, report.ScopeSummary!.AdmittedCommitCount);
        Assert.All(report.SourcePortfolio!.Items, item => Assert.Equal(1, item.AnalyzedPathCount));
        Assert.Contains(report.SourcePortfolio.Diagnostics, diagnostic => diagnostic.Code == "FB5336");
        Assert.DoesNotContain("victorzakharov/efforthours", json, StringComparison.OrdinalIgnoreCase);
        ProcessResult plain = await RunCliAsync("change", "portfolio", "--author-period-manifest", input,
            "--scope", "engineering", "--no-rate", "--compact");
        Assert.True(plain.ExitCode == 0, plain.StandardError);
        ChangePortfolioReport source = ContractJson.Deserialize<ChangePortfolioReport>(plain.StandardOutput);
        Assert.Empty(ContractValidation.Validate(source));
        Assert.Equal(report.SourcePortfolio.TotalEffort, source.TotalEffort);
        Assert.All(source.Items, item => Assert.Equal(1, item.AnalyzedPathCount));
    }
}
