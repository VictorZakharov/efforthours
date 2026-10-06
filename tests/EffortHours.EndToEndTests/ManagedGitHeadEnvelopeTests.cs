using System.Globalization;
using EffortHours.Change;

namespace EffortHours.EndToEndTests;

public sealed class ManagedGitHeadEnvelopeTests : ChangeCliTestSupport
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LargerHeadSetsUseBoundedFetchesAndRetainCompletedBatchesOnFailure(bool failSecondBatch)
    {
        using GitFixture provider = await GitFixture.CreateAsync();
        provider.WriteText("body.txt", "immutable fixture\n");
        string baseline = await provider.CommitAsync("base");
        string tree = await provider.GitAsync("rev-parse", baseline + "^{tree}");
        List<DiscoveredHead> heads = [];
        for (int index = 0; index < 70; index++)
        {
            string id = await provider.GitAsync("commit-tree", tree, "-p", baseline, "-m", "head " + index.ToString(CultureInfo.InvariantCulture));
            heads.Add(new("head-" + index.ToString(CultureInfo.InvariantCulture), id, id));
        }
        string root = Path.Combine(provider.RootPath, ".git", "private-cache");
        RecordingRunner runner = new() { FailSecond = failSecondBatch };
        GitHubRepositoryCache cache = new(runner, new GitClient(runner, (_, _, _) => throw new NotSupportedException()), root,
            _ => new Uri(provider.RootPath + Path.DirectorySeparatorChar).AbsoluteUri);
        if (failSecondBatch)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.EnsureAsync("example/project", heads, CancellationToken.None));
            runner.FailSecond = false;
        }
        RepositoryAcquisitionResult complete = await cache.EnsureAsync("example/project", heads, CancellationToken.None);
        Assert.Equal(failSecondBatch ? 32 : 0, complete.LocalHeadCount);
        Assert.Equal(failSecondBatch ? 38 : 70, complete.AcquiredHeadCount);
        Assert.All(runner.Fetches, arguments =>
        {
            Assert.InRange(arguments.SkipWhile(value => !value.StartsWith("file:", StringComparison.Ordinal)).Skip(1).Count(), 1, 32);
            Assert.InRange(arguments.Count(value => value.StartsWith("--negotiation-tip=", StringComparison.Ordinal)), 0, 32);
            Assert.Contains("--no-write-fetch-head", arguments);
        });
        Assert.All(runner.Fetches.Skip(1), arguments => Assert.Equal(32,
            arguments.Count(value => value.StartsWith("--negotiation-tip=", StringComparison.Ordinal))));
        int calls = runner.Fetches.Count;
        RepositoryAcquisitionResult warm = await cache.EnsureAsync("example/project", heads, CancellationToken.None);
        Assert.Equal(70, warm.LocalHeadCount);
        Assert.Equal(0, warm.AcquiredHeadCount);
        Assert.Equal(calls, runner.Fetches.Count);
        Assert.False(File.Exists(Path.Combine(complete.RepositoryPath, "FETCH_HEAD")));
        Assert.False(File.Exists(Path.Combine(complete.RepositoryPath, "index")));
        ExternalCommandResult refs = await new ExternalCommandRunner().RunAsync("git", complete.RepositoryPath, ["for-each-ref", "--format=%(refname)"], CancellationToken.None);
        Assert.Equal("", refs.StandardOutput.Trim());
    }

    private sealed class RecordingRunner : IExternalCommandRunner
    {
        public bool FailSecond { get; set; }
        public List<IReadOnlyList<string>> Fetches { get; } = [];
        public Task<ExternalCommandResult> RunAsync(string executable, string workingDirectory, IReadOnlyList<string> arguments,
            CancellationToken token, bool requireSuccess = true)
        {
            if (arguments.Contains("fetch"))
            {
                Fetches.Add([.. arguments]);
                if (FailSecond && Fetches.Count == 2) throw new ExternalCommandException("git", 1, "Synthetic second-batch failure.");
            }
            return new ExternalCommandRunner().RunAsync(executable, workingDirectory, arguments, token, requireSuccess);
        }
    }
}
