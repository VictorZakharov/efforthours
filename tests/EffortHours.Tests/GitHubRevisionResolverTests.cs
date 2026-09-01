using EffortHours.Change;

namespace EffortHours.Tests;

public sealed class GitHubRevisionResolverTests
{
    [Fact]
    public async Task ResolverCanonicalizesRepositoryAndResolvesEachSelector()
    {
        const string first = "1111111111111111111111111111111111111111";
        const string second = "2222222222222222222222222222222222222222";
        RecordingRunner runner = new([first, second]);

        IReadOnlyList<ResolvedGitRevision> resolved = await new GitHubRevisionResolver(runner)
            .ResolveAsync("Acme/Demo", ["main", "feature/name"], CancellationToken.None);

        Assert.Equal([first, second], [.. resolved.Select(value => value.ObjectId)]);
        Assert.Equal(
            [
                "repos/acme/demo/commits/main",
                "repos/acme/demo/commits/feature%2Fname",
            ],
            [.. runner.Calls.Select(call => call[1])]);
        Assert.All(runner.Calls, arguments => Assert.Equal(["gh", "api"], ["gh", arguments[0]]));
        Assert.All(runner.Calls, arguments =>
            Assert.Equal(["--jq", ".sha"], arguments.Skip(arguments.Count - 2)));
    }

    [Fact]
    public async Task ResolverRejectsMalformedProviderOutputWithoutLeakingIt()
    {
        RecordingRunner runner = new(["private malformed provider payload"]);

        GitHubProviderException exception = await Assert.ThrowsAsync<GitHubProviderException>(() =>
            new GitHubRevisionResolver(runner).ResolveAsync(
                "acme/demo",
                ["main"],
                CancellationToken.None));

        Assert.Equal("github-provider-response-malformed", exception.Action.FailureCode);
        Assert.DoesNotContain("private malformed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolverRejectsUnboundedRepositoryIdentity()
    {
        RecordingRunner runner = new([new string('a', 40)]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new GitHubRevisionResolver(runner).ResolveAsync(
                new string('a', 510) + "/demo",
                ["main"],
                CancellationToken.None));

        Assert.Empty(runner.Calls);
    }

    private sealed class RecordingRunner(IReadOnlyList<string> outputs) : IExternalCommandRunner
    {
        private int _index;

        public List<IReadOnlyList<string>> Calls { get; } = [];

        public Task<ExternalCommandResult> RunAsync(
            string executable,
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            bool requireSuccess = true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = workingDirectory;
            _ = requireSuccess;
            Assert.Equal("gh", executable);
            Calls.Add([.. arguments]);
            return Task.FromResult(new ExternalCommandResult(0, outputs[_index++], string.Empty));
        }
    }
}
