using System.Text.Json.Nodes;
using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Fact]
    public async Task ProviderDiagnosticsAreOptionalSchemaValidatedAndOutsideTheSemanticDigest()
    {
        DateTimeOffset since = new(2026, 8, 21, 4, 0, 0, TimeSpan.Zero);
        DateTimeOffset asOf = since.AddHours(10);
        ChangeAuthorPeriodManifest manifest = TodayManifest(since, asOf);
        ChangePortfolioCandidate candidate = await CandidateAsync(
            "today", asOf.AddHours(-1), [Match("me", ChangePortfolioContributorMatchKind.DirectAuthor)]);
        ChangePortfolioReport source = ChangePortfolioReconciler.Reconcile(
            Selection(manifest), [candidate], EstimationProfile.Implementation);
        ChangePortfolioComparisonInputs inputs = ChangePortfolioComparisonInputLoader.CreateTodayToDate(
            source.Selection.AuthorPeriodManifest!, asOf, 8m);
        ChangePortfolioComparisonBuildOptions options = TodayBuildOptions(manifest, inputs, asOf, 1);
        ChangePortfolioComparisonReport original = ChangePortfolioComparisonBuilder.Build(source, options);
        ChangePortfolioProviderDiagnostics diagnostics = new()
        {
            MetadataCacheStatus = "missing",
            IdentityResolution = "provider-linked-aliases",
            OpenPullRequestCandidateRepositoryCount = 1,
            DefaultHeadBatchCount = 1,
            DefaultHeadQueryCount = 2,
            OpenPullRequestAccountQueryCount = 1,
            OpenPullRequestQueryCount = 1,
            HistoricalPullRequests = new()
            {
                CandidateCount = 2,
                CacheHitCount = 2,
                CompletedCount = 2,
                SelectedCount = 1,
            },
            LastRequest = new()
            {
                Phase = "open-pr-discovery",
                Operation = "pull-inventory",
                Api = "graphql",
                Outcome = "success",
                ExitCode = 0,
                State = "complete",
                PageCount = 1,
                ElapsedMilliseconds = 2m,
            },
            RepositoryObservations = [new()
            {
                RepositoryDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest("repository"),
                Phase = "open-pr-discovery", State = "complete", QueryCount = 1, PageCount = 1,
                ElapsedMilliseconds = 20m, ElapsedKind = "cumulative-request", WallElapsedMilliseconds = 10m,
            }],
            Fallbacks = [new()
            {
                Phase = "default-head", Reason = "incomplete-history", RepositoryCount = 1,
            }],
        };
        ChangePortfolioComparisonReport observed = ChangePortfolioComparisonBuilder.Build(source, options with
        {
            Discovery = options.Discovery! with { ProviderDiagnostics = diagnostics },
        });

        Assert.Equal(original.Verification.SemanticDigest, observed.Verification.SemanticDigest);
        foreach (ChangePortfolioComparisonReport report in new[] { original, observed })
        {
            Assert.Empty(ContractValidation.Validate(report));
            SchemaValidationResult result = ContractSchemaValidator.Validate(
                SchemaNames.ChangePortfolioComparisonReport,
                new ChangePortfolioComparisonJsonRenderer().Render(report));
            Assert.True(result.IsValid, string.Join("\n", result.Errors));
        }

        JsonNode legacy = JsonNode.Parse(new ChangePortfolioComparisonJsonRenderer().Render(observed))!;
        JsonObject legacyDiagnostics = legacy["discovery"]!["providerDiagnostics"]!.AsObject();
        Assert.True(legacyDiagnostics.Remove("identityResolution"));
        Assert.True(legacyDiagnostics.Remove("openPullRequestCandidateRepositoryCount"));
        Assert.True(legacyDiagnostics.Remove("historicalPullRequests"));
        Assert.True(legacyDiagnostics.Remove("lastRequest"));
        JsonObject legacyObservation = legacyDiagnostics["repositoryObservations"]![0]!.AsObject();
        Assert.True(legacyObservation.Remove("elapsedKind"));
        Assert.True(legacyObservation.Remove("wallElapsedMilliseconds"));
        string legacyJson = legacy.ToJsonString();
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioComparisonReport, legacyJson).IsValid);
        ChangePortfolioComparisonReport restored = ContractJson.Deserialize<ChangePortfolioComparisonReport>(legacyJson);
        Assert.Empty(ContractValidation.Validate(restored));
        Assert.Equal("not-observed", restored.Discovery!.ProviderDiagnostics!.IdentityResolution);
        Assert.Equal(0, restored.Discovery.ProviderDiagnostics.OpenPullRequestCandidateRepositoryCount);
        Assert.Null(restored.Discovery.ProviderDiagnostics.HistoricalPullRequests);
        Assert.Null(restored.Discovery.ProviderDiagnostics.LastRequest);
        Assert.Null(restored.Discovery.ProviderDiagnostics.RepositoryObservations![0].ElapsedKind);
        Assert.Null(restored.Discovery.ProviderDiagnostics.RepositoryObservations[0].WallElapsedMilliseconds);
        Assert.Equal(original.Verification.SemanticDigest, restored.Verification.SemanticDigest);
        foreach (ChangePortfolioProviderDiagnostics invalid in new[]
        {
            diagnostics with { HistoricalPullRequests = diagnostics.HistoricalPullRequests! with { PendingCount = 1 } },
            diagnostics with { LastRequest = diagnostics.LastRequest! with { Operation = "private-operation" } },
            diagnostics with { LastRequest = diagnostics.LastRequest! with { Api = "private-url" } },
            diagnostics with { LastRequest = diagnostics.LastRequest! with { Outcome = "private-error" } },
            diagnostics with { LastRequest = diagnostics.LastRequest! with { TimeoutOwner = "private-process" } },
            diagnostics with { LastRequest = diagnostics.LastRequest! with { HttpStatus = 503 } },
            diagnostics with { RepositoryObservations = [diagnostics.RepositoryObservations![0] with { WallElapsedMilliseconds = 15m }] },
            diagnostics with { IdentityResolution = "private-login" },
            diagnostics with { OpenPullRequestCandidateRepositoryCount = -1 },
            diagnostics with { OpenPullRequestCandidateRepositoryCount = observed.Discovery!.ConsideredRepositoryCount + 1 },
        })
        {
            Assert.NotEmpty(ContractValidation.Validate(observed with
            {
                Discovery = observed.Discovery! with { ProviderDiagnostics = invalid },
            }));
        }

        Assert.Contains("default-head / incomplete-history / 1 repositories",
            ChangePortfolioTodayMarkdownRenderer.Render(observed), StringComparison.Ordinal);
        Assert.NotEmpty(ContractValidation.Validate(observed with
        {
            Discovery = observed.Discovery! with
            {
                ProviderDiagnostics = diagnostics with { MetadataCacheStatus = "private-path" },
            },
        }));
        Assert.NotEmpty(ContractValidation.Validate(observed with
        {
            Discovery = observed.Discovery! with
            {
                ProviderDiagnostics = diagnostics with { DefaultHeadQueryCount = 99 },
            },
        }));
    }
}
