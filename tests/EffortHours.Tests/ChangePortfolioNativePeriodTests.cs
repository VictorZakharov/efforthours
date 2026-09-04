using System.Globalization;
using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Theory]
    [InlineData(ChangePortfolioNativePeriodKind.ThisWeek, "2026-08-31T04:00:00Z", "2026-09-04T15:00:00Z")]
    [InlineData(ChangePortfolioNativePeriodKind.LastWeek, "2026-08-24T04:00:00Z", "2026-08-31T04:00:00Z")]
    [InlineData(ChangePortfolioNativePeriodKind.ThisMonth, "2026-09-01T04:00:00Z", "2026-09-04T15:00:00Z")]
    [InlineData(ChangePortfolioNativePeriodKind.LastMonth, "2026-08-01T04:00:00Z", "2026-09-01T04:00:00Z")]
    public void NamedPeriodsResolveFromLocalCalendarBoundaries(
        ChangePortfolioNativePeriodKind kind,
        string expectedSince,
        string expectedUntil)
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");

        ChangePortfolioNamedPeriodRange range = ChangePortfolioNamedPeriodResolver.Resolve(
            kind,
            DateTimeOffset.Parse("2026-09-04T15:00:00Z", CultureInfo.InvariantCulture),
            zone);

        Assert.Equal(DateTimeOffset.Parse(expectedSince, CultureInfo.InvariantCulture), range.SinceInclusive);
        Assert.Equal(DateTimeOffset.Parse(expectedUntil, CultureInfo.InvariantCulture), range.UntilExclusive);
    }

    [Fact]
    public async Task DailyPeriodPublishesDailyAndCapacityWeightedOverallMultipliers()
    {
        DateTimeOffset asOf = DateTimeOffset.Parse(
            "2026-03-09T12:00:00Z",
            CultureInfo.InvariantCulture);
        ChangePortfolioNamedPeriodRange range = ChangePortfolioNamedPeriodResolver.Resolve(
            ChangePortfolioNativePeriodKind.LastWeek,
            asOf,
            TimeZoneInfo.FindSystemTimeZoneById("America/Toronto"));
        ChangeAuthorPeriodManifest manifest = TodayManifest(
            range.SinceInclusive,
            range.UntilExclusive);
        ChangePortfolioCandidate candidate = await CandidateAsync(
            "period",
            range.UntilExclusive.AddHours(-8),
            [Match("me", ChangePortfolioContributorMatchKind.DirectAuthor)]);
        ChangePortfolioReport source = ChangePortfolioReconciler.Reconcile(
            Selection(manifest),
            [candidate],
            EstimationProfile.Implementation);
        ChangePortfolioComparisonInputs inputs =
            ChangePortfolioComparisonInputLoader.CreateNamedPeriod(
                source.Selection.AuthorPeriodManifest!,
                ChangePortfolioNativePeriodKind.LastWeek,
                ChangePortfolioNativeBreakdown.CalendarDay,
                8m);
        ChangePortfolioNativePeriod native = new()
        {
            Kind = ChangePortfolioNativePeriodKind.LastWeek,
            Breakdown = ChangePortfolioNativeBreakdown.CalendarDay,
            CapacityHoursPerDay = 8m,
            ContributorSelection = new ChangePortfolioContributorSelection
            {
                Mode = ChangePortfolioContributorSelectionMode.SingleContributor,
                Complete = true,
                EligiblePopulationCount = 1,
                IncludedContributorIds = ["me"],
                InputDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest("single-me"),
            },
        };
        ChangePortfolioComparisonBuildOptions build = TodayBuildOptions(
            manifest,
            inputs,
            asOf,
            selectedChanges: 1) with
        {
            ContributorNormalization = ChangePortfolioContributorNormalization.Isolated,
            NativePeriod = native,
        };

        ChangePortfolioComparisonReport report = ChangePortfolioComparisonBuilder.Build(source, build);
        ChangePortfolioComparisonSeries portfolio = report.Series.Single(series =>
            series.Kind == ChangePortfolioSeriesKind.Portfolio);

        Assert.Empty(ContractValidation.Validate(report));
        Assert.Equal(7, report.Buckets.Count);
        Assert.Equal(56m, portfolio.TotalCapacityHours);
        Assert.Equal(
            decimal.Round(source.TotalEffort.Expected / 56m, 6, MidpointRounding.AwayFromZero),
            portfolio.TotalCapacityRatio!.Expected);
        Assert.Equal(167, (range.UntilExclusive - range.SinceInclusive).TotalHours);

        string json = new ChangePortfolioComparisonJsonRenderer().Render(report);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioComparisonReport,
            json);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        string markdown = ChangePortfolioPeriodMarkdownRenderer.Render(report);
        Assert.Contains("## Daily breakdown", markdown, StringComparison.Ordinal);
        Assert.Contains("`All selected contributors`", markdown, StringComparison.Ordinal);
        Assert.Contains("total expected EHE / total reference capacity", markdown, StringComparison.Ordinal);
        Assert.Contains("not an unweighted average", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("person-a@example.test", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TotalPeriodUsesOneBucketAndOneCapacityEntryPerContributor()
    {
        ChangeAuthorPeriodManifest manifest = TodayManifest(
            new DateTimeOffset(2026, 8, 1, 4, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 1, 4, 0, 0, TimeSpan.Zero));
        ChangePortfolioAuthorPeriodManifestSelection selection =
            ChangeAuthorPeriodManifestIdentity.CreateReportSelection(manifest).AuthorPeriodManifest!;

        ChangePortfolioComparisonInputs inputs = ChangePortfolioComparisonInputLoader.CreateNamedPeriod(
            selection,
            ChangePortfolioNativePeriodKind.LastMonth,
            ChangePortfolioNativeBreakdown.Total,
            8m);

        Assert.Single(inputs.Buckets);
        Assert.Equal(ChangePortfolioComparisonPolicies.NamedPeriodTotalV1, inputs.BucketPolicy);
        Assert.Equal(248m, Assert.Single(inputs.CapacityManifest!.Entries).Hours);
    }
}

public sealed class ChangePortfolioNativePeriodCommandTests
{
    [Fact]
    public void ParserAcceptsSingleAndTeamNamedPeriodWorkflows()
    {
        ChangePortfolioCommandParseResult single = ChangePortfolioCommandOptionsParser.Parse(
        [
            "--native-period", "--owner", "example-owner", "--author", "@me",
            "--period", "last-week", "--breakdown", "day", "--timezone", "UTC",
            "--scope", "engineering", "--capacity-hours-per-day", "8",
        ]);
        ChangePortfolioCommandParseResult team = ChangePortfolioCommandOptionsParser.Parse(
        [
            "--compare-team", "--owner", "example-owner",
            "--contributors-from", "example-owner/reference", "--sample", "3",
            "--sample-seed", "stable-seed", "--include-author", "@me",
            "--period", "last-month", "--timezone", "UTC", "--scope", "engineering",
            "--capacity-hours-per-day", "7.5",
        ]);
        ChangePortfolioCommandParseResult misplaced = ChangePortfolioCommandOptionsParser.Parse(
        [
            "--author-period-manifest", "manifest.json", "--breakdown", "total",
            "--output", "report.json",
        ]);

        ChangePortfolioCommandOptions singleOptions =
            Assert.IsType<ChangePortfolioCommandOptions>(single.Options);
        ChangePortfolioCommandOptions teamOptions =
            Assert.IsType<ChangePortfolioCommandOptions>(team.Options);
        Assert.Null(single.Error);
        Assert.Equal(ChangePortfolioNativePeriodKind.LastWeek, singleOptions.Period);
        Assert.Equal(ChangePortfolioNativeBreakdown.CalendarDay, singleOptions.Breakdown);
        Assert.Equal(ChangePortfolioContributorNormalization.Isolated, singleOptions.ContributorNormalization);
        Assert.Null(team.Error);
        Assert.True(teamOptions.TeamComparison);
        Assert.Equal(3, teamOptions.SampleSize);
        Assert.Equal(["@me"], teamOptions.IncludedAuthors);
        Assert.Null(teamOptions.OutputPath);
        Assert.Contains("require change today", misplaced.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProviderFailureWritesValidatedIncompletePeriodReport()
    {
        ChangePortfolioCommand command = new(
            new ChangeEstimator(),
            (path, pull, repository, fetch, token) => throw new NotSupportedException(),
            (path, planOptions, telemetry, token) => throw new NotSupportedException(),
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
            "--native-period", "--owner", "example-owner", "--author", "@me",
            "--period", "last-week", "--breakdown", "day", "--timezone", "UTC",
            "--scope", "engineering", "--capacity-hours-per-day", "8",
            "--generated-at", "2026-09-04T12:00:00Z", "--format", "markdown", "--no-rate",
        ], stdout, stderr, CancellationToken.None);

        Assert.Equal(CliExitCodes.InvalidInput, exitCode);
        Assert.Contains("Status: **incomplete**", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("Period: **last week**", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("Agent action:", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("## Overall", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic provider detail", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteNamedPeriodWithNoWorkPublishesZero()
    {
        DateTimeOffset since = new(2026, 8, 24, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset until = new(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);
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
                    Id = "contributor",
                    Aliases = ["person@example.test"],
                },
            ],
            Repositories = [],
        };
        ChangePortfolioContributorSelection contributorSelection = new()
        {
            Mode = ChangePortfolioContributorSelectionMode.SingleContributor,
            Complete = true,
            EligiblePopulationCount = 1,
            IncludedContributorIds = ["contributor"],
            InputDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest("zero-period"),
        };
        ChangePortfolioCommand command = new(
            new ChangeEstimator(),
            (path, pull, repository, fetch, token) => throw new NotSupportedException(),
            (path, planOptions, telemetry, token) => throw new NotSupportedException(),
            (path, token) => throw new NotSupportedException(),
            (path, telemetry, token) => throw new NotSupportedException(),
            measureAuthorPeriodManifest: null,
            (request, token) => Task.FromResult(new GitHubAuthorPeriodDiscoveryResult
            {
                Manifest = manifest,
                RepositoryPaths = new Dictionary<string, string>(StringComparer.Ordinal),
                PathAdmissions = new Dictionary<string, ChangePathAdmission>(StringComparer.Ordinal),
                Discovery = new ChangePortfolioHostDiscovery
                {
                    ScopeDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest("zero-scope"),
                    IdentitySources = "provider-login",
                    Complete = true,
                    ProviderQueryCount = 1,
                    ProviderPageCount = 1,
                    ProviderProcessCount = 1,
                },
                ScopeProfile = EngineeringScopeProfile.LoadBundled().Contract,
                AsOf = request.AsOf,
                ContributorSelection = contributorSelection,
            }));
        StringWriter stdout = new();
        StringWriter stderr = new();

        int exitCode = await command.ExecuteAsync(
        [
            "--native-period", "--owner", "example-owner", "--author", "person@example.test",
            "--period", "last-week", "--timezone", "UTC", "--scope", "engineering",
            "--capacity-hours-per-day", "8", "--generated-at", "2026-09-04T12:00:00Z",
            "--format", "markdown", "--no-rate",
        ], stdout, stderr, CancellationToken.None);

        Assert.True(
            exitCode == CliExitCodes.Success,
            $"Expected success but received {exitCode}.\nSTDERR:\n{stderr}\nSTDOUT:\n{stdout}");
        Assert.Contains("Status: **complete**", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("0.00×", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("person@example.test", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
