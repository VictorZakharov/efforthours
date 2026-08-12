using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class DockerScannerClassificationTests
{
    [Fact]
    public async Task ScannerClassifiesOnlyFilenameQualifiedDockerArtifacts()
    {
        InMemoryRepository repository = new();
        repository.WriteText("Dockerfile", "FROM scratch\n");
        repository.WriteText("containers/Dockerfile.dev", "FROM scratch\n");
        repository.WriteText("containers/api.Dockerfile", "FROM scratch\n");
        repository.WriteText("compose.yml", "services: {}\n");
        repository.WriteText("deploy/compose.override.yaml", "services: {}\n");
        repository.WriteText("docker-compose.test.yml", "services: {}\n");
        repository.WriteText(".dockerignore", "bin/\n");
        repository.WriteText("config.yml", "services:\n  app: {}\n");
        repository.WriteText("compose.json", "{}\n");
        repository.WriteText("docker-compose.txt", "services: {}\n");

        RepositoryEvidence evidence = await new RepositoryScanner(repository).ScanAsync(repository.RootPath);

        Assert.Contains("docker", evidence.Repository.Ecosystems);
        AssertContainerFile(evidence, "Dockerfile");
        AssertContainerFile(evidence, "containers/Dockerfile.dev");
        AssertContainerFile(evidence, "containers/api.Dockerfile");
        AssertContainerFile(evidence, "compose.yml");
        AssertContainerFile(evidence, "deploy/compose.override.yaml");
        AssertContainerFile(evidence, "docker-compose.test.yml");
        AssertContainerFile(evidence, ".dockerignore");
        AssertNotContainerFile(evidence, "config.yml");
        AssertNotContainerFile(evidence, "compose.json");
        AssertNotContainerFile(evidence, "docker-compose.txt");
        Assert.Equal("0.2.12", RepositoryScanner.AnalyzerVersion);
    }

    private static void AssertContainerFile(RepositoryEvidence evidence, string path)
    {
        EvidenceFact file = Assert.Single(evidence.Facts, fact => fact.Id == $"file:{path}");
        Assert.Contains("role:container-configuration", file.Tags);
        Assert.Contains("ecosystem:docker", file.Tags);
    }

    private static void AssertNotContainerFile(RepositoryEvidence evidence, string path)
    {
        EvidenceFact file = Assert.Single(evidence.Facts, fact => fact.Id == $"file:{path}");
        Assert.DoesNotContain("role:container-configuration", file.Tags);
        Assert.DoesNotContain("ecosystem:docker", file.Tags);
    }
}
