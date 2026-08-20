using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Analyzers.DotNet;

namespace EffortHours.Tests;

public sealed class CSharpVersionedAnalysisTests
{
    [Fact]
    public async Task NumericLiteralEditAdvancesEvidenceWithoutAnotherFullAnalysis()
    {
        RepositoryAnalysisArtifactCache analysisCache = new();
        RepositoryVersionedAnalysisCache versionCache = new();
        const string path = "src/Example.cs";
        const string before = "public sealed class Example { public int Value { get; } = 100; }";
        const string middle = "public sealed class Example { public int Value { get; } = 101; }";
        const string after = "public sealed class Example { public int Value { get; } = 102; }";
        VersionedInMemoryRepository parent = new(
            path,
            before,
            "blob-before",
            previousContentId: null,
            analysisCache,
            versionCache);
        VersionedInMemoryRepository middleFile = new(
            path,
            middle,
            "blob-middle",
            "blob-before",
            analysisCache,
            versionCache);
        VersionedInMemoryRepository child = new(
            path,
            after,
            "blob-after",
            "blob-middle",
            analysisCache,
            versionCache);

        _ = await AnalyzeAsync(parent, path, before);
        _ = await AnalyzeAsync(middleFile, path, middle);
        Assert.True(await CSharpEvidenceLineage.TryAdvanceEvidenceAsync(
            child,
            FullPath(child, path),
            path,
            Encoding.UTF8.GetBytes(after),
            CancellationToken.None));

        Assert.True(versionCache.TryGetExistingAsync(
            CSharpEvidenceLineage.ArtifactKey(
                "blob-after",
                path),
            out Task<CSharpEvidenceState>? childArtifact));
        CSharpEvidenceState state = await childArtifact;
        Assert.True(state.Text.ContentEquals(
            CSharpEvidenceLineage.CreateSourceText(Encoding.UTF8.GetBytes(after))));
    }

    [Fact]
    public async Task StructuralEditRejectsEvidenceReuseAndMatchesColdAnalysis()
    {
        RepositoryAnalysisArtifactCache analysisCache = new();
        RepositoryVersionedAnalysisCache versionCache = new();
        const string path = "src/Example.cs";
        const string before = "public sealed class Example { public int First() => 1; }";
        const string middle = "public sealed class Example { public int Second() => 1; }";
        const string after = "public sealed class Example { public int Third() => 1; }";
        VersionedInMemoryRepository parent = new(
            path,
            before,
            "blob-before",
            previousContentId: null,
            analysisCache,
            versionCache);
        VersionedInMemoryRepository middleFile = new(
            path,
            middle,
            "blob-middle",
            "blob-before",
            analysisCache,
            versionCache);
        VersionedInMemoryRepository child = new(
            path,
            after,
            "blob-after",
            "blob-middle",
            analysisCache,
            versionCache);

        _ = await AnalyzeAsync(parent, path, before);
        CSharpFileAnalysis middleAnalysis = await AnalyzeAsync(middleFile, path, middle);
        Assert.False(await CSharpEvidenceLineage.TryAdvanceEvidenceAsync(
            child,
            FullPath(child, path),
            path,
            Encoding.UTF8.GetBytes(after),
            CancellationToken.None));
        CSharpFileAnalysis childAnalysis = await AnalyzeAsync(child, path, after);
        InMemoryRepository coldRepository = new();
        coldRepository.WriteText(path, after);
        CSharpFileAnalysis coldAnalysis = await new CSharpFileAnalyzer(
            coldRepository,
            coldRepository.RootPath).AnalyzeAsync(
                path,
                Sha256(after),
                "project-a",
                isTestFile: false,
                CancellationToken.None);

        Assert.NotSame(middleAnalysis, childAnalysis);
        Assert.Equal(
            coldAnalysis.Structure with { CallableStructuralMetrics = [] },
            childAnalysis.Structure with { CallableStructuralMetrics = [] });
        Assert.Equal(
            coldAnalysis.Structure.CallableStructuralMetrics,
            childAnalysis.Structure.CallableStructuralMetrics);
        Assert.Equal(
            coldAnalysis.Facts.Select(fact => fact.Id),
            childAnalysis.Facts.Select(fact => fact.Id));
        Assert.Equal(
            coldAnalysis.Diagnostics.Select(diagnostic => diagnostic.Code),
            childAnalysis.Diagnostics.Select(diagnostic => diagnostic.Code));
        Assert.True(versionCache.TryGetExistingAsync(
            CSharpEvidenceLineage.ArtifactKey(
                "blob-after",
                path),
            out Task<CSharpEvidenceState>? childArtifact));
        CSharpEvidenceState state = await childArtifact;
        Assert.True(state.TemplateTree.GetText().ContentEquals(
            CSharpEvidenceLineage.CreateSourceText(Encoding.UTF8.GetBytes(after))));
    }

    private static async Task<CSharpFileAnalysis> AnalyzeAsync(
        VersionedInMemoryRepository fileSystem,
        string path,
        string source) => await new CSharpFileAnalyzer(
            fileSystem,
            fileSystem.RootPath).AnalyzeAsync(
                path,
                Sha256(source),
                "project-a",
                isTestFile: false,
                CancellationToken.None);

    private static string Sha256(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string FullPath(VersionedInMemoryRepository fileSystem, string path) =>
        fileSystem.GetFullPath(Path.Combine(
            fileSystem.RootPath,
            path.Replace('/', Path.DirectorySeparatorChar)));

}
