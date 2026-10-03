using System.Text;
using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests : ChangeCliTestSupport
{
    private static readonly string[] FinalDeltaAliases = ["selected@example.invalid"];

    [Fact]
    public async Task AuthorPeriodPartialReversalMatchesEndpointWithCompleteContextAndCheckpointReuse()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        await repository.GitAsync("config", "user.email", "baseline@example.invalid");
        repository.WriteText("Demo.csproj", ProjectFile);
        repository.WriteText("ZRetained.cs", FinalDeltaSource("Retained", 400));
        repository.WriteText("Feature.cs", FinalDeltaSource("Feature", 1));
        string opening = await repository.CommitAsync("opening");
        await repository.GitAsync("config", "user.email", "selected@example.invalid");
        repository.WriteText("Feature.cs", FinalDeltaSource("Feature", 101));
        string expansion = await HistoricalCommitAsync(repository, "expansion", "2026-01-01T09:00:00Z", "2026-01-01T09:00:00Z");
        repository.WriteText("Feature.cs", FinalDeltaSource("Feature", 2));
        string closing = await HistoricalCommitAsync(repository, "partial reversal", "2026-01-01T10:00:00Z", "2026-01-01T10:00:00Z");
        string status = await repository.GitAsync("status", "--porcelain=v1");
        ProcessResult direct = await RunCliAsync("change", repository.RootPath, "--base", opening,
            "--head", closing, "--no-rate", "--compact");
        Assert.True(direct.ExitCode == 0, direct.StandardError);
        ChangeEstimateReport endpoint = ContractJson.Deserialize<ChangeEstimateReport>(direct.StandardOutput);
        ProcessResult grow = await RunCliAsync("change", repository.RootPath, "--base", opening,
            "--head", expansion, "--no-rate", "--compact");
        Assert.True(grow.ExitCode == 0, grow.StandardError);
        ChangeEstimateReport growth = ContractJson.Deserialize<ChangeEstimateReport>(grow.StandardOutput);
        Assert.Contains(growth.Evidence.Diagnostics, diagnostic => diagnostic.Code == "FB5205" &&
            diagnostic.Message.Contains("2 relevant unchanged context artifact(s)", StringComparison.Ordinal));
        string executionRoot = Path.Combine(Path.GetTempPath(), "efforthours-final-delta", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(executionRoot);
        try
        {
            string manifest = Path.Combine(executionRoot, "manifest.json");
            string output = Path.Combine(executionRoot, "result.json");
            string checkpoint = Path.Combine(executionRoot, "checkpoint");
            File.WriteAllText(manifest, JsonSerializer.Serialize(new
            {
                schemaVersion = "1.0.0",
                selection = new
                {
                    sinceInclusive = "2026-01-01T00:00:00Z",
                    untilExclusive = "2026-01-02T00:00:00Z",
                    timeZone = "UTC",
                    dateField = "committer",
                    mergePolicy = "exclude",
                    coauthorPolicy = "include",
                    intervalSemantics = "since-inclusive-until-exclusive",
                },
                contributors = new[] { new { id = "contributor", aliases = FinalDeltaAliases } },
                repositories = new[] { new { id = "repository", repositoryPath = repository.RootPath,
                    heads = new[] { new { id = "default", objectId = closing } } } },
            }), new UTF8Encoding(false));
            ProcessResult joint = await RunCliAsync("change", "portfolio", "--author-period-manifest", manifest,
                "--bucket", "calendar-month", "--checkpoint", checkpoint, "--output", output, "--no-rate");
            Assert.True(joint.ExitCode == 0, joint.StandardError);
            ChangePortfolioComparisonReport report = ContractJson.Deserialize<ChangePortfolioComparisonReport>(
                File.ReadAllText(output, Encoding.UTF8));
            Assert.Equal(endpoint.TotalEffort, report.SourcePortfolio!.TotalEffort);
            Assert.Equal(ContractJson.Serialize(endpoint.Categories), ContractJson.Serialize(report.SourcePortfolio.Categories));
            Assert.Contains(report.SourcePortfolio.Diagnostics, diagnostic => diagnostic.Code == "FB5336");
            ProcessResult warm = await RunCliAsync("change", "portfolio", "--author-period-manifest", manifest,
                "--bucket", "calendar-month", "--checkpoint", checkpoint, "--output", output, "--no-rate");
            Assert.True(warm.ExitCode == 0, warm.StandardError);
            ChangePortfolioComparisonReport cached = ContractJson.Deserialize<ChangePortfolioComparisonReport>(
                File.ReadAllText(output, Encoding.UTF8));
            Assert.Equal(report.Verification.SemanticDigest, cached.Verification.SemanticDigest);
            Assert.Equal(ChangePortfolioRepositoryExecutionStatus.Reused, Assert.Single(cached.Execution.Repositories).Status);
            Assert.Equal(0, cached.Execution.Resources!.SnapshotAnalysisRequests);
            Assert.Empty(ContractValidation.Validate(cached));
            SchemaValidationResult schema = ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioComparisonReport,
                ContractJson.Serialize(cached));
            Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
            Assert.DoesNotContain(repository.RootPath, ContractJson.Serialize(cached), StringComparison.OrdinalIgnoreCase);
            ProcessResult daily = await RunCliAsync("change", "portfolio", "--author-period-manifest", manifest,
                "--bucket", "independent-day", "--checkpoint", checkpoint, "--output", output, "--no-rate");
            Assert.True(daily.ExitCode == 0, daily.StandardError);
            ChangePortfolioComparisonReport independent = ContractJson.Deserialize<ChangePortfolioComparisonReport>(
                File.ReadAllText(output, Encoding.UTF8));
            Assert.Equal(endpoint.TotalEffort, independent.SourcePortfolio!.TotalEffort);
            Assert.Equal(ChangePortfolioRepositoryExecutionStatus.Complete, Assert.Single(independent.Execution.Repositories).Status);
            Assert.Equal(ChangePortfolioDailyNormalization.Policy, independent.SourcePortfolio.DailyNormalization!.Protocol);
            Assert.Empty(ContractValidation.Validate(independent));
        }
        finally { DeleteDirectory(executionRoot); }
        Assert.Equal(status, await repository.GitAsync("status", "--porcelain=v1"));
    }

    private static string FinalDeltaSource(string name, int methods) =>
        $"namespace Demo; public sealed class {name} {{\n" + string.Join('\n', Enumerable.Range(0, methods)
            .Select(index => $"public int Operation{index}(int input) => input + {index};")) + "\n}\n";
}
