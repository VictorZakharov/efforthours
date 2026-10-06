using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioReconcilerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(999)]
    public async Task ReplayAllocationConservesBudgetShowsStandaloneCapAndKeepsPeriodPartitions(int novelHours)
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeState original = State(("Demo.csproj", ProjectFile), ("A.cs", "public class A { public int Read() => 1; }"));
        ChangeState upstream = State(("Demo.csproj", ProjectFile), ("Up.cs", "public class Up { }"));
        ChangeState replay = State(("Demo.csproj", ProjectFile), ("Up.cs", "public class Up { }"), ("A.cs", "public class A { public int Read() => 1; }"));
        ChangeState retained = State(("Demo.csproj", ProjectFile), ("Up.cs", "public class Up { }"), ("A.cs", "public class A { public int Read() => 1; }"), ("B.cs", "public class B { public int Sum(int a, int b) => a + b; }"));
        ChangeEstimateReport originalReport = await ReportAsync(ChangeSelectionKind.Commit, 0, initial, original);
        ChangeEstimateReport retainedReport = await ReportAsync(ChangeSelectionKind.Commit, 0, upstream, retained);
        DateTimeOffset day = new(2026, 1, 19, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset eventDate = day.AddDays(3);
        ChangePortfolioSelection selection = ManifestAuthorPeriod(retained.ObjectId);
        ChangePortfolioReplayEvent declaration = new()
        {
            Id = "event",
            OldBaseObjectId = initial.ObjectId,
            OriginalObjectId = original.ObjectId,
            NewBaseObjectId = upstream.ObjectId,
            ReplayObjectId = replay.ObjectId,
            RewrittenObjectId = retained.ObjectId,
            ReplayProvenanceId = "reviewed-replay",
            EventTimestamp = eventDate,
            EventProvenanceId = "workday-record",
        };
        List<ChangeRewriteComparison> comparisons = [];
        await Compare("original-implementation", initial, original);
        await Compare("inherited-upstream", initial, upstream);
        await Compare("retained-feature", upstream, retained);
        await Compare("replayed-implementation", upstream, replay);
        await Compare("novel-retained-delta", replay, retained);
        comparisons[^1] = comparisons[^1] with { Effort = new EffortRange { Low = novelHours, Expected = novelHours, High = novelHours } };
        ChangeRewriteReviewReport review = new()
        {
            RepositoryId = "repository-a",
            InputDigest = "sha256:" + new string('a', 64),
            Status = "complete-evidence-review",
            SinceInclusive = selection.AuthorPeriodManifest!.SinceInclusive,
            UntilExclusive = selection.AuthorPeriodManifest.UntilExclusive,
            OriginalAuthorTimestamp = day,
            RewrittenCommitterTimestamp = eventDate.AddHours(1),
            EventTimestamp = eventDate,
            ReplayProvenanceId = declaration.ReplayProvenanceId,
            EventProvenanceId = declaration.EventProvenanceId,
            ReplayConfidence = "exact-path-replay-verified",
            ReplayProof = new() { ExactReplayPathCount = 1, InheritedUpstreamPathCount = 1 },
            EventAttributionStatus = "attributed-under-declared-replay",
            EventAttributedNovelEffort = comparisons[^1].Effort,
            OriginalCommitCount = 1,
            RewrittenCommitCount = 1,
            Comparisons = comparisons,
        };
        ChangePortfolioReplayEvidence evidence = new()
        { Event = declaration, Review = review, OriginalObjectIds = [original.ObjectId], RetainedObjectIds = [retained.ObjectId] };
        ChangePortfolioCandidate[] candidates = [Row("original", originalReport, day), Row("retained", retainedReport, day.AddDays(1))];
        candidates[0] = candidates[0] with { ReplayEvidence = evidence };
        ChangePortfolioReport full = Reconcile(selection, candidates);
        ChangePortfolioReport ordinary = Reconcile(selection, [.. candidates.Select(row => row with
        { ReplayEvidence = null, Attribution = row.Attribution with { Replay = null } })]);
        Assert.Equal(ordinary.TotalEffort, full.TotalEffort);
        ChangePortfolioReplayAllocation allocation = Assert.Single(full.ReplayAllocations!);
        Assert.Equal(novelHours, allocation.StandaloneNovelExpectedHours);
        Assert.Equal(Math.Min(novelHours, full.TotalEffort.Expected), allocation.AllocatedEventExpectedHours);
        Assert.Equal(novelHours > full.TotalEffort.Expected, allocation.AllocationCapped);
        Assert.Equal(full.TotalEffort.Expected, full.Items.Sum(item => item.AllocatedExpectedHours));
        Assert.Equal(ContractJson.SerializeCompact(full), ContractJson.SerializeCompact(Reconcile(selection, [.. candidates.Reverse()])));
        SchemaValidationResult schema = ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioReport, ContractJson.Serialize(full));
        Assert.True(schema.IsValid, string.Join(" ", schema.Errors));
        Assert.Contains("Standalone novel", ChangePortfolioMarkdownRenderer.Render(full), StringComparison.Ordinal);
        DateTimeOffset split = new(eventDate.Year, eventDate.Month, eventDate.Day, 0, 0, 0, TimeSpan.Zero);
        ChangePortfolioReport earlier = Partition(selection.AuthorPeriodManifest.SinceInclusive, split);
        ChangePortfolioReport later = Partition(split, selection.AuthorPeriodManifest.UntilExclusive);
        Assert.Equal(full.TotalEffort.Expected, earlier.TotalEffort.Expected + later.TotalEffort.Expected);
        Assert.Equal(allocation.AllocatedEventExpectedHours, later.TotalEffort.Expected);
        Assert.Equal(0m, earlier.ReplayAllocations![0].AllocatedEventExpectedHours);
        Assert.NotEmpty(ContractValidation.Validate(full with { ReplayAllocations = [allocation with { AllocatedEventExpectedHours = 9999m }] }));
        Assert.NotEmpty(ContractValidation.Validate(full with { ReplayAllocations = null }));
        Assert.NotEmpty(ContractValidation.Validate(full with { ReplayAllocations = [allocation with { AvailableJointExpectedHours = allocation.AvailableJointExpectedHours + 1m }] }));

        ChangePortfolioReplayEvent unknown = declaration with { EventTimestamp = null, EventProvenanceId = null };
        ChangePortfolioReplayEvidence unknownEvidence = evidence with
        {
            Event = unknown,
            Review = review with
            {
                EventTimestamp = null,
                EventProvenanceId = null,
                EventAttributionStatus = "unresolved-event-date",
                EventAttributedNovelEffort = null
            }
        };
        ChangePortfolioReport unresolved = Reconcile(selection, [.. candidates.Select(row => row with
        {
            ReplayEvidence = row.ReplayEvidence is null ? null : unknownEvidence,
            Attribution = row.Attribution with { SelectedTimestamp = row.Attribution.Replay!.OriginalSelectedTimestamp },
        })]);
        Assert.Null(unresolved.ReplayAllocations![0].AllocatedEventExpectedHours);
        Assert.Equal("unresolved-event-date", unresolved.ReplayAllocations[0].Status);
        Assert.Equal(ordinary.TotalEffort, unresolved.TotalEffort);

        ChangePortfolioReport Partition(DateTimeOffset since, DateTimeOffset until)
        {
            bool inside = eventDate >= since && eventDate < until;
            ChangePortfolioSelection subset = selection with { AuthorPeriodManifest = selection.AuthorPeriodManifest with { SinceInclusive = since, UntilExclusive = until } };
            ChangePortfolioReplayEvidence proof = evidence with
            {
                Review = review with
                {
                    SinceInclusive = since,
                    UntilExclusive = until,
                    EventAttributionStatus = inside ? "attributed-under-declared-replay" : "event-outside-period",
                    EventAttributedNovelEffort = inside ? comparisons[^1].Effort : new EffortRange { Low = 0m, Expected = 0m, High = 0m }
                }
            };
            return Reconcile(subset, [.. candidates.Select(row =>
            {
                DateTimeOffset date = row.Attribution.Replay!.Role == "original" ? day : eventDate;
                bool support = date < since || date >= until;
                return row with { ReplayEvidence = row.ReplayEvidence is null ? null : proof,
                    Attribution = row.Attribution with { SelectedTimestamp = support ? since : date,
                        Replay = row.Attribution.Replay with { SupportOnly = support } } };
            })]);
        }
        ChangePortfolioCandidate Row(string role, ChangeEstimateReport report, DateTimeOffset date) =>
            Candidate("repository-a", role, report, role == "original" ? date : eventDate) with
            {
                Attribution = new()
                {
                    Kind = ChangePortfolioAttributionKind.DirectAuthor,
                    SelectedTimestamp = role == "original" ? date : eventDate,
                    ParentCount = 1,
                    HeadIds = ["default"],
                    ContributorMatches = [new() { ContributorId = "contributor-a", Kind = ChangePortfolioContributorMatchKind.DirectAuthor }],
                    Replay = new() { EventId = "event", Role = role, OriginalSelectedTimestamp = date }
                },
            };
        async Task Compare(string role, ChangeState before, ChangeState after)
        {
            ChangeEstimateReport report = await ReportAsync(ChangeSelectionKind.BaseHead, 0, before, after);
            comparisons.Add(new()
            {
                Role = role,
                Selection = report.Selection,
                Profile = report.Profile,
                EstimatorVersion = report.EstimatorVersion,
                Effort = report.TotalEffort,
                SourceReportDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(ContractJson.SerializeCompact(report)),
                RepresentedPathCount = report.Evidence.Paths.Count
            });
        }
    }
}
