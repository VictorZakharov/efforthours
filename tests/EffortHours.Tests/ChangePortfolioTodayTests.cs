using System.Text.Json;
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
        DateTimeOffset asOf = since.AddHours(10);
        ChangeAuthorPeriodManifest manifest = TodayManifest(since, asOf);
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
        Assert.Contains("X low / expected / high:", markdown, StringComparison.Ordinal);
        Assert.Contains("Reference-capacity policy: **8.00 caller-supplied hours", markdown, StringComparison.Ordinal);
        Assert.Contains("1 identity-selected commits", markdown, StringComparison.Ordinal);
        Assert.Contains("## Repository attribution", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("OLS", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("R-squared", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0%", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("person-a@example.test", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManifestPortfolioCanRepresentACompleteZeroWorkDay()
    {
        DateTimeOffset since = new(2026, 8, 21, 4, 0, 0, TimeSpan.Zero);
        DateTimeOffset asOf = since.AddHours(10);
        ChangeAuthorPeriodManifest manifest = TodayManifest(since, asOf) with { Repositories = [] };
        ChangePortfolioReport source = ChangePortfolioReconciler.Reconcile(
            Selection(manifest),
            [],
            EstimationProfile.Implementation);
        ChangePortfolioComparisonInputs inputs =
            ChangePortfolioComparisonInputLoader.CreateTodayToDate(
                source.Selection.AuthorPeriodManifest!,
                asOf,
                8m);
        ChangePortfolioComparisonBuildOptions buildOptions =
            TodayBuildOptions(manifest, inputs, asOf, selectedChanges: 0);
        buildOptions = buildOptions with
        {
            Discovery = buildOptions.Discovery! with
            {
                ConsideredRepositoryCount = 0,
                ActiveRepositoryCount = 0,
                DefaultHeadCount = 0,
                OpenPullRequestHeadCount = 0,
                OpenPullRequestCount = 0,
                LocalObjectCount = 0,
            },
        };
        ChangePortfolioComparisonReport report = ChangePortfolioComparisonBuilder.Build(
            source,
            buildOptions);

        Assert.Empty(source.Items);
        Assert.Equal(new EffortRange { Low = 0m, Expected = 0m, High = 0m }, source.TotalEffort);
        Assert.True(Assert.Single(source.Aggregation!.Contributors).NoSelectedCommits);
        Assert.Empty(source.Aggregation.Repositories);
        Assert.Empty(ContractValidation.Validate(report));
        string json = new ChangePortfolioComparisonJsonRenderer().Render(report);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioComparisonReport,
            json);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        string markdown = ChangePortfolioTodayMarkdownRenderer.Render(report);
        Assert.Contains("X low / expected / high: **0x / 0x / 0x**", markdown, StringComparison.Ordinal);
        Assert.Contains("0 identity-selected commits", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void PinnedReferenceGoldenIsAnonymizedVersionedAndReconcilesExactly()
    {
        EngineeringScopeProfile scope = EngineeringScopeProfile.LoadBundled();
        ReferenceTodayGolden golden = new(
            Snapshot: new DateTimeOffset(2026, 8, 23, 19, 20, 10, TimeSpan.FromHours(-4)),
            CandidateCommitCount: 17,
            SelectedChangeCount: 12,
            RepositorySelectedChanges:
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["repository-alpha"] = 5,
                    ["repository-beta"] = 5,
                    ["repository-gamma"] = 2,
                },
            CapacityHours: 8m,
            Effort: new EffortRange
            {
                Low = 195.80m,
                Expected = 356.79m,
                High = 667.95m,
            },
            Ratio: new EffortRange
            {
                Low = 24.475m,
                Expected = 44.59875m,
                High = 83.49375m,
            });

        Assert.Equal("change-seed/0.18.2+seed-rules/0.4.0", ChangeEstimator.Version);
        Assert.Equal("engineering-scope/1.0.0", scope.Contract.Version);
        Assert.Equal(
            "sha256:98b530f76f6cbd75e18ead6a52f7394f8ac00ddf23252ebb652971d9f2cb1893",
            scope.Contract.Digest);
        Assert.Equal(3, golden.RepositorySelectedChanges.Count);
        Assert.Equal(golden.SelectedChangeCount, golden.RepositorySelectedChanges.Values.Sum());
        Assert.True(golden.SelectedChangeCount <= golden.CandidateCommitCount);
        Assert.Equal(
            golden.Ratio,
            new EffortRange
            {
                Low = golden.Effort.Low / golden.CapacityHours,
                Expected = golden.Effort.Expected / golden.CapacityHours,
                High = golden.Effort.High / golden.CapacityHours,
            });
        Assert.True(golden.Effort.Low <= golden.Effort.Expected);
        Assert.True(golden.Effort.Expected <= golden.Effort.High);
    }

    [Fact]
    public void IncompleteTodayMarkdownPublishesSafeFailureContextWithoutAnAggregate()
    {
        DateTimeOffset since = new(2026, 8, 21, 4, 0, 0, TimeSpan.Zero);
        DateTimeOffset asOf = since.AddHours(10);
        ChangeAuthorPeriodManifest manifest = TodayManifest(since, asOf);
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
                AdmittedChangeCount = selectedChanges,
                ScopeEmptyChangeCount = 0,
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
                IdentitySources = "provider-viewer",
                Complete = true,
                ProviderRepositoryCount = 3,
                ConsideredRepositoryCount = 1,
                ActiveRepositoryCount = 1,
                DefaultHeadCount = 1,
                OpenPullRequestHeadCount = 0,
                OpenPullRequestCount = 1,
                ProviderQueryCount = 4,
                ProviderPageCount = 4,
                ProviderProcessCount = 4,
                LocalObjectCount = 1,
                ElapsedMilliseconds = 12.5m,
            },
            ScopeProfile = new ChangePortfolioScopeProfile
            {
                Id = "engineering",
                Version = "engineering-scope/1.0.0",
                Digest = ChangePortfolioComparisonIdentity.ComputeTextDigest("engineering-scope"),
                Source = "bundled",
                ImportantExclusions = ["documentation", "locks", "vendor"],
            },
            ScopeSummary = new ChangePortfolioScopeSummary
            {
                IdentitySelectedCommitCount = selectedChanges,
                AdmittedCommitCount = selectedChanges,
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

    private sealed record ReferenceTodayGolden(
        DateTimeOffset Snapshot,
        int CandidateCommitCount,
        int SelectedChangeCount,
        IReadOnlyDictionary<string, int> RepositorySelectedChanges,
        decimal CapacityHours,
        EffortRange Effort,
        EffortRange Ratio);
}

public sealed class ChangePortfolioTodayCommandTests
{
    [Fact]
    public async Task ProviderFailureWritesValidatedIncompleteTodayReport()
    {
        ChangePortfolioCommand command = new(
            new ChangeEstimator(),
            (path, pull, repository, fetch, token) =>
                throw new NotSupportedException(),
            (path, planOptions, telemetry, token) =>
                throw new NotSupportedException(),
            (path, token) => throw new NotSupportedException(),
            (path, telemetry, token) => throw new NotSupportedException(),
            measureAuthorPeriodManifest: null,
            (request, token) =>
            {
                request.ExecutionTelemetry!.Start(ChangePortfolioExecutionPhases.ProviderDiscovery);
                throw new InvalidOperationException("synthetic provider detail");
            });
        StringWriter stdout = new();
        StringWriter stderr = new();

        int exitCode = await command.ExecuteAsync(
        [
            "--today",
            "--owner", "example-owner",
            "--author", "@me",
            "--timezone", "UTC",
            "--scope", "engineering",
            "--capacity-hours", "8",
            "--generated-at", "2026-08-23T12:34:56Z",
            "--format", "markdown",
            "--no-rate",
        ], stdout, stderr, CancellationToken.None);

        Assert.Equal(CliExitCodes.InvalidInput, exitCode);
        Assert.True(stdout.ToString().Length > 0, stderr.ToString());
        Assert.Contains("Status: **incomplete**", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("provider-discovery", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("Agent action:", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("github-provider-request-failed", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("X low / expected / high", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic provider detail", stdout.ToString(), StringComparison.Ordinal);
        string actionLine = stderr.ToString().ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Last();
        using JsonDocument action = JsonDocument.Parse(actionLine);
        Assert.Equal(
            "efforthours-agent-action/1.0",
            action.RootElement.GetProperty("schema").GetString());
        Assert.Equal(
            "github-provider-request-failed",
            action.RootElement.GetProperty("failureCode").GetString());
    }

    [Fact]
    public void ParserAcceptsTheOneCommandWorkflowWithoutAnOutputFile()
    {
        ChangePortfolioCommandParseResult parsed = ChangePortfolioCommandOptionsParser.Parse(
        [
            "--owner", "example-owner",
            "--author", "@me",
            "--today",
            "--timezone", "America/Toronto",
            "--include-open-prs",
            "--scope", "engineering",
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

    [Fact]
    public void ParserRecoversPowerShellsUnquotedAtMeInTodayMode()
    {
        ChangePortfolioCommandParseResult parsed = ChangePortfolioCommandOptionsParser.Parse(
        [
            "--today",
            "--owner", "example-owner",
            "--author",
            "--timezone", "America/Toronto",
            "--scope", "engineering",
            "--capacity-hours", "8",
        ]);

        ChangePortfolioCommandOptions options = Assert.IsType<ChangePortfolioCommandOptions>(parsed.Options);
        Assert.Null(parsed.Error);
        Assert.Equal(["@me"], options.AuthorAliases);
    }

    [Theory]
    [InlineData("--capacity-hours")]
    [InlineData("--owner")]
    [InlineData("--scope")]
    [InlineData("--timezone")]
    public void ParserRequiresTheTodayWorkflowInputs(string omitted)
    {
        List<string> arguments =
        [
            "--owner", "example-owner",
            "--author", "@me",
            "--today",
            "--timezone", "America/Toronto",
            "--scope", "engineering",
            "--capacity-hours", "8",
        ];
        int index = arguments.IndexOf(omitted);
        arguments.RemoveRange(index, 2);

        ChangePortfolioCommandParseResult parsed = ChangePortfolioCommandOptionsParser.Parse([.. arguments]);

        Assert.NotNull(parsed.Error);
    }
}
