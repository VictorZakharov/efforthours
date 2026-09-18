using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed class ChangeProviderLoginTests
{
    [Theory]
    [InlineData("@me")]
    [InlineData("@ME")]
    [InlineData("another-user")]
    public void ProviderAccountDoesNotReplaceOrExpandGitAliases(string login)
    {
        ChangePortfolioCommandParseResult result = Parse("--provider-login", login);
        Assert.Null(result.Error);
        Assert.Equal(login, result.Options!.ProviderLogin);
        Assert.Equal(["work@example.test", "42+selected@users.noreply.github.com"], result.Options.AuthorAliases);
    }

    [Theory]
    [InlineData("work@example.test")]
    [InlineData("../selected")]
    [InlineData("")]
    public void InvalidProviderAccountsAreRejected(string login) =>
        Assert.NotNull(Parse("--provider-login", login).Error);

    [Fact]
    public void DuplicateOrMisplacedProviderAccountIsRejected()
    {
        Assert.NotNull(Parse("--provider-login", "first", "--provider-login", "second").Error);
        Assert.NotNull(ChangePortfolioCommandOptionsParser.Parse(
            ["repository", "--pr", "7", "--provider-login", "selected"]).Error);
        Assert.NotNull(ChangePortfolioCommandOptionsParser.Parse(
            ["--today", "--owner", "owner", "--author", "mail@example.test", "--scope", "engineering",
             "--timezone", "UTC", "--capacity-hours", "8", "--provider-login", "selected"]).Error);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("markdown")]
    public async Task UnresolvedIdentityPublishesAnActionableIncompleteReportWithoutPrivateInputs(string format)
    {
        ChangePortfolioCommand command = new(new ChangeEstimator(),
            (path, pull, repository, fetch, token) => throw new NotSupportedException(),
            (path, options, telemetry, token) => throw new NotSupportedException(),
            (path, token) => throw new NotSupportedException(),
            (path, telemetry, token) => throw new NotSupportedException(), null,
            (request, token) => throw GitHubProviderFailure.UnresolvedContributor());
        StringWriter stdout = new();
        StringWriter stderr = new();
        int exitCode = await command.ExecuteAsync(
            [.. Arguments, "--format", format, "--generated-at", "2026-09-18T12:00:00Z"],
            stdout, stderr, default);
        Assert.Equal(CliExitCodes.InvalidInput, exitCode);
        Assert.Contains("--provider-login", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("github-contributor-identity-unresolved", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("work@example.test", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("42+selected", stdout.ToString(), StringComparison.Ordinal);
        if (format == "json")
        {
            SchemaValidationResult schema = ContractSchemaValidator.Validate(
                SchemaNames.ChangePortfolioComparisonReport, stdout.ToString());
            Assert.True(schema.IsValid, string.Join("\n", schema.Errors));
            ChangePortfolioComparisonReport report = ContractJson.Deserialize<ChangePortfolioComparisonReport>(stdout.ToString());
            Assert.Empty(ContractValidation.Validate(report));
            Assert.Empty(report.Series);
        }
    }

    private static readonly string[] Arguments =
    [
        "--today", "--owner", "owner", "--author", "work@example.test",
        "--author", "42+selected@users.noreply.github.com", "--include-open-prs",
        "--timezone", "UTC", "--scope", "engineering", "--capacity-hours", "8", "--no-rate",
    ];

    private static ChangePortfolioCommandParseResult Parse(params string[] extra) =>
        ChangePortfolioCommandOptionsParser.Parse([.. Arguments, .. extra]);
}
