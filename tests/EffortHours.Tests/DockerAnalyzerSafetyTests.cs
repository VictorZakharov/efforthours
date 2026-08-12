using System.Security.Cryptography;
using EffortHours.Analysis;
using EffortHours.Analyzers.Docker;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.Tests;

public sealed class DockerAnalyzerSafetyTests
{
    private const string Analyzer = "efforthours.docker-analyzer";

    [Fact]
    public async Task DigestChangesAfterScanningAreRejected()
    {
        InMemoryRepository repository = new();
        repository.WriteText("Dockerfile", "FROM scratch\n");
        RepositoryEvidence common = await new RepositoryScanner(repository).ScanAsync(repository.RootPath);
        repository.WriteText("Dockerfile", "FROM private-registry.example/sentinel\n");

        RepositoryAnalysisContribution contribution = await new DockerRepositoryAnalyzer(repository)
            .AnalyzeAsync(repository.RootPath, common);

        Assert.Contains(contribution.Diagnostics, diagnostic => diagnostic.Code == "FB8901" &&
            diagnostic.Message.Contains("changed after common scanning", StringComparison.Ordinal));
        Assert.DoesNotContain(contribution.Facts, fact => fact.Provenance.Analyzer == Analyzer &&
            fact.Locations.Any(location => location.Path == "Dockerfile"));
        Assert.DoesNotContain("private-registry.example/sentinel", ContractJson.Serialize(contribution),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidUtf8AndEscapingScopesFailClosedWithoutValueDisclosure()
    {
        InMemoryRepository repository = new();
        repository.WriteText("Dockerfile", "FROM scratch\n");
        RepositoryEvidence common = await new RepositoryScanner(repository).ScanAsync(repository.RootPath);
        EvidenceFact file = Assert.Single(common.Facts, fact => fact.Id == "file:Dockerfile");
        byte[] invalid = [0xc3, 0x28];
        repository.WriteBytes("Dockerfile", invalid);
        EvidenceFact invalidFact = file with
        {
            Tags = [.. file.Tags.Where(tag => !tag.StartsWith("sha256:", StringComparison.Ordinal)),
                $"sha256:{Convert.ToHexString(SHA256.HashData(invalid)).ToLowerInvariant()}"],
            Measurements = [.. file.Measurements.Select(measurement => measurement.Name == "bytes"
                ? measurement with { Value = invalid.Length }
                : measurement)],
        };
        DockerTextReader reader = new(repository, repository.RootPath);

        DockerTextReadResult invalidResult = await reader.ReadAsync(invalidFact, CancellationToken.None);
        DockerTextReadResult escapingResult = await reader.ReadAsync(
            invalidFact with { Scope = "../private/Dockerfile" }, CancellationToken.None);

        Assert.Equal("FB8901", invalidResult.Diagnostic?.Code);
        Assert.Contains("not valid UTF-8", invalidResult.Diagnostic?.Message, StringComparison.Ordinal);
        Assert.Equal("FB8901", escapingResult.Diagnostic?.Code);
        Assert.Contains("outside repository scope", escapingResult.Diagnostic?.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ff", ContractJson.Serialize(invalidResult.Diagnostic), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Utf16BomIsRejectedRatherThanAutoDetected()
    {
        InMemoryRepository repository = new();
        byte[] utf16 = [0xff, 0xfe, 0x46, 0x00, 0x52, 0x00, 0x4f, 0x00, 0x4d, 0x00];
        repository.WriteBytes("Dockerfile", utf16);
        RepositoryEvidence common = await new RepositoryScanner(repository).ScanAsync(repository.RootPath);
        EvidenceFact file = Assert.Single(common.Facts, fact => fact.Id == "file:Dockerfile");

        DockerTextReadResult result = await new DockerTextReader(repository, repository.RootPath)
            .ReadAsync(file, CancellationToken.None);

        Assert.Equal("FB8901", result.Diagnostic?.Code);
        Assert.Contains("not valid UTF-8", result.Diagnostic?.Message, StringComparison.Ordinal);
        Assert.Null(result.Text);
    }

    [Fact]
    public async Task DeclaredOversizedArtifactIsRejectedBeforeReading()
    {
        InMemoryRepository repository = new();
        repository.WriteText("compose.yml", "services: {}\n");
        RepositoryEvidence common = await new RepositoryScanner(repository).ScanAsync(repository.RootPath);
        EvidenceFact file = Assert.Single(common.Facts, fact => fact.Id == "file:compose.yml");
        EvidenceFact oversized = file with
        {
            Measurements = [.. file.Measurements.Select(measurement => measurement.Name == "bytes"
                ? measurement with { Value = DockerTextReader.MaximumBytes + 1 }
                : measurement)],
        };

        DockerTextReadResult result = await new DockerTextReader(repository, repository.RootPath)
            .ReadAsync(oversized, CancellationToken.None);

        Assert.Equal("FB8901", result.Diagnostic?.Code);
        Assert.Contains("eight-megabyte", result.Diagnostic?.Message, StringComparison.Ordinal);
        Assert.Null(result.Text);
    }

    [Fact]
    public async Task MalformedAndBoundedDynamicSyntaxRemainsVisibleAtLowConfidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText("Dockerfile", "FROM \"unterminated\nRUN echo private-value\n");
        repository.WriteText(
            "compose.yml",
            "services:\n\tapp:\n    image: [unterminated\n---\nservices: {}\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        string json = ContractJson.Serialize(evidence);

        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8902");
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == Analyzer &&
            fact.Tags.Contains("parser-confidence:low", StringComparer.Ordinal));
        Assert.DoesNotContain("private-value", json, StringComparison.Ordinal);
        Assert.Empty(ContractValidation.Validate(evidence));
    }
}
