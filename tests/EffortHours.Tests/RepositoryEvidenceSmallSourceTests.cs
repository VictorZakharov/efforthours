using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class RepositoryEvidenceSmallSourceTests
{
    [Fact]
    public async Task MixedSmallDeltaMatchesColdWithSharedContextAndRejectsContextMutation()
    {
        Dictionary<string, string> older = new()
        {
            ["source.cs"] = "public class Source { public int Value() => 099; }",
            ["web/source.ts"] = "export function value() { return 099; }",
            ["query.sql"] = "CREATE TABLE item (id INT DEFAULT 099);",
            ["web/package.json"] = "{\"name\":\"web\"}",
            ["App.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\" />",
        };
        Dictionary<string, string> before = older.ToDictionary(item => item.Key,
            item => item.Value.Replace("099", "100", StringComparison.Ordinal));
        Dictionary<string, string> after = before.ToDictionary(item => item.Key,
            item => item.Value.Replace("100", "101", StringComparison.Ordinal));
        RepositoryAnalysisArtifactCache cache = new();
        RepositoryVersionedAnalysisCache versions = new();
        VersionedInMemoryRepository parent = new(before, older, cache, versions);
        VersionedInMemoryRepository current = new(after, before, cache, versions);
        RepositoryEvidence previous = await new RepositoryAnalysisPipeline(parent, analysisArtifactCache: cache)
            .ScanAsync(parent.RootPath);
        string[] paths = ["query.sql", "source.cs", "web/source.ts"];
        RepositoryEvidence? derived = await RepositoryEvidenceIncrementalDeriver.TryDeriveAsync(previous,
            current, current.RootPath, paths, CancellationToken.None);
        Assert.NotNull(derived);
        RepositoryEvidence cold = await new RepositoryAnalysisPipeline(current, analysisArtifactCache: cache)
            .ScanAsync(current.RootPath);
        Assert.Equal(ContractJson.Serialize(cold), ContractJson.Serialize(derived with { Repository = cold.Repository }));
        after["web/package.json"] = "{\"name\":\"changed\"}";
        VersionedInMemoryRepository changed = new(after, before, cache, versions);
        Assert.Null(await RepositoryEvidenceIncrementalDeriver.TryDeriveAsync(previous,
            changed, changed.RootPath, paths, CancellationToken.None));
    }

    [Theory]
    [InlineData("source.ts", "export function value() { return 100; }", "export function value() { return 101; }")]
    [InlineData("source.sql", "CREATE TABLE item (id INT DEFAULT 100);", "CREATE TABLE item (id INT DEFAULT 101);")]
    public async Task ExactLocalProofMatchesColdEvidenceAndAllProfiles(string path, string before, string after)
    {
        RepositoryAnalysisArtifactCache cache = new();
        RepositoryVersionedAnalysisCache versions = new();
        VersionedInMemoryRepository parent = new(path, before, "before", "older", cache, versions);
        VersionedInMemoryRepository current = new(path, after, "after", "before", cache, versions);
        RepositoryEvidence previous = await new RepositoryAnalysisPipeline(parent, analysisArtifactCache: cache)
            .ScanAsync(parent.RootPath);
        RepositoryEvidence? derived = await RepositoryEvidenceIncrementalDeriver.TryDeriveAsync(previous,
            current, current.RootPath, [path], CancellationToken.None);
        Assert.NotNull(derived);
        RepositoryEvidence cold = await new RepositoryAnalysisPipeline(current, analysisArtifactCache: cache)
            .ScanAsync(current.RootPath);
        derived = derived with { Repository = cold.Repository };
        Assert.Equal(ContractJson.Serialize(cold), ContractJson.Serialize(derived));
        SeedEstimator estimator = new();
        foreach (EstimationProfile profile in Enum.GetValues<EstimationProfile>())
            Assert.Equal(ContractJson.Serialize(estimator.Estimate(cold, profile)),
                ContractJson.Serialize(ChangeEstimator.RefreshDerivedEstimate(estimator.Estimate(previous, profile), derived)));
    }

    [Theory]
    [InlineData("source.ts", "export function value() { return 100; }", "export function value() { if (1){}  ; }")]
    [InlineData("source.sql", "CREATE TABLE item (id INT DEFAULT 100);", "CREATE TABLE item (id INT DEFAULT 101);", true)]
    public async Task UnprovenLocalChangesFallBack(string path, string before, string after, bool changedContext = false)
    {
        RepositoryAnalysisArtifactCache cache = new();
        RepositoryVersionedAnalysisCache versions = new();
        VersionedInMemoryRepository parent = new(path, before, "before", "older", cache, versions);
        VersionedInMemoryRepository current = new(path, after, "after", "before", cache, versions);
        RepositoryEvidence previous = await new RepositoryAnalysisPipeline(parent, analysisArtifactCache: cache)
            .ScanAsync(parent.RootPath);
        if (changedContext) current.RepositoryPathSetIdentity = "changed";
        Assert.Null(await RepositoryEvidenceIncrementalDeriver.TryDeriveAsync(previous,
            current, current.RootPath, [path], CancellationToken.None));
    }
}
