using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Fact]
    public async Task CalendarMonthUsesTimezoneBoundariesAcrossDaylightSaving()
    {
        ChangePortfolioAuthorPeriodManifestSelection selection = new()
        {
            ManifestDigest = "sha256:" + new string('a', 64),
            SinceInclusive = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.FromHours(-5)),
            UntilExclusive = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.FromHours(-4)),
            TimeZone = "America/Toronto",
            DateField = ChangePortfolioDateField.Author,
            MergePolicy = ChangePortfolioMergePolicy.Exclude,
            CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include,
            ContributorIds = ["contributor-a"],
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
                            ObjectId = new string('a', 40),
                        },
                    ],
                },
            ],
        };

        ChangePortfolioComparisonInputs inputs =
            await ChangePortfolioComparisonInputLoader.LoadAsync(
                new ChangePortfolioCommandOptions { Bucket = "calendar-month" },
                selection,
                CancellationToken.None);

        ChangePortfolioComparisonBucket bucket = Assert.Single(inputs.Buckets);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 5, 0, 0, TimeSpan.Zero), bucket.SinceInclusive);
        Assert.Equal(new DateTimeOffset(2026, 4, 1, 4, 0, 0, TimeSpan.Zero), bucket.UntilExclusive);
        Assert.Equal(TimeSpan.FromHours(743), bucket.UntilExclusive - bucket.SinceInclusive);
        Assert.False(bucket.PartialStart);
        Assert.False(bucket.PartialEnd);
    }

    [Fact]
    public async Task CompleteLogicalReportSupportsMaximumRepositoryEvidenceShards()
    {
        ChangeAuthorPeriodManifest source = Manifest();
        ChangeAuthorPeriodManifest manifest = source with
        {
            Repositories =
            [
                source.Repositories[0],
                .. Enumerable.Range(
                    1,
                    ChangeAuthorPeriodManifestLimits.MaximumRepositories - 1).Select(index =>
                    new ChangeAuthorPeriodManifestRepository
                    {
                        Id = $"repository-{index:000}",
                        RepositoryPath = $"private-repository-{index:000}",
                        Heads =
                        [
                            new ChangeAuthorPeriodManifestHead
                            {
                                Id = "default",
                                ObjectId = index.ToString("x40", System.Globalization.CultureInfo.InvariantCulture),
                            },
                        ],
                    }),
            ],
        };

        Assert.Empty(ContractValidation.Validate(manifest));
        AssertSchema(SchemaNames.ChangeAuthorPeriodManifest, ContractJson.Serialize(manifest));
        ChangePortfolioReport sourceReport = await SourceReportAsync(manifest);
        ChangePortfolioComparisonReport report = ChangePortfolioComparisonBuilder.Build(
            sourceReport,
            BuildOptions(manifest));

        Assert.Empty(ContractValidation.Validate(report));
        Assert.Equal(
            ChangeAuthorPeriodManifestLimits.MaximumRepositories,
            report.Execution.RepositoryShardCount);
        Assert.Equal(
            ChangeAuthorPeriodManifestLimits.MaximumRepositories,
            report.Execution.Repositories.Count);
        Assert.Equal(
            ChangeAuthorPeriodManifestLimits.MaximumRepositories,
            report.SourcePortfolio!.Aggregation!.Repositories.Count);
        AssertSchema(
            SchemaNames.ChangePortfolioComparisonReport,
            new ChangePortfolioComparisonJsonRenderer().Render(report));
    }

    [Fact]
    public void FailedAnalysisRetainsTheCompletedSelectionScope()
    {
        ChangePortfolioExecutionTelemetry telemetry = new();
        telemetry.Start(ChangePortfolioExecutionPhases.StaticAnalysis);
        ChangePortfolioRepositoryOutcome outcome = new()
        {
            RepositoryId = "repository-a",
            InputDigest = "sha256:" + new string('a', 64),
            Status = ChangePortfolioRepositoryExecutionStatus.Failed,
            CheckpointDisposition = ChangePortfolioCheckpointDisposition.Disabled,
            Scope = new GitAuthorPeriodManifestRepositoryScope
            {
                RepositoryId = "repository-a",
                HeadCount = 1,
                CandidateCount = 3,
                SelectedChangeCount = 2,
                SharedContributorChangeCount = 0,
                ProjectedSnapshotRequests = 4,
                SelectionChunkCount = 1,
                AnalysisChunkCount = 1,
                ChargedCandidateLedgerBytes = 3_000,
                MaximumCandidateLedgerBytes =
                    ChangeAuthorPeriodManifestLimits.MaximumCandidateLedgerBytesPerRepository,
                Contributors =
                [
                    new GitAuthorPeriodManifestScopeContributor
                    {
                        ContributorId = "contributor-a",
                        CandidateCount = 3,
                        DirectAuthorCandidateCount = 3,
                        SelectedChangeCount = 2,
                    },
                ],
            },
            Telemetry = telemetry,
            Failure = new ChangePortfolioComparisonFailure
            {
                RepositoryId = "repository-a",
                Phase = ChangePortfolioExecutionPhases.StaticAnalysis,
                Category = nameof(InvalidOperationException),
                Message = "Synthetic failure.",
                MessageDigest = "sha256:" + new string('b', 64),
            },
        };

        ChangePortfolioComparisonExecution execution =
            ChangePortfolioComparisonExecutionFactory.Create([outcome], checkpointEnabled: false);

        ChangePortfolioComparisonRepositoryExecution repository =
            Assert.Single(execution.Repositories);
        Assert.Equal(2, repository.SelectedChangeCount);
        Assert.Equal(3, repository.CandidateCount);
        Assert.True(execution.Resources!.SelectionScopeComplete);
        Assert.Equal(2, execution.Resources.SelectedChangeCount);
        Assert.Equal(4, execution.Resources.ProjectedSnapshotRequests);
        Assert.Equal(0, execution.Resources.SnapshotAnalysisRequests);
    }

    [Fact]
    public void IncompleteReportPreservesRootFailureAndAStartedPhaseWithNoProgress()
    {
        ChangeAuthorPeriodManifest manifest = Manifest();
        ChangePortfolioExecutionTelemetry telemetry = new();
        telemetry.Start(ChangePortfolioExecutionPhases.StaticAnalysis);
        const string message = "The immutable repository object was not available locally.";
        ChangePortfolioRepositoryOutcome outcome = new()
        {
            RepositoryId = "repository-a",
            InputDigest = ChangePortfolioComparisonIdentity.ComputeRepositoryInputDigest(
                manifest,
                "repository-a",
                EstimationProfile.Implementation,
                ChangeEstimator.Version),
            Status = ChangePortfolioRepositoryExecutionStatus.Failed,
            CheckpointDisposition = ChangePortfolioCheckpointDisposition.MissFailed,
            Telemetry = telemetry,
            Failure = new ChangePortfolioComparisonFailure
            {
                RepositoryId = "repository-a",
                BucketId = "2026-03",
                Phase = ChangePortfolioExecutionPhases.StaticAnalysis,
                Category = nameof(InvalidOperationException),
                Message = message,
                MessageDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(
                    nameof(InvalidOperationException) + "\n" + message),
            },
        };
        ChangePortfolioComparisonExecution execution =
            ChangePortfolioComparisonExecutionFactory.Create([outcome], checkpointEnabled: true);
        ChangePortfolioComparisonProgress observed = Assert.IsType<ChangePortfolioComparisonProgress>(
            execution.Repositories[0].LastProgress);
        Assert.Equal(ChangePortfolioExecutionPhases.StaticAnalysis, observed.Phase);
        Assert.Equal(0, observed.ProcessedUnits);
        Assert.Equal(0, observed.TotalUnits);
        Assert.NotEqual(default, observed.ObservedAt);
        ChangePortfolioComparisonProgress progress = observed with
        {
            ObservedAt = new DateTimeOffset(2026, 8, 20, 11, 58, 0, TimeSpan.Zero),
            ElapsedMilliseconds = 1250m,
            WorkingSetBytes = 64 * 1024 * 1024,
            PeakWorkingSetBytes = 72 * 1024 * 1024,
        };
        execution = execution with
        {
            RuntimeFramework = "test-runtime/1.0",
            OperatingSystemFamily = "other",
            ProcessArchitecture = "test-architecture",
            LogicalProcessorCount = 8,
            Repositories =
            [
                execution.Repositories[0] with
                {
                    ElapsedMilliseconds = 1250m,
                    LastProgress = progress,
                },
            ],
            LastProgress = progress,
            Reuse = execution.Reuse with { PeakWorkingSetBytes = progress.PeakWorkingSetBytes },
            Resources = execution.Resources! with
            {
                PeakWorkingSetBytes = progress.PeakWorkingSetBytes,
                MaximumConcurrentCpuWorkItems = 8,
                MaximumConcurrentGitTreeReads = 8,
                MaximumPendingFileInspections = 8,
            },
        };
        ChangePortfolioComparisonBuildOptions options = BuildOptions(manifest) with
        {
            View = ChangePortfolioComparisonView.Findings,
            Title = "Synthetic incomplete findings",
            ExecutionOverride = execution,
        };

        ChangePortfolioComparisonReport report =
            ChangePortfolioComparisonBuilder.BuildIncomplete(options);

        Assert.Empty(ContractValidation.Validate(report));
        Assert.Equal(ChangePortfolioComparisonStatus.Incomplete, report.Status);
        Assert.Null(report.SourcePortfolio);
        Assert.Empty(report.Series);
        Assert.False(report.Verification.CompleteAggregates);
        Assert.Equal(1, report.Execution.Checkpoint.MissCount);
        Assert.Equal(1, report.Execution.Checkpoint.FailureCount);
        Assert.Equal(message, Assert.Single(report.Execution.Failures).Message);
        string json = new ChangePortfolioComparisonJsonRenderer().Render(report);
        AssertSchema(SchemaNames.ChangePortfolioComparisonReport, json);

        string findings = ChangePortfolioComparisonMarkdownRenderer.Render(report);
        Assert.DoesNotContain('\r', findings);
        Assert.Contains("Synthetic incomplete findings", findings, StringComparison.Ordinal);
        Assert.Contains("The immutable repository object was not available locally.", findings, StringComparison.Ordinal);
        Assert.Contains(progress.ObservedAt.ToString("O"), findings, StringComparison.Ordinal);
        Assert.Contains("no aggregate EHE or trend", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-repository-path", findings, StringComparison.OrdinalIgnoreCase);
        ChangePortfolioComparisonReport trendView = ChangePortfolioComparisonBuilder.BuildIncomplete(
            options with { View = ChangePortfolioComparisonView.Trend });
        Assert.Equal(
            report.Verification.SemanticDigest,
            trendView.Verification.SemanticDigest);
        Assert.Equal(findings, ChangePortfolioComparisonMarkdownRenderer.Render(report));
    }
}
