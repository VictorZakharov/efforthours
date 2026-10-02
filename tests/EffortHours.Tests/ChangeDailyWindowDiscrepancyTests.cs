using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    // Diagnostic baseline. An independent-day mode must invert this relation.
    [Fact]
    public async Task JointRangeAllocationChangesADayDespiteIdenticalSelectedDayInputs()
    {
        DateTimeOffset first = new(2026, 1, 19, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset second = first.AddDays(1);
        ChangeAuthorPeriodManifest manifest = Manifest() with
        {
            Selection = Manifest().Selection with { SinceInclusive = first, UntilExclusive = second.AddDays(1) },
        };
        ChangePortfolioCandidate earlier = await OverlappingCandidateAsync("earlier", first.AddHours(12), 1);
        ChangePortfolioCandidate target = await OverlappingCandidateAsync("target", second.AddHours(12), 2);
        ChangePortfolioComparisonBucket firstBucket = DailyBucket("first", first);
        ChangePortfolioComparisonBucket targetBucket = DailyBucket("target", second);
        ChangePortfolioComparisonBuildOptions Options(ChangeAuthorPeriodManifest selected,
            params ChangePortfolioComparisonBucket[] buckets) => BuildOptions(selected) with
            {
                SourceManifest = selected,
                CapacityManifest = null,
                Buckets = buckets,
                ExecutionOverride = FixedExecution(selected) with
                {
                    Repositories = [.. FixedExecution(selected).Repositories.Select(repository => repository with
                {
                    SelectedChangeCount = buckets.Length, AdmittedChangeCount = buckets.Length,
                })],
                },
                BucketManifest = new ChangePortfolioBucketManifest
                {
                    Buckets = [.. buckets.Select(bucket => new ChangePortfolioBucketDefinition
                {
                    Id = bucket.Id, Label = bucket.Label, SinceInclusive = bucket.SinceInclusive,
                    UntilExclusive = bucket.UntilExclusive,
                })],
                },
            };
        ChangePortfolioComparisonReport combined = ChangePortfolioComparisonBuilder.Build(
            ChangePortfolioReconciler.Reconcile(Selection(manifest), [earlier, target], EstimationProfile.Implementation),
            Options(manifest, firstBucket, targetBucket));
        ChangeAuthorPeriodManifest oneDay = manifest with
        {
            Selection = manifest.Selection with { SinceInclusive = second },
        };
        ChangePortfolioComparisonReport isolated = ChangePortfolioComparisonBuilder.Build(
            ChangePortfolioReconciler.Reconcile(Selection(oneDay), [target], EstimationProfile.Implementation),
            Options(oneDay, targetBucket));

        ChangePortfolioComparisonPoint fromCombined = combined.Series.Single(series =>
            series.Kind == ChangePortfolioSeriesKind.Portfolio).Points[1];
        ChangePortfolioComparisonPoint fromOneDay = isolated.Series.Single(series =>
            series.Kind == ChangePortfolioSeriesKind.Portfolio).Points[0];
        Assert.Equal(1, fromCombined.SelectedChangeCount);
        Assert.Equal(fromCombined.SelectedChangeCount, fromOneDay.SelectedChangeCount);
        Assert.Equal(ContractJson.SerializeCompact(combined.SourcePortfolio!.Items.Single(item =>
            item.SelectorId == target.SelectorId).Selection),
            ContractJson.SerializeCompact(isolated.SourcePortfolio!.Items.Single().Selection));
        Assert.NotEqual(fromOneDay.Effort.Expected, fromCombined.Effort.Expected);
        Assert.True(fromCombined.Effort.Expected < fromOneDay.Effort.Expected);
        Assert.Empty(ContractValidation.Validate(combined));
        Assert.Empty(ContractValidation.Validate(isolated));
        AssertSchema(SchemaNames.ChangePortfolioComparisonReport, ContractJson.Serialize(combined));
    }

    private static ChangePortfolioComparisonBucket DailyBucket(string id, DateTimeOffset since) => new()
    {
        Id = id,
        Label = id,
        SinceInclusive = since,
        UntilExclusive = since.AddDays(1),
    };

    private static async Task<ChangePortfolioCandidate> OverlappingCandidateAsync(string id, DateTimeOffset timestamp, int value)
    {
        const string project = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>";
        InMemoryChangeSnapshot before = new(("Demo.csproj", project),
            ("Feature.cs", "namespace Demo; public class Feature { public int Value => 0; }"));
        InMemoryChangeSnapshot after = new(("Demo.csproj", project),
            ("Feature.cs", "namespace Demo; public class Feature { public int Value => " + value + "; }"));
        ChangeEstimateReport report = await new ChangeEstimator().EstimateAsync(new GitChangePlan
        {
            RepositoryPath = "virtual",
            Selection = new ChangeSelection
            {
                Kind = ChangeSelectionKind.Commit,
                Base = Reference("base", before.ObjectId),
                Head = Reference("head", after.ObjectId),
                Commit = after.ObjectId,
            },
            OpenBaseAsync = _ => Task.FromResult<IChangeSnapshot>(before),
            OpenHeadAsync = _ => Task.FromResult<IChangeSnapshot>(after),
        }, EstimationProfile.Implementation);
        return new ChangePortfolioCandidate
        {
            RepositoryId = "repository-a",
            SelectorId = id,
            Report = report,
            Attribution = new ChangePortfolioAttribution
            {
                Kind = ChangePortfolioAttributionKind.DirectAuthor,
                ParentCount = 1,
                SelectedTimestamp = timestamp,
                HeadIds = ["default"],
                ContributorMatches = [Match("contributor-a", ChangePortfolioContributorMatchKind.DirectAuthor)],
            },
        };
    }
}
