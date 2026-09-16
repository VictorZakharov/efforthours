using EffortHours.Change;

namespace EffortHours.Tests;

public sealed class GitFetchNegotiationTests
{
    [Fact]
    public async Task FetchAdvertisesOnlyBoundedImmutableTipsAndPreservesSourceRefs()
    {
        RecordingRunner runner = new();
        GitClient client = new(runner, (_, _, _) => throw new InvalidOperationException());
        string first = new('a', 40);
        string second = new('b', 40);

        await client.FetchManagedObjectsAsync(
            "virtual-repository",
            "https://github.com/example/repository.git",
            ["refs/heads/main", "refs/pull/7/head"],
            [second, first, second],
            CancellationToken.None);

        Assert.Equal(
            ["--negotiation-tip=" + first, "--negotiation-tip=" + second],
            runner.Arguments.Where(argument => argument.StartsWith("--negotiation-tip=", StringComparison.Ordinal)));
        Assert.Equal(
            ["https://github.com/example/repository.git", "refs/heads/main", "refs/pull/7/head"],
            runner.Arguments.TakeLast(3));
        Assert.Contains("--no-write-fetch-head", runner.Arguments);
        Assert.Contains("--no-tags", runner.Arguments);
        Assert.Contains("--no-recurse-submodules", runner.Arguments);
    }

    [Theory]
    [InlineData("main")]
    [InlineData("--all")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa*")]
    public async Task MutableOrMalformedNegotiationTipsNeverReachGit(string tip)
    {
        RecordingRunner runner = new();
        GitClient client = new(runner, (_, _, _) => throw new InvalidOperationException());

        await Assert.ThrowsAsync<ArgumentException>(() => client.FetchManagedObjectsAsync(
            "virtual-repository", "source", ["refs/heads/main"], [tip], CancellationToken.None));

        Assert.Empty(runner.Arguments);
    }

    [Fact]
    public async Task OversizedNegotiationSetsNeverReachGit()
    {
        RecordingRunner runner = new();
        GitClient client = new(runner, (_, _, _) => throw new InvalidOperationException());

        await Assert.ThrowsAsync<ArgumentException>(() => client.FetchManagedObjectsAsync(
            "virtual-repository",
            "source",
            ["refs/heads/main"],
            [.. Enumerable.Repeat(new string('a', 40), GitHubFetchNegotiationCache.MaximumTips + 1)],
            CancellationToken.None));

        Assert.Empty(runner.Arguments);
    }

    [Fact]
    public void HintParsingAcceptsBothObjectFormatsAndRejectsIncompatibleOrUnboundedInput()
    {
        string first = new('a', 40);
        string second = new('b', 64);
        string protocol = GitHubFetchNegotiationCache.Protocol;

        Assert.Equal([first, second],
            GitHubFetchNegotiationCache.Parse(protocol + "\n" + second + "\n" + first + "\n" + first));
        foreach (string invalid in new[]
        {
            string.Empty,
            protocol,
            "future-version\n" + first,
            protocol + "\nmain",
            protocol + "\n" + first + "\n",
            protocol + "\n" + new string('a', GitHubFetchNegotiationCache.MaximumBytes),
            protocol + "\n" + string.Join('\n', Enumerable.Repeat(first, 33)),
        })
        {
            Assert.Empty(GitHubFetchNegotiationCache.Parse(invalid));
        }
    }

    private sealed class RecordingRunner : IExternalCommandRunner
    {
        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public Task<ExternalCommandResult> RunAsync(
            string executable,
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            bool requireSuccess = true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Arguments = [.. arguments];
            return Task.FromResult(new ExternalCommandResult(0, string.Empty, string.Empty));
        }
    }
}
