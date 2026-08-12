using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class DockerAnalyzerTests
{
    private const string Analyzer = "efforthours.docker-analyzer";

    [Fact]
    public async Task DockerfileComposeAndIgnoreStructureProduceTraceableBoundedEvidenceAndEffort()
    {
        InMemoryRepository repository = RichRepository();

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);
        EvidenceFact dockerfile = AnalyzerFact(evidence, "containers/api.Dockerfile", "docker-artifact:dockerfile");
        EvidenceFact compose = AnalyzerFact(evidence, "deploy/compose.yml", "docker-artifact:compose");
        EvidenceFact ignore = AnalyzerFact(evidence, ".dockerignore", "docker-artifact:dockerignore");

        Assert.Equal("0.1.0", dockerfile.Provenance.AnalyzerVersion);
        Assert.Equal(2m, Measurement(dockerfile, "stages"));
        Assert.True(Measurement(dockerfile, "build-steps") >= 3m);
        Assert.Equal(1m, Measurement(dockerfile, "multi-stage-copies"));
        Assert.Equal(1m, Measurement(dockerfile, "health-checks"));
        Assert.Equal(1m, Measurement(dockerfile, "users"));
        Assert.Equal(1m, Measurement(dockerfile, "secret-or-ssh-mounts"));
        Assert.Equal(2m, Measurement(compose, "services"));
        Assert.Equal(1m, Measurement(compose, "build-definitions"));
        Assert.True(Measurement(compose, "ports") >= 1m);
        Assert.True(Measurement(compose, "dependencies") >= 1m);
        Assert.True(Measurement(compose, "security-settings") >= 1m);
        Assert.True(Measurement(compose, "secrets") >= 1m);
        Assert.Equal(1m, Measurement(compose, "local-dockerfile-references"));
        Assert.Equal(3m, Measurement(ignore, "ignore-rules"));
        Assert.Equal(1m, Measurement(ignore, "negated-rules"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Provenance.Analyzer == Analyzer &&
            fact.Locations.Any(location => location.Path == "containers/api.Dockerfile"));
        Assert.Contains(estimate.Categories, category =>
            category.Category == EffortCategory.PackagingDeploymentAndReleaseArtifacts &&
            category.Hours.Expected > 0m);
        Assert.DoesNotContain(estimate.Diagnostics, diagnostic => diagnostic.Code is "FB1001" or "FB1002");
        Assert.Empty(ContractValidation.Validate(evidence));
        Assert.Empty(ContractValidation.Validate(estimate));
    }

    [Fact]
    public async Task ConfiguredValuesAndSourceExcerptsAreNeverDisclosed()
    {
        InMemoryRepository repository = RichRepository();

        string json = ContractJson.Serialize(await ScanAsync(repository));

        Assert.DoesNotContain("private-registry.example/sentinel", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-secret-name", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-environment-value", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-ignore-pattern", json, StringComparison.Ordinal);
        Assert.Contains("source-excerpts:not-emitted", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenericYamlAndSimilarNamesDoNotCreateDockerAnalyzerFacts()
    {
        InMemoryRepository repository = new();
        repository.WriteText("config.yml", "services:\n  private-service: {}\n");
        repository.WriteText("docker-compose.txt", "services: {}\n");
        repository.WriteText("compose.json", "{}\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.DoesNotContain(evidence.Facts, fact => fact.Provenance.Analyzer == Analyzer);
        Assert.DoesNotContain("docker", evidence.Repository.Ecosystems);
    }

    [Fact]
    public async Task ExactDuplicateDockerBodiesAreValuedOnce()
    {
        const string source = "FROM scratch\nCOPY app /app\nENTRYPOINT [\"/app\"]\n";
        EstimateReport single = await EstimateAsync(("Dockerfile", source));
        EstimateReport duplicate = await EstimateAsync(("Dockerfile", source), ("copy.Dockerfile", source));

        Assert.Equal(single.TotalEffort, duplicate.TotalEffort);
        Assert.Equal(
            Category(single, EffortCategory.PackagingDeploymentAndReleaseArtifacts),
            Category(duplicate, EffortCategory.PackagingDeploymentAndReleaseArtifacts));
        Assert.Contains(duplicate.Diagnostics, diagnostic => diagnostic.Code == "FB8906");
    }

    [Fact]
    public async Task DockerCommentsAndLayoutDoNotIncreaseRepositoryEffort()
    {
        EstimateReport compact = await EstimateAsync(
            ("Dockerfile", "FROM scratch AS build\nCOPY app /app\nCMD [\"/app\"]\n"),
            ("compose.yml", "services:\n  app:\n    build: .\n    ports:\n      - 8080:8080\n"));
        EstimateReport formatted = await EstimateAsync(
            ("Dockerfile", "# ordinary note\nFROM scratch AS build\n\nCOPY app /app\n# another note\nCMD [\"/app\"]\n"),
            ("compose.yml", "# ordinary note\nservices:\n    app:\n        build: .\n        ports: [8080:8080]\n"));

        Assert.Equal(compact.TotalEffort, formatted.TotalEffort);
        Assert.Equal(
            Category(compact, EffortCategory.PackagingDeploymentAndReleaseArtifacts),
            Category(formatted, EffortCategory.PackagingDeploymentAndReleaseArtifacts));
    }

    [Fact]
    public async Task DynamicComposeFeaturesStayVisibleWithReducedConfidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "compose.yml",
            "x-defaults: &defaults\n  restart: unless-stopped\n" +
            "services:\n  app:\n    <<: *defaults\n    image: ${PRIVATE_IMAGE}\n    command: |\n      run-private-command\n" +
            "\n      services:\n        fake: value\n" +
            "include:\n  - private.compose.yml\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact compose = AnalyzerFact(evidence, "compose.yml", "docker-artifact:compose");

        Assert.True(Measurement(compose, "anchors-aliases-merges") >= 2m);
        Assert.Equal(1m, Measurement(compose, "interpolations"));
        Assert.Equal(1m, Measurement(compose, "block-scalars"));
        Assert.Equal(1m, Measurement(compose, "services"));
        Assert.Contains("parser-confidence:medium", compose.Tags);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8903");
        Assert.DoesNotContain("run-private-command", ContractJson.Serialize(evidence), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MultiDocumentAndInlineComposeRemainExplicitUncertainty()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "compose.yml",
            "services:\n  app: { image: first-image }\n---\nservices: { worker: { image: second-image } }\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact compose = AnalyzerFact(evidence, "compose.yml", "docker-artifact:compose");

        Assert.Equal(2m, Measurement(compose, "documents"));
        Assert.True(Measurement(compose, "dynamic-values") >= 2m);
        Assert.Contains("parser-confidence:low", compose.Tags);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8902");
    }

    [Fact]
    public async Task RootComposeBuildResolvesTheRootDockerfile()
    {
        InMemoryRepository repository = new();
        repository.WriteText("Dockerfile", "FROM scratch\n");
        repository.WriteText("compose.yml", "services:\n  app:\n    build: .\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact compose = AnalyzerFact(evidence, "compose.yml", "docker-artifact:compose");

        Assert.Equal(1m, Measurement(compose, "local-dockerfile-references"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Provenance.Analyzer == Analyzer &&
            fact.Locations.Any(location => location.Path == "Dockerfile"));
        Assert.DoesNotContain(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8904");
    }

    private static InMemoryRepository RichRepository()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "containers/api.Dockerfile",
            "# syntax=docker/dockerfile:1\n" +
            "FROM private-registry.example/sentinel:latest AS build\n" +
            "RUN --mount=type=secret,id=private-secret-name echo build\n" +
            "COPY src /src\nRUN build-command\n" +
            "FROM scratch AS runtime\nCOPY --from=build /src/app /app\n" +
            "ENV MODE=private-environment-value\nUSER 1000\nEXPOSE 8080\n" +
            "HEALTHCHECK CMD [\"/app\", \"health\"]\nENTRYPOINT [\"/app\"]\n");
        repository.WriteText(
            "deploy/compose.yml",
            "services:\n  api:\n    build:\n      context: ../containers\n      dockerfile: api.Dockerfile\n" +
            "    ports:\n      - 8080:8080\n    environment:\n      PRIVATE_MODE: private-environment-value\n" +
            "    depends_on:\n      database:\n        condition: service_healthy\n    read_only: true\n" +
            "    secrets:\n      - private-secret-name\n  database:\n    image: private-registry.example/sentinel\n" +
            "    healthcheck:\n      test: [CMD, health]\nnetworks:\n  private-network: {}\n" +
            "secrets:\n  private-secret-name:\n    file: ./private.txt\n");
        repository.WriteText(
            ".dockerignore",
            "private-ignore-pattern/\n**/*.secret\n!example.secret\n");
        return repository;
    }

    private static Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static async Task<EstimateReport> EstimateAsync(params (string Path, string Content)[] files)
    {
        InMemoryRepository repository = new();
        foreach ((string path, string content) in files) repository.WriteText(path, content);
        return new SeedEstimator().Estimate(await ScanAsync(repository), EstimationProfile.Implementation);
    }

    private static EvidenceFact AnalyzerFact(RepositoryEvidence evidence, string path, string tag) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.ContainerConfiguration &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains(tag, StringComparer.Ordinal) &&
            fact.Locations.Any(location => location.Path == path));

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;

    private static EffortRange Category(EstimateReport report, EffortCategory category) =>
        Assert.Single(report.Categories, item => item.Category == category).Hours;
}
