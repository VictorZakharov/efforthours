using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed class PullRequestVerificationTests
{
    private const string ProjectFile =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework>" +
        "</PropertyGroup></Project>";

    [Fact]
    public async Task ReportExposesComparisonProvenanceAndWarnsOnPathCountDrift()
    {
        SnapshotState before = State(("Demo.csproj", ProjectFile));
        SnapshotState after = State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", "namespace Demo; public sealed class Feature { }\n"));
        ChangeEstimateInput input = new()
        {
            RepositoryName = "in-memory-pr",
            Selection = new ChangeSelection
            {
                Kind = ChangeSelectionKind.PullRequest,
                Base = Reference("comparison-base", before.ObjectId),
                Head = Reference("head", after.ObjectId),
                PullRequest = new PullRequestReference
                {
                    Input = "42",
                    Number = 42,
                    ProviderBaseObjectId = before.ObjectId,
                    ComparisonBasePolicy = PullRequestComparisonBasePolicy.ProviderBaseHeadMergeBase,
                    ObjectAcquisition = PullRequestObjectAcquisition.LocalReuse,
                    ProviderChangedFileCount = 2,
                },
            },
            OpenBaseAsync = before.OpenAsync,
            OpenHeadAsync = after.OpenAsync,
        };

        ChangeEstimateReport report = await new ChangeEstimator().EstimateAsync(
            input,
            EstimationProfile.Implementation);
        PullRequestReference pullRequest = report.Selection.PullRequest!;
        string markdown = ChangeEstimateMarkdownRenderer.Render(report);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangeEstimateReport,
            ContractJson.Serialize(report));
        ChangeEvidence legacyEvidence = report.Evidence with
        {
            Selection = report.Evidence.Selection with
            {
                PullRequest = new PullRequestReference { Input = "42", Number = 42 },
            },
        };
        SchemaValidationResult legacySchema = ContractSchemaValidator.Validate(
            SchemaNames.ChangeEvidence,
            ContractJson.Serialize(legacyEvidence));
        ChangePortfolioReport portfolio = ChangePortfolioReconciler.Reconcile(
            new ChangePortfolioSelection { Kind = ChangePortfolioSelectionKind.PullRequests },
            [
                new ChangePortfolioCandidate
                {
                    RepositoryId = "repository",
                    SelectorId = "pr:42",
                    Report = report,
                    Attribution = new ChangePortfolioAttribution
                    {
                        Kind = ChangePortfolioAttributionKind.PullRequest,
                        MergeCommit = false,
                        ParentCount = 0,
                    },
                },
            ],
            EstimationProfile.Implementation);
        string portfolioMarkdown = ChangePortfolioMarkdownRenderer.Render(portfolio);

        Assert.Equal(1, pullRequest.AnalyzedChangedPathCount);
        Assert.Equal(1, pullRequest.RepresentedChangedPathCount);
        Assert.Equal(PullRequestPathCountStatus.Mismatch, pullRequest.PathCountStatus);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "FB5107");
        Assert.Contains("provider-base-head-merge-base", markdown, StringComparison.Ordinal);
        Assert.Contains("mismatch", markdown, StringComparison.Ordinal);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(legacyEvidence));
        Assert.True(legacySchema.IsValid, string.Join(Environment.NewLine, legacySchema.Errors));
        Assert.Contains(portfolio.Diagnostics, diagnostic => diagnostic.Code == "FB5107");
        Assert.Contains("Pull-request comparison verification", portfolioMarkdown, StringComparison.Ordinal);
        Assert.Contains("mismatch", portfolioMarkdown, StringComparison.Ordinal);
    }

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.GitTree,
    };

    private static SnapshotState State(params (string Path, string Content)[] files)
    {
        InMemoryChangeSnapshot snapshot = new(files);
        return new SnapshotState(snapshot.ObjectId, InMemoryChangeSnapshot.Factory(files));
    }

    private sealed record SnapshotState(
        string ObjectId,
        Func<CancellationToken, Task<IChangeSnapshot>> OpenAsync);
}
