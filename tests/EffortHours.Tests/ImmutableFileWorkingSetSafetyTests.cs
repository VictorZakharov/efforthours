using System.Collections.Concurrent;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Analyzers.JavaScript;
using EffortHours.Analyzers.Sql;
using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ImmutableFileWorkingSetTests
{
    [Theory]
    [InlineData("sql", "app/sql/File0.sql", "FB6001", 8388608)]
    [InlineData("frontend", "web/component.html", "FB4201", 4194304)]
    public async Task CachedLocalAnalysisStillEnforcesDeclaredByteBounds(
        string stage, string path, string code, int limit)
    {
        RepositoryAnalysisArtifactCache cache = new(4, workingSet: new());
        GitArchiveSnapshot snapshot = Snapshot(cache);
        CountingFileSystem fs = new(snapshot, new(StringComparer.Ordinal), false);
        RepositoryEvidence common = await new RepositoryScanner(fs, analysisArtifactCache: cache)
            .ScanAsync(snapshot.RootPath);
        IRepositoryEvidenceAnalyzer analyzer = Analyzer(stage, fs);
        _ = await analyzer.AnalyzeAsync(snapshot.RootPath, common);
        RepositoryEvidence oversized = common with
        {
            Facts = [.. common.Facts.Select(fact => fact.Id == "file:" + path ? fact with
            {
                Measurements = [.. fact.Measurements.Select(measurement => measurement.Name == "bytes"
                    ? measurement with { Value = limit + 1 } : measurement)],
            } : fact)],
        };
        RepositoryAnalysisContribution result = await analyzer.AnalyzeAsync(snapshot.RootPath, oversized);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code &&
            diagnostic.Locations.Any(location => location.Path == path));
    }

    [Theory]
    [InlineData("sql", "FB6001")]
    [InlineData("frontend", "FB4201")]
    public async Task TransientLocalReadFailuresAreNotRetained(string stage, string code)
    {
        RepositoryAnalysisArtifactCache cache = new(4, workingSet: new());
        GitArchiveSnapshot snapshot = Snapshot(cache);
        ConcurrentDictionary<string, int> reads = new(StringComparer.Ordinal);
        CountingFileSystem normal = new(snapshot, reads, false);
        RepositoryEvidence common = await new RepositoryScanner(normal, analysisArtifactCache: cache)
            .ScanAsync(snapshot.RootPath);
        RepositoryAnalysisContribution failed = await Analyzer(stage, new CountingFileSystem(snapshot, reads, true))
            .AnalyzeAsync(snapshot.RootPath, common);
        Assert.Contains(failed.Diagnostics, diagnostic => diagnostic.Code == code);
        RepositoryAnalysisContribution recovered = await Analyzer(stage, normal).AnalyzeAsync(snapshot.RootPath, common);
        Assert.DoesNotContain(recovered.Diagnostics, diagnostic => diagnostic.Code == code);
        Assert.NotEmpty(recovered.Facts);
    }

    [Fact]
    public async Task SqlCacheIdentityDoesNotInspectOutsideScope()
    {
        RepositoryAnalysisArtifactCache cache = new(4, workingSet: new());
        GitArchiveSnapshot snapshot = Snapshot(cache);
        ConcurrentDictionary<string, int> reads = new(StringComparer.Ordinal);
        CountingFileSystem fs = new(snapshot, reads, false);
        RepositoryEvidence common = await new RepositoryScanner(fs, analysisArtifactCache: cache)
            .ScanAsync(snapshot.RootPath);
        EvidenceFact file = common.Facts.Single(fact => fact.Id == "file:app/sql/File0.sql");
        RepositoryEvidence outside = common with
        {
            Facts = [file with { Id = "file:../outside.sql", Scope = "../outside.sql",
                Locations = [new EvidenceLocation { Path = "../outside.sql" }] }],
        };
        RepositoryAnalysisContribution result = await new SqlRepositoryAnalyzer(fs)
            .AnalyzeAsync(snapshot.RootPath, outside);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "FB6001");
        Assert.DoesNotContain(reads.Keys, key => key.Contains("outside", StringComparison.Ordinal));
    }

    private static GitArchiveSnapshot Snapshot(RepositoryAnalysisArtifactCache cache) =>
        new(Files().ToDictionary(pair => pair.Key, pair => Encoding.UTF8.GetBytes(pair.Value),
            StringComparer.Ordinal), cache);

    private static IRepositoryEvidenceAnalyzer Analyzer(string stage, IRepositoryFileSystem fs) =>
        stage == "sql" ? new SqlRepositoryAnalyzer(fs) : new JavaScriptRepositoryAnalyzer(fs);
}
