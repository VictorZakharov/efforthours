using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Theory]
    [InlineData("2026-01-19T00:00:00-05:00", "2026-01-23T00:00:00-05:00", "2026-01-22T12:00:00-05:00")]
    [InlineData("2026-03-07T00:00:00-05:00", "2026-03-09T00:00:00-04:00", "2026-03-08T12:00:00-04:00")]
    public async Task ReplayProtectsOriginalTwoHoursAndDatesOnlyNovelRemainderAcrossOffsetsAndDst(string since, string until, string eventDate)
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Demo.csproj", ProjectFile);
        repository.WriteText("Feature.cs", "public class Feature { public int Value => 0; }\n");
        string oldBase = (await repository.CommitAsync("base")).Trim();
        await repository.GitAsync("switch", "-c", "original");
        repository.WriteText("Feature.cs", "public class Feature { public int Value => 1; }\n");
        DateTimeOffset originalDate = Instant(since).AddHours(12);
        string original = (await HistoricalCommitAsync(repository, "original", originalDate.ToString("O"), originalDate.ToString("O"))).Trim();
        await repository.GitAsync("switch", "main");
        repository.WriteText("Upstream.cs", "public class Upstream { public bool Ready => true; }\n");
        string newBase = (await repository.CommitAsync("upstream by other contributor")).Trim();
        await repository.GitAsync("switch", "-c", "replay");
        repository.WriteText("Feature.cs", "public class Feature { public int Value => 1; }\n");
        string replay = (await HistoricalCommitAsync(repository, "pure replay", originalDate.ToString("O"), Instant(eventDate).ToString("O"))).Trim();
        await repository.GitAsync("switch", "-c", "retained", newBase);
        repository.WriteText("Feature.cs", "public class Feature { public int Value => 1; public int Sum(int[] values) { if (values == null) throw new System.ArgumentException(); int total = 0; foreach (int value in values) { if (value < 0) throw new System.ArgumentException(); total += value; } return total; } }\n");
        string retained = (await HistoricalCommitAsync(repository, "retained resolution", originalDate.ToString("O"), Instant(eventDate).ToString("O"))).Trim();
        ChangePortfolioReplayEvent declaration = new()
        {
            Id = "rewrite",
            OldBaseObjectId = oldBase,
            OriginalObjectId = original,
            NewBaseObjectId = newBase,
            RewrittenObjectId = retained,
            ReplayObjectId = replay,
            ReplayProvenanceId = "reviewed-replay",
            EventTimestamp = Instant(eventDate),
            EventProvenanceId = "external-record",
        };
        ChangeAuthorPeriodManifest manifest = new()
        {
            Selection = new()
            {
                SinceInclusive = Instant(since),
                UntilExclusive = Instant(until),
                TimeZone = "America/Toronto",
                DateField = ChangePortfolioDateField.Author,
                MergePolicy = ChangePortfolioMergePolicy.Exclude,
                CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include
            },
            Contributors = [new() { Id = "selected", Aliases = ["selected@example.invalid"] }],
            Repositories = [new() { Id = "repository", RepositoryPath = repository.RootPath,
                Heads = [new() { Id = "original", ObjectId = original }, new() { Id = "retained", ObjectId = retained }], ReplayEvents = [declaration] }],
        };
        var offset = await Run(manifest);
        var utc = await Run(manifest with
        {
            Selection = manifest.Selection with { SinceInclusive = Instant(since).ToUniversalTime(), UntilExclusive = Instant(until).ToUniversalTime() },
            Repositories = [manifest.Repositories[0] with { ReplayEvents = [declaration with { EventTimestamp = Instant(eventDate).ToUniversalTime() }] }],
        });
        Assert.Equal(offset.Verification.SemanticDigest, utc.Verification.SemanticDigest);
        var legacy = await Run(manifest with
        {
            Repositories = [manifest.Repositories[0] with
        {
            ReplayEvents = null,
            RewriteEvents = [new() { OriginalObjectId = original, RewrittenObjectId = retained, OldBaseObjectId = oldBase,
                NewBaseObjectId = newBase, EventTimestamp = Instant(eventDate).ToUniversalTime() }],
        }]
        });
        Assert.Equal(legacy.SourcePortfolio!.TotalEffort, offset.SourcePortfolio!.TotalEffort);
        Assert.Equal(ContractJson.SerializeCompact(legacy.SourcePortfolio.Categories), ContractJson.SerializeCompact(offset.SourcePortfolio.Categories));

        Assert.Equal(2.75m, offset.SourcePortfolio!.TotalEffort.Expected);
        var allocation = Assert.Single(offset.SourcePortfolio.ReplayAllocations!);
        Assert.Equal(2m, allocation.ReservedOriginalExpectedHours);
        Assert.Equal(.75m, allocation.AllocatedEventExpectedHours);
        var points = offset.Series.Single(value => value.Kind == ChangePortfolioSeriesKind.Portfolio).Points;
        Assert.Equal(2m, points[0].Effort.Expected);
        Assert.Equal(.75m, points[^1].Effort.Expected);
        var eventOnly = await Run(manifest with { Selection = manifest.Selection with { SinceInclusive = Instant(eventDate) } });
        Assert.Equal(.75m, eventOnly.SourcePortfolio!.TotalEffort.Expected);
        var beforeEvent = await Run(manifest with { Selection = manifest.Selection with { UntilExclusive = Instant(eventDate) } });
        Assert.Equal(2m, beforeEvent.SourcePortfolio!.TotalEffort.Expected);
        Assert.Equal("event-outside-period", Assert.Single(beforeEvent.SourcePortfolio.ReplayAllocations!).Status);
        var pure = await Run(manifest with
        {
            Repositories = [manifest.Repositories[0] with
        { Heads = [manifest.Repositories[0].Heads[0], new() { Id = "retained", ObjectId = replay }], ReplayEvents = [declaration with { RewrittenObjectId = replay }] }]
        });
        Assert.Equal(0m, Assert.Single(pure.SourcePortfolio!.ReplayAllocations!).AllocatedEventExpectedHours);
        Assert.Equal(2m, pure.SourcePortfolio.TotalEffort.Expected);

        async Task<ChangePortfolioComparisonReport> Run(ChangeAuthorPeriodManifest input)
        {
            string path = Path.Combine(repository.RootPath, ".git", "date-manifest.json");
            string output = Path.Combine(repository.RootPath, ".git", "date-report.json");
            await File.WriteAllTextAsync(path, ContractJson.Serialize(input), new UTF8Encoding(false));
            ProcessResult result = await RunCliAsync("change", "portfolio", "--author-period-manifest", path, "--bucket", "calendar-day",
                "--scope", "engineering", "--no-rate", "--compact", "--no-checkpoint", "--output", output);
            Assert.True(result.ExitCode == 0, result.StandardError);
            string json = await File.ReadAllTextAsync(output, Encoding.UTF8);
            Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioComparisonReport, json).IsValid);
            var report = ContractJson.Deserialize<ChangePortfolioComparisonReport>(json);
            Assert.Empty(ContractValidation.Validate(report));
            return report;
        }
    }
}
