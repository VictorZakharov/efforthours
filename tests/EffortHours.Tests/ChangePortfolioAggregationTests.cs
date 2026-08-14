using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed class ChangePortfolioAggregationTests
{
    private const string ProjectFile =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework>" +
        "</PropertyGroup></Project>\n";

    [Fact]
    public async Task MatchSetGroupsKeepSharedEffortOnceAndPreserveZeroRows()
    {
        ChangeState initial = State(
            ("Demo.csproj", ProjectFile),
            ("Widget.cs", "namespace Demo; public sealed class Widget { public int Value => 0; }\n"));
        ChangeEstimateReport contributorA = await ReportAsync(
            initial,
            State(
                ("Demo.csproj", ProjectFile),
                ("Widget.cs", "namespace Demo; public sealed class Widget { public int Value => 1; }\n")));
        ChangeEstimateReport contributorB = await ReportAsync(
            initial,
            State(
                ("Demo.csproj", ProjectFile),
                ("Widget.cs", "namespace Demo; public sealed class Widget { public int Value => 2; }\n")));
        ChangeEstimateReport shared = await ReportAsync(
            initial,
            State(
                ("Demo.csproj", ProjectFile),
                ("Widget.cs", "namespace Demo; public sealed class Widget { public int Value => 0; }\n"),
                ("Shared.cs", "namespace Demo; public sealed class Shared { public bool Enabled => true; }\n")));
        ChangePortfolioSelection selection = Selection(
            contributorB.Selection.Head.ObjectId,
            contributorA.Selection.Head.ObjectId,
            initial.ObjectId);
        DateTimeOffset selectedAt = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        ChangePortfolioCandidate[] candidates =
        [
            Candidate(
                "commit:a",
                contributorA,
                selectedAt,
                [Match("contributor-a", ChangePortfolioContributorMatchKind.DirectAuthor)],
                ["open"]),
            Candidate(
                "commit:b",
                contributorB,
                selectedAt.AddMinutes(1),
                [Match("contributor-b", ChangePortfolioContributorMatchKind.DirectAuthor)],
                ["default"]),
            Candidate(
                "commit:shared",
                shared,
                selectedAt.AddMinutes(2),
                [
                    Match("contributor-a", ChangePortfolioContributorMatchKind.DirectAuthor),
                    Match("contributor-b", ChangePortfolioContributorMatchKind.Coauthor),
                ],
                ["default", "open"]),
        ];

        ChangePortfolioReport report = Reconcile(selection, candidates);
        ChangePortfolioReport reordered = Reconcile(selection, [.. candidates.Reverse()]);
        ChangePortfolioAggregation aggregation = Assert.IsType<ChangePortfolioAggregation>(report.Aggregation);

        Assert.Equal(
            ContractJson.SerializeCompact(report),
            ContractJson.SerializeCompact(reordered));
        Assert.Equal(report.TotalEffort, Sum(aggregation.ContributorGroups.Select(group => group.NormalizedEffort)));
        Assert.Equal(report.IsolatedEffort, Sum(aggregation.ContributorGroups.Select(group => group.IsolatedEffort)));
        Assert.Equal(report.Items.Count, aggregation.ContributorGroups.Sum(group => group.SelectedCommitCount));

        ChangePortfolioContributorGroup sharedGroup = Assert.Single(
            aggregation.ContributorGroups,
            group => group.Kind == ChangePortfolioContributorGroupKind.SharedContributors);
        Assert.Equal(["contributor-a", "contributor-b"], sharedGroup.ContributorIds);
        Assert.Equal(1, sharedGroup.SelectedCommitCount);
        Assert.Equal(1, sharedGroup.DirectAuthorMatchCount);
        Assert.Equal(1, sharedGroup.CoauthorMatchCount);
        Assert.Single(sharedGroup.ItemIds);

        ChangePortfolioContributorSummary contributorAView = Assert.Single(
            aggregation.Contributors,
            contributor => contributor.ContributorId == "contributor-a");
        Assert.Equal(2, contributorAView.SelectedCommitCount);
        Assert.Equal(1, contributorAView.SingleContributorSelectedCommitCount);
        Assert.Equal(1, contributorAView.SharedContributorSelectedCommitCount);
        ChangePortfolioContributorSummary contributorBView = Assert.Single(
            aggregation.Contributors,
            contributor => contributor.ContributorId == "contributor-b");
        Assert.Equal(1, contributorBView.DirectAuthorMatchCount);
        Assert.Equal(1, contributorBView.CoauthorMatchCount);

        ChangePortfolioContributorSummary emptyContributor = Assert.Single(
            aggregation.Contributors,
            contributor => contributor.ContributorId == "contributor-c");
        Assert.True(emptyContributor.NoSelectedCommits);
        Assert.Equal(0, emptyContributor.SelectedCommitCount);
        ChangePortfolioContributorGroup emptyContributorGroup = Assert.Single(
            aggregation.ContributorGroups,
            group => group.Id == emptyContributor.SingleContributorGroupId);
        Assert.Equal(Zero(), emptyContributorGroup.NormalizedEffort);
        Assert.Empty(emptyContributorGroup.ItemIds);

        ChangePortfolioRepositorySummary repositoryA = Assert.Single(
            aggregation.Repositories,
            repository => repository.RepositoryId == "repository-a");
        Assert.Equal(repositoryA.NormalizedEffort, Sum(repositoryA.HeadGroups.Select(group => group.NormalizedEffort)));
        Assert.Equal(repositoryA.IsolatedEffort, Sum(repositoryA.HeadGroups.Select(group => group.IsolatedEffort)));
        Assert.Equal(1, repositoryA.SharedContributorSelectedCommitCount);
        Assert.Equal(1, repositoryA.SharedHeadSelectedCommitCount);
        Assert.All(repositoryA.Heads, head =>
        {
            Assert.Equal(2, head.ReachableSelectedCommitCount);
            Assert.Equal(1, head.UniqueSelectedCommitCount);
            Assert.Equal(1, head.SharedSelectedCommitCount);
            Assert.False(head.NoUniqueSelectedCommits);
        });
        Assert.Contains(repositoryA.HeadGroups, group =>
            group.Kind == ChangePortfolioHeadGroupKind.SharedHeads &&
            group.HeadIds.SequenceEqual(["default", "open"], StringComparer.Ordinal));

        ChangePortfolioRepositorySummary emptyRepository = Assert.Single(
            aggregation.Repositories,
            repository => repository.RepositoryId == "repository-b");
        Assert.True(emptyRepository.NoSelectedCommits);
        Assert.Equal(Zero(), emptyRepository.NormalizedEffort);
        ChangePortfolioHeadSummary emptyHead = Assert.Single(emptyRepository.Heads);
        Assert.True(emptyHead.NoUniqueSelectedCommits);
        Assert.Equal(0, emptyHead.ReachableSelectedCommitCount);
        Assert.Equal(Zero(), Assert.Single(emptyRepository.HeadGroups).NormalizedEffort);

        Assert.NotEmpty(report.Adjustments);
        Assert.Contains(aggregation.ContributorGroups
            .SelectMany(group => group.RepositoryAllocations), allocation =>
                allocation.AdjustmentIds.Count > 0);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "FB5323");
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "FB5324");
    }

    [Fact]
    public async Task FullCompactAndSavedMarkdownReportsAgreeOnAggregateLedger()
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeEstimateReport source = await ReportAsync(
            initial,
            State(
                ("Demo.csproj", ProjectFile),
                ("Feature.cs", "namespace Demo; public sealed class Feature { }\n")));
        ChangePortfolioSelection selection = Selection(
            source.Selection.Head.ObjectId,
            initial.ObjectId,
            new string('f', 64));
        ChangePortfolioReport report = Reconcile(
            selection,
            [
                Candidate(
                    "commit:feature",
                    source,
                    new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero),
                    [Match("contributor-a", ChangePortfolioContributorMatchKind.DirectAuthor)],
                    ["default"]),
            ]);

        string full = new ChangePortfolioJsonRenderer().Render(report);
        string compact = new ChangePortfolioJsonRenderer(compact: true).Render(report);
        ChangePortfolioReport saved = ContractJson.Deserialize<ChangePortfolioReport>(full);
        string markdown = ChangePortfolioMarkdownRenderer.Render(saved);
        ChangePortfolioReport legacy = report with
        {
            EstimatorVersion = "change-portfolio/0.1.0+change-seed/0.18.1+seed-rules/0.4.0",
            Aggregation = null,
        };
        SchemaValidationResult missingCurrentAggregation = ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioReport,
            ContractJson.SerializeCompact(report with { Aggregation = null }));
        SchemaValidationResult legacySchema = ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioReport,
            ContractJson.SerializeCompact(legacy));

        Assert.Equal(
            ContractJson.SerializeCompact(ContractJson.Deserialize<ChangePortfolioReport>(full)),
            ContractJson.SerializeCompact(ContractJson.Deserialize<ChangePortfolioReport>(compact)));
        Assert.Contains("Contributor match-set allocation", markdown, StringComparison.Ordinal);
        Assert.Contains("Head-reachability allocation", markdown, StringComparison.Ordinal);
        Assert.Contains("contributor-c", markdown, StringComparison.Ordinal);
        Assert.Contains("repository-b", markdown, StringComparison.Ordinal);
        Assert.Contains("Zero row", markdown, StringComparison.Ordinal);
        Assert.False(missingCurrentAggregation.IsValid);
        Assert.True(legacySchema.IsValid, string.Join(Environment.NewLine, legacySchema.Errors));
        Assert.Empty(ContractValidation.Validate(legacy));
        Assert.DoesNotContain("\"aggregation\"", ContractJson.SerializeCompact(legacy), StringComparison.Ordinal);
        Assert.DoesNotContain("private@example.invalid", full, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\private", full, StringComparison.OrdinalIgnoreCase);
    }

    private static ChangePortfolioReport Reconcile(
        ChangePortfolioSelection selection,
        IReadOnlyList<ChangePortfolioCandidate> candidates) => ChangePortfolioReconciler.Reconcile(
            selection,
            candidates,
            EstimationProfile.Implementation);

    private static ChangePortfolioCandidate Candidate(
        string selectorId,
        ChangeEstimateReport report,
        DateTimeOffset selectedAt,
        IReadOnlyList<ChangePortfolioContributorMatch> matches,
        IReadOnlyList<string> headIds) => new()
        {
            RepositoryId = "repository-a",
            SelectorId = selectorId,
            Report = report,
            Attribution = new ChangePortfolioAttribution
            {
                Kind = matches.Any(match => match.Kind == ChangePortfolioContributorMatchKind.DirectAuthor)
                    ? ChangePortfolioAttributionKind.DirectAuthor
                    : ChangePortfolioAttributionKind.Coauthor,
                SelectedTimestamp = selectedAt,
                MergeCommit = false,
                ParentCount = 1,
                ContributorMatches = matches,
                HeadIds = headIds,
                AmbiguityReasons = matches.Count > 1
                    ? ["Multiple requested contributors match this immutable commit; it remains one portfolio item."]
                    : [],
            },
        };

    private static ChangePortfolioContributorMatch Match(
        string contributorId,
        ChangePortfolioContributorMatchKind kind) => new()
        {
            ContributorId = contributorId,
            Kind = kind,
        };

    private static ChangePortfolioSelection Selection(
        string defaultHead,
        string openHead,
        string emptyRepositoryHead) => new()
        {
            Kind = ChangePortfolioSelectionKind.AuthorPeriod,
            ManifestBased = true,
            AuthorPeriodManifest = new ChangePortfolioAuthorPeriodManifestSelection
            {
                ManifestDigest = "sha256:" + new string('a', 64),
                SinceInclusive = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
                UntilExclusive = new DateTimeOffset(2026, 8, 8, 0, 0, 0, TimeSpan.Zero),
                TimeZone = "UTC",
                DateField = ChangePortfolioDateField.Author,
                MergePolicy = ChangePortfolioMergePolicy.Exclude,
                CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include,
                ContributorIds = ["contributor-a", "contributor-b", "contributor-c"],
                Repositories =
                [
                    new ChangePortfolioAuthorPeriodManifestRepository
                    {
                        Id = "repository-a",
                        Heads =
                        [
                            new ChangePortfolioAuthorPeriodManifestHead
                            {
                                Id = "default",
                                ObjectId = defaultHead,
                            },
                            new ChangePortfolioAuthorPeriodManifestHead
                            {
                                Id = "open",
                                ObjectId = openHead,
                            },
                        ],
                    },
                    new ChangePortfolioAuthorPeriodManifestRepository
                    {
                        Id = "repository-b",
                        Heads =
                        [
                            new ChangePortfolioAuthorPeriodManifestHead
                            {
                                Id = "default",
                                ObjectId = emptyRepositoryHead,
                            },
                        ],
                    },
                ],
            },
        };

    private static async Task<ChangeEstimateReport> ReportAsync(
        ChangeState before,
        ChangeState after) => await new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "portfolio-repository",
                Selection = new ChangeSelection
                {
                    Kind = ChangeSelectionKind.Commit,
                    Base = Reference("base", before.ObjectId),
                    Head = Reference("head", after.ObjectId),
                    Commit = after.ObjectId,
                    Parent = before.ObjectId,
                },
                OpenBaseAsync = before.OpenAsync,
                OpenHeadAsync = after.OpenAsync,
            },
            EstimationProfile.Implementation);

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.GitTree,
    };

    private static ChangeState State(params (string Path, string Content)[] files)
    {
        InMemoryChangeSnapshot snapshot = new(files);
        return new ChangeState(snapshot.ObjectId, InMemoryChangeSnapshot.Factory(files));
    }

    private static EffortRange Sum(IEnumerable<EffortRange> ranges) => ContractValidation.Sum(ranges);

    private static EffortRange Zero() => new() { Low = 0m, Expected = 0m, High = 0m };

    private sealed record ChangeState(
        string ObjectId,
        Func<CancellationToken, Task<IChangeSnapshot>> OpenAsync);
}
