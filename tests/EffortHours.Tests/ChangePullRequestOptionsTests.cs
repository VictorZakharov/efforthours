using EffortHours.Cli;

namespace EffortHours.Tests;

public sealed class ChangePullRequestOptionsTests
{
    [Fact]
    public void SingleChangeFetchMissingIsExplicitAndPullRequestOnly()
    {
        ChangeCommandParseResult parsed = ChangeCommandOptionsParser.Parse(
            ["repository", "--pr", "42", "--fetch-missing", "--no-rate"]);
        ChangeCommandParseResult invalid = ChangeCommandOptionsParser.Parse(
            ["repository", "--commit", "HEAD", "--fetch-missing"]);

        Assert.Null(parsed.Error);
        Assert.True(parsed.Options!.FetchMissing);
        Assert.Contains("valid only with --pr", invalid.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void PortfolioFetchMissingSupportsPrSelectorsButNotAuthorSelectors()
    {
        ChangePortfolioCommandParseResult repeated = ChangePortfolioCommandOptionsParser.Parse(
            ["repository", "--pr", "42", "--fetch-missing", "--no-rate"]);
        ChangePortfolioCommandParseResult manifest = ChangePortfolioCommandOptionsParser.Parse(
            ["--manifest", "prs.json", "--fetch-missing", "--no-rate"]);
        ChangePortfolioCommandParseResult authorManifest = ChangePortfolioCommandOptionsParser.Parse(
            ["--author-period-manifest", "authors.json", "--fetch-missing", "--no-rate"]);

        Assert.Null(repeated.Error);
        Assert.True(repeated.Options!.FetchMissing);
        Assert.Null(manifest.Error);
        Assert.True(manifest.Options!.FetchMissing);
        Assert.Contains("repeated --pr or --manifest", authorManifest.Error, StringComparison.Ordinal);
        Assert.Contains("--fetch-missing", ChangePortfolioHelp.Text, StringComparison.Ordinal);
    }
}
