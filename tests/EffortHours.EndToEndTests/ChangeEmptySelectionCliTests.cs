using System.Globalization;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests : ChangeCliTestSupport
{
    [Theory]
    [InlineData("calendar-day")]
    [InlineData("independent-day")]
    public async Task ExplicitManifestEmptySelectionIsCompleteZero(string bucket)
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Feature.cs", "public class Feature {}\n");
        string head = (await repository.CommitAsync("feature")).Trim();
        string path = Path.Combine(repository.RootPath, ".git", "empty-manifest.json");
        ChangeAuthorPeriodManifest manifest = new()
        {
            Selection = new()
            {
                SinceInclusive = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture),
                UntilExclusive = DateTimeOffset.Parse("2000-01-02T00:00:00Z", CultureInfo.InvariantCulture),
                TimeZone = "UTC",
                DateField = ChangePortfolioDateField.Author,
                MergePolicy = ChangePortfolioMergePolicy.Exclude,
                CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include
            },
            Contributors = [new() { Id = "selected", Aliases = ["selected@example.invalid"] }],
            Repositories = [new() { Id = "repository", RepositoryPath = repository.RootPath,
                Heads = [new() { Id = "default", ObjectId = head }] }],
        };
        await File.WriteAllTextAsync(path, ContractJson.Serialize(manifest), new UTF8Encoding(false));
        string output = Path.Combine(repository.RootPath, ".git", "empty-report.json");
        ProcessResult result = await RunCliAsync("change", "portfolio", "--author-period-manifest", path,
            "--bucket", bucket, "--scope", "engineering", "--output", output, "--no-checkpoint", "--no-rate", "--compact");
        Assert.True(result.ExitCode == 0, result.StandardError);
        string json = await File.ReadAllTextAsync(output);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioComparisonReport, json).IsValid);
        ChangePortfolioComparisonReport report = ContractJson.Deserialize<ChangePortfolioComparisonReport>(json);
        Assert.Empty(ContractValidation.Validate(report));
        Assert.Equal(ChangePortfolioComparisonStatus.Complete, report.Status);
        Assert.Empty(report.Execution.Failures);
        Assert.Equal(0, report.Execution.Resources!.SnapshotAnalysisRequests);
        Assert.Equal(0, report.Execution.Resources.ProjectedSnapshotRequests);
        Assert.Empty(report.SourcePortfolio!.Items);
        Assert.Equal(0m, report.SourcePortfolio.TotalEffort.Expected);
        Assert.All(report.Series.SelectMany(series => series.Points), point => Assert.Equal(0m, point.Effort.Expected));
        ProcessResult plain = await RunCliAsync("change", "portfolio", "--author-period-manifest", path,
            "--scope", "engineering", "--no-rate", "--compact");
        Assert.True(plain.ExitCode == 0, plain.StandardError);
        Assert.Empty(ContractValidation.Validate(ContractJson.Deserialize<ChangePortfolioReport>(plain.StandardOutput)));
    }
}
