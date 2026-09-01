using EffortHours.Cli;

namespace EffortHours.Tests;

public sealed class ChangePullRequestOptionsTests
{
    [Fact]
    public void SingleChangeFetchMissingIsExplicitAndCheckoutFreeOnly()
    {
        ChangeCommandParseResult parsed = ChangeCommandOptionsParser.Parse(
            ["repository", "--pr", "42", "--fetch-missing", "--no-rate"]);
        ChangeCommandParseResult invalid = ChangeCommandOptionsParser.Parse(
            ["repository", "--commit", "HEAD", "--fetch-missing"]);

        Assert.Null(parsed.Error);
        Assert.True(parsed.Options!.FetchMissing);
        Assert.Contains("checkout-free Git selector", invalid.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckoutFreePullRequestAcceptsUrlOrNumberWithRepository()
    {
        ChangeCommandParseResult url = ChangeCommandOptionsParser.Parse(
            ["--pr", "https://github.com/acme/demo/pull/42", "--fetch-missing", "--no-rate"]);
        ChangeCommandParseResult number = ChangeCommandOptionsParser.Parse(
            ["--pr", "42", "--repo", "acme/demo", "--fetch-missing", "--no-rate"]);
        ChangeCommandParseResult missingRepository = ChangeCommandOptionsParser.Parse(
            ["--pr", "42", "--fetch-missing", "--no-rate"]);
        ChangeCommandParseResult missingGitRepository = ChangeCommandOptionsParser.Parse(
            ["--commit", "HEAD", "--no-rate"]);

        Assert.Null(url.Error);
        Assert.Null(url.Options!.RepositoryPath);
        Assert.Null(number.Error);
        Assert.Equal("acme/demo", number.Options!.GitHubRepository);
        Assert.Contains("require --repo", missingRepository.Error, StringComparison.Ordinal);
        Assert.Contains("require --repo", missingGitRepository.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckoutFreeGitSelectorsAcceptRepositoryIdentity()
    {
        ChangeCommandParseResult commit = ChangeCommandOptionsParser.Parse(
            ["--commit", "main", "--repo", "acme/demo", "--fetch-missing", "--no-rate"]);
        ChangeCommandParseResult range = ChangeCommandOptionsParser.Parse(
            ["--range", "main..feature", "--repo", "acme/demo", "--fetch-missing", "--no-rate"]);
        ChangeCommandParseResult baseHead = ChangeCommandOptionsParser.Parse(
            ["--base", "main", "--head", "feature", "--repo", "acme/demo", "--no-rate"]);

        Assert.Null(commit.Error);
        Assert.Null(range.Error);
        Assert.Null(baseHead.Error);
        Assert.All<ChangeCommandParseResult>([commit, range, baseHead], parsed =>
        {
            Assert.Null(parsed.Options!.RepositoryPath);
            Assert.Equal("acme/demo", parsed.Options.GitHubRepository);
        });
    }

    [Fact]
    public void PortfolioFetchMissingSupportsEveryProviderBackedSelector()
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
        ChangePortfolioCommandParseResult directAuthor = ChangePortfolioCommandOptionsParser.Parse(
            ["--author", "person@example.test", "--since", "2026-08-01T00:00:00Z",
             "--until", "2026-08-02T00:00:00Z", "--repo", "acme/demo", "--fetch-missing", "--no-rate"]);

        Assert.Null(authorManifest.Error);
        Assert.True(authorManifest.Options!.FetchMissing);
        Assert.Null(directAuthor.Error);
        Assert.Contains("--fetch-missing", ChangePortfolioHelp.Text, StringComparison.Ordinal);
    }
}
