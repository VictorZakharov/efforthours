using EffortHours.Change;

namespace EffortHours.Tests;

public sealed class GitHubPullRequestResolverTests
{
    [Fact]
    public async Task ResolverUsesGhOnlyForImmutableIdentity()
    {
        const string baseObjectId = "1111111111111111111111111111111111111111";
        const string headObjectId = "2222222222222222222222222222222222222222";
        RecordingRunner runner = new(new ExternalCommandResult(
            0,
            $$"""{"number":42,"url":"https://github.com/acme/demo/pull/42","baseRefName":"main","baseRefOid":"{{baseObjectId}}","headRefOid":"{{headObjectId}}","changedFiles":7}""",
            string.Empty));
        GitHubPullRequestResolver resolver = new(runner);

        ResolvedPullRequest result = await resolver.ResolveAsync(
            "virtual-repository",
            "42",
            "acme/demo");

        Assert.Equal(baseObjectId, result.BaseObjectId);
        Assert.Equal(headObjectId, result.HeadObjectId);
        Assert.Equal(42, result.Reference.Number);
        Assert.Equal("acme/demo", result.Reference.Repository);
        Assert.Equal("main", result.BaseRefName);
        Assert.Equal("https://github.com/acme/demo.git", result.FetchSource);
        Assert.Equal(7, result.ChangedFileCount);
        Assert.Equal("gh", runner.Executable);
        Assert.Equal("virtual-repository", runner.WorkingDirectory);
        Assert.Equal(
            ["pr", "view", "42", "--json", "number,url,baseRefName,baseRefOid,headRefOid,changedFiles", "--repo", "acme/demo"],
            runner.Arguments);
    }

    [Fact]
    public async Task ResolverReportsMalformedIdentityWithoutLeakingOutput()
    {
        RecordingRunner runner = new(new ExternalCommandResult(
            0,
            "{\"number\":42,\"baseRefOid\":\"not-an-object\",\"headRefOid\":\"also-bad\"}",
            string.Empty));
        GitHubPullRequestResolver resolver = new(runner);

        GitHubProviderException exception = await Assert.ThrowsAsync<GitHubProviderException>(() =>
            resolver.ResolveAsync("virtual-repository", "42", null));

        Assert.Contains("malformed or incomplete", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not-an-object", exception.Message, StringComparison.Ordinal);
        Assert.Equal("github-provider-response-malformed", exception.Action.FailureCode);
    }

    [Fact]
    public async Task ResolverExplainsOptionalGhFailure()
    {
        RecordingRunner runner = new(new ExternalCommandException(
            "gh",
            null,
            "Could not start required executable 'gh'."));
        GitHubPullRequestResolver resolver = new(runner);

        GitHubProviderException exception = await Assert.ThrowsAsync<GitHubProviderException>(() =>
            resolver.ResolveAsync("virtual-repository", "42", null));

        Assert.Contains("could not be started", exception.Message, StringComparison.Ordinal);
        Assert.Equal("github-cli-executable-missing", exception.Action.FailureCode);
    }

    [Fact]
    public async Task ResolverDerivesCanonicalRepositoryFromReturnedUrl()
    {
        RecordingRunner runner = new(new ExternalCommandResult(
            0,
            "{\"number\":42,\"url\":\"https://github.com/Acme/Demo/pull/42\",\"baseRefName\":\"main\",\"baseRefOid\":\"1111111111111111111111111111111111111111\",\"headRefOid\":\"2222222222222222222222222222222222222222\",\"changedFiles\":1}",
            string.Empty));

        ResolvedPullRequest result = await new GitHubPullRequestResolver(runner).ResolveAsync(
            "ordinary-folder",
            "https://github.com/Acme/Demo/pull/42",
            repository: null);

        Assert.Equal("acme/demo", result.Reference.Repository);
        Assert.Equal("https://github.com/acme/demo.git", result.FetchSource);
        Assert.DoesNotContain("--repo", runner.Arguments);
    }

    [Fact]
    public async Task ResolverClassifiesMissingPullRequestWithoutLeakingProviderOutput()
    {
        RecordingRunner runner = new(new ExternalCommandResult(
            1,
            string.Empty,
            "HTTP 404 private/provider/detail"));

        GitHubProviderException exception = await Assert.ThrowsAsync<GitHubProviderException>(() =>
            new GitHubPullRequestResolver(runner).ResolveAsync(
                "ordinary-folder",
                "42",
                "acme/demo"));

        Assert.Equal("github-pull-request-forbidden-or-not-found", exception.Action.FailureCode);
        Assert.Equal("pull-request-resolution", exception.Action.Phase);
        Assert.DoesNotContain("private/provider/detail", exception.Message, StringComparison.Ordinal);
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
