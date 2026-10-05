using EffortHours.Cli;

namespace EffortHours.Tests;

public sealed class CliUsageExamplesTests
{
    [Fact]
    public async Task DefaultAndAliasSelectSameCommonSubsetOfAllRecipes()
    {
        var popular = await RunAsync("examples");
        var alias = await RunAsync("--examples");
        var (Code, Output, Error) = await RunAsync("examples", "all");
        Assert.Equal(0, popular.Code);
        Assert.Equal(popular, alias);
        Assert.Equal(string.Empty, Error);
        Assert.Contains("experimental and uncalibrated", popular.Output, StringComparison.Ordinal);
        string[] commonCommands = Commands(popular.Output);
        string[] allCommands = Commands(Output);
        Assert.True(commonCommands.Length >= 8);
        Assert.True(allCommands.Length > commonCommands.Length);
        Assert.Equal(allCommands.Length, allCommands.Distinct(StringComparer.Ordinal).Count());
        Assert.All(commonCommands, command => Assert.Contains(command, allCommands));
    }

    [Theory]
    [InlineData("repository")]
    [InlineData("change")]
    [InlineData("portfolio")]
    [InlineData("calendar")]
    [InlineData("report")]
    [InlineData("review")]
    [InlineData("inspect")]
    [InlineData("agent")]
    public async Task TopicSelectsOnlyItsRecipes(string topic)
    {
        var (Code, Output, Error) = await RunAsync("examples", topic);
        var all = await RunAsync("examples", "all");
        Assert.Equal(0, Code);
        Assert.Empty(Error);
        string[] titles = [.. Output.Split('\n').Where(line => line.StartsWith('['))];
        Assert.NotEmpty(titles);
        Assert.All(titles, title => Assert.StartsWith($"[{topic}] ", title));
        Assert.All(Commands(Output), command => Assert.Contains(command, Commands(all.Output)));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("--format", "json")]
    [InlineData("all", "repository")]
    public async Task InvalidSelectionIsAUsageErrorOnStderrOnly(params string[] args)
    {
        var (Code, Output, Error) = await RunAsync(["examples", .. args]);
        Assert.Equal(CliExitCodes.UsageError, Code);
        Assert.Empty(Output);
        Assert.Contains("Topics:", Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GuideExplainsNetworkWritesAndSelectionBoundaries()
    {
        var (_, Output, _) = await RunAsync("examples", "all");
        Assert.Contains("--fetch-missing", Output, StringComparison.Ordinal);
        Assert.Contains("Requires authenticated gh", Output, StringComparison.Ordinal);
        Assert.Contains("reference denominator, not actual hours", Output, StringComparison.Ordinal);
        Assert.Contains("do not add separate PR estimates", Output, StringComparison.Ordinal);
        Assert.Contains("Explicitly write the packaged companion skill", Output, StringComparison.Ordinal);
        Assert.Contains("<placeholders>", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryTopLevelHelpAliasIncludesExactInstalledVersionAndExamplesDiscovery()
    {
        var (_, Output, _) = await RunAsync("version");
        foreach (string[] args in new[] { Array.Empty<string>(), ["help"], ["--help"], ["-h"] })
        {
            var help = await RunAsync(args);
            Assert.Equal(0, help.Code);
            Assert.Empty(help.Error);
            Assert.StartsWith($"EffortHours {Output.Trim()}" + Environment.NewLine, help.Output);
            Assert.Contains("eh examples", help.Output, StringComparison.Ordinal);
        }
    }

    private static string[] Commands(string output) => [.. output.Split('\n')
        .Where(line => line.StartsWith("  eh ", StringComparison.Ordinal)).Select(line => line.Trim())];

    private static async Task<(int Code, string Output, string Error)> RunAsync(params string[] args)
    {
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        int code = await new EffortHoursApplication().RunAsync(args, stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }
}
