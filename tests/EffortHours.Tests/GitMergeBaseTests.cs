using EffortHours.Change;

namespace EffortHours.Tests;

public sealed class GitMergeBaseTests
{
    private const string First = "1111111111111111111111111111111111111111";
    private const string Second = "2222222222222222222222222222222222222222";
    private const string MergeBase = "3333333333333333333333333333333333333333";

    [Fact]
    public async Task UniqueMergeBaseUsesAllCandidateQueryAndReturnsCanonicalObject()
    {
        RecordingRunner runner = new(new ExternalCommandResult(0, $"{MergeBase.ToUpperInvariant()}\n", string.Empty));
        GitClient client = Client(runner);

        string result = await client.ResolveMergeBaseAsync("virtual-repository", First, Second);

        Assert.Equal(MergeBase, result);
        Assert.Equal(["merge-base", "--all", First, Second], runner.Arguments);
        Assert.False(runner.RequireSuccess);
    }

    [Fact]
    public async Task MissingMergeBaseFailsWithActionableBoundary()
    {
        GitClient client = Client(new RecordingRunner(new ExternalCommandResult(1, string.Empty, string.Empty)));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ResolveMergeBaseAsync("virtual-repository", First, Second));

        Assert.Contains("no common ancestor", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MultipleMergeBasesFailInsteadOfSelectingArbitrarily()
    {
        string other = new('4', 40);
        GitClient client = Client(new RecordingRunner(new ExternalCommandResult(
            0,
            $"{MergeBase}\n{other}\n",
            string.Empty)));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ResolveMergeBaseAsync("virtual-repository", First, Second));

        Assert.Contains("multiple merge bases", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--base", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitFailurePreservesCommandCategoryAndDiagnostic()
    {
        GitClient client = Client(new RecordingRunner(new ExternalCommandResult(
            128,
            string.Empty,
            "fatal: synthetic merge-base failure")));

        ExternalCommandException exception = await Assert.ThrowsAsync<ExternalCommandException>(() =>
            client.ResolveMergeBaseAsync("virtual-repository", First, Second));

        Assert.Equal("git", exception.Command);
        Assert.Equal(128, exception.ExitCode);
        Assert.Contains("synthetic merge-base failure", exception.Message, StringComparison.Ordinal);
    }

    private static GitClient Client(IExternalCommandRunner runner) => new(
        runner,
        (_, _, _) => throw new NotSupportedException());

    private sealed class RecordingRunner(ExternalCommandResult result) : IExternalCommandRunner
    {
        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public bool RequireSuccess { get; private set; }

        public Task<ExternalCommandResult> RunAsync(
            string executable,
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            bool requireSuccess = true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("git", executable);
            Assert.Equal("virtual-repository", workingDirectory);
            Arguments = [.. arguments];
            RequireSuccess = requireSuccess;
            return Task.FromResult(result);
        }
    }
}
