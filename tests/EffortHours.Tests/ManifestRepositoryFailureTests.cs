using EffortHours.Change;

namespace EffortHours.Tests;

public sealed class ManifestRepositoryFailureTests
{
    [Theory]
    [InlineData(
        "fatal: detected dubious ownership in repository at 'C:/private/repository'\nTo add an exception, call git config --global --add safe.directory C:/private/repository",
        "dubious-ownership safety check")]
    [InlineData(
        "fatal: not a git repository (or any of the parent directories): .git",
        "is not a Git worktree")]
    [InlineData(
        "fatal: repository version 99 is not supported",
        "format unsupported by the installed Git executable")]
    public void KnownGitFailuresKeepActionableCategoryWithoutLeakingPath(
        string gitMessage,
        string expected)
    {
        ExternalCommandException source = new("git", 128, $"'git' failed: {gitMessage}");

        InvalidOperationException mapped = ManifestRepositoryFailure.Create("repository-b", source);

        Assert.Contains(expected, mapped.Message, StringComparison.Ordinal);
        Assert.Contains("repository-b", mapped.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("C:/private/repository", mapped.Message, StringComparison.Ordinal);
        Assert.Same(source, mapped.InnerException);
    }

    [Fact]
    public void UnknownFailureRetainsSanitizedFallback()
    {
        ExternalCommandException source = new(
            "git",
            128,
            "'git' failed: private and unclassified target-specific detail");

        InvalidOperationException mapped = ManifestRepositoryFailure.Create("repository-a", source);

        Assert.Equal(
            "Repository 'repository-a' is not a readable local Git repository.",
            mapped.Message);
        Assert.DoesNotContain("private and unclassified", mapped.Message, StringComparison.Ordinal);
    }
}
