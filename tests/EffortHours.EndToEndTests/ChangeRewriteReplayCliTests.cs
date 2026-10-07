using System.Globalization;
using System.Text;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests : ChangeCliTestSupport
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplayRangeReviewSeparatesActualRebaseNovelDeltaAndPreservesSquashedResults(bool conflict)
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Demo.csproj", ProjectFile);
        repository.WriteText("Feature.cs", "public class Feature { public int Value => 0; }\n");
        string oldBase = await Dated("base", "2025-01-01T12:00:00Z");
        await repository.GitAsync("switch", "-c", "original");
        repository.WriteText("Feature.cs", "public class Feature { public int Value => 1; }\n");
        await Dated("first feature", "2026-01-19T12:00:00Z");
        repository.WriteText("Validation.cs", "public class Validation { public bool Valid(int value) => value >= 0; }\n");
        string original = await Dated("second feature", "2026-01-20T12:00:00Z");
        await repository.GitAsync("switch", "main");
        repository.WriteText("Upstream.cs", "public class Upstream { public bool Ready => true; }\n");
        if (conflict) repository.WriteText("Feature.cs", "public class Feature { public int Value => 2; }\n");
        string newBase = await Dated("upstream", "2026-01-21T12:00:00Z");
        string replay = await Rebase("replay", false);
        string rewritten = await Rebase("rewritten", true);
        ChangeRewriteReviewManifest manifest = new()
        {
            RepositoryId = "repository",
            RepositoryPath = repository.RootPath,
            OldBaseObjectId = oldBase,
            OriginalObjectId = original,
            NewBaseObjectId = newBase,
            RewrittenObjectId = rewritten,
            ReplayObjectId = replay,
            ReplayProvenanceId = "reviewed-replay",
            EventTimestamp = Instant("2026-01-22T16:00:00Z"),
            EventProvenanceId = "external-event-record",
            SinceInclusive = Instant("2026-01-19T00:00:00Z"),
            UntilExclusive = Instant("2026-01-24T00:00:00Z"),
        };
        string status = await repository.GitAsync("status", "--porcelain=v1");
        string refs = await repository.GitAsync("show-ref");
        ChangeRewriteReviewReport full = await Review(manifest);
        ChangeRewriteReviewReport offset = await Review(manifest with
        {
            SinceInclusive = manifest.SinceInclusive.ToOffset(TimeSpan.FromHours(-5)),
            UntilExclusive = manifest.UntilExclusive.ToOffset(TimeSpan.FromHours(-5)),
            EventTimestamp = manifest.EventTimestamp!.Value.ToOffset(TimeSpan.FromHours(-5)),
        });
        Assert.Equal(ContractJson.SerializeCompact(full), ContractJson.SerializeCompact(offset));
        Assert.Equal(manifest.ReplayProvenanceId, full.ReplayProvenanceId);
        Assert.Equal(manifest.EventProvenanceId, full.EventProvenanceId);
        Assert.Equal(oldBase, full.Comparisons.Single(value => value.Role == "original-implementation").Selection.Base.ObjectId);
        Assert.Equal(original, full.Comparisons.Single(value => value.Role == "original-implementation").Selection.Head.ObjectId);
        Assert.Equal(newBase, full.Comparisons.Single(value => value.Role == "retained-feature").Selection.Base.ObjectId);
        Assert.Equal(replay, full.Comparisons.Single(value => value.Role == "novel-retained-delta").Selection.Base.ObjectId);
        Assert.Equal(rewritten, full.Comparisons.Single(value => value.Role == "novel-retained-delta").Selection.Head.ObjectId);
        Assert.Equal(2, full.OriginalCommitCount);
        Assert.Equal(conflict ? 2 : 3, full.RewrittenCommitCount);
        Assert.Equal(conflict ? "caller-declared-conflict-replay" : "exact-path-replay-verified", full.ReplayConfidence);
        Assert.Equal(conflict ? 1 : 0, full.ReplayProof!.DeclaredConflictPathCount);
        Assert.Equal(1, full.ReplayProof.InheritedUpstreamPathCount);
        Assert.Equal(Instant("2026-01-20T12:00:00Z"), full.OriginalAuthorTimestamp);
        Assert.NotEqual(full.OriginalAuthorTimestamp, full.RewrittenCommitterTimestamp);
        Assert.NotEqual(full.EventTimestamp, full.RewrittenCommitterTimestamp);
        Assert.True(full.EventAttributedNovelEffort!.Expected > 0m);
        var retained = full.Comparisons.Single(value => value.Role == "retained-feature");
        ChangeEstimateReport direct = await new ChangeEstimator().EstimateAsync(
            await new GitChangePlanner().PlanBaseHeadAsync(repository.RootPath, newBase, rewritten),
            EstimationProfile.Implementation, rateCard: null);
        Assert.Equal(direct.TotalEffort, retained.Effort);
        ChangeRewriteReviewReport eventOnly = await Review(manifest with { SinceInclusive = Instant("2026-01-22T00:00:00Z") });
        Assert.Equal(full.EventAttributedNovelEffort, eventOnly.EventAttributedNovelEffort);
        ChangeRewriteReviewReport earlier = await Review(manifest with { UntilExclusive = Instant("2026-01-22T00:00:00Z") });
        Assert.Equal("event-outside-period", earlier.EventAttributionStatus);
        Assert.Equal(0m, earlier.EventAttributedNovelEffort!.Expected);
        ChangeRewriteReviewReport unknown = await Review(manifest with { EventTimestamp = null, EventProvenanceId = null });
        Assert.Equal("unresolved-event-date", unknown.EventAttributionStatus);
        Assert.Null(unknown.EventAttributedNovelEffort);
        ChangeRewriteReviewReport missing = await Review(manifest with { ReplayObjectId = null, ReplayProvenanceId = null });
        Assert.Equal("unresolved-replay-evidence", missing.Status);
        Assert.Null(missing.EventAttributedNovelEffort);
        Assert.Equal(3, missing.Comparisons.Count);
        ChangeRewriteReviewReport pure = await Review(manifest with { RewrittenObjectId = replay });
        Assert.Equal(0m, pure.EventAttributedNovelEffort!.Expected);
        await VerifyDailyReplayPortfolioAsync(repository, manifest, partitions: true);
        await VerifyDailyReplayPortfolioAsync(repository, manifest with { RewrittenObjectId = replay }, partitions: false);
        string squashed = await Squash(rewritten);
        ChangeRewriteReviewReport squash = await Review(manifest with { RewrittenObjectId = squashed });
        await VerifyDailyReplayPortfolioAsync(repository, manifest with { RewrittenObjectId = squashed }, partitions: false);
        Assert.Equal(1, squash.RewrittenCommitCount);
        Assert.Equal(retained.Effort, squash.Comparisons.Single(value => value.Role == "retained-feature").Effort);
        Assert.Equal(full.EventAttributedNovelEffort, squash.EventAttributedNovelEffort);
        await repository.GitAsync("switch", "-c", "copied", newBase);
        string[] commits = (await repository.GitAsync("rev-list", "--reverse", newBase + ".." + rewritten)).Split('\n');
        var pick = StartInfo("git", repository.RootPath);
        pick.Environment["GIT_COMMITTER_DATE"] = "2026-01-23T12:00:00Z";
        pick.ArgumentList.Add("cherry-pick");
        foreach (string commit in commits) pick.ArgumentList.Add(commit);
        ProcessResult copied = await RunAsync(pick);
        Assert.True(copied.ExitCode == 0, copied.StandardError);
        string copiedHead = (await repository.GitAsync("rev-parse", "HEAD")).Trim();
        Assert.NotEqual(rewritten, copiedHead);
        ChangeRewriteReviewReport copy = await Review(manifest with { RewrittenObjectId = copiedHead });
        await VerifyDailyReplayPortfolioAsync(repository, manifest with { RewrittenObjectId = copiedHead }, partitions: false);
        Assert.Equal(retained.Effort, copy.Comparisons.Single(value => value.Role == "retained-feature").Effort);
        Assert.Equal(full.EventAttributedNovelEffort, copy.EventAttributedNovelEffort);
        await Review(manifest with { ReplayObjectId = rewritten }, valid: false); // Hidden novel file is not a replay.
        ChangeRewriteReviewReport unavailable = await Review(manifest with { ReplayObjectId = new string('a', 40) }, unresolved: true);
        Assert.Equal("unresolved-object-evidence", unavailable.EventAttributionStatus);
        Assert.Null(unavailable.EventAttributedNovelEffort);
        Assert.Empty(unavailable.Comparisons);
        Assert.Equal(status, await repository.GitAsync("status", "--porcelain=v1"));
        // Only the explicit fixture squash/copy creates new refs; review leaves refs intact.
        Assert.Equal(refs, string.Join('\n', (await repository.GitAsync("show-ref")).Split('\n').Where(line => !line.EndsWith("refs/heads/squashed", StringComparison.Ordinal) && !line.EndsWith("refs/heads/copied", StringComparison.Ordinal))));

        async Task<string> Dated(string message, string date)
        {
            if (message != "upstream") return (await HistoricalCommitAsync(repository, message, date, date)).Trim();
            await repository.GitAsync("add", "--all");
            var start = StartInfo("git", repository.RootPath);
            start.Environment["GIT_AUTHOR_DATE"] = date;
            start.Environment["GIT_COMMITTER_DATE"] = date;
            start.Environment["GIT_AUTHOR_NAME"] = "Upstream Contributor";
            start.Environment["GIT_AUTHOR_EMAIL"] = "upstream@example.invalid";
            foreach (string argument in new[] { "commit", "--quiet", "-m", message }) start.ArgumentList.Add(argument);
            Assert.Equal(0, (await RunAsync(start)).ExitCode);
            return (await repository.GitAsync("rev-parse", "HEAD")).Trim();
        }
        async Task<string> Rebase(string branch, bool novel)
        {
            await repository.GitAsync("switch", "-c", branch, original);
            var start = StartInfo("git", repository.RootPath);
            start.Environment["GIT_COMMITTER_DATE"] = "2026-01-22T12:00:00Z";
            foreach (string value in new[] { "rebase", "main" }) start.ArgumentList.Add(value);
            ProcessResult result = await RunAsync(start);
            Assert.Equal(conflict ? 1 : 0, result.ExitCode);
            if (conflict)
            {
                Assert.Contains("Feature.cs", await repository.GitAsync("diff", "--name-only", "--diff-filter=U"), StringComparison.Ordinal);
                repository.WriteText("Feature.cs", "public class Feature { public int Value => 1; }\n");
                if (novel) AddNovel();
                await repository.GitAsync("add", "--all");
                var finish = StartInfo("git", repository.RootPath);
                finish.Environment["GIT_COMMITTER_DATE"] = "2026-01-22T12:00:00Z";
                foreach (string value in new[] { "-c", "core.editor=true", "rebase", "--continue" }) finish.ArgumentList.Add(value);
                result = await RunAsync(finish);
                Assert.True(result.ExitCode == 0, result.StandardError);
            }
            else if (novel) { AddNovel(); await Dated("novel resolution", "2026-01-22T12:00:00Z"); }
            return (await repository.GitAsync("rev-parse", "HEAD")).Trim();
        }
        void AddNovel() => repository.WriteText("Resolution.cs", "public class Resolution { public int Sum(int[] values) { int total = 0; foreach (int value in values) { if (value < 0) throw new System.ArgumentException(); total += value; } return total; } }\n");
        async Task<string> Squash(string head)
        {
            await repository.GitAsync("switch", "-c", "squashed", newBase);
            await repository.GitAsync("merge", "--squash", head);
            return await Dated("squashed retained feature", "2026-01-22T12:00:00Z");
        }
        async Task<ChangeRewriteReviewReport> Review(ChangeRewriteReviewManifest input, bool valid = true, bool unresolved = false)
        {
            string manifestPath = Path.Combine(repository.RootPath, ".git", "review.json");
            await File.WriteAllTextAsync(manifestPath, ContractJson.Serialize(input), new UTF8Encoding(false));
            ProcessResult result = await RunCliAsync("change", "review-rewrite", manifestPath, "--scope", "all", "--compact");
            if (!valid)
            {
                Assert.NotEqual(0, result.ExitCode);
                Assert.Empty(result.StandardOutput);
                return null!;
            }
            Assert.True(result.ExitCode == (unresolved ? 3 : 0), result.StandardError);
            SchemaValidationResult schema = ContractSchemaValidator.Validate(SchemaNames.ChangeRewriteReviewReport, result.StandardOutput);
            Assert.True(schema.IsValid, string.Join(" ", schema.Errors));
            ChangeRewriteReviewReport report = ContractJson.Deserialize<ChangeRewriteReviewReport>(result.StandardOutput);
            Assert.Empty(ContractValidation.Validate(report));
            Assert.DoesNotContain(repository.RootPath, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Resolution.cs", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("efforthours-e2e@example.invalid", result.StandardOutput, StringComparison.Ordinal);
            return report;
        }
    }

    private static DateTimeOffset Instant(string date) => DateTimeOffset.Parse(date, CultureInfo.InvariantCulture);
}
