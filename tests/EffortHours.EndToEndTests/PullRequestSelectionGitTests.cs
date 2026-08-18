using System.Diagnostics;
using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed class PullRequestSelectionGitTests
{
    private const string ProjectFile =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework>" +
        "</PropertyGroup></Project>\n";

    [Fact]
    public async Task DivergedBaseTipUsesMergeBaseAndExcludesBaseOnlyDrift()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Demo.csproj", ProjectFile);
        string branchPoint = await repository.CommitAsync("base");

        await repository.GitAsync("switch", "--quiet", "-c", "feature");
        repository.WriteText("Feature.cs", "namespace Demo; public sealed class Feature { }\n");
        string headObjectId = await repository.CommitAsync("feature");

        await repository.GitAsync("switch", "--quiet", "main");
        repository.WriteText("BaseOnly.cs", "namespace Demo; public sealed class BaseOnly { }\n");
        string providerBaseTip = await repository.CommitAsync("unrelated base change");

        GitChangePlanner planner = new(
            new GitClient(),
            new FixedPullRequestResolver(providerBaseTip, headObjectId));
        GitChangePlan pullRequestPlan = await planner.PlanPullRequestAsync(
            repository.RootPath,
            "17",
            "acme/demo");
        GitChangePlan explicitPlan = await planner.PlanBaseHeadAsync(
            repository.RootPath,
            branchPoint,
            headObjectId);
        GitChangePlan directProviderTipPlan = await planner.PlanBaseHeadAsync(
            repository.RootPath,
            providerBaseTip,
            headObjectId);
        GitChangePlan commitPlan = await planner.PlanCommitAsync(
            repository.RootPath,
            headObjectId,
            parentRevision: null);

        ChangeEstimator estimator = new();
        ChangeEstimateReport pullRequest = await estimator.EstimateAsync(
            pullRequestPlan,
            EstimationProfile.Implementation);
        ChangeEstimateReport explicitDelta = await estimator.EstimateAsync(
            explicitPlan,
            EstimationProfile.Implementation);
        ChangeEstimateReport directProviderTip = await estimator.EstimateAsync(
            directProviderTipPlan,
            EstimationProfile.Implementation);
        ChangeEstimateReport commit = await estimator.EstimateAsync(
            commitPlan,
            EstimationProfile.Implementation);

        Assert.Equal(branchPoint, pullRequest.Selection.Base.ObjectId);
        Assert.Equal(headObjectId, pullRequest.Selection.Head.ObjectId);
        Assert.Equal(providerBaseTip, pullRequest.Selection.PullRequest!.ProviderBaseObjectId);
        Assert.Equal(
            PullRequestComparisonBasePolicy.ProviderBaseHeadMergeBase,
            pullRequest.Selection.PullRequest.ComparisonBasePolicy);
        Assert.Equal(
            PullRequestObjectAcquisition.LocalReuse,
            pullRequest.Selection.PullRequest.ObjectAcquisition);
        Assert.Equal(1, pullRequest.Selection.PullRequest.ProviderChangedFileCount);
        Assert.Equal(1, pullRequest.Selection.PullRequest.AnalyzedChangedPathCount);
        Assert.Equal(1, pullRequest.Selection.PullRequest.RepresentedChangedPathCount);
        Assert.Equal(PullRequestPathCountStatus.Match, pullRequest.Selection.PullRequest.PathCountStatus);
        Assert.Equal(explicitDelta.Evidence.BaseEvidenceDigest, pullRequest.Evidence.BaseEvidenceDigest);
        Assert.Equal(explicitDelta.Evidence.HeadEvidenceDigest, pullRequest.Evidence.HeadEvidenceDigest);
        Assert.Equal(explicitDelta.TotalEffort, pullRequest.TotalEffort);
        Assert.Equal(commit.TotalEffort, pullRequest.TotalEffort);
        ChangePathEvidence path = Assert.Single(pullRequest.Evidence.Paths);
        Assert.Equal("Feature.cs", path.Path);
        Assert.DoesNotContain(
            pullRequest.Evidence.Paths,
            candidate => string.Equals(candidate.Path, "BaseOnly.cs", StringComparison.Ordinal));
        Assert.Equal(2, directProviderTip.Evidence.Paths.Count);
        Assert.Contains(
            directProviderTip.Evidence.Paths,
            candidate => candidate.Path == "BaseOnly.cs" && candidate.Status == ChangePathStatus.Removed);
        Assert.NotEqual(directProviderTip.TotalEffort, pullRequest.TotalEffort);
    }

    [Fact]
    public async Task ExplicitFetchAcquiresMissingObjectsWithoutChangingCheckoutState()
    {
        using GitFixture provider = await GitFixture.CreateAsync();
        provider.WriteText("Demo.csproj", ProjectFile);
        string branchPoint = await provider.CommitAsync("base");
        using GitFixture local = await GitFixture.CloneAsync(provider.RootPath);

        provider.WriteText("BaseOnly.cs", "namespace Demo; public sealed class BaseOnly { }\n");
        string providerBaseTip = await provider.CommitAsync("base drift");
        await provider.GitAsync("switch", "--quiet", "-c", "feature", branchPoint);
        provider.WriteText("Feature.cs", "namespace Demo; public sealed class Feature { }\n");
        string headObjectId = await provider.CommitAsync("feature");
        await provider.GitAsync("update-ref", "refs/pull/17/head", headObjectId);

        GitClient git = new();
        Assert.False(await git.CommitExistsAsync(local.RootPath, providerBaseTip));
        Assert.False(await git.CommitExistsAsync(local.RootPath, headObjectId));
        string refsBefore = await local.GitAsync("for-each-ref", "--format=%(refname):%(objectname)");
        string statusBefore = await local.GitAsync("status", "--porcelain=v1");
        string indexBefore = await local.GitAsync("write-tree");
        byte[]? fetchHeadBefore = await local.ReadGitFileAsync("FETCH_HEAD");

        GitChangePlanner planner = new(
            git,
            new FixedPullRequestResolver(
                providerBaseTip,
                headObjectId,
                changedFileCount: 1,
                baseRefName: "main",
                fetchSource: provider.RootPath));
        InvalidOperationException defaultFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            planner.PlanPullRequestAsync(local.RootPath, "17", "acme/demo"));
        Assert.Contains("--fetch-missing", defaultFailure.Message, StringComparison.Ordinal);
        Assert.False(await git.CommitExistsAsync(local.RootPath, providerBaseTip));
        Assert.False(await git.CommitExistsAsync(local.RootPath, headObjectId));
        GitChangePlan plan = await planner.PlanPullRequestAsync(
            local.RootPath,
            "17",
            "acme/demo",
            fetchMissing: true);
        ChangeEstimateReport report = await new ChangeEstimator().EstimateAsync(
            plan,
            EstimationProfile.Implementation);

        Assert.True(await git.CommitExistsAsync(local.RootPath, providerBaseTip));
        Assert.True(await git.CommitExistsAsync(local.RootPath, headObjectId));
        Assert.Equal(refsBefore, await local.GitAsync("for-each-ref", "--format=%(refname):%(objectname)"));
        Assert.Equal(statusBefore, await local.GitAsync("status", "--porcelain=v1"));
        Assert.Equal(indexBefore, await local.GitAsync("write-tree"));
        Assert.Equal(fetchHeadBefore, await local.ReadGitFileAsync("FETCH_HEAD"));
        Assert.Equal(branchPoint, report.Selection.Base.ObjectId);
        Assert.Equal(providerBaseTip, report.Selection.PullRequest!.ProviderBaseObjectId);
        Assert.Equal(PullRequestObjectAcquisition.ExplicitFetch, report.Selection.PullRequest.ObjectAcquisition);
        Assert.Equal(PullRequestPathCountStatus.Match, report.Selection.PullRequest.PathCountStatus);
        Assert.Contains(plan.Diagnostics, diagnostic => diagnostic.Code == "FB5106");
    }

    private sealed class FixedPullRequestResolver(
        string baseObjectId,
        string headObjectId,
        int? changedFileCount = 1,
        string? baseRefName = null,
        string? fetchSource = null)
        : IPullRequestResolver
    {
        public Task<ResolvedPullRequest> ResolveAsync(
            string repositoryPath,
            string input,
            string? repository,
            CancellationToken cancellationToken = default)
        {
            _ = repositoryPath;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ResolvedPullRequest
            {
                BaseObjectId = baseObjectId,
                HeadObjectId = headObjectId,
                BaseRefName = baseRefName,
                FetchSource = fetchSource,
                ChangedFileCount = changedFileCount,
                Reference = new PullRequestReference
                {
                    Input = input,
                    Number = int.Parse(input, System.Globalization.CultureInfo.InvariantCulture),
                    Repository = repository,
                },
            });
        }
    }

    private sealed class GitFixture : IDisposable
    {
        private GitFixture(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static async Task<GitFixture> CreateAsync()
        {
            string rootPath = Path.Combine(
                Path.GetTempPath(),
                "efforthours-pr-selection-e2e",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);
            GitFixture fixture = new(rootPath);
            await fixture.GitAsync("init", "--initial-branch=main");
            await fixture.GitAsync("config", "user.name", "EffortHours E2E");
            await fixture.GitAsync("config", "user.email", "efforthours-e2e@example.invalid");
            return fixture;
        }

        public static async Task<GitFixture> CloneAsync(string sourcePath)
        {
            string parentPath = Path.Combine(
                Path.GetTempPath(),
                "efforthours-pr-selection-e2e");
            Directory.CreateDirectory(parentPath);
            string rootPath = Path.Combine(parentPath, Guid.NewGuid().ToString("N"));
            await RunGitAsync(parentPath, "clone", "--quiet", sourcePath, rootPath);
            return new GitFixture(rootPath);
        }

        public void WriteText(string relativePath, string content)
        {
            string path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public async Task<string> CommitAsync(string message)
        {
            await GitAsync("add", "--all");
            await GitAsync("commit", "--quiet", "-m", message);
            return await GitAsync("rev-parse", "HEAD");
        }

        public async Task<string> GitAsync(params string[] arguments)
        {
            return await RunGitAsync(RootPath, arguments);
        }

        public async Task<byte[]?> ReadGitFileAsync(string name)
        {
            string gitDirectory = await GitAsync("rev-parse", "--git-dir");
            string path = Path.GetFullPath(Path.Combine(RootPath, gitDirectory, name));
            return File.Exists(path) ? await File.ReadAllBytesAsync(path) : null;
        }

        private static async Task<string> RunGitAsync(
            string workingDirectory,
            params string[] arguments)
        {
            ProcessStartInfo startInfo = new("git")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start Git.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
            string output = (await stdout).Trim();
            string error = (await stderr).Trim();
            Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {error}");
            return output;
        }

        public void Dispose()
        {
            foreach (string file in Directory.EnumerateFiles(RootPath, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(RootPath, recursive: true);
        }
    }
}
