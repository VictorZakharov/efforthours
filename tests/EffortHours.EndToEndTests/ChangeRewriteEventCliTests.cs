using System.Globalization;
using System.Text;
using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests : ChangeCliTestSupport
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DeclaredRewriteDatesOnlyNovelContributionAndPreservesPeriodSum(bool actualRebase, bool resolution)
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Demo.csproj", ProjectFile);
        repository.WriteText("Feature.cs", "public class Feature { public int Value => 0; }\n");
        string oldBase = (await repository.CommitAsync("base")).Trim();
        await repository.GitAsync("switch", "-c", "original");
        repository.WriteText("Feature.cs", "public class Feature { public int Value => 1; }\n");
        string original = (await HistoricalCommitAsync(repository, "feature", "2026-01-19T12:00:00Z", "2026-01-19T12:00:00Z")).Trim();
        await repository.GitAsync("switch", "main");
        repository.WriteText("Upstream.cs", "public class Upstream { public bool Ready => true; }\n");
        if (actualRebase) repository.WriteText("Feature.cs", "public class Feature { public int Value => 2; }\n");
        string newBase = (await repository.CommitAsync("upstream")).Trim();
        if (actualRebase)
        {
            await repository.GitAsync("switch", "-c", "rewritten", original);
            var rebase = StartInfo("git", repository.RootPath);
            rebase.ArgumentList.Add("rebase"); rebase.ArgumentList.Add("main");
            Assert.NotEqual(0, (await RunAsync(rebase)).ExitCode);
            Assert.Contains("Feature.cs", await repository.GitAsync("diff", "--name-only", "--diff-filter=U"), StringComparison.Ordinal);
        }
        else await repository.GitAsync("switch", "-c", "rewritten");
        repository.WriteText("Feature.cs", "public class Feature { public int Value => 1; }\n");
        if (resolution) repository.WriteText("Resolution.cs", "public class Resolution { public int Sum(int[] values) { int total = 0; foreach (int value in values) { if (value < 0) throw new System.ArgumentException(); total += value; } return total; } }\n");
        await repository.GitAsync("add", "--all");
        string rewritten;
        if (actualRebase)
        {
            var finish = StartInfo("git", repository.RootPath);
            finish.Environment["GIT_COMMITTER_DATE"] = "2026-01-22T12:00:00Z";
            foreach (string argument in new[] { "-c", "core.editor=true", "rebase", "--continue" }) finish.ArgumentList.Add(argument);
            ProcessResult result = await RunAsync(finish);
            Assert.True(result.ExitCode == 0, result.StandardError);
            rewritten = (await repository.GitAsync("rev-parse", "HEAD")).Trim();
        }
        else rewritten = (await HistoricalCommitAsync(repository, "rewrite", "2026-01-19T12:00:00Z", "2026-01-22T12:00:00Z")).Trim();
        async Task<string> Copy(string commit, string parent, string name)
        {
            await repository.GitAsync("switch", "-c", name, parent);
            var cherryPick = StartInfo("git", repository.RootPath);
            cherryPick.Environment["GIT_COMMITTER_DATE"] = "2026-01-23T12:00:00Z";
            cherryPick.ArgumentList.Add("cherry-pick"); cherryPick.ArgumentList.Add(commit);
            ProcessResult picked = await RunAsync(cherryPick);
            Assert.True(picked.ExitCode == 0, picked.StandardError);
            return (await repository.GitAsync("rev-parse", "HEAD")).Trim();
        }
        string originalCopy = await Copy(original, oldBase, "copy-original");
        string rewrittenCopy = await Copy(rewritten, newBase, "copy-rewritten");
        Assert.NotEqual(original, originalCopy);
        Assert.NotEqual(rewritten, rewrittenCopy);
        string status = await repository.GitAsync("status", "--porcelain=v1");
        string input = Path.Combine(repository.RootPath, ".git", "rewrite-manifest.json");
        async Task<ChangePortfolioComparisonReport> Estimate(string since, string until, bool eventKnown = true, bool includePair = true, bool invalidBase = false, bool copies = false)
        {
            ChangeAuthorPeriodManifest manifest = new()
            {
                Selection = new()
                {
                    SinceInclusive = DateTimeOffset.Parse(since, CultureInfo.InvariantCulture),
                    UntilExclusive = DateTimeOffset.Parse(until, CultureInfo.InvariantCulture),
                    TimeZone = "UTC",
                    DateField = ChangePortfolioDateField.Author,
                    MergePolicy = ChangePortfolioMergePolicy.Exclude,
                    CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include
                },
                Contributors = [new() { Id = "selected", Aliases = ["selected@example.invalid"] }],
                Repositories = [new() { Id = "repository", RepositoryPath = repository.RootPath,
                    Heads = copies
                        ? [new() { Id = "original", ObjectId = original }, new() { Id = "rewritten", ObjectId = rewritten },
                            new() { Id = "copy-original", ObjectId = originalCopy }, new() { Id = "copy-rewritten", ObjectId = rewrittenCopy }]
                        : [new() { Id = "original", ObjectId = original }, new() { Id = "rewritten", ObjectId = rewritten }],
                    RewriteEvents = includePair ? [new() { OriginalObjectId = original, RewrittenObjectId = rewritten, OldBaseObjectId = invalidBase ? newBase : oldBase,
                        NewBaseObjectId = newBase, EventTimestamp = eventKnown ? DateTimeOffset.Parse("2026-01-22T12:00:00Z", CultureInfo.InvariantCulture) : null }] : null }],
            };
            await File.WriteAllTextAsync(input, ContractJson.Serialize(manifest), new UTF8Encoding(false));
            string capacityPath = Path.Combine(repository.RootPath, ".git", "rewrite-capacity.json");
            ChangePortfolioCapacityManifest capacity = new()
            {
                CalendarPolicy = "Fixed eight-hour daily reference, independent of logged labor.",
                Entries = [.. Enumerable.Range(0, (int)(manifest.Selection.UntilExclusive - manifest.Selection.SinceInclusive).TotalDays)
                    .Select(index => new ChangePortfolioCapacityEntry
                    {
                        BucketId = "day-" + manifest.Selection.SinceInclusive.AddDays(index).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        ContributorId = "selected", Hours = 8m,
                    })],
            };
            await File.WriteAllTextAsync(capacityPath, ContractJson.Serialize(capacity), new UTF8Encoding(false));
            string output = Path.Combine(repository.RootPath, ".git", "rewrite-report.json");
            ProcessResult result = await RunCliAsync("change", "portfolio", "--author-period-manifest", input,
                "--bucket", "calendar-day", "--scope", "engineering", "--output", output, "--capacity-manifest", capacityPath, "--no-checkpoint", "--no-rate", "--compact");
            Assert.True(result.ExitCode == (invalidBase ? 3 : 0), result.StandardError);
            string json = await File.ReadAllTextAsync(output);
            SchemaValidationResult schema = ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioComparisonReport, json);
            Assert.True(schema.IsValid, string.Join(" ", schema.Errors));
            ChangePortfolioComparisonReport report = ContractJson.Deserialize<ChangePortfolioComparisonReport>(json);
            Assert.Empty(ContractValidation.Validate(report));
            Assert.DoesNotContain(repository.RootPath, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("selected@example.invalid", json, StringComparison.Ordinal);
            return report;
        }
        ChangePortfolioComparisonReport full = await Estimate("2026-01-19T00:00:00Z", "2026-01-23T00:00:00Z");
        ChangePortfolioComparisonReport control = await Estimate("2026-01-19T00:00:00Z", "2026-01-23T00:00:00Z", includePair: false);
        Assert.Equal(control.SourcePortfolio!.TotalEffort, full.SourcePortfolio!.TotalEffort);
        Assert.Equal(ContractJson.Serialize(control.SourcePortfolio.Categories), ContractJson.Serialize(full.SourcePortfolio.Categories));
        ChangePortfolioComparisonSeries series = full.Series.Single(value => value.Kind == ChangePortfolioSeriesKind.Portfolio);
        Assert.All(series.Points, point =>
        {
            Assert.Equal(8m, point.CapacityHours);
            Assert.Equal(decimal.Round(point.Effort.Expected / 8m, 6, MidpointRounding.AwayFromZero), point.CapacityRatio!.Expected);
        });
        decimal originalHours = series.Points[0].Effort.Expected;
        decimal novelHours = series.Points[3].Effort.Expected;
        Assert.True(originalHours > 0m);
        if (resolution) Assert.True(novelHours > 0m); else Assert.Equal(0m, novelHours);
        Assert.Equal(full.SourcePortfolio!.TotalEffort.Expected, originalHours + novelHours);
        Assert.All(full.SourcePortfolio.Items, item => Assert.Equal(ChangeRewriteAttribution.JointBudgetBasis, item.Attribution.Rewrite!.AllocationBasis));
        Assert.Contains(full.SourcePortfolio.Diagnostics, diagnostic => diagnostic.Code == "FB5342");
        ChangePortfolioReport legacy = full.SourcePortfolio with
        {
            Items = [.. full.SourcePortfolio.Items.Select(item => item with
            { Attribution = item.Attribution with { Rewrite = item.Attribution.Rewrite! with { AllocationBasis = null } } })],
        };
        Assert.Empty(ContractValidation.Validate(legacy));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioReport, ContractJson.Serialize(legacy)).IsValid);
        ChangePortfolioComparisonReport copied = await Estimate("2026-01-19T00:00:00Z", "2026-01-24T00:00:00Z", copies: true);
        Assert.Equal(full.SourcePortfolio.TotalEffort, copied.SourcePortfolio!.TotalEffort);
        Assert.Equal(originalHours, copied.Series.Single(value => value.Kind == ChangePortfolioSeriesKind.Portfolio).Points[0].Effort.Expected);
        Assert.Equal(novelHours, copied.Series.Single(value => value.Kind == ChangePortfolioSeriesKind.Portfolio).Points[3].Effort.Expected);
        Assert.All(copied.SourcePortfolio.Items.Where(item => item.Attribution.Rewrite is null), item => Assert.Equal(0m, item.AllocatedExpectedHours));
        ChangePortfolioComparisonReport copiedEvent = await Estimate("2026-01-22T00:00:00Z", "2026-01-23T00:00:00Z", copies: true);
        Assert.Equal(novelHours, copiedEvent.SourcePortfolio!.TotalEffort.Expected);
        Assert.DoesNotContain(full.SourcePortfolio.Items, item => item.Selection.Head.ObjectId == newBase);
        ChangePortfolioComparisonReport eventOnly = await Estimate("2026-01-22T00:00:00Z", "2026-01-23T00:00:00Z");
        Assert.Equal(novelHours, eventOnly.SourcePortfolio!.TotalEffort.Expected);
        Assert.Equal(0m, eventOnly.SourcePortfolio.Items.Single(item => item.Attribution.Rewrite!.Role == "original").AllocatedExpectedHours);
        ChangePortfolioComparisonReport originalOnly = await Estimate("2026-01-19T00:00:00Z", "2026-01-20T00:00:00Z");
        Assert.Equal(originalHours, originalOnly.SourcePortfolio!.TotalEffort.Expected);
        ChangePortfolioComparisonReport unresolved = await Estimate("2026-01-19T00:00:00Z", "2026-01-23T00:00:00Z", false);
        Assert.Contains(unresolved.SourcePortfolio!.Diagnostics, diagnostic => diagnostic.Code == "FB5340");
        Assert.Equal(full.SourcePortfolio.TotalEffort, unresolved.SourcePortfolio.TotalEffort);
        ChangePortfolioComparisonReport missing = await Estimate("2026-01-19T00:00:00Z", "2026-01-23T00:00:00Z", invalidBase: true);
        Assert.Equal(ChangePortfolioComparisonStatus.Incomplete, missing.Status);
        Assert.Null(missing.SourcePortfolio);
        Assert.Empty(missing.Series);
        Assert.Contains("rewrite evidence", Assert.Single(missing.Execution.Failures).Message, StringComparison.Ordinal);
        Assert.Equal(status, await repository.GitAsync("status", "--porcelain=v1"));
    }
}
