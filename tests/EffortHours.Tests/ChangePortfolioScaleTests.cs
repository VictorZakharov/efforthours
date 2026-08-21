using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioReconcilerTests
{
    [Fact]
    public async Task ClosedMonthScalePortfolioReconcilesMoreThanFormerRowLimit()
    {
        const int selectedChanges = 129;
        List<ChangePortfolioCandidate> candidates = [];
        DateTimeOffset firstTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        string? headObjectId = null;
        for (int index = 0; index < selectedChanges; index++)
        {
            ChangeState before = State(
                ("Demo.csproj", ProjectFile),
                ("Shared.cs", $"namespace Demo; public sealed class Shared {{ public int Value => {index}; }}\n"));
            ChangeState after = State(
                ("Demo.csproj", ProjectFile),
                ("Shared.cs", $"namespace Demo; public sealed class Shared {{ public int Value => {index + 1}; }}\n"));
            ChangeEstimateReport isolated = await ReportAsync(
                ChangeSelectionKind.Commit,
                index,
                before,
                after);
            headObjectId = after.ObjectId;
            candidates.Add(Candidate(
                "repository-a",
                $"commit-{index:D3}",
                isolated,
                firstTimestamp.AddHours(index)));
        }

        ChangePortfolioReport report = Reconcile(AuthorPeriod(headObjectId!), candidates);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioReport,
            ContractJson.Serialize(report));

        Assert.Equal(selectedChanges, report.Items.Count);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(report));
        Assert.Equal(
            report.TotalEffort.Expected,
            report.Items.Sum(item => item.AllocatedExpectedHours));
    }

    [Fact]
    public async Task MoreThanTenThousandChangesReconcileAndRenderOneDeterministicSummary()
    {
        const int selectedChanges = 10_001;
        DateTimeOffset since = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset until = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
        ChangeState before = State(("Demo.csproj", ProjectFile));
        ChangeState after = State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", "namespace Demo; public sealed class Feature { }\n"));
        ChangeEstimateReport isolated = await ReportAsync(
            ChangeSelectionKind.Commit,
            1,
            before,
            after);
        ChangePortfolioCandidate[] candidates = [.. Enumerable.Range(0, selectedChanges).Select(index =>
            Candidate(
                "repository-a",
                $"commit-{index:D4}",
                isolated,
                new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(index)) with
            {
                Attribution = new ChangePortfolioAttribution
                {
                    Kind = ChangePortfolioAttributionKind.DirectAuthor,
                    SelectedTimestamp = new DateTimeOffset(
                        2026,
                        7,
                        1,
                        0,
                        0,
                        0,
                        TimeSpan.Zero).AddMinutes(index),
                    MergeCommit = false,
                    ParentCount = 1,
                    ContributorMatches =
                    [
                        new ChangePortfolioContributorMatch
                        {
                            ContributorId = "contributor-a",
                            Kind = ChangePortfolioContributorMatchKind.DirectAuthor,
                        },
                    ],
                    HeadIds = ["default"],
                },
            })];

        ChangePortfolioSelection selection = ManifestAuthorPeriod(after.ObjectId);
        ChangePortfolioReport report = Reconcile(selection, candidates);
        ChangePortfolioReport reordered = Reconcile(
            selection,
            [.. candidates.Reverse()]);
        ChangeAuthorPeriodManifest manifest = new()
        {
            Selection = new ChangeAuthorPeriodManifestSelection
            {
                SinceInclusive = since,
                UntilExclusive = until,
                TimeZone = "UTC",
                DateField = ChangePortfolioDateField.Author,
                MergePolicy = ChangePortfolioMergePolicy.Exclude,
                CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include,
            },
            Contributors =
            [
                new ChangeAuthorPeriodManifestContributor
                {
                    Id = "contributor-a",
                    Aliases = ["private@example.test"],
                },
            ],
            Repositories =
            [
                new ChangeAuthorPeriodManifestRepository
                {
                    Id = "repository-a",
                    RepositoryPath = "private-repository-path",
                    Heads =
                    [
                        new ChangeAuthorPeriodManifestHead
                        {
                            Id = "default",
                            ObjectId = after.ObjectId,
                        },
                    ],
                },
            ],
        };
        ChangePortfolioBucketManifest bucketManifest = new()
        {
            Buckets =
            [
                new ChangePortfolioBucketDefinition
                {
                    Id = "full-period",
                    Label = "Full period",
                    SinceInclusive = since,
                    UntilExclusive = until,
                },
            ],
        };
        ChangePortfolioComparisonBuildOptions options = new()
        {
            View = ChangePortfolioComparisonView.Trend,
            Title = "High-volume author-period summary",
            GeneratedAt = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero),
            CliVersion = "0.10.0-test",
            Profile = EstimationProfile.Implementation,
            BucketKind = ChangePortfolioBucketPolicyKind.Custom,
            BucketPolicy = ChangePortfolioComparisonPolicies.CustomClosedBucketsV1,
            BucketManifest = bucketManifest,
            Buckets =
            [
                new ChangePortfolioComparisonBucket
                {
                    Id = "full-period",
                    Label = "Full period",
                    SinceInclusive = since,
                    UntilExclusive = until,
                },
            ],
            SourceManifest = manifest,
        };
        ChangePortfolioComparisonReport summary =
            ChangePortfolioComparisonBuilder.Build(report, options);
        string markdown = ChangePortfolioComparisonMarkdownRenderer.Render(summary);

        Assert.Equal(selectedChanges, report.Items.Count);
        Assert.Empty(ContractValidation.Validate(report));
        Assert.Equal(report.TotalEffort, reordered.TotalEffort);
        Assert.Equal(
            ChangePortfolioComparisonIdentity.ComputePortfolioDigest(report),
            ChangePortfolioComparisonIdentity.ComputePortfolioDigest(reordered));
        ChangePortfolioComparisonSeries portfolio = Assert.Single(
            summary.Series,
            series => series.Kind == ChangePortfolioSeriesKind.Portfolio);
        Assert.Equal(selectedChanges, Assert.Single(portfolio.Points).SelectedChangeCount);
        Assert.Equal(report.TotalEffort, portfolio.TotalEffort);
        Assert.Equal(
            summary.Verification.SemanticDigest,
            ChangePortfolioComparisonIdentity.ComputeSemanticDigest(
                reordered,
                summary.BucketPolicy,
                summary.Buckets,
                summary.Series));
        Assert.Contains(
            $"Selected immutable changes after repository-scoped deduplication: {selectedChanges}",
            markdown,
            StringComparison.Ordinal);
        Assert.DoesNotContain("commit-0000", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("private@example.test", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-repository-path", markdown, StringComparison.OrdinalIgnoreCase);
    }
}
