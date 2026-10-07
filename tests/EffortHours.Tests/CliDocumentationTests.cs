using System.Text.RegularExpressions;
using EffortHours.Cli;

namespace EffortHours.Tests;

public sealed partial class CliDocumentationTests
{
    [Fact]
    public async Task EveryListedInstalledTopicCanBeReadAsCompleteMarkdown()
    {
        var list = await RunAsync("docs");
        Assert.Equal(0, list.Code);
        Assert.Empty(list.Error);
        Assert.Equal(list, await RunAsync("docs", "list"));
        Assert.Equal(list, await RunAsync("--docs"));
        string[] topics = [.. list.Output.Split('\n').Where(line => line.StartsWith("  ", StringComparison.Ordinal))
            .Select(line => line.Trim().Split(" - ", 2, StringSplitOptions.None)[0])];
        Assert.Equal(topics.Length, topics.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(topics.Order(StringComparer.Ordinal), topics);
        Assert.Contains("historical-refresh-integration", topics);
        Assert.Contains("historical-refresh-example", topics);
        Assert.Contains("getting-started", topics);
        foreach (string topic in topics)
        {
            var (Code, Output, Error) = await RunAsync("docs", "show", topic);
            Assert.Equal(0, Code);
            Assert.Empty(Error);
            Assert.StartsWith("# ", Output);
        }

        var integration = await RunAsync("docs", "show", "historical-refresh-integration");
        Assert.Contains("$LASTEXITCODE", integration.Output, StringComparison.Ordinal);
        Assert.Contains("experimental", integration.Output, StringComparison.Ordinal);
        Assert.Contains("complete checked snapshot", integration.Output, StringComparison.Ordinal);
        var unicode = await RunAsync("docs", "show", "code-budgets");
        Assert.Contains("\u2014", unicode.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AllCliDocumentationPointersResolveToInstalledTopics()
    {
        string[][] guides = [["--help"], ["examples", "all"], ["change", "check-refresh", "--help"],
            ["change", "plan-refresh", "--help"], ["change", "review-rewrite", "--help"],
            ["change", "review-days", "--help"], ["change", "allocate-days", "--help"]];
        HashSet<string> topics = new(StringComparer.Ordinal);
        foreach (string[] args in guides)
        {
            var (Code, Output, Error) = await RunAsync(args);
            Assert.Equal(0, Code);
            Assert.Empty(Error);
            Assert.DoesNotContain("docs/", Output, StringComparison.Ordinal);
            Assert.DoesNotContain("github.com", Output, StringComparison.OrdinalIgnoreCase);
            foreach (Match match in DocumentationTopicCommands().Matches(Output))
            {
                topics.Add(match.Groups[1].Value);
            }
        }

        Assert.Contains("historical-refresh-integration", topics);
        foreach (string topic in topics)
        {
            Assert.Equal(0, (await RunAsync("docs", "show", topic)).Code);
        }
    }

    [Theory]
    [InlineData("show", "../../README.md")]
    [InlineData("show", "unknown")]
    [InlineData("show")]
    [InlineData("export")]
    [InlineData("list", "extra")]
    [InlineData("unknown")]
    public async Task InvalidDocumentationSelectionOnlyEmitsUsageOnStderr(params string[] args)
    {
        var (Code, Output, Error) = await RunAsync(["docs", .. args]);
        Assert.Equal(CliExitCodes.UsageError, Code);
        Assert.Empty(Output);
        Assert.NotEmpty(Error);
    }

    [Fact]
    public async Task CancelledReadKeepsNativeCancellationExit()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        int code = await new EffortHoursApplication().RunAsync(["docs", "show", "getting-started"],
            stdout, stderr, cancellation.Token);
        Assert.Equal(CliExitCodes.Cancelled, code);
        Assert.Empty(stdout.ToString());
    }

    private static async Task<(int Code, string Output, string Error)> RunAsync(params string[] args)
    {
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        int code = await new EffortHoursApplication().RunAsync(args, stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }

    [GeneratedRegex("eh docs show ([a-z0-9-]+)")]
    private static partial Regex DocumentationTopicCommands();
}
