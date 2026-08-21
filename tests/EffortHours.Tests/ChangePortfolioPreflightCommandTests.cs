using System.Globalization;
using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioCommandTests
{
    [Fact]
    public async Task AuthorManifestPreflightRecommendsOneCheckpointedSummaryWithoutAnalyzing()
    {
        ChangeAuthorPeriodManifest manifest = Manifest();
        ChangePortfolioCommand command = PreflightCommand(manifest, selectedChanges: 2_001);
        StringWriter stdout = new();
        StringWriter stderr = new();

        int exitCode = await command.ExecuteAsync(
            [
                "--author-period-manifest", "private-manifest.json",
                "--preflight",
                "--format", "json",
                "--compact",
                "--no-rate",
            ],
            stdout,
            stderr,
            CancellationToken.None);

        Assert.Equal(CliExitCodes.Success, exitCode);
        ChangePortfolioPreflightReport report =
            ContractJson.Deserialize<ChangePortfolioPreflightReport>(stdout.ToString());
        Assert.Equal(ChangePortfolioPreflightStatus.SummaryRecommended, report.Status);
        Assert.Equal(
            ChangePortfolioPreflightAction.RunCheckpointedSummary,
            report.Recommendation.Action);
        Assert.Equal(2_001, report.Totals.SelectedChangeCount);
        Assert.Equal(4_002, report.Totals.ProjectedSnapshotRequests);
        Assert.Equal(126, report.Resources.AnalysisChunkCount);
        Assert.Equal(4, report.Resources.MaximumConcurrentChangesPerRepository);
        Assert.InRange(report.Resources.MaximumConcurrentCpuWorkItems, 1, 24);
        Assert.InRange(report.Resources.MaximumConcurrentGitTreeReads, 1, 8);
        Assert.InRange(report.Resources.MaximumPendingFileInspections, 2, 8);
        Assert.False(report.Verification.CalculationPerformed);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioPreflightReport,
            stdout.ToString()).IsValid);
        Assert.DoesNotContain("private@example.test", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-repository-path", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "do not split the interval",
            string.Join(' ', report.Recommendation.Steps),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("portfolio phase rendering", stderr.ToString(), StringComparison.Ordinal);

        StringWriter markdown = new();
        int markdownExitCode = await command.ExecuteAsync(
            [
                "--author-period-manifest", "private-manifest.json",
                "--preflight",
                "--format", "markdown",
                "--no-rate",
            ],
            markdown,
            new StringWriter(),
            CancellationToken.None);
        Assert.Equal(CliExitCodes.Success, markdownExitCode);
        Assert.Contains("# Author-period portfolio preflight", markdown.ToString(), StringComparison.Ordinal);
        Assert.Contains("## Recommendation", markdown.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("private@example.test", markdown.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BlockedPreflightEmitsALowerBoundAndReturnsNonzero()
    {
        ChangeAuthorPeriodManifest manifest = Manifest();
        ChangePortfolioCommand command = PreflightCommand(
            manifest,
            selectedChanges: null,
            blockingResource: "candidate-ledger-bytes");
        StringWriter stdout = new();

        int exitCode = await command.ExecuteAsync(
            ["--author-period-manifest", "private-manifest.json", "--preflight", "--no-rate"],
            stdout,
            new StringWriter(),
            CancellationToken.None);

        Assert.Equal(CliExitCodes.InvalidInput, exitCode);
        ChangePortfolioPreflightReport report =
            ContractJson.Deserialize<ChangePortfolioPreflightReport>(stdout.ToString());
        Assert.Equal(ChangePortfolioPreflightStatus.Blocked, report.Status);
        Assert.True(report.Totals.CandidateCountIsLowerBound);
        Assert.Null(report.Totals.SelectedChangeCount);
        Assert.Equal(
            ChangePortfolioPreflightAction.ResolveResourceBudget,
            report.Recommendation.Action);
        Assert.Equal("candidate-ledger-bytes", report.Resources.BlockingResource);
    }

    [Fact]
    public void PreflightRequiresAnAuthorPeriodManifestAndRejectsAnalysisOptions()
    {
        ChangePortfolioCommandParseResult wrongSelector = ChangePortfolioCommandOptionsParser.Parse(
            ["repository", "--author", "person", "--since", "2026-08-01T00:00:00Z", "--until", "2026-09-01T00:00:00Z", "--preflight"]);
        ChangePortfolioCommandParseResult comparison = ChangePortfolioCommandOptionsParser.Parse(
            ["--author-period-manifest", "manifest.json", "--preflight", "--bucket", "calendar-month", "--output", "report.md"]);
        ChangePortfolioCommandParseResult pricing = ChangePortfolioCommandOptionsParser.Parse(
            ["--author-period-manifest", "manifest.json", "--preflight", "--hourly-rate", "100"]);
        ChangePortfolioCommandParseResult valid = ChangePortfolioCommandOptionsParser.Parse(
            ["--author-period-manifest", "manifest.json", "--preflight", "--format", "markdown"]);

        Assert.Contains("requires --author-period-manifest", wrongSelector.Error, StringComparison.Ordinal);
        Assert.Contains("measures the manifest selection only", comparison.Error, StringComparison.Ordinal);
        Assert.Contains("does not estimate EHE or pricing", pricing.Error, StringComparison.Ordinal);
        Assert.True(valid.Options!.Preflight);
    }

    private static ChangePortfolioCommand PreflightCommand(
        ChangeAuthorPeriodManifest manifest,
        int? selectedChanges,
        string? blockingResource = null) => new(
            new ChangeEstimator(),
            (_, _, _, _, _) => throw new InvalidOperationException(
                "Pull-request planning was not expected."),
            (_, _, _, _) => throw new InvalidOperationException(
                "Direct author-period planning was not expected."),
            (_, _) => throw new InvalidOperationException(
                "PR manifest loading was not expected."),
            (_, _, _) => throw new InvalidOperationException(
                "EHE planning was not expected during preflight."),
            (path, telemetry, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.Equal("private-manifest.json", path);
                int candidates = selectedChanges ?? 4_097;
                return Task.FromResult(new GitAuthorPeriodManifestScopePlan
                {
                    Manifest = manifest,
                    Selection = ChangeAuthorPeriodManifestIdentity.CreateReportSelection(manifest),
                    CompleteScope = blockingResource is null,
                    ExecutionTelemetry = telemetry,
                    Repositories =
                    [
                        new GitAuthorPeriodManifestRepositoryScope
                        {
                            RepositoryId = "repository-a",
                            HeadCount = 2,
                            CandidateCount = candidates,
                            CandidateCountIsLowerBound = blockingResource is not null,
                            SelectedChangeCount = selectedChanges,
                            SharedContributorChangeCount = selectedChanges is null ? null : 0,
                            ProjectedSnapshotRequests = selectedChanges * 2L,
                            SelectionChunkCount = ((candidates - 1) / 1_024) + 1,
                            AnalysisChunkCount = selectedChanges is null
                                ? null
                                : ((selectedChanges.Value - 1) / 16) + 1,
                            ChargedCandidateLedgerBytes = blockingResource is null
                                ? 4_000_000
                                : ChangeAuthorPeriodManifestLimits.MaximumCandidateLedgerBytesPerRepository + 1,
                            MaximumCandidateLedgerBytes =
                                ChangeAuthorPeriodManifestLimits.MaximumCandidateLedgerBytesPerRepository,
                            BlockingResource = blockingResource,
                            Contributors =
                            [
                                new GitAuthorPeriodManifestScopeContributor
                                {
                                    ContributorId = "contributor-a",
                                    CandidateCount = candidates,
                                    DirectAuthorCandidateCount = candidates,
                                    SelectedChangeCount = selectedChanges,
                                },
                            ],
                        },
                    ],
                });
            });

    private static ChangeAuthorPeriodManifest Manifest() => new()
    {
        Selection = new ChangeAuthorPeriodManifestSelection
        {
            SinceInclusive = DateTimeOffset.Parse("2026-08-01T00:00:00Z", CultureInfo.InvariantCulture),
            UntilExclusive = DateTimeOffset.Parse("2026-09-01T00:00:00Z", CultureInfo.InvariantCulture),
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
                RepositoryPath = "secret-repository-path",
                Heads =
                [
                    new ChangeAuthorPeriodManifestHead
                    {
                        Id = "default",
                        ObjectId = new string('a', 40),
                    },
                    new ChangeAuthorPeriodManifestHead
                    {
                        Id = "open-change",
                        ObjectId = new string('b', 40),
                    },
                ],
            },
        ],
    };
}
