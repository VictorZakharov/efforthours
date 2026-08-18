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

    private sealed class FixedPullRequestResolver(string baseObjectId, string headObjectId)
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
            ProcessStartInfo startInfo = new("git")
            {
                WorkingDirectory = RootPath,
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
