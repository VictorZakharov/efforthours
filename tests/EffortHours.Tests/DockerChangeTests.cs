using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class DockerChangeTests
{
    [Fact]
    public async Task DockerfileCommentsCaseAndLayoutOnlyChangeHasZeroEffort()
    {
        const string before = "# old note\nFROM scratch\nCOPY app /app\nCMD [\"/app\"]\n";
        const string after = "# new note\n  from scratch\n\n  COPY app /app\n# another note\ncmd [\"/app\"]\n";

        ChangeEstimateReport report = await EstimateAsync(
            State(("Dockerfile", before)),
            State(("Dockerfile", after)));

        Assert.Equal(ChangePathClassification.FormattingOnly, Assert.Single(report.Evidence.Paths).Classification);
        Assert.Equal(0m, report.TotalEffort.Expected);
    }

    [Fact]
    public async Task ComposeCommentsAndIndentWidthOnlyChangeHasZeroEffort()
    {
        const string before =
            "services:\n  app:\n    image: example/app\n    ports:\n      - 8080:8080\n";
        const string after =
            "# new note\nservices :\n    app:\n        image :  example/app # inline note\n        ports:\n            - 8080:8080\n";

        ChangeEstimateReport report = await EstimateAsync(
            State(("compose.yml", before)),
            State(("compose.yml", after)));

        Assert.Equal(ChangePathClassification.FormattingOnly, Assert.Single(report.Evidence.Paths).Classification);
        Assert.Equal(0m, report.TotalEffort.Expected);
    }

    [Fact]
    public async Task DockerIgnoreCommentsAndBlankLinesOnlyChangeHasZeroEffort()
    {
        ChangeEstimateReport report = await EstimateAsync(
            State((".dockerignore", "# old\nbin/\n!bin/example\n")),
            State((".dockerignore", "\n# new\n  bin/  \n!bin/example\n")));

        Assert.Equal(ChangePathClassification.FormattingOnly, Assert.Single(report.Evidence.Paths).Classification);
        Assert.Equal(0m, report.TotalEffort.Expected);
    }

    [Theory]
    [InlineData("# syntax=docker/dockerfile:1\nFROM scratch\n", "# syntax=docker/dockerfile:1.9\nFROM scratch\n")]
    [InlineData("FROM example/app:1\n", "FROM example/app:2\n")]
    [InlineData("FROM scratch\nRUN echo ready\n", "FROM scratch\nRUN --mount=type=secret,id=token echo ready\n")]
    [InlineData("FROM scratch\nCMD [\"one\"]\n", "FROM scratch\nCMD [\"two\"]\n")]
    public async Task DockerfileSemanticChangesRemainMeaningful(string before, string after)
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("Dockerfile", before)),
            State(("Dockerfile", after)));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
        Assert.Contains(report.Categories, item =>
            item.Category == EffortCategory.PackagingDeploymentAndReleaseArtifacts && item.Hours.Expected > 0m);
    }

    [Fact]
    public async Task ComposeTopologyRuntimeAndSecurityChangesReachPackagingCategory()
    {
        const string before = "services:\n  app:\n    image: example/app:1\n";
        const string after =
            "services:\n  app:\n    image: example/app:2\n    ports:\n      - 8080:8080\n" +
            "    depends_on:\n      - database\n    healthcheck:\n      test: [CMD, health]\n" +
            "    read_only: true\n    secrets:\n      - token\n  database:\n    image: example/database\n" +
            "secrets:\n  token:\n    file: ./token.txt\n";

        ChangeEstimateReport report = await EstimateAsync(
            State(("compose.yml", before)),
            State(("compose.yml", after)));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
        Assert.Contains(report.Categories, item =>
            item.Category == EffortCategory.PackagingDeploymentAndReleaseArtifacts && item.Hours.Expected > 0m);
        Assert.Equal("change-seed/0.17.0+seed-rules/0.4.0", report.EstimatorVersion);
    }

    [Fact]
    public async Task DockerIgnoreRuleChangeRemainsMeaningful()
    {
        ChangeEstimateReport report = await EstimateAsync(
            State((".dockerignore", "bin/\n")),
            State((".dockerignore", "bin/\nprivate/\n")));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
        Assert.Contains(report.Categories, item =>
            item.Category == EffortCategory.PackagingDeploymentAndReleaseArtifacts);
    }

    [Fact]
    public async Task ComposeBlockScalarFormattingFailsClosed()
    {
        const string before = "services:\n  app:\n    command: |\n      echo ready\n";
        const string after = "services:\n    app:\n        command: |\n            echo ready\n";

        ChangeEstimateReport report = await EstimateAsync(
            State(("compose.yml", before)),
            State(("compose.yml", after)));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
    }

    [Fact]
    public async Task AddedDockerStackUsesCurrentVersionAndDoesNotInventProductionCode()
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(),
            State(
                ("Dockerfile", "FROM scratch\nCOPY app /app\nENTRYPOINT [\"/app\"]\n"),
                ("compose.yml", "services:\n  app:\n    build: .\n    restart: unless-stopped\n"),
                (".dockerignore", "bin/\n")));

        Assert.Equal("change-seed/0.17.0+seed-rules/0.4.0", report.EstimatorVersion);
        Assert.Contains(report.Categories, item =>
            item.Category == EffortCategory.PackagingDeploymentAndReleaseArtifacts && item.Hours.Expected > 0m);
        Assert.DoesNotContain(report.Categories, item =>
            item.Category == EffortCategory.ProductionImplementation && item.Hours.Expected > 0m);
    }

    private static Task<ChangeEstimateReport> EstimateAsync(ChangeState before, ChangeState after) =>
        new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "in-memory-docker-change",
                Selection = new ChangeSelection
                {
                    Kind = ChangeSelectionKind.BaseHead,
                    Base = Reference("base", before.ObjectId),
                    Head = Reference("head", after.ObjectId),
                },
                OpenBaseAsync = before.OpenAsync,
                OpenHeadAsync = after.OpenAsync,
            },
            EstimationProfile.Implementation);

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.GitTree,
    };

    private static ChangeState State(params (string Path, string Content)[] files)
    {
        InMemoryChangeSnapshot snapshot = new(files);
        return new ChangeState(snapshot.ObjectId, InMemoryChangeSnapshot.Factory(files));
    }

    private sealed record ChangeState(
        string ObjectId,
        Func<CancellationToken, Task<IChangeSnapshot>> OpenAsync);
}
