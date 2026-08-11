using System.Text.Json;
using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ChangePortfolioCommandTests
{
    private const string ProjectFile =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework>" +
        "</PropertyGroup></Project>\n";

    [Fact]
    public async Task RepeatedPullRequestCommandComposesImmutablePlansWithoutStorage()
    {
        SnapshotState initial = State(("Demo.csproj", ProjectFile));
        Dictionary<string, GitChangePlan> plans = new(StringComparer.Ordinal)
        {
            ["11"] = Plan(
                11,
                initial,
                State(
                    ("Demo.csproj", ProjectFile),
                    ("Alpha.cs", "namespace Demo; public sealed class Alpha { }\n"))),
            ["12"] = Plan(
                12,
                initial,
                State(
                    ("Demo.csproj", ProjectFile),
                    ("Beta.cs", "namespace Demo; public sealed class Beta { }\n"))),
        };
        List<string> requested = [];
        ChangePortfolioCommand command = new(
            new ChangeEstimator(),
            (repository, input, githubRepository, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.Equal("virtual-repository", repository);
                Assert.Equal("example/project", githubRepository);
                requested.Add(input);
                return Task.FromResult(plans[input]);
            },
            (_, _, _) => throw new InvalidOperationException("Author planner was not expected."));
        StringWriter stdout = new();
        StringWriter stderr = new();

        int exitCode = await command.ExecuteAsync(
            [
                "virtual-repository",
                "--pr", "11",
                "--pr", "12",
                "--repo", "example/project",
                "--no-rate",
                "--compact",
            ],
            stdout,
            stderr,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Equal(["11", "12"], requested);
        using JsonDocument report = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("pull-requests", report.RootElement.GetProperty("selection").GetProperty("kind").GetString());
        Assert.Equal(2, report.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(
            report.RootElement.GetProperty("totalEffort").GetProperty("expected").GetDecimal(),
            report.RootElement.GetProperty("items").EnumerateArray()
                .Sum(item => item.GetProperty("allocatedExpectedHours").GetDecimal()));
    }

    [Fact]
    public async Task RepeatingTheSamePullRequestProducesDistinctRowsAndOneAllocation()
    {
        SnapshotState initial = State(("Demo.csproj", ProjectFile));
        GitChangePlan plan = Plan(
            13,
            initial,
            State(
                ("Demo.csproj", ProjectFile),
                ("Repeated.cs", "namespace Demo; public sealed class Repeated { }\n")));
        ChangePortfolioCommand command = new(
            new ChangeEstimator(),
            (_, _, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(plan);
            },
            (_, _, _) => throw new InvalidOperationException("Author planner was not expected."));
        StringWriter stdout = new();

        int exitCode = await command.ExecuteAsync(
            ["virtual-repository", "--pr", "13", "--pr", "13", "--no-rate", "--compact"],
            stdout,
            new StringWriter(),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        using JsonDocument report = JsonDocument.Parse(stdout.ToString());
        JsonElement[] items = [.. report.RootElement.GetProperty("items").EnumerateArray()];
        Assert.Equal(["pr:13", "pr:13:duplicate-2"], [.. items
            .Select(item => item.GetProperty("selectorId").GetString()!)
            .Order(StringComparer.Ordinal)]);
        Assert.Single(items, item => item.TryGetProperty("duplicateOfItemId", out _));
        Assert.Single(items, item => item.GetProperty("allocatedExpectedHours").GetDecimal() == 0m);
    }

    [Fact]
    public async Task ManifestCommandPreservesCallerRepositoryIdsWithoutHostPaths()
    {
        SnapshotState initial = State(("Demo.csproj", ProjectFile));
        GitChangePlan first = Plan(
            21,
            initial,
            State(
                ("Demo.csproj", ProjectFile),
                ("Api.cs", "namespace Demo; public sealed class Api { }\n"))) with
        {
            RepositoryPath = "virtual-api",
        };
        GitChangePlan second = Plan(
            22,
            initial,
            State(
                ("Demo.csproj", ProjectFile),
                ("Web.cs", "namespace Demo; public sealed class Web { }\n"))) with
        {
            RepositoryPath = "virtual-web",
        };
        Dictionary<string, GitChangePlan> plans = new(StringComparer.Ordinal)
        {
            ["virtual-api"] = first,
            ["virtual-web"] = second,
        };
        ResolvedChangePortfolioManifestItem[] manifest =
        [
            Resolved("api-row", "api", "virtual-api", "21", "example/api"),
            Resolved("web-row", "web", "virtual-web", "22", "example/web"),
        ];
        ChangePortfolioCommand command = new(
            new ChangeEstimator(),
            (repository, _, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(plans[repository]);
            },
            (_, _, _) => throw new InvalidOperationException("Author planner was not expected."),
            (path, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.Equal("portfolio.json", path);
                return Task.FromResult<IReadOnlyList<ResolvedChangePortfolioManifestItem>>(manifest);
            });
        StringWriter stdout = new();
        StringWriter stderr = new();

        int exitCode = await command.ExecuteAsync(
            ["--manifest", "portfolio.json", "--no-rate", "--compact"],
            stdout,
            stderr,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.DoesNotContain("virtual-api", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("virtual-web", stdout.ToString(), StringComparison.Ordinal);
        using JsonDocument report = JsonDocument.Parse(stdout.ToString());
        Assert.True(report.RootElement.GetProperty("selection").GetProperty("manifestBased").GetBoolean());
        Assert.Equal(
            ["api", "web"],
            [.. report.RootElement.GetProperty("repositoryGroups").EnumerateArray().Select(group => group.GetProperty("repositoryId").GetString()!)]);
        Assert.Contains(
            report.RootElement.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "FB5320");
    }

    [Fact]
    public async Task ManifestCannotSplitOneRepositoryAcrossSeveralIds()
    {
        SnapshotState initial = State(("Demo.csproj", ProjectFile));
        GitChangePlan plan = Plan(
            31,
            initial,
            State(
                ("Demo.csproj", ProjectFile),
                ("Feature.cs", "namespace Demo; public sealed class Feature { }\n")));
        ResolvedChangePortfolioManifestItem[] manifest =
        [
            Resolved("first-row", "first", "first-path", "31", "example/project"),
            Resolved("second-row", "second", "second-path", "32", "example/project"),
        ];
        ChangePortfolioCommand command = new(
            new ChangeEstimator(),
            (_, _, _, _) => Task.FromResult(plan),
            (_, _, _) => throw new InvalidOperationException("Author planner was not expected."),
            (_, _) => Task.FromResult<IReadOnlyList<ResolvedChangePortfolioManifestItem>>(manifest));
        StringWriter stderr = new();

        int exitCode = await command.ExecuteAsync(
            ["--manifest", "portfolio.json", "--no-rate"],
            new StringWriter(),
            stderr,
            CancellationToken.None);

        Assert.Equal(CliExitCodes.InvalidInput, exitCode);
        Assert.Contains("same local Git repository", stderr.ToString(), StringComparison.Ordinal);
    }

    private static GitChangePlan Plan(int number, SnapshotState before, SnapshotState after) => new()
    {
        RepositoryPath = "virtual-repository",
        Selection = new ChangeSelection
        {
            Kind = ChangeSelectionKind.PullRequest,
            Base = Reference("base", before.ObjectId),
            Head = Reference("head", after.ObjectId),
            PullRequest = new PullRequestReference
            {
                Input = number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Number = number,
            },
        },
        OpenBaseAsync = before.OpenAsync,
        OpenHeadAsync = after.OpenAsync,
    };

    private static ResolvedChangePortfolioManifestItem Resolved(
        string id,
        string repositoryId,
        string repositoryPath,
        string pullRequest,
        string githubRepository) => new(
            new ChangePortfolioManifestItem
            {
                Id = id,
                RepositoryId = repositoryId,
                RepositoryPath = repositoryPath,
                PullRequest = pullRequest,
                GitHubRepository = githubRepository,
            },
            repositoryPath);

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.GitTree,
    };

    private static SnapshotState State(params (string Path, string Content)[] files)
    {
        InMemoryChangeSnapshot snapshot = new(files);
        return new SnapshotState(snapshot.ObjectId, InMemoryChangeSnapshot.Factory(files));
    }

    private sealed record SnapshotState(
        string ObjectId,
        Func<CancellationToken, Task<IChangeSnapshot>> OpenAsync);
}
