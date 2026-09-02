using EffortHours.Cli;

namespace EffortHours.Tests;

public sealed class RepositoryQuerySurfaceTests
{
    [Theory]
    [InlineData("scan --help")]
    [InlineData("estimate --help")]
    [InlineData("explain --help")]
    [InlineData("review packet --help")]
    [InlineData("review query --help")]
    public async Task EveryRepositoryCommandAdvertisesSharedCheckoutFreeInput(
        string command)
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = await new EffortHoursApplication().RunAsync(
            command.Split(' '),
            output,
            error);

        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("--repo <owner/name>", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--revision <value>", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--fetch-missing", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void SharedInputParserRejectsLocalAndRemoteSourcesTogether()
    {
        string[] arguments = [".", "--repo", "acme/demo"];
        RepositoryInputOptionsBuilder builder = new(arguments);
        int index = builder.FirstOptionIndex;

        Assert.True(builder.TryConsume(arguments, ref index, out string? consumeError));
        Assert.Null(consumeError);
        Assert.False(builder.TryBuild(out _, out string? error));
        Assert.Contains("cannot be combined", error, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedInputParserDefaultsRemoteRevisionToHead()
    {
        string[] arguments = ["--repo", "acme/demo", "--fetch-missing"];
        RepositoryInputOptionsBuilder builder = new(arguments);
        for (int index = builder.FirstOptionIndex; index < arguments.Length; index++)
        {
            Assert.True(builder.TryConsume(arguments, ref index, out string? error));
            Assert.Null(error);
        }

        Assert.True(builder.TryBuild(out RepositoryInputSelection? selection, out string? buildError));
        Assert.Null(buildError);
        Assert.NotNull(selection);
        Assert.Equal("HEAD", selection.Revision);
        Assert.True(selection.FetchMissing);
    }
}
