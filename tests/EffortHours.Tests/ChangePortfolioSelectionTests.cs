using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ChangePortfolioSelectionTests
{
    private static readonly DateTimeOffset Since =
        new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Until = Since.AddDays(1);

    [Fact]
    public void AuthorSelectorUsesInclusiveExclusiveBoundsAndExactAliases()
    {
        GitCommitMetadata first = Commit("a", Since, authorName: "Target", authorEmail: "target@example.test");
        GitCommitMetadata second = Commit(
            "b",
            Until.AddTicks(-1),
            parents: [first.ObjectId],
            authorName: "Other",
            authorEmail: "other@example.test",
            coauthors: [new GitCommitIdentity("Target", "target@example.test")]);
        GitCommitMetadata excludedEnd = Commit(
            "c",
            Until,
            parents: [second.ObjectId],
            authorName: "Target",
            authorEmail: "target@example.test");
        GitCommitMetadata excludedAlias = Commit(
            "d",
            Since.AddHours(2),
            parents: [first.ObjectId],
            authorName: "Target Team",
            authorEmail: "team@example.test");

        AuthorPeriodSelectionResult result = AuthorPeriodCommitSelector.Select(
            [first, second, excludedEnd, excludedAlias],
            Options(),
            ["target@example.test"]);

        Assert.Equal(2, result.Commits.Count);
        Assert.Equal(first.ObjectId, result.Commits[0].Metadata.ObjectId);
        Assert.Equal(ChangePortfolioAttributionKind.DirectAuthor, result.Commits[0].Kind);
        Assert.Equal(second.ObjectId, result.Commits[1].Metadata.ObjectId);
        Assert.Equal(ChangePortfolioAttributionKind.Coauthor, result.Commits[1].Kind);
        Assert.Contains(result.Commits[1].AmbiguityReasons, reason =>
            reason.Contains("shared-credit", StringComparison.Ordinal));
    }

    [Fact]
    public void CommitterDateMergeAndCoauthorPoliciesAreExplicit()
    {
        GitCommitMetadata merge = Commit(
            "e",
            Since.AddDays(-2),
            committerTimestamp: Since.AddHours(1),
            parents: [new string('1', 40), new string('2', 40)],
            authorName: "Target",
            authorEmail: "target@example.test");
        GitCommitMetadata coauthored = Commit(
            "f",
            Since.AddHours(2),
            authorName: "Other",
            authorEmail: "other@example.test",
            coauthors: [new GitCommitIdentity("Target", "target@example.test")]);
        GitAuthorPeriodPortfolioOptions includeMerge = Options() with
        {
            DateField = ChangePortfolioDateField.Committer,
            MergePolicy = ChangePortfolioMergePolicy.FirstParent,
            CoauthorPolicy = ChangePortfolioCoauthorPolicy.Exclude,
        };

        AuthorPeriodSelectionResult included = AuthorPeriodCommitSelector.Select(
            [merge, coauthored],
            includeMerge,
            ["Target <target@example.test>"]);
        AuthorPeriodSelectionResult excluded = AuthorPeriodCommitSelector.Select(
            [merge, coauthored],
            includeMerge with { MergePolicy = ChangePortfolioMergePolicy.Exclude },
            ["Target <target@example.test>"]);

        SelectedAuthorCommit selected = Assert.Single(included.Commits);
        Assert.Equal(merge.ObjectId, selected.Metadata.ObjectId);
        Assert.Equal(merge.CommitterTimestamp, selected.SelectedTimestamp);
        Assert.Contains(selected.AmbiguityReasons, reason =>
            reason.Contains("first parent", StringComparison.Ordinal));
        Assert.Empty(excluded.Commits);
        Assert.Contains(excluded.Diagnostics, diagnostic => diagnostic.Code == "FB5311");
    }

    [Fact]
    public void DirectAuthorWithCoauthorRetainsPairWorkAmbiguity()
    {
        GitCommitMetadata paired = Commit(
            "9",
            Since.AddHours(1),
            authorName: "Target",
            authorEmail: "target@example.test",
            coauthors: [new GitCommitIdentity("Partner", "partner@example.test")]);

        SelectedAuthorCommit selected = Assert.Single(AuthorPeriodCommitSelector.Select(
            [paired],
            Options(),
            ["target@example.test"]).Commits);

        Assert.Equal(ChangePortfolioAttributionKind.DirectAuthor, selected.Kind);
        Assert.Contains(selected.AmbiguityReasons, reason =>
            reason.Contains("pair-work", StringComparison.Ordinal));
    }

    [Fact]
    public void InterleavedSelectedCommitsRetainAttributionAmbiguity()
    {
        GitCommitMetadata first = Commit(
            "3",
            Since.AddHours(1),
            authorName: "Target",
            authorEmail: "target@example.test");
        GitCommitMetadata second = Commit(
            "5",
            Since.AddHours(3),
            parents: [new string('4', 40)],
            authorName: "Target",
            authorEmail: "target@example.test");

        AuthorPeriodSelectionResult result = AuthorPeriodCommitSelector.Select(
            [first, second],
            Options(),
            ["Target"]);

        Assert.Contains(result.Commits[1].AmbiguityReasons, reason =>
            reason.Contains("interleaved work", StringComparison.Ordinal));
    }

    [Fact]
    public void GitMetadataParserExtractsOnlyStructuredIdentityAndCoauthorFields()
    {
        string objectId = new('a', 40);
        string parent = new('b', 40);
        string coauthorTrailers = "Partner <partner@example.test>";
        string output = string.Join('\0',
            objectId,
            parent,
            "Target",
            "target@example.test",
            "2026-03-01T09:00:00+00:00",
            "Integrator",
            "integrator@example.test",
            "2026-03-01T10:00:00+00:00",
            coauthorTrailers) + '\0';

        GitCommitMetadata parsed = Assert.Single(GitCommitMetadataParser.Parse(output));

        Assert.Equal(objectId, parsed.ObjectId);
        Assert.Equal(parent, Assert.Single(parsed.ParentObjectIds));
        Assert.Equal(new GitCommitIdentity("Target", "target@example.test"), parsed.Author);
        Assert.Equal(
            new GitCommitIdentity("Partner", "partner@example.test"),
            Assert.Single(parsed.Coauthors));
    }

    [Fact]
    public void GitMetadataParserPreservesAnEmptyCoauthorField()
    {
        string output = string.Join('\0',
            new string('a', 40),
            string.Empty,
            "Target",
            "target@example.test",
            "2026-03-01T09:00:00+00:00",
            "Integrator",
            "integrator@example.test",
            "2026-03-01T10:00:00+00:00",
            string.Empty) + "\0\n";

        GitCommitMetadata parsed = Assert.Single(GitCommitMetadataParser.Parse(output));

        Assert.Empty(parsed.Coauthors);
        Assert.Empty(parsed.ParentObjectIds);
    }

    [Fact]
    public void CommandParserSupportsRepeatedPullRequestsAndRejectsAuthorOnlyOptions()
    {
        ChangePortfolioCommandParseResult parsed = ChangePortfolioCommandOptionsParser.Parse(
            [".", "--pr", "1", "--pr", "owner/repository#2", "--no-rate"]);
        ChangePortfolioCommandParseResult invalid = ChangePortfolioCommandOptionsParser.Parse(
            [".", "--pr", "1", "--timezone", "UTC"]);

        Assert.Null(parsed.Error);
        Assert.Equal(["1", "owner/repository#2"], parsed.Options!.PullRequests);
        Assert.True(parsed.Options.NoRate);
        Assert.Contains("valid only with --author", invalid.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandParserCanonicalizesAuthorPeriodToUtc()
    {
        ChangePortfolioCommandParseResult parsed = ChangePortfolioCommandOptionsParser.Parse(
        [
            ".",
            "--author", "target@example.test",
            "--since", "2026-03-01T00:00:00",
            "--until", "2026-03-02T00:00:00",
            "--timezone", "UTC",
            "--date-field", "committer",
            "--merge-policy", "first-parent",
            "--coauthors", "exclude",
            "--no-rate",
        ]);

        ChangePortfolioCommandOptions options = Assert.IsType<ChangePortfolioCommandOptions>(parsed.Options);
        Assert.Null(parsed.Error);
        Assert.Equal(Since, options.SinceInclusive);
        Assert.Equal(Until, options.UntilExclusive);
        Assert.Equal(ChangePortfolioDateField.Committer, options.DateField);
        Assert.Equal(ChangePortfolioMergePolicy.FirstParent, options.MergePolicy);
        Assert.Equal(ChangePortfolioCoauthorPolicy.Exclude, options.CoauthorPolicy);
    }

    [Fact]
    public void CommandParserAcceptsOnlyTheAuthorPeriodManifestSelectorFamily()
    {
        ChangePortfolioCommandParseResult parsed = ChangePortfolioCommandOptionsParser.Parse(
        [
            "--author-period-manifest", "portfolio.json",
            "--profile", "recreation",
            "--no-rate",
        ]);
        ChangePortfolioCommandParseResult positional = ChangePortfolioCommandOptionsParser.Parse(
            [".", "--author-period-manifest", "portfolio.json"]);
        ChangePortfolioCommandParseResult policy = ChangePortfolioCommandOptionsParser.Parse(
            ["--author-period-manifest", "portfolio.json", "--since", "2026-03-01T00:00:00Z"]);
        ChangePortfolioCommandParseResult mixed = ChangePortfolioCommandOptionsParser.Parse(
            ["--author-period-manifest", "portfolio.json", "--manifest", "prs.json"]);

        ChangePortfolioCommandOptions options = Assert.IsType<ChangePortfolioCommandOptions>(parsed.Options);
        Assert.Null(parsed.Error);
        Assert.True(options.IsAuthorPeriodManifest);
        Assert.Equal("portfolio.json", options.AuthorPeriodManifestPath);
        Assert.Equal(EstimationProfile.Recreation, options.Profile);
        Assert.Contains("omit positional", positional.Error, StringComparison.Ordinal);
        Assert.Contains("valid only with --author", policy.Error, StringComparison.Ordinal);
        Assert.Contains("Select exactly one", mixed.Error, StringComparison.Ordinal);
        Assert.Contains("--author-period-manifest <path>", ChangePortfolioHelp.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void OffsetFreeDaylightSavingGapsAndAmbiguitiesRequireExplicitOffsets()
    {
        TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone(
            "Synthetic-Eastern",
            TimeSpan.FromHours(-5),
            "Synthetic Eastern",
            "Synthetic Standard",
            "Synthetic Daylight",
            [TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                new DateTime(2026, 1, 1),
                new DateTime(2026, 12, 31),
                TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
                    new DateTime(1, 1, 1, 2, 0, 0),
                    3,
                    2,
                    DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
                    new DateTime(1, 1, 1, 2, 0, 0),
                    11,
                    1,
                    DayOfWeek.Sunday))]);

        Assert.False(ChangePortfolioTimeParser.TryParse(
            "2026-03-08T02:30:00",
            zone,
            out _,
            out string? skippedError));
        Assert.Contains("skipped", skippedError, StringComparison.Ordinal);
        Assert.False(ChangePortfolioTimeParser.TryParse(
            "2026-11-01T01:30:00",
            zone,
            out _,
            out string? ambiguousError));
        Assert.Contains("ambiguous", ambiguousError, StringComparison.Ordinal);
        Assert.True(ChangePortfolioTimeParser.TryParse(
            "2026-11-01T01:30:00-04:00",
            zone,
            out DateTimeOffset explicitOffset,
            out _));
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero), explicitOffset);
    }

    [Fact]
    public void ManifestContractIsVersionedBoundedAndRejectsDuplicateIds()
    {
        ChangePortfolioManifest manifest = new()
        {
            Items =
            [
                new ChangePortfolioManifestItem
                {
                    Id = "api-pr",
                    RepositoryId = "api",
                    RepositoryPath = "repositories/api",
                    PullRequest = "42",
                    GitHubRepository = "example/api",
                },
            ],
        };
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioManifest,
            ContractJson.Serialize(manifest));
        ChangePortfolioManifest duplicate = manifest with
        {
            Items = [manifest.Items[0], manifest.Items[0]],
        };

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(manifest));
        Assert.Contains(ContractValidation.Validate(duplicate), error =>
            error.Contains("duplicated", StringComparison.Ordinal));
    }

    private static GitAuthorPeriodPortfolioOptions Options() => new()
    {
        Aliases = ["target@example.test"],
        SinceInclusive = Since,
        UntilExclusive = Until,
        TimeZone = "UTC",
        DateField = ChangePortfolioDateField.Author,
        MergePolicy = ChangePortfolioMergePolicy.Exclude,
        CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include,
    };

    private static GitCommitMetadata Commit(
        string digit,
        DateTimeOffset authorTimestamp,
        DateTimeOffset? committerTimestamp = null,
        IReadOnlyList<string>? parents = null,
        string authorName = "Other",
        string authorEmail = "other@example.test",
        IReadOnlyList<GitCommitIdentity>? coauthors = null) => new()
        {
            ObjectId = new string(digit[0], 40),
            ParentObjectIds = parents ?? [],
            Author = new GitCommitIdentity(authorName, authorEmail),
            AuthorTimestamp = authorTimestamp,
            Committer = new GitCommitIdentity("Integrator", "integrator@example.test"),
            CommitterTimestamp = committerTimestamp ?? authorTimestamp,
            Coauthors = coauthors ?? [],
        };
}
