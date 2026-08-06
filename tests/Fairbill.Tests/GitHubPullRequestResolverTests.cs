using Fairbill.Change;

namespace Fairbill.Tests;

public sealed class GitHubPullRequestResolverTests
{
    [Fact]
    public async Task ResolverUsesGhOnlyForImmutableIdentity()
    {
        const string baseObjectId = "1111111111111111111111111111111111111111";
        const string headObjectId = "2222222222222222222222222222222222222222";
        RecordingRunner runner = new(new ExternalCommandResult(
            0,
            $$"""{"number":42,"url":"https://github.com/acme/demo/pull/42","baseRefOid":"{{baseObjectId}}","headRefOid":"{{headObjectId}}"}""",
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
        Assert.Equal("gh", runner.Executable);
        Assert.Equal("virtual-repository", runner.WorkingDirectory);
        Assert.Equal(
            ["pr", "view", "42", "--json", "number,url,baseRefOid,headRefOid", "--repo", "acme/demo"],
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

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolver.ResolveAsync("virtual-repository", "42", null));

        Assert.Contains("incomplete pull-request identity", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not-an-object", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolverExplainsOptionalGhFailure()
    {
        RecordingRunner runner = new(new ExternalCommandException(
            "gh",
            null,
            "Could not start required executable 'gh'."));
        GitHubPullRequestResolver resolver = new(runner);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolver.ResolveAsync("virtual-repository", "42", null));

        Assert.Contains("optional gh CLI support", exception.Message, StringComparison.Ordinal);
        Assert.Contains("installed, authenticated", exception.Message, StringComparison.Ordinal);
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
