using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class CliTests
{
    [Fact]
    public async Task VendorManifestScanEstimateAndSavedEvidenceRetainDecisions()
    {
        using TemporaryRepository repository = new();
        using TemporaryRepository outputs = new();
        const string library = "/*! Synthetic vendor. MIT. */ export function clamp(x) { return x < 0 ? 0 : x; }";
        repository.WriteText("common/widget.js", library);
        repository.WriteText("adapter.js", "export function adapt(widget, value) { return widget(value); }");
        string manifestPath = Path.Combine(outputs.RootPath, "vendor.json");
        ReviewedVendorManifest manifest = VendorCliManifest(library);
        await File.WriteAllTextAsync(manifestPath, ContractJson.Serialize(manifest));
        string evidencePath = Path.Combine(outputs.RootPath, "scan.json");
        ProcessResult scan = await RunCliAsync("scan", repository.RootPath,
            "--vendor-manifest", manifestPath, "--output", evidencePath);
        Assert.Equal(0, scan.ExitCode);
        Assert.Empty(scan.StandardOutput);
        Assert.Empty(scan.StandardError);
        RepositoryEvidence evidence = ContractJson.Deserialize<RepositoryEvidence>(await File.ReadAllTextAsync(evidencePath));
        Assert.Contains(evidence.Facts, fact => fact.Id == "ownership:reviewed-vendor:common/widget.js");
        ProcessResult direct = await RunCliAsync("estimate", repository.RootPath, "--vendor-manifest", manifestPath, "--no-rate");
        ProcessResult saved = await RunCliAsync("estimate", evidencePath, "--no-rate");
        Assert.Equal(0, direct.ExitCode);
        Assert.Equal(0, saved.ExitCode);
        Assert.Equal(direct.StandardOutput, saved.StandardOutput);
        ProcessResult rejected = await RunCliAsync("estimate", evidencePath, "--vendor-manifest", manifestPath);
        Assert.NotEqual(0, rejected.ExitCode);
        Assert.Empty(rejected.StandardOutput);
        Assert.Contains("saved evidence cannot be reclassified", rejected.StandardError, StringComparison.Ordinal);
        Assert.Equal(library, await File.ReadAllTextAsync(Path.Combine(repository.RootPath, "common/widget.js")));
        repository.WriteText("common/widget.js", library + " // maintained adaptation");
        ProcessResult stale = await RunCliAsync("estimate", repository.RootPath, "--vendor-manifest", manifestPath, "--no-rate");
        Assert.NotEqual(0, stale.ExitCode);
        Assert.Empty(stale.StandardOutput);
        Assert.Contains("hash no longer matches", stale.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReviewedVendorRehashesChangedBytesWithIdenticalLengthAndTimestamp()
    {
        using TemporaryRepository repository = new();
        using TemporaryRepository outputs = new();
        const string library = "export function clamp(x) { return x < 0 ? 0 : x; }";
        repository.WriteText("common/widget.js", library);
        string sourcePath = Path.Combine(repository.RootPath, "common/widget.js");
        DateTime timestamp = File.GetLastWriteTimeUtc(sourcePath);
        string manifestPath = Path.Combine(outputs.RootPath, "vendor.json");
        string cachePath = Path.Combine(outputs.RootPath, "cache.json");
        await File.WriteAllTextAsync(manifestPath, ContractJson.Serialize(VendorCliManifest(library)));
        ProcessResult first = await RunCliAsync("scan", repository.RootPath, "--cache", cachePath,
            "--vendor-manifest", manifestPath);
        Assert.Equal(0, first.ExitCode);
        repository.WriteText("common/widget.js", library.Replace("x < 0", "x > 0", StringComparison.Ordinal));
        File.SetLastWriteTimeUtc(sourcePath, timestamp);
        ProcessResult changed = await RunCliAsync("scan", repository.RootPath, "--cache", cachePath,
            "--vendor-manifest", manifestPath);
        Assert.NotEqual(0, changed.ExitCode);
        Assert.Empty(changed.StandardOutput);
        Assert.Contains("hash no longer matches", changed.StandardError, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{not json}")]
    [InlineData("oversized")]
    public async Task InvalidVendorManifestProducesNoAggregate(string input)
    {
        using TemporaryRepository repository = new();
        using TemporaryRepository outputs = new();
        repository.WriteText("app.js", "export const ready = true;");
        string path = Path.Combine(outputs.RootPath, "vendor.json");
        await File.WriteAllTextAsync(path, input == "oversized" ? new string('x', ReviewedVendorManifest.MaximumBytes + 1) : input);
        ProcessResult result = await RunCliAsync("estimate", repository.RootPath, "--vendor-manifest", path, "--no-rate");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.NotEmpty(result.StandardError);
    }

    private static ReviewedVendorManifest VendorCliManifest(string library) => new()
    {
        Files = [new ReviewedVendorEntry
        {
            Path = "common/widget.js",
            Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(library))).ToLowerInvariant(),
            Library = "Synthetic fixture", Provenance = "Repository MIT fixture", Rationale = "Reviewed unmodified third-party body",
        }],
    };
}
