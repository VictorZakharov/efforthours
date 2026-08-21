using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Fact]
    public async Task TodayToDateBuildsOnePartialBucketAndPreservesExactRatio()
    {
        DateTimeOffset since = new(2026, 8, 21, 4, 0, 0, TimeSpan.Zero);
        DateTimeOffset until = since.AddDays(1);
        DateTimeOffset asOf = since.AddHours(10);
        ChangeAuthorPeriodManifest manifest = TodayManifest(since, until);
        ChangePortfolioCandidate candidate = await CandidateAsync(
            "today",
            asOf.AddHours(-1),
            [Match("me", ChangePortfolioContributorMatchKind.DirectAuthor)]);
        ChangePortfolioReport source = ChangePortfolioReconciler.Reconcile(
            Selection(manifest),
            [candidate],
            EstimationProfile.Implementation);
        ChangePortfolioComparisonInputs inputs =
            ChangePortfolioComparisonInputLoader.CreateTodayToDate(
                source.Selection.AuthorPeriodManifest!,
                asOf,
                8m);
        ChangePortfolioComparisonReport report = ChangePortfolioComparisonBuilder.Build(
            source,
            TodayBuildOptions(manifest, inputs, asOf, selectedChanges: 1));

        Assert.Empty(ContractValidation.Validate(report));
        Assert.Equal(ChangePortfolioComparisonPolicies.TodayToDateV1, report.BucketPolicy.Policy);
        Assert.True(Assert.Single(report.Buckets).PartialEnd);
        ChangePortfolioComparisonSeries portfolio = Assert.Single(
            report.Series,
            series => series.Kind == ChangePortfolioSeriesKind.Portfolio);
        Assert.Equal(8m, portfolio.TotalCapacityHours);
        Assert.Equal(
            decimal.Round(source.TotalEffort.Expected / 8m, 6, MidpointRounding.AwayFromZero),
            portfolio.TotalCapacityRatio!.Expected);

        string json = new ChangePortfolioComparisonJsonRenderer().Render(report);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioComparisonReport,
            json);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        string markdown = ChangePortfolioTodayMarkdownRenderer.Render(report);
        Assert.Contains("Today-to-date expected ratio:", markdown, StringComparison.Ordinal);
        Assert.Contains("Reference capacity: **8.00 h**", markdown, StringComparison.Ordinal);
        Assert.Contains("Selected changes: **1**", markdown, StringComparison.Ordinal);
        Assert.Contains("Open PR heads included: **1**", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("person-a@example.test", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManifestPortfolioCanRepresentACompleteZeroWorkDay()
    {
        DateTimeOffset since = new(2026, 8, 21, 4, 0, 0, TimeSpan.Zero);
        DateTimeOffset asOf = since.AddHours(10);
        ChangeAuthorPeriodManifest manifest = TodayManifest(since, since.AddDays(1));
        ChangePortfolioReport source = ChangePortfolioReconciler.Reconcile(
            Selection(manifest),
            [],
            EstimationProfile.Implementation);
        ChangePortfolioComparisonInputs inputs =
            ChangePortfolioComparisonInputLoader.CreateTodayToDate(
                source.Selection.AuthorPeriodManifest!,
                asOf,
                8m);
        ChangePortfolioComparisonReport report = ChangePortfolioComparisonBuilder.Build(
            source,
            TodayBuildOptions(manifest, inputs, asOf, selectedChanges: 0));

        Assert.Empty(source.Items);
        Assert.Equal(new EffortRange { Low = 0m, Expected = 0m, High = 0m }, source.TotalEffort);
        Assert.True(Assert.Single(source.Aggregation!.Contributors).NoSelectedCommits);
        Assert.Empty(ContractValidation.Validate(report));
        string json = new ChangePortfolioComparisonJsonRenderer().Render(report);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioComparisonReport,
            json);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        string markdown = ChangePortfolioTodayMarkdownRenderer.Render(report);
        Assert.Contains("Today-to-date expected ratio: **0.00x**", markdown, StringComparison.Ordinal);
        Assert.Contains("Selected changes: **0**", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void IncompleteTodayMarkdownPublishesSafeFailureContextWithoutAnAggregate()
    {
        DateTimeOffset since = new(2026, 8, 21, 4, 0, 0, TimeSpan.Zero);
        DateTimeOffset asOf = since.AddHours(10);
        ChangeAuthorPeriodManifest manifest = TodayManifest(since, since.AddDays(1));
        ChangePortfolioComparisonInputs inputs =
            ChangePortfolioComparisonInputLoader.CreateTodayToDate(
                ChangeAuthorPeriodManifestIdentity.CreateReportSelection(manifest)
                    .AuthorPeriodManifest!,
                asOf,
                8m);
        const string message = "Synthetic privacy-safe repository failure.";
        ChangePortfolioComparisonFailure failure = new()
        {
            RepositoryId = "repository-a",
            Phase = ChangePortfolioExecutionPhases.StaticAnalysis,
            Category = nameof(InvalidOperationException),
            Message = message,
            MessageDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(
                nameof(InvalidOperationException) + "\n" + message),
        };
        ChangePortfolioRepositoryOutcome outcome = new()
        {
            RepositoryId = "repository-a",
            InputDigest = ChangePortfolioComparisonIdentity.ComputeRepositoryInputDigest(
                manifest,
                "repository-a",
                EstimationProfile.Implementation,
                ChangeEstimator.Version),
            Status = ChangePortfolioRepositoryExecutionStatus.Failed,
            CheckpointDisposition = ChangePortfolioCheckpointDisposition.Disabled,
            Telemetry = new ChangePortfolioExecutionTelemetry(),
            Failure = failure,
        };
        ChangePortfolioComparisonBuildOptions options = TodayBuildOptions(
            manifest,
            inputs,
            asOf,
            selectedChanges: 0) with
        {
            ExecutionOverride = ChangePortfolioComparisonExecutionFactory.Create(
                [outcome],
                checkpointEnabled: false),
        };

        ChangePortfolioComparisonReport report =
            ChangePortfolioComparisonBuilder.BuildIncomplete(options);
        string markdown = ChangePortfolioTodayMarkdownRenderer.Render(report);

        Assert.Contains(message, markdown, StringComparison.Ordinal);
        Assert.Contains(failure.MessageDigest, markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Today-to-date expected ratio", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("private-repository-path", markdown, StringComparison.OrdinalIgnoreCase);
    }

    private static ChangePortfolioComparisonBuildOptions TodayBuildOptions(
        ChangeAuthorPeriodManifest manifest,
        ChangePortfolioComparisonInputs inputs,
        DateTimeOffset asOf,
        int selectedChanges)
    {
        ChangePortfolioComparisonExecution execution = FixedExecution(manifest);
        execution = execution with
        {
            Repositories = [.. execution.Repositories.Select(repository => repository with
            {
                SelectedChangeCount = selectedChanges,
                CandidateCount = selectedChanges,
            })],
        };
        return new ChangePortfolioComparisonBuildOptions
        {
            View = ChangePortfolioComparisonView.Trend,
            Title = "Today-to-date capacity",
            GeneratedAt = asOf,
            AsOf = asOf,
            Discovery = new ChangePortfolioHostDiscovery
            {
                ScopeDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest("scope"),
                IdentitySources = "provider-viewer-and-local-git",
                Complete = true,
                ProviderRepositoryCount = 3,
                WorkspaceRepositoryCount = 2,
                ConsideredRepositoryCount = 1,
                ActiveRepositoryCount = 1,
                DefaultHeadCount = 1,
                OpenPullRequestHeadCount = 1,
                ProviderQueryCount = 4,
                ProviderPageCount = 4,
                LocalObjectCount = 2,
                ElapsedMilliseconds = 12.5m,
            },
            CliVersion = "0.10.0-test",
            Profile = EstimationProfile.Implementation,
            BucketKind = inputs.BucketKind,
            BucketPolicy = inputs.BucketPolicy,
            BucketManifest = inputs.BucketManifest,
            Buckets = inputs.Buckets,
            CapacityManifest = inputs.CapacityManifest,
            SourceManifest = manifest,
            ExecutionOverride = execution,
        };
    }

    private static ChangeAuthorPeriodManifest TodayManifest(
        DateTimeOffset since,
        DateTimeOffset until) => new()
        {
            Selection = new ChangeAuthorPeriodManifestSelection
            {
                SinceInclusive = since,
                UntilExclusive = until,
                TimeZone = "America/Toronto",
                DateField = ChangePortfolioDateField.Author,
                MergePolicy = ChangePortfolioMergePolicy.Exclude,
                CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include,
            },
            Contributors =
            [
                new ChangeAuthorPeriodManifestContributor
                {
                    Id = "me",
                    Aliases = ["person-a@example.test"],
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
                            ObjectId = new string('a', 40),
                        },
                    ],
                },
            ],
        };
}

public sealed class ChangePortfolioTodayCommandTests
{
    [Fact]
    public void ParserAcceptsTheOneCommandWorkflowWithoutAnOutputFile()
    {
        ChangePortfolioCommandParseResult parsed = ChangePortfolioCommandOptionsParser.Parse(
        [
            "--owner", "example-owner",
            "--workspace", ".",
            "--author", "@me",
            "--today",
            "--timezone", "America/Toronto",
            "--include-open-prs",
            "--fetch-missing",
            "--capacity-hours", "8",
            "--format", "markdown",
            "--no-rate",
        ]);

        ChangePortfolioCommandOptions options = Assert.IsType<ChangePortfolioCommandOptions>(parsed.Options);
        Assert.Null(parsed.Error);
        Assert.True(options.Today);
        Assert.True(options.IncludeOpenPullRequests);
        Assert.Equal(8m, options.CapacityHours);
        Assert.Null(options.OutputPath);
        Assert.Contains("--capacity-hours", ChangePortfolioHelp.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--capacity-hours")]
    [InlineData("--owner")]
    [InlineData("--workspace")]
    [InlineData("--timezone")]
    public void ParserRequiresTheTodayWorkflowInputs(string omitted)
    {
        List<string> arguments =
        [
            "--owner", "example-owner",
            "--workspace", ".",
            "--author", "@me",
            "--today",
            "--timezone", "America/Toronto",
            "--capacity-hours", "8",
        ];
        int index = arguments.IndexOf(omitted);
        arguments.RemoveRange(index, 2);

        ChangePortfolioCommandParseResult parsed = ChangePortfolioCommandOptionsParser.Parse([.. arguments]);

        Assert.NotNull(parsed.Error);
    }
}
