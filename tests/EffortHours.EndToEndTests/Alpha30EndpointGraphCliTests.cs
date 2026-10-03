using System.Text;
using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests : ChangeCliTestSupport
{
    [Fact]
    public async Task ForkMergeRetainedSquashAndReversedDatesMatchEndpointAndPreserveDailyCheckpoint()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Demo.csproj", ProjectFile);
        repository.WriteText("Feature.cs", FinalDeltaSource("Feature", 1));
        repository.WriteText("Side.cs", FinalDeltaSource("Side", 1));
        string opening = await repository.CommitAsync("opening");
        repository.WriteText("Feature.cs", FinalDeltaSource("Feature", 101));
        await HistoricalCommitAsync(repository, "expand", "2026-01-01T09:00:00Z", "2026-01-01T09:00:00Z");
        repository.WriteText("Feature.cs", FinalDeltaSource("Feature", 2));
        await HistoricalCommitAsync(repository, "reduce", "2026-01-01T08:00:00Z", "2026-01-01T08:00:00Z");
        await repository.GitAsync("switch", "-c", "side", opening);
        repository.WriteText("Side.cs", FinalDeltaSource("Side", 4));
        await HistoricalCommitAsync(repository, "side first", "2026-01-01T10:00:00Z", "2026-01-01T10:00:00Z");
        repository.WriteText("Side.cs", FinalDeltaSource("Side", 7));
        string side = await HistoricalCommitAsync(repository, "side final", "2026-01-01T10:30:00Z", "2026-01-01T10:30:00Z");
        await repository.GitAsync("switch", "main");
        await repository.GitAsync("merge", "--no-ff", "--no-commit", "side");
        string closing = await HistoricalCommitAsync(repository, "merge", "2026-01-01T07:00:00Z", "2026-01-01T07:00:00Z");
        await repository.GitAsync("switch", "-c", "retained-squash", opening);
        repository.WriteText("Side.cs", FinalDeltaSource("Side", 7));
        string squash = await HistoricalCommitAsync(repository, "retained squash", "2026-01-01T11:00:00Z", "2026-01-01T11:00:00Z");
        await repository.GitAsync("switch", "main");
        string status = await repository.GitAsync("status", "--porcelain=v1");
        string refs = await repository.GitAsync("show-ref");
        ProcessResult direct = await RunCliAsync("change", repository.RootPath, "--base", opening, "--head", closing, "--no-rate", "--compact");
        Assert.True(direct.ExitCode == 0, direct.StandardError);
        ChangeEstimateReport endpoint = ContractJson.Deserialize<ChangeEstimateReport>(direct.StandardOutput);
        Assert.Contains(endpoint.Diagnostics, diagnostic => diagnostic.Code == "FB5210");
        string executionRoot = Path.Combine(Path.GetTempPath(), "efforthours-graph-proof", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(executionRoot);
        try
        {
            string manifest = Path.Combine(executionRoot, "manifest.json");
            string output = Path.Combine(executionRoot, "report.json");
            string checkpoint = Path.Combine(executionRoot, "checkpoint");
            void Manifest(string since, string mergePolicy = "first-parent") => File.WriteAllText(manifest, JsonSerializer.Serialize(new
            {
                schemaVersion = "1.0.0",
                selection = new
                {
                    sinceInclusive = since,
                    untilExclusive = "2026-01-02T00:00:00Z",
                    timeZone = "UTC",
                    dateField = "committer",
                    mergePolicy,
                    coauthorPolicy = "include",
                    intervalSemantics = "since-inclusive-until-exclusive"
                },
                contributors = new[] { new { id = "contributor", aliases = FinalDeltaAliases } },
                repositories = new[] { new { id = "repository", repositoryPath = repository.RootPath,
                    heads = new[] { new { id = "default", objectId = closing }, new { id = "side", objectId = side },
                        new { id = "retained", objectId = squash } } } },
            }), new UTF8Encoding(false));
            async Task<ChangePortfolioComparisonReport> Run()
            {
                ProcessResult result = await RunCliAsync("change", "portfolio", "--author-period-manifest", manifest,
                    "--bucket", "independent-day", "--checkpoint", checkpoint, "--output", output, "--no-rate");
                Assert.True(result.ExitCode == 0, result.StandardError);
                return ContractJson.Deserialize<ChangePortfolioComparisonReport>(File.ReadAllText(output, Encoding.UTF8));
            }
            Manifest("2026-01-01T00:00:00Z");
            ChangePortfolioComparisonReport report = await Run();
            Assert.Equal(endpoint.TotalEffort, report.SourcePortfolio!.TotalEffort);
            Assert.Equal(ContractJson.Serialize(endpoint.Categories), ContractJson.Serialize(report.SourcePortfolio.Categories));
            Assert.Contains(report.SourcePortfolio.Diagnostics, diagnostic => diagnostic.Code == "FB5336");
            Assert.Contains(report.SourcePortfolio.Diagnostics, diagnostic => diagnostic.Code == "FB5210");
            Assert.DoesNotContain(report.SourcePortfolio.Diagnostics, diagnostic => diagnostic.Code == "FB5337");
            Assert.Contains(report.SourcePortfolio.Items, item => item.ExactComposition is not null || item.DuplicateOfItemId is not null);
            Assert.Equal(report.SourcePortfolio.TotalEffort.Expected, report.SourcePortfolio.Items.Sum(item => item.AllocatedExpectedHours));
            ChangePortfolioComparisonReport warm = await Run();
            Assert.Equal(report.Verification.SemanticDigest, warm.Verification.SemanticDigest);
            Assert.Equal(ChangePortfolioRepositoryExecutionStatus.Reused, Assert.Single(warm.Execution.Repositories).Status);
            Assert.Equal(0, warm.Execution.Resources!.SnapshotAnalysisRequests);
            Manifest("2025-12-31T00:00:00Z");
            ChangePortfolioComparisonReport wider = await Run();
            Assert.Equal(ContractJson.Serialize(report.SourcePortfolio.DailyNormalization!.Days),
                ContractJson.Serialize(wider.SourcePortfolio!.DailyNormalization!.Days));
            Manifest("2026-01-01T00:00:00Z", "exclude");
            ChangePortfolioComparisonReport rejected = await Run();
            Assert.DoesNotContain(rejected.SourcePortfolio!.Diagnostics, diagnostic => diagnostic.Code == "FB5336");
            Diagnostic reason = Assert.Single(rejected.SourcePortfolio.Diagnostics, diagnostic => diagnostic.Code == "FB5337");
            Assert.Contains("rejection=inventory-mismatch;", reason.Message, StringComparison.Ordinal);
            ChangePortfolioComparisonReport rejectedWarm = await Run();
            Assert.Equal(rejected.Verification.SemanticDigest, rejectedWarm.Verification.SemanticDigest);
            Assert.Equal(ContractJson.Serialize(reason), ContractJson.Serialize(Assert.Single(rejectedWarm.SourcePortfolio!.Diagnostics, diagnostic => diagnostic.Code == "FB5337")));
            Assert.Equal(ChangePortfolioRepositoryExecutionStatus.Reused, Assert.Single(rejectedWarm.Execution.Repositories).Status);
            Assert.Equal(0, rejectedWarm.Execution.Resources!.SnapshotAnalysisRequests);
            Assert.Empty(ContractValidation.Validate(wider));
            Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioComparisonReport, ContractJson.Serialize(wider)).IsValid);
            Assert.DoesNotContain(repository.RootPath, ContractJson.Serialize(wider), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("selected@example.invalid", ContractJson.Serialize(wider), StringComparison.OrdinalIgnoreCase);
        }
        finally { DeleteDirectory(executionRoot); }
        Assert.Equal(status, await repository.GitAsync("status", "--porcelain=v1"));
        Assert.Equal(refs, await repository.GitAsync("show-ref"));
    }
}
