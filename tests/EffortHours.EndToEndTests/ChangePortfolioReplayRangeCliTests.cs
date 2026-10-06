using System.Text;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    private static async Task VerifyDailyReplayPortfolioAsync(GitFixture repository, ChangeRewriteReviewManifest review, bool partitions)
    {
        string checkpoint = Path.Combine(repository.RootPath, ".git", "replay-checkpoint-" + Guid.NewGuid().ToString("N"));
        ChangePortfolioReplayEvent declaration = new()
        {
            Id = "rewrite",
            OldBaseObjectId = review.OldBaseObjectId,
            OriginalObjectId = review.OriginalObjectId,
            NewBaseObjectId = review.NewBaseObjectId,
            RewrittenObjectId = review.RewrittenObjectId,
            ReplayObjectId = review.ReplayObjectId,
            ReplayProvenanceId = review.ReplayProvenanceId,
            EventTimestamp = review.EventTimestamp,
            EventProvenanceId = review.EventProvenanceId,
        };
        ChangeAuthorPeriodManifest manifest = new()
        {
            Selection = new()
            {
                SinceInclusive = review.SinceInclusive,
                UntilExclusive = review.UntilExclusive,
                TimeZone = "UTC",
                DateField = ChangePortfolioDateField.Author,
                MergePolicy = ChangePortfolioMergePolicy.Exclude,
                CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include
            },
            Contributors = [new() { Id = "selected", Aliases = ["selected@example.invalid"] }],
            Repositories = [new() { Id = "repository", RepositoryPath = repository.RootPath,
                Heads = [new() { Id = "original", ObjectId = review.OriginalObjectId }, new() { Id = "retained", ObjectId = review.RewrittenObjectId }],
                ReplayEvents = [declaration] }],
        };
        _ = await new GitPortfolioPlanner().PlanAuthorPeriodManifestAsync(manifest, ChangeAuthorPeriodManifestIdentity.ComputeDigest(manifest),
            new Dictionary<string, string> { ["repository"] = repository.RootPath });
        ChangePortfolioComparisonReport cold = await Run(manifest);
        ChangePortfolioComparisonReport warm = await Run(manifest);
        Assert.Equal(cold.Verification.SemanticDigest, warm.Verification.SemanticDigest);
        Assert.Equal(1, warm.Execution.Checkpoint.HitCount);
        ChangePortfolioReport full = cold.SourcePortfolio!;
        ChangePortfolioReplayAllocation allocation = Assert.Single(full.ReplayAllocations!);
        decimal standalone = allocation.Evidence.Review.Comparisons.Single(value => value.Role == "novel-retained-delta").Effort.Expected;
        Assert.Equal(Math.Min(standalone, allocation.AvailableJointExpectedHours), allocation.AllocatedEventExpectedHours);
        Assert.Equal(standalone > allocation.AvailableJointExpectedHours, allocation.AllocationCapped);
        Assert.All(full.Items.Where(item => item.Attribution.Replay?.Role == "retained"), item => Assert.Equal(review.EventTimestamp, item.Attribution.SelectedTimestamp));
        Assert.All(full.Items.Where(item => item.DuplicateOfItemId is not null || item.ExactComposition is not null), item => Assert.Equal(0m, item.AllocatedExpectedHours));
        ChangePortfolioComparisonReport ordinary = await Run(manifest with { Repositories = [manifest.Repositories[0] with { ReplayEvents = null }] });
        Assert.Equal(ordinary.SourcePortfolio!.TotalEffort, full.TotalEffort);
        if (!partitions) return;
        DateTimeOffset split = Instant("2026-01-22T00:00:00Z");
        ChangePortfolioComparisonReport earlier = await Run(manifest with { Selection = manifest.Selection with { UntilExclusive = split } });
        ChangePortfolioComparisonReport later = await Run(manifest with { Selection = manifest.Selection with { SinceInclusive = split } });
        Assert.Equal(full.TotalEffort.Expected, earlier.SourcePortfolio!.TotalEffort.Expected + later.SourcePortfolio!.TotalEffort.Expected);
        Assert.Equal(allocation.AllocatedEventExpectedHours, later.SourcePortfolio.TotalEffort.Expected);
        ChangePortfolioComparisonReport unknown = await Run(manifest with
        {
            Repositories = [manifest.Repositories[0] with
            { ReplayEvents = [declaration with { EventTimestamp = null, EventProvenanceId = null }] }]
        });
        Assert.Equal("unresolved-event-date", Assert.Single(unknown.SourcePortfolio!.ReplayAllocations!).Status);
        Assert.Null(unknown.SourcePortfolio.ReplayAllocations![0].AllocatedEventExpectedHours);
        await Run(manifest with { Repositories = [manifest.Repositories[0] with { ReplayEvents = [declaration with { ReplayObjectId = new string('a', 40) }] }] }, incomplete: true);
        await Run(manifest with { Repositories = [manifest.Repositories[0] with { ReplayEvents = [declaration, declaration with { Id = "competing" }] }] }, incomplete: true);

        async Task<ChangePortfolioComparisonReport> Run(ChangeAuthorPeriodManifest input, bool incomplete = false)
        {
            string path = Path.Combine(repository.RootPath, ".git", "daily-replay.json");
            string output = Path.Combine(repository.RootPath, ".git", "daily-replay-" + Guid.NewGuid().ToString("N") + ".json");
            await File.WriteAllTextAsync(path, ContractJson.Serialize(input), new UTF8Encoding(false));
            ProcessResult result = await RunCliAsync("change", "portfolio", "--author-period-manifest", path,
                "--bucket", "calendar-day", "--output", output, "--checkpoint", checkpoint, "--no-rate", "--compact");
            Assert.True(result.ExitCode == (incomplete ? 3 : 0), result.StandardError);
            string json = await File.ReadAllTextAsync(output, Encoding.UTF8);
            SchemaValidationResult schema = ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioComparisonReport, json);
            Assert.True(schema.IsValid, string.Join(" ", schema.Errors));
            ChangePortfolioComparisonReport report = ContractJson.Deserialize<ChangePortfolioComparisonReport>(json);
            Assert.Empty(ContractValidation.Validate(report));
            Assert.DoesNotContain(repository.RootPath, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("selected@example.invalid", json, StringComparison.Ordinal);
            if (incomplete) { Assert.Null(report.SourcePortfolio); Assert.Empty(report.Series); }
            return report;
        }
    }
}
