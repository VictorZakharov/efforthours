using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    private static readonly string[] AnnualContributorIds =
        ["contributor-a", "contributor-b", "contributor-c"];

    [Fact]
    public async Task AnnualComparisonHasTwelveBucketsZeroCellsBoundaryAllocationAndStableSemantics()
    {
        DateTimeOffset annualSince = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset annualUntil = annualSince.AddYears(1);
        ChangeAuthorPeriodManifest manifest = AnnualManifest(annualSince, annualUntil);
        IReadOnlyList<ChangePortfolioCandidate> candidates =
        [
            await CandidateAsync(
                "annual-a",
                annualSince,
                [Match("contributor-a", ChangePortfolioContributorMatchKind.DirectAuthor)]),
            await CandidateAsync(
                "annual-b",
                annualSince.AddMonths(1),
                [Match("contributor-b", ChangePortfolioContributorMatchKind.DirectAuthor)]),
            await CandidateAsync(
                "annual-shared",
                annualUntil.AddDays(-1),
                [
                    Match("contributor-a", ChangePortfolioContributorMatchKind.DirectAuthor),
                    Match("contributor-b", ChangePortfolioContributorMatchKind.Coauthor),
                ]),
        ];
        ChangePortfolioReport source = ChangePortfolioReconciler.Reconcile(
            Selection(manifest),
            candidates,
            EstimationProfile.Implementation);
        ChangePortfolioBucketManifest buckets = AnnualBuckets(annualSince);
        ChangePortfolioCapacityManifest capacity = AnnualCapacity(buckets);
        ChangePortfolioComparisonReport report = ChangePortfolioComparisonBuilder.Build(
            source,
            AnnualOptions(manifest, buckets, capacity));

        Assert.Empty(ContractValidation.Validate(report));
        Assert.Equal(12, report.Buckets.Count);
        ChangePortfolioComparisonSeries portfolio = Series(
            report,
            ChangePortfolioSeriesKind.Portfolio,
            "portfolio");
        ChangePortfolioComparisonSeries contributorA = Series(
            report,
            ChangePortfolioSeriesKind.ContributorExclusive,
            "contributor-contributor-a");
        ChangePortfolioComparisonSeries contributorB = Series(
            report,
            ChangePortfolioSeriesKind.ContributorExclusive,
            "contributor-contributor-b");
        ChangePortfolioComparisonSeries contributorWithoutWork = Series(
            report,
            ChangePortfolioSeriesKind.ContributorExclusive,
            "contributor-contributor-c");
        ChangePortfolioComparisonSeries shared = Assert.Single(
            report.Series,
            series => series.Kind == ChangePortfolioSeriesKind.SharedContributors);

        Assert.Equal(source.TotalEffort, portfolio.TotalEffort);
        Assert.Equal(1, contributorA.Points[0].SelectedChangeCount);
        Assert.Equal(0, contributorB.Points[0].SelectedChangeCount);
        Assert.Equal(1, contributorB.Points[1].SelectedChangeCount);
        Assert.Equal(0, contributorA.Points[1].SelectedChangeCount);
        Assert.All(contributorWithoutWork.Points, point =>
        {
            Assert.Equal(0, point.SelectedChangeCount);
            Assert.Equal(0m, point.Effort.Expected);
        });
        Assert.Equal(1, shared.Points[^1].SelectedChangeCount);
        Assert.Equal(3, portfolio.Points.Sum(point => point.SelectedChangeCount));
        Assert.Equal(5760m, portfolio.TotalCapacityHours);
        Assert.Equal(
            portfolio.TotalEffort,
            Sum(report.Series.Where(series => series.AdditiveToPortfolio)
                .Select(series => series.TotalEffort)));

        ChangeAuthorPeriodManifest reorderedManifest = manifest with
        {
            Contributors = [.. manifest.Contributors.Reverse().Select(contributor => contributor with
            {
                Aliases = [.. contributor.Aliases.Reverse()],
            })],
            Repositories = [.. manifest.Repositories.Reverse().Select(repository => repository with
            {
                Heads = [.. repository.Heads.Reverse()],
            })],
        };
        ChangePortfolioReport reorderedSource = ChangePortfolioReconciler.Reconcile(
            Selection(reorderedManifest),
            [.. candidates.Reverse()],
            EstimationProfile.Implementation);
        ChangePortfolioComparisonReport reordered = ChangePortfolioComparisonBuilder.Build(
            reorderedSource,
            AnnualOptions(
                reorderedManifest,
                buckets with { Buckets = [.. buckets.Buckets.Reverse()] },
                capacity with { Entries = [.. capacity.Entries.Reverse()] }));
        Assert.Equal(
            report.Verification.SourcePortfolioDigest,
            reordered.Verification.SourcePortfolioDigest);
        Assert.Equal(
            report.Verification.SemanticDigest,
            reordered.Verification.SemanticDigest);
    }

    private static ChangePortfolioComparisonSeries Series(
        ChangePortfolioComparisonReport report,
        ChangePortfolioSeriesKind kind,
        string id) => Assert.Single(
            report.Series,
            series => series.Kind == kind && series.Id == id);

    private static ChangeAuthorPeriodManifest AnnualManifest(
        DateTimeOffset since,
        DateTimeOffset until) => new()
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
                Contributor("contributor-a", "a@example.test", "Person A"),
                Contributor("contributor-b", "b@example.test", "Person B"),
                Contributor("contributor-c", "c@example.test", "Person C"),
            ],
            Repositories =
            [
                Repository(
                    "repository-a",
                    "private-repository-a",
                    ("default", new string('a', 40)),
                    ("open-change", new string('b', 40))),
                Repository(
                    "repository-b",
                    "private-repository-b",
                    ("default", new string('c', 40))),
            ],
        };

    private static ChangeAuthorPeriodManifestContributor Contributor(
        string id,
        params string[] aliases) => new()
        {
            Id = id,
            Aliases = aliases,
        };

    private static ChangeAuthorPeriodManifestRepository Repository(
        string id,
        string path,
        params (string Id, string ObjectId)[] heads) => new()
        {
            Id = id,
            RepositoryPath = path,
            Heads = [.. heads.Select(head => new ChangeAuthorPeriodManifestHead
            {
                Id = head.Id,
                ObjectId = head.ObjectId,
            })],
        };

    private static ChangePortfolioBucketManifest AnnualBuckets(DateTimeOffset since) => new()
    {
        Buckets = [.. Enumerable.Range(0, 12).Select(index =>
            Bucket(
                since.AddMonths(index).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
                since.AddMonths(index).ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture),
                since.AddMonths(index),
                since.AddMonths(index + 1)))],
    };

    private static ChangePortfolioCapacityManifest AnnualCapacity(
        ChangePortfolioBucketManifest buckets) => new()
        {
            CalendarPolicy = "Synthetic fixed reference capacity for twelve complete UTC months.",
            Entries = [.. buckets.Buckets.SelectMany(bucket =>
                AnnualContributorIds.Select(
                    contributor => new ChangePortfolioCapacityEntry
                    {
                        BucketId = bucket.Id,
                        ContributorId = contributor,
                        Hours = 160m,
                    }))],
        };

    private static ChangePortfolioComparisonBuildOptions AnnualOptions(
        ChangeAuthorPeriodManifest manifest,
        ChangePortfolioBucketManifest buckets,
        ChangePortfolioCapacityManifest capacity) => new()
        {
            View = ChangePortfolioComparisonView.Trend,
            Title = "Annual synthetic comparison",
            GeneratedAt = new DateTimeOffset(2027, 1, 2, 0, 0, 0, TimeSpan.Zero),
            CliVersion = "0.10.0-test",
            Profile = EstimationProfile.Implementation,
            BucketKind = ChangePortfolioBucketPolicyKind.CalendarMonth,
            BucketPolicy = ChangePortfolioComparisonPolicies.CalendarMonthV1,
            BucketManifest = buckets,
            Buckets = [.. buckets.Buckets.OrderBy(bucket => bucket.SinceInclusive).Select(bucket =>
                new ChangePortfolioComparisonBucket
                {
                    Id = bucket.Id,
                    Label = bucket.Label,
                    SinceInclusive = bucket.SinceInclusive,
                    UntilExclusive = bucket.UntilExclusive,
                })],
            CapacityManifest = capacity,
            SourceManifest = manifest,
            ExecutionOverride = AnnualExecution(manifest),
        };

    private static ChangePortfolioComparisonExecution AnnualExecution(
        ChangeAuthorPeriodManifest manifest) => new()
        {
            RuntimeFramework = "test-runtime/1.0",
            OperatingSystemFamily = "other",
            ProcessArchitecture = "test-architecture",
            LogicalProcessorCount = 8,
            ShardPolicy = ChangePortfolioComparisonPolicies.RepositoryEvidenceShardsV1,
            Checkpoint = new ChangePortfolioComparisonCheckpoint { Enabled = false },
            RepositoryShardCount = manifest.Repositories.Count,
            Repositories = [.. manifest.Repositories.OrderBy(repository => repository.Id, StringComparer.Ordinal)
                .Select(repository => new ChangePortfolioComparisonRepositoryExecution
                {
                    RepositoryId = repository.Id,
                    Status = ChangePortfolioRepositoryExecutionStatus.Complete,
                    CheckpointDisposition = ChangePortfolioCheckpointDisposition.Disabled,
                    SelectedChangeCount = repository.Id == "repository-a" ? 3 : 0,
                    ElapsedMilliseconds = repository.Id == "repository-a" ? 90m : 10m,
                    InputDigest = ChangePortfolioComparisonIdentity.ComputeRepositoryInputDigest(
                        manifest,
                        repository.Id,
                        EstimationProfile.Implementation,
                        ChangeEstimator.Version),
                })],
            Reuse = new ChangePortfolioComparisonReuse(),
        };
}
