using EffortHours.Change;

namespace EffortHours.Tests;

public sealed class GitPullRequestAcquisitionTests
{
    [Fact]
    public async Task FetchUsesNarrowProviderRefsWithoutWritingFetchHeadOrLocalRefs()
    {
        RecordingRunner runner = new(new ExternalCommandResult(0, string.Empty, string.Empty));
        GitClient client = new(
            runner,
            (_, _, _) => throw new InvalidOperationException("Snapshot factory was not expected."));

        await client.FetchPullRequestObjectsAsync(
            "virtual-repository",
            "https://github.com/acme/demo.git",
            "main",
            42);

        Assert.Equal("git", runner.Executable);
        Assert.Equal("virtual-repository", runner.WorkingDirectory);
        Assert.Equal(
            [
                "-c",
                "credential.helper=",
                "-c",
                "credential.helper=!gh auth git-credential",
                "fetch",
                "--no-tags",
                "--no-write-fetch-head",
                "--no-recurse-submodules",
                "https://github.com/acme/demo.git",
                "refs/heads/main",
                "refs/pull/42/head",
            ],
            runner.Arguments);
    }

    [Fact]
    public async Task AuthorPeriodFetchUsesDistinctNarrowProviderRefsWithoutWritingFetchHeadOrLocalRefs()
    {
        RecordingRunner runner = new(new ExternalCommandResult(0, string.Empty, string.Empty));
        GitClient client = new(
            runner,
            (_, _, _) => throw new InvalidOperationException("Snapshot factory was not expected."));

        await client.FetchAuthorPeriodHeadObjectsAsync(
            "virtual-repository",
            "https://github.com/acme/demo.git",
            ["refs/pull/42/head", "refs/heads/main", "refs/pull/42/head"]);

        Assert.Equal(
            [
                "-c",
                "credential.helper=",
                "-c",
                "credential.helper=!gh auth git-credential",
                "fetch",
                "--no-tags",
                "--no-write-fetch-head",
                "--no-recurse-submodules",
                "https://github.com/acme/demo.git",
                "refs/heads/main",
                "refs/pull/42/head",
            ],
            runner.Arguments);
    }

    [Fact]
    public async Task FetchFailureExplainsNoninteractiveCredentialAndRefBoundary()
    {
        GitClient client = new(
            new RecordingRunner(new ExternalCommandException("git", 128, "authentication failed")),
            (_, _, _) => throw new InvalidOperationException("Snapshot factory was not expected."));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.FetchPullRequestObjectsAsync(
                "virtual-repository",
                "https://github.com/acme/demo.git",
                "main",
                42));

        Assert.Contains("authenticated gh account", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PR and base refs", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FetchPropagatesCancellationWithoutTranslatingItToInputFailure()
    {
        GitClient client = new(
            new RecordingRunner(new ExternalCommandResult(0, string.Empty, string.Empty)),
            (_, _, _) => throw new InvalidOperationException("Snapshot factory was not expected."));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.FetchPullRequestObjectsAsync(
                "virtual-repository",
                "https://github.com/acme/demo.git",
                "main",
                42,
                cancellation.Token));
    }

    private sealed class RecordingRunner : IExternalCommandRunner
    {
        private readonly ExternalCommandResult _result;
        private readonly Exception? _exception;

        public RecordingRunner(ExternalCommandResult result)
        {
            _result = result;
        }

        public RecordingRunner(Exception exception)
        {
            _exception = exception;
        }

        public string? Executable { get; private set; }

        public string? WorkingDirectory { get; private set; }

        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public Task<ExternalCommandResult> RunAsync(
            string executable,
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            bool requireSuccess = true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = requireSuccess;
            Executable = executable;
            WorkingDirectory = workingDirectory;
            Arguments = [.. arguments];
            return _exception is null
                ? Task.FromResult(_result)
                : Task.FromException<ExternalCommandResult>(_exception);
        }
    }
}
