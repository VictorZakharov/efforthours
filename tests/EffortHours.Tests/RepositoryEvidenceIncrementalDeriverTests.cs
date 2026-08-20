using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class RepositoryEvidenceIncrementalDeriverTests
{
    private const string PathName = "src/Example.cs";

    [Fact]
    public async Task NumericLiteralEditMatchesColdRepositoryAnalysis()
    {
        const string before = "public sealed class Example { public int Value { get; } = 100; }";
        const string after = "public sealed class Example { public int Value { get; } = 101; }";
        Fixture fixture = await CreateFixtureAsync(before, after);

        RepositoryEvidence? derived = await RepositoryEvidenceIncrementalDeriver.TryDeriveAsync(
            fixture.Previous,
            fixture.Current,
            fixture.Current.RootPath,
            [PathName],
            CancellationToken.None);
        RepositoryEvidence cold = await new RepositoryAnalysisPipeline(
            fixture.Current,
            new InMemoryScanCacheStore(),
            fixture.AnalysisCache).ScanAsync(fixture.Current.RootPath);

        Assert.NotNull(derived);
        RepositoryEvidence normalized = derived with { Repository = cold.Repository };
        Assert.Equal(ContractJson.Serialize(cold), ContractJson.Serialize(normalized));

        RepositoryEvidence previousScoped = WithScopeDiagnostic(
            fixture.Previous,
            "Previous snapshot scope.");
        RepositoryEvidence currentScoped = WithScopeDiagnostic(
            normalized,
            "Current snapshot scope.");
        SeedEstimator estimator = new();
        EstimateReport previousEstimate = estimator.Estimate(
            previousScoped,
            EstimationProfile.Implementation);
        EstimateReport expectedEstimate = estimator.Estimate(
            currentScoped,
            EstimationProfile.Implementation);
        EstimateReport refreshed = ChangeEstimator.RefreshDerivedEstimate(
            previousEstimate,
            currentScoped);
        Assert.Equal(
            ContractJson.Serialize(expectedEstimate),
            ContractJson.Serialize(refreshed));
    }

    [Fact]
    public async Task UnchangedExactScopeReusesEvidenceAndDropsPriorScopeDiagnostic()
    {
        const string source = "public sealed class Example { public int Value { get; } = 100; }";
        Fixture fixture = await CreateFixtureAsync(source, source);
        RepositoryEvidence previous = fixture.Previous with
        {
            Diagnostics =
            [
                .. fixture.Previous.Diagnostics,
                new Diagnostic
                {
                    Code = "FB5205",
                    Severity = DiagnosticSeverity.Information,
                    Message = "Previous exact-scope identity.",
                },
            ],
        };

        RepositoryEvidence? derived = await RepositoryEvidenceIncrementalDeriver.TryDeriveAsync(
            previous,
            fixture.Current,
            fixture.Current.RootPath,
            [],
            CancellationToken.None);

        Assert.NotNull(derived);
        Assert.Same(previous.Facts, derived.Facts);
        Assert.DoesNotContain(derived.Diagnostics, diagnostic => diagnostic.Code == "FB5205");
    }

    [Theory]
    [InlineData("structural")]
    [InlineData("length")]
    [InlineData("duplicate")]
    [InlineData("duplicate-current")]
    [InlineData("generated")]
    [InlineData("syntax")]
    public async Task UnsafeOrEvidenceChangingEditUsesFullAnalysisFallback(string scenario)
    {
        string before = scenario == "syntax"
            ? "public sealed class Example { public int First() => 100; "
            : "public sealed class Example { public int First() => 100; }";
        string after = scenario switch
        {
            "structural" => "public sealed class Example { public int Other() => 100; }",
            "length" => "public sealed class Example { public int First() => 1000; }",
            "syntax" => "public sealed class Example { public int First() => 101; ",
            _ => "public sealed class Example { public int First() => 101; }",
        };
        Fixture fixture = await CreateFixtureAsync(before, after);
        RepositoryEvidence previous = scenario switch
        {
            "duplicate" => AddDuplicateDigest(fixture.Previous),
            "duplicate-current" => AddDuplicateDigest(fixture.Previous, Sha256(after)),
            "generated" => MarkGenerated(fixture.Previous),
            _ => fixture.Previous,
        };

        RepositoryEvidence? derived = await RepositoryEvidenceIncrementalDeriver.TryDeriveAsync(
            previous,
            fixture.Current,
            fixture.Current.RootPath,
            [PathName],
            CancellationToken.None);

        Assert.Null(derived);
    }

    private static async Task<Fixture> CreateFixtureAsync(string before, string after)
    {
        RepositoryAnalysisArtifactCache analysisCache = new();
        RepositoryVersionedAnalysisCache versionCache = new();
        VersionedInMemoryRepository parent = new(
            PathName,
            before,
            "blob-before",
            "blob-grandparent",
            analysisCache,
            versionCache);
        VersionedInMemoryRepository current = new(
            PathName,
            after,
            "blob-after",
            "blob-before",
            analysisCache,
            versionCache);
        RepositoryEvidence previous = await new RepositoryAnalysisPipeline(
            parent,
            new InMemoryScanCacheStore(),
            analysisCache).ScanAsync(parent.RootPath);
        return new Fixture(previous, current, analysisCache);
    }

    private static RepositoryEvidence AddDuplicateDigest(
        RepositoryEvidence evidence,
        string? replacementSha256 = null)
    {
        EvidenceFact original = FileFact(evidence);
        EvidenceFact duplicate = original with
        {
            Id = "file:src/Duplicate.cs",
            Scope = "src/Duplicate.cs",
            Locations = [new EvidenceLocation { Path = "src/Duplicate.cs" }],
            Tags = replacementSha256 is null
                ? original.Tags
                :
                [
                    .. original.Tags
                        .Where(tag => !tag.StartsWith("sha256:", StringComparison.Ordinal))
                        .Append($"sha256:{replacementSha256}")
                        .Order(StringComparer.Ordinal),
                ],
        };
        return evidence with { Facts = [.. evidence.Facts, duplicate] };
    }

    private static RepositoryEvidence MarkGenerated(RepositoryEvidence evidence)
    {
        EvidenceFact original = FileFact(evidence);
        EvidenceFact generated = original with
        {
            Tags = [.. original.Tags.Append("classification:generated").Order(StringComparer.Ordinal)],
        };
        return evidence with
        {
            Facts =
            [
                .. evidence.Facts.Select(fact => fact.Id == original.Id ? generated : fact),
            ],
        };
    }

    private static RepositoryEvidence WithScopeDiagnostic(
        RepositoryEvidence evidence,
        string message) => evidence with
        {
            Diagnostics =
            [
                .. evidence.Diagnostics.Where(diagnostic => diagnostic.Code != "FB5205"),
                new Diagnostic
                {
                    Code = "FB5205",
                    Severity = DiagnosticSeverity.Information,
                    Message = message,
                },
            ],
        };

    private static EvidenceFact FileFact(RepositoryEvidence evidence) =>
        Assert.Single(evidence.Facts, fact => fact.Id == $"file:{PathName}");

    private static string Sha256(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record Fixture(
        RepositoryEvidence Previous,
        VersionedInMemoryRepository Current,
        RepositoryAnalysisArtifactCache AnalysisCache);
}
