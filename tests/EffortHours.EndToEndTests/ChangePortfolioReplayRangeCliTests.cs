using System.Text;
using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

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
        Assert.Equal(Math.Min(standalone, allocation.AvailableJointExpectedHours - allocation.ReservedOriginalExpectedHours!.Value), allocation.AllocatedEventExpectedHours);
        Assert.Equal(standalone > allocation.AvailableJointExpectedHours - allocation.ReservedOriginalExpectedHours!.Value, allocation.AllocationCapped);
        Assert.True(full.Items.Where(item => item.Attribution.Replay?.Role == "original").Sum(item => item.AllocatedExpectedHours) >= allocation.ReservedOriginalExpectedHours);
        Assert.All(full.Items.Where(item => item.Attribution.Replay?.Role == "retained"), item => Assert.Equal(review.EventTimestamp, item.Attribution.SelectedTimestamp));
        Assert.All(full.Items.Where(item => item.DuplicateOfItemId is not null || item.ExactComposition is not null), item => Assert.Equal(0m, item.AllocatedExpectedHours));
        ChangePortfolioComparisonReport ordinary = await Run(manifest with { Repositories = [manifest.Repositories[0] with { ReplayEvents = null }] });
        Assert.Equal(ordinary.SourcePortfolio!.TotalEffort, full.TotalEffort);
        Assert.Equal("available", cold.AttributionCompleteness!.DeclaredEventStatus);
        Assert.Equal("unresolved-original-workday", cold.AttributionCompleteness.OriginalWorkdayStatus);
        Assert.Equal("unknown", cold.AttributionCompleteness.IntermediateHistoryStatus);
        AssertResolvedEventStillHasUnresolvedWorkdays(cold);
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
        Assert.Contains(unknown.SourcePortfolio.Diagnostics, value => value.Code == "FB5344");
        Assert.Contains(unknown.Diagnostics, value => value.Code == "FB5344");
        Assert.Equal("unresolved", unknown.AttributionCompleteness!.DeclaredEventStatus);
        Assert.Equal(1, unknown.AttributionCompleteness.MissingEventDateCount);
        Assert.Contains("FB5344", ChangePortfolioComparisonMarkdownRenderer.Render(unknown), StringComparison.Ordinal);
        Assert.Contains("FB5344", ChangePortfolioComparisonMarkdownRenderer.Render(unknown with { View = ChangePortfolioComparisonView.Findings }), StringComparison.Ordinal);
        Assert.NotEmpty(ContractValidation.Validate(unknown with { AttributionCompleteness = unknown.AttributionCompleteness with { MissingEventDateCount = 0 } }));
        Assert.Empty(ContractValidation.Validate(unknown with { AttributionCompleteness = null })); // Saved v1 compatibility.
        ChangePortfolioComparisonReport emptyUnknown = await Run(manifest with
        {
            Selection = manifest.Selection with { SinceInclusive = Instant("2026-01-23T00:00:00Z") },
            Repositories = [manifest.Repositories[0] with { ReplayEvents = [declaration with { EventTimestamp = null, EventProvenanceId = null }] }]
        });
        Assert.All(emptyUnknown.Series[0].Points, point => Assert.Equal(0m, point.Effort.Expected));
        Assert.Equal("unresolved", emptyUnknown.AttributionCompleteness!.DeclaredEventStatus);
        Assert.Contains(emptyUnknown.Diagnostics, value => value.Code == "FB5344");
        ChangeWorkRecordManifest records = new()
        {
            SourceSemanticDigest = unknown.Verification.SemanticDigest,
            Records = [new() { RecordId = "implementation", Date = "2026-01-19", Kind = "implementation", RepositoryIds = ["repository"] }]
        };
        ChangeWorkdayReviewReport unresolvedReview = ChangeWorkdayReviewer.Review(unknown, records, ChangeWorkdayReviewPolicies.EqualEntries);
        Assert.Equal("unresolved-event-attribution", unresolvedReview.Days[0].Status);
        Assert.Null(unresolvedReview.Days[0].MatchedDailyMultiplier);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeWorkdayReviewReport, ContractJson.Serialize(unresolvedReview)).IsValid);
        AssertDeclaredProjectionKeepsReplayUncertainty(unknown, records);
        ChangePortfolioComparisonReport missing = await Run(manifest with
        {
            Repositories = [manifest.Repositories[0] with
            { ReplayEvents = [declaration with { ReplayObjectId = null, ReplayProvenanceId = null }] }]
        });
        Assert.Equal("unresolved-replay-evidence", Assert.Single(missing.SourcePortfolio!.ReplayAllocations!).Status);
        Assert.Contains(missing.SourcePortfolio.Diagnostics, value => value.Code == "FB5345");
        Assert.Contains(missing.Diagnostics, value => value.Code == "FB5345");
        Assert.Equal(1, missing.AttributionCompleteness!.MissingReplayBaselineCount);
        AssertDeclaredProjectionKeepsReplayUncertainty(missing, records with { SourceSemanticDigest = missing.Verification.SemanticDigest });
        await Run(manifest with { Repositories = [manifest.Repositories[0] with { ReplayEvents = [declaration with { ReplayObjectId = new string('a', 40) }] }] }, incomplete: true);
        await Run(manifest with { Repositories = [manifest.Repositories[0] with { ReplayEvents = [declaration, declaration with { Id = "competing" }] }] }, incomplete: true);

        static void AssertResolvedEventStillHasUnresolvedWorkdays(ChangePortfolioComparisonReport source)
        {
            ChangeWorkRecordManifest activity = new()
            {
                SourceSemanticDigest = source.Verification.SemanticDigest,
                Records = [new() { RecordId = "later-work", Date = "2026-01-23", Kind = "implementation", RepositoryIds = ["repository"] }],
            };
            var reviewed = ChangeWorkdayReviewer.Review(source, activity);
            var day = reviewed.Days.Single(value => value.Date == "2026-01-23");
            Assert.Equal("unresolved-workday", day.Status);
            ChangeHistoricalRefreshManifest entries = new()
            {
                SourceSemanticDigest = reviewed.SourceSemanticDigest,
                WorkRecordInputDigest = reviewed.WorkRecordInputDigest,
                SinceInclusiveDate = "2026-01-23",
                UntilExclusiveDate = "2026-01-24",
                Entries = [new() { RecordId = "later-work", NotePermission = "allowed", EhePermission = "allowed", Restriction = "none",
                    Original = JsonSerializer.SerializeToElement(new { description = "Testing and review follow-up", ticket = "private-ticket", hours = 4 }) }],
            };
            var plan = ChangeHistoricalRefreshPlanner.Plan(reviewed, entries, "both");
            Assert.Contains("Source declared events: available", plan.Proposals[0].ProposedDescription, StringComparison.Ordinal);
            Assert.Contains("Original daily attribution is unresolved", plan.Proposals[0].ProposedDescription, StringComparison.Ordinal);
            Assert.Null(plan.Proposals[0].ProposedMultiplierContribution);
            Assert.Equal("blocked", ChangeHistoricalRefreshPreflight.Check(plan, entries).Status);
            Assert.Equal("ready-for-confirmation", ChangeHistoricalRefreshPreflight.Check(ChangeHistoricalRefreshPlanner.Plan(reviewed, entries), entries).Status);
        }

        static void AssertDeclaredProjectionKeepsReplayUncertainty(ChangePortfolioComparisonReport source, ChangeWorkRecordManifest records)
        {
            string before = ContractJson.SerializeCompact(source);
            ChangeWorkdayManifest dates = new()
            {
                SourceSemanticDigest = records.SourceSemanticDigest,
                Workdays = [new() { RecordId = records.Records[0].RecordId, Date = records.Records[0].Date }]
            };
            ChangeWorkdayReviewReport declared = ChangeDeclaredWorkdayReviewer.Review(source, records, dates,
                ChangeWorkdayPolicies.EqualDeclaredDaysV1, ChangeDeclaredWorkdayReviewPolicies.EqualEntries);
            Assert.Equal("unresolved", declared.AttributionCompleteness!.DeclaredEventStatus);
            Assert.Equal(source.AttributionCompleteness, declared.AttributionCompleteness);
            Assert.Equal("external-work-record", declared.Days[0].WorkdayEvidenceBasis);
            Assert.True(declared.Days[0].MatchedDailyMultiplier > 0);
            Assert.Equal(source.SourcePortfolio!.TotalEffort, declared.WorkdayResolution!.Allocation.TotalEffort);
            ChangeHistoricalRefreshManifest refresh = new()
            {
                SourceSemanticDigest = declared.SourceSemanticDigest,
                WorkRecordInputDigest = declared.WorkRecordInputDigest,
                SinceInclusiveDate = declared.Days[0].Date,
                UntilExclusiveDate = "2026-01-24",
                Entries = [new() { RecordId = records.Records[0].RecordId, NotePermission = "allowed",
                    EhePermission = "allowed", Restriction = "none", Original = JsonSerializer.SerializeToElement(new { description = "Original task", hours = 4 }) }]
            };
            var plan = ChangeHistoricalRefreshPlanner.Plan(declared, refresh, "both");
            Assert.Contains("Source declared events: unresolved", plan.Proposals[0].ProposedDescription, StringComparison.Ordinal);
            Assert.Equal(before, ContractJson.SerializeCompact(source));
            Assert.Empty(ContractValidation.Validate(declared));
            Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeWorkdayReviewReport, ContractJson.Serialize(declared)).IsValid);
            Assert.Contains("Source declared events: unresolved", ChangeWorkdayReviewMarkdownRenderer.Render(declared), StringComparison.Ordinal);
        }

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
