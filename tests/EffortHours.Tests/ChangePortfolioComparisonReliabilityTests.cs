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
    public async Task CompleteLogicalReportSupportsFortyRepositoryEvidenceShards()
    {
        ChangeAuthorPeriodManifest source = Manifest();
        ChangeAuthorPeriodManifest manifest = source with
        {
            Repositories =
            [
                source.Repositories[0],
                .. Enumerable.Range(1, 39).Select(index =>
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
        Assert.Equal(40, report.Execution.RepositoryShardCount);
        Assert.Equal(40, report.Execution.Repositories.Count);
        Assert.Equal(40, report.SourcePortfolio!.Aggregation!.Repositories.Count);
        AssertSchema(
            SchemaNames.ChangePortfolioComparisonReport,
            new ChangePortfolioComparisonJsonRenderer().Render(report));
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
        Assert.Equal(
            "sha256:b7633f9e6f5d85628c101b22d5572ebaeb5534126e3856ded4035e654b750079",
            ChangePortfolioComparisonIdentity.ComputeTextDigest(findings));
    }
}
