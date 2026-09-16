using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.Tests;

public sealed class ReviewedVendorManifestTests
{
    private const string Library = "/*! Synthetic Widget. Copyright Example Vendor. MIT. */\nexport function clamp(x) { return x < 0 ? 0 : x; }\n";

    [Theory]
    [InlineData("common/widget.js")]
    [InlineData("Plugin/widget.js")]
    [InlineData("src/widget.js")]
    public async Task ReviewedBodiesAreExcludedWhileOwnedSourceSurvives(string path)
    {
        InMemoryRepository repository = new();
        repository.WriteText(path, Library);
        repository.WriteText("src/adapter.js", "export function adapt(widget, value) { return widget(value); }");
        repository.WriteText("src/owned.js", "/*! Copyright My App. MIT. */ export function owned() { return true; }");
        RepositoryAnalysisPipeline pipeline = new(repository);
        RepositoryEvidence original = await pipeline.ScanAsync(repository.RootPath);
        ReviewedVendorManifest manifest = Manifest(path);
        RepositoryEvidence reviewed = await pipeline.ScanAsync(repository.RootPath, new() { VendorManifest = manifest });

        Assert.Contains("role:source", File(original, path).Tags);
        Assert.Contains("classification:vendored", File(reviewed, path).Tags);
        Assert.Contains("role:source", File(reviewed, "src/owned.js").Tags);
        Assert.Equal(ContractJson.Serialize(File(original, "src/adapter.js")), ContractJson.Serialize(File(reviewed, "src/adapter.js")));
        Assert.Equal(3, Functions(original));
        Assert.Equal(2, Functions(reviewed));
        Assert.NotEqual(original.Repository.SourceDigest, reviewed.Repository.SourceDigest);
        EvidenceFact decision = Assert.Single(reviewed.Facts, fact => fact.Kind == "ownership-decision");
        Assert.Equal(EvidenceSourceKind.DeclaredAssumed, decision.Provenance.SourceKind);
        Assert.Contains("ownership-manifest:" + ReviewedVendorManifestValidation.ComputeDigest(manifest), decision.Tags);
        string json = ContractJson.Serialize(reviewed);
        Assert.DoesNotContain("private-review-rationale", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-provenance", json, StringComparison.Ordinal);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.RepositoryEvidence, json).IsValid);
        Assert.Empty(ContractValidation.Validate(reviewed));
        Assert.Equal(ContractJson.Serialize(original), ContractJson.Serialize(await pipeline.ScanAsync(repository.RootPath)));
    }

    [Fact]
    public async Task ReviewedDecisionsNeverContaminateFileCaches()
    {
        InMemoryRepository repository = new();
        repository.WriteText("common/widget.js", Library);
        InMemoryScanCacheStore cache = new();
        RepositoryAnalysisArtifactCache artifacts = new();
        RepositoryAnalysisPipeline pipeline = new(repository, cache, artifacts);
        RepositoryScanOptions options = new() { CachePath = Path.Combine(Path.GetDirectoryName(repository.RootPath)!, "review-cache.json") };
        RepositoryEvidence original = await pipeline.ScanAsync(repository.RootPath, options);
        RepositoryEvidence reviewed = await pipeline.ScanAsync(repository.RootPath, options with { VendorManifest = Manifest("common/widget.js") });
        Assert.Contains("classification:vendored", File(reviewed, "common/widget.js").Tags);
        Assert.Equal(ContractJson.Serialize(original), ContractJson.Serialize(await pipeline.ScanAsync(repository.RootPath, options)));
        Assert.Equal(ContractJson.Serialize(reviewed), ContractJson.Serialize(await pipeline.ScanAsync(repository.RootPath, options with { VendorManifest = Manifest("common/widget.js") })));
        repository.WriteText("common/widget.js", Library.Replace("x < 0", "x > 0", StringComparison.Ordinal));
        await Assert.ThrowsAsync<InvalidDataException>(() => pipeline.ScanAsync(repository.RootPath, options with { VendorManifest = Manifest("common/widget.js") }));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("changed")]
    [InlineData("link")]
    [InlineData("ignored")]
    [InlineData("excluded-directory")]
    public async Task UnverifiableDecisionsFailInsteadOfDiscardingSource(string state)
    {
        InMemoryRepository repository = new();
        string path = state == "excluded-directory" ? "vendor/widget.js" : "common/widget.js";
        if (state != "missing") repository.WriteText(path, state == "changed" ? Library + "// local adaptation" : Library);
        if (state == "link") repository.SetAttributes(path, FileAttributes.ReparsePoint);
        if (state == "ignored") repository.WriteText(".efforthoursignore", "common/widget.js\n");
        await Assert.ThrowsAsync<InvalidDataException>(() => new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath, new() { VendorManifest = Manifest(path) }));
    }

    [Theory]
    [InlineData("../escape.js")]
    [InlineData("/absolute.js")]
    [InlineData("C:/absolute.js")]
    [InlineData("common\\widget.js")]
    [InlineData("common//widget.js")]
    [InlineData("common/./widget.js")]
    [InlineData("common/*.js")]
    public void UnsafePathsAreRejected(string path) => Assert.NotEmpty(ReviewedVendorManifestValidation.Validate(Manifest(path)));

    [Fact]
    public void ManifestSchemaBoundsIdentityAndPrivateMetadataAreExplicit()
    {
        ReviewedVendorManifest manifest = Manifest("common/widget.js");
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ReviewedVendorManifest, ContractJson.Serialize(manifest)).IsValid);
        Assert.Empty(ReviewedVendorManifestValidation.Validate(manifest));
        Assert.NotEmpty(ReviewedVendorManifestValidation.Validate(manifest with { ProtocolVersion = "future" }));
        Assert.NotEmpty(ReviewedVendorManifestValidation.Validate(manifest with { Files = [manifest.Files[0], manifest.Files[0] with { Path = "COMMON/widget.js" }] }));
        Assert.NotEmpty(ReviewedVendorManifestValidation.Validate(manifest with { Files = [manifest.Files[0] with { Sha256 = new string('a', 63) }] }));
        Assert.NotEmpty(ReviewedVendorManifestValidation.Validate(manifest with { Files = [manifest.Files[0] with { Classification = "customized" }] }));
        Assert.NotEmpty(ReviewedVendorManifestValidation.Validate(manifest with { Files = [manifest.Files[0] with { Rationale = "" }] }));
        Assert.NotEmpty(ReviewedVendorManifestValidation.Validate(manifest with { Files = [manifest.Files[0] with { Rationale = new string('a', 2049) }] }));
        Assert.NotEmpty(ReviewedVendorManifestValidation.Validate(manifest with { Files = [.. Enumerable.Repeat(manifest.Files[0], 4097)] }));
        ReviewedVendorManifest pair = manifest with { Files = [manifest.Files[0], manifest.Files[0] with { Path = "Plugin/widget.js" }] };
        Assert.Equal(ReviewedVendorManifestValidation.ComputeDigest(pair), ReviewedVendorManifestValidation.ComputeDigest(pair with { Files = [.. pair.Files.Reverse()] }));
        Assert.NotEqual(ReviewedVendorManifestValidation.ComputeDigest(manifest), ReviewedVendorManifestValidation.ComputeDigest(manifest with { Files = [manifest.Files[0] with { Rationale = "Different reviewed decision" }] }));
    }

    private static ReviewedVendorManifest Manifest(string path) => new()
    {
        Files = [new ReviewedVendorEntry
        {
            Path = path, Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Library))).ToLowerInvariant(),
            Library = "Synthetic Widget", Provenance = "private-provenance", Rationale = "private-review-rationale",
        }],
    };
    private static EvidenceFact File(RepositoryEvidence evidence, string path) => evidence.Facts.Single(fact => fact.Id == "file:" + path);
    private static decimal Functions(RepositoryEvidence evidence) => evidence.Facts.Where(fact => fact.Id.StartsWith("javascript:source-structure:", StringComparison.Ordinal))
        .SelectMany(fact => fact.Measurements).Where(measurement => measurement.Name == "functions").Sum(measurement => measurement.Value);
}
