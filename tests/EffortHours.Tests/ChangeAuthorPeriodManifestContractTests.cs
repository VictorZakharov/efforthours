using System.Globalization;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ChangeAuthorPeriodManifestContractTests
{
    private static readonly DateTimeOffset Since =
        new(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Until = Since.AddDays(7);

    [Fact]
    public void ManifestIsSchemaValidAndSemanticallyValid()
    {
        ChangeAuthorPeriodManifest manifest = ValidManifest();

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangeAuthorPeriodManifest,
            ContractJson.Serialize(manifest));

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(manifest));
    }

    [Fact]
    public void ManifestAcceptsRaisedRepositoryAndHeadEnvelopeAndRejectsOverflow()
    {
        ChangeAuthorPeriodManifest source = ValidManifest();
        ChangeAuthorPeriodManifest atLimit = source with
        {
            Contributors = [source.Contributors[0]],
            Repositories = [.. Enumerable
                .Range(0, ChangeAuthorPeriodManifestLimits.MaximumRepositories)
                .Select(MaximumEnvelopeRepository)],
        };

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangeAuthorPeriodManifest,
            ContractJson.Serialize(atLimit));

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(atLimit));
        Assert.Equal(
            ChangeAuthorPeriodManifestLimits.MaximumHeads,
            atLimit.Repositories.Sum(repository => repository.Heads.Count));

        ChangeAuthorPeriodManifest tooManyRepositories = atLimit with
        {
            Repositories = [
                .. atLimit.Repositories,
                MaximumEnvelopeRepository(ChangeAuthorPeriodManifestLimits.MaximumRepositories),
            ],
        };
        Assert.Contains(ContractValidation.Validate(tooManyRepositories), error =>
            error.Contains(
                $"{ChangeAuthorPeriodManifestLimits.MaximumRepositories} repositories",
                StringComparison.Ordinal));
        Assert.False(ContractSchemaValidator.Validate(
            SchemaNames.ChangeAuthorPeriodManifest,
            ContractJson.Serialize(tooManyRepositories)).IsValid);

        ChangeAuthorPeriodManifest tooManyHeads = atLimit with
        {
            Repositories = [
                atLimit.Repositories[0] with
                {
                    Heads = [
                        .. atLimit.Repositories[0].Heads,
                        new ChangeAuthorPeriodManifestHead
                        {
                            Id = "third",
                            ObjectId = new string('f', 40),
                        },
                    ],
                },
                .. atLimit.Repositories.Skip(1),
            ],
        };
        Assert.Contains(ContractValidation.Validate(tooManyHeads), error =>
            error.Contains(
                $"{ChangeAuthorPeriodManifestLimits.MaximumHeads} heads",
                StringComparison.Ordinal));
    }

    [Fact]
    public void DigestIsInvariantToContributorRepositoryHeadAndAliasOrder()
    {
        ChangeAuthorPeriodManifest manifest = ValidManifest();
        ChangeAuthorPeriodManifest reordered = manifest with
        {
            Contributors = [.. manifest.Contributors.Reverse().Select(contributor => contributor with
            {
                Aliases = [.. contributor.Aliases.Reverse()],
            })],
            Repositories = [.. manifest.Repositories.Reverse().Select(repository => repository with
            {
                Heads = [.. repository.Heads.Reverse()],
            })],
        };

        Assert.Equal(
            ChangeAuthorPeriodManifestIdentity.ComputeDigest(manifest),
            ChangeAuthorPeriodManifestIdentity.ComputeDigest(reordered));
    }

    [Fact]
    public void DigestMatchesTheFrozenV1Vector()
    {
        Assert.Equal(
            "sha256:072258b22249642955c3c009c6b68c5d2da176bdfc67a14ea755c4982f903c08",
            ChangeAuthorPeriodManifestIdentity.ComputeDigest(ValidManifest()));
    }

    [Fact]
    public void DigestChangesWhenAnExecutionSelectorChanges()
    {
        ChangeAuthorPeriodManifest manifest = ValidManifest();
        ChangeAuthorPeriodManifest changed = manifest with
        {
            Contributors =
            [
                manifest.Contributors[0] with
                {
                    Aliases = ["replacement@example.test"],
                },
                manifest.Contributors[1],
            ],
        };

        Assert.NotEqual(
            ChangeAuthorPeriodManifestIdentity.ComputeDigest(manifest),
            ChangeAuthorPeriodManifestIdentity.ComputeDigest(changed));
    }

    [Fact]
    public void DigestDoesNotDependOnExecutionOnlyRepositoryPaths()
    {
        ChangeAuthorPeriodManifest manifest = ValidManifest();
        ChangeAuthorPeriodManifest relocated = manifest with
        {
            Repositories = [.. manifest.Repositories.Select(repository => repository with
            {
                RepositoryPath = $"other-checkout/{repository.Id}",
            })],
        };

        Assert.Equal(
            ChangeAuthorPeriodManifestIdentity.ComputeDigest(manifest),
            ChangeAuthorPeriodManifestIdentity.ComputeDigest(relocated));
    }

    [Fact]
    public void ValidationRejectsCrossContributorAliasesAndRepeatedRepositoryHeadObjects()
    {
        ChangeAuthorPeriodManifest manifest = ValidManifest();
        ChangeAuthorPeriodManifest invalid = manifest with
        {
            Contributors =
            [
                manifest.Contributors[0],
                manifest.Contributors[1] with
                {
                    Aliases = [manifest.Contributors[0].Aliases[0]],
                },
            ],
            Repositories =
            [
                manifest.Repositories[0] with
                {
                    Heads =
                    [
                        manifest.Repositories[0].Heads[0],
                        manifest.Repositories[0].Heads[1] with
                        {
                            ObjectId = manifest.Repositories[0].Heads[0].ObjectId,
                        },
                    ],
                },
                manifest.Repositories[1],
            ],
        };

        IReadOnlyList<string> errors = ContractValidation.Validate(invalid);

        Assert.Contains(errors, error => error.Contains("more than one contributor", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("repeats immutable head object", StringComparison.Ordinal));
    }

    [Fact]
    public void ReportSelectionContainsStableIdsAndObjectsButNoPathsOrAliases()
    {
        ChangeAuthorPeriodManifest manifest = ValidManifest();
        ChangePortfolioSelection selection = ReportSelection(manifest);

        string json = ContractJson.Serialize(selection);

        Assert.Empty(ContractValidation.Validate(selection));
        Assert.Contains("contributor-a", json, StringComparison.Ordinal);
        Assert.Contains(manifest.Repositories[0].Heads[0].ObjectId, json, StringComparison.Ordinal);
        Assert.DoesNotContain("repositories/alpha", json, StringComparison.Ordinal);
        Assert.DoesNotContain("person-a@example.test", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportSelectionRejectsNoncanonicalContributorOrder()
    {
        ChangePortfolioSelection selection = ReportSelection(ValidManifest());
        ChangePortfolioSelection reordered = selection with
        {
            AuthorPeriodManifest = selection.AuthorPeriodManifest! with
            {
                ContributorIds = [.. selection.AuthorPeriodManifest!.ContributorIds.Reverse()],
            },
        };

        Assert.Contains(ContractValidation.Validate(reordered), error =>
            error.Contains("canonical ordinal order", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ManifestBasedPortfolioReportValidatesWithoutExecutionOnlyValues()
    {
        const string project =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n";
        InMemoryChangeSnapshot before = new(("Demo.csproj", project));
        InMemoryChangeSnapshot after = new(
            ("Demo.csproj", project),
            ("Feature.cs", "namespace Demo; public sealed class Feature { }\n"));
        ChangeEstimateReport isolated = await new ChangeEstimator().EstimateAsync(
            new GitChangePlan
            {
                RepositoryPath = "virtual-repository",
                Selection = new ChangeSelection
                {
                    Kind = ChangeSelectionKind.Commit,
                    Base = Reference("base", before.ObjectId),
                    Head = Reference("head", after.ObjectId),
                    Commit = after.ObjectId,
                },
                OpenBaseAsync = _ => Task.FromResult<IChangeSnapshot>(before),
                OpenHeadAsync = _ => Task.FromResult<IChangeSnapshot>(after),
            },
            EstimationProfile.Implementation);
        ChangeAuthorPeriodManifest manifest = ValidManifest();
        ChangePortfolioReport report = ChangePortfolioReconciler.Reconcile(
            ReportSelection(manifest),
            [
                new ChangePortfolioCandidate
                {
                    RepositoryId = "repository-a",
                    SelectorId = $"repository-a:commit:{after.ObjectId}",
                    Report = isolated,
                    Attribution = new ChangePortfolioAttribution
                    {
                        Kind = ChangePortfolioAttributionKind.DirectAuthor,
                        SelectedTimestamp = Since.AddHours(1),
                        ParentCount = 1,
                        ContributorMatches =
                        [
                            new ChangePortfolioContributorMatch
                            {
                                ContributorId = "contributor-a",
                                Kind = ChangePortfolioContributorMatchKind.DirectAuthor,
                            },
                        ],
                        HeadIds = ["default"],
                    },
                },
            ],
            EstimationProfile.Implementation);
        string json = ContractJson.Serialize(report);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioReport,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(report));
        Assert.DoesNotContain("virtual-repository", json, StringComparison.Ordinal);
        Assert.DoesNotContain("person-a@example.test", json, StringComparison.Ordinal);
    }

    private static ChangePortfolioSelection ReportSelection(ChangeAuthorPeriodManifest manifest) => new()
    {
        Kind = ChangePortfolioSelectionKind.AuthorPeriod,
        ManifestBased = true,
        AuthorPeriodManifest = new ChangePortfolioAuthorPeriodManifestSelection
        {
            ManifestDigest = ChangeAuthorPeriodManifestIdentity.ComputeDigest(manifest),
            SinceInclusive = manifest.Selection.SinceInclusive,
            UntilExclusive = manifest.Selection.UntilExclusive,
            TimeZone = manifest.Selection.TimeZone,
            DateField = manifest.Selection.DateField,
            MergePolicy = manifest.Selection.MergePolicy,
            CoauthorPolicy = manifest.Selection.CoauthorPolicy,
            ContributorIds = [.. manifest.Contributors
                .Select(contributor => contributor.Id)
                .Order(StringComparer.Ordinal)],
            Repositories = [.. manifest.Repositories
                .OrderBy(repository => repository.Id, StringComparer.Ordinal)
                .Select(repository =>
                new ChangePortfolioAuthorPeriodManifestRepository
                {
                    Id = repository.Id,
                    Heads = [.. repository.Heads
                        .OrderBy(head => head.Id, StringComparer.Ordinal)
                        .Select(head =>
                        new ChangePortfolioAuthorPeriodManifestHead
                        {
                            Id = head.Id,
                            ObjectId = head.ObjectId,
                        })],
                })],
        },
    };

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.GitTree,
    };

    private static ChangeAuthorPeriodManifestRepository MaximumEnvelopeRepository(int index) => new()
    {
        Id = $"repository-{index:000}",
        RepositoryPath = $"repositories/repository-{index:000}",
        Heads =
        [
            new ChangeAuthorPeriodManifestHead
            {
                Id = "default",
                ObjectId = ((index * 2) + 1).ToString("x40", CultureInfo.InvariantCulture),
            },
            new ChangeAuthorPeriodManifestHead
            {
                Id = "open-change",
                ObjectId = ((index * 2) + 2).ToString("x40", CultureInfo.InvariantCulture),
            },
        ],
    };

    private static ChangeAuthorPeriodManifest ValidManifest() => new()
    {
        Selection = new ChangeAuthorPeriodManifestSelection
        {
            SinceInclusive = Since,
            UntilExclusive = Until,
            TimeZone = "America/Toronto",
            DateField = ChangePortfolioDateField.Author,
            MergePolicy = ChangePortfolioMergePolicy.Exclude,
            CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include,
        },
        Contributors =
        [
            new ChangeAuthorPeriodManifestContributor
            {
                Id = "contributor-a",
                Aliases = ["Person A", "person-a@example.test"],
            },
            new ChangeAuthorPeriodManifestContributor
            {
                Id = "contributor-b",
                Aliases = ["Person B", "person-b@example.test"],
            },
        ],
        Repositories =
        [
            new ChangeAuthorPeriodManifestRepository
            {
                Id = "repository-a",
                RepositoryPath = "repositories/alpha",
                Heads =
                [
                    new ChangeAuthorPeriodManifestHead
                    {
                        Id = "default",
                        ObjectId = new string('a', 40),
                    },
                    new ChangeAuthorPeriodManifestHead
                    {
                        Id = "open-change",
                        ObjectId = new string('b', 40),
                    },
                ],
            },
            new ChangeAuthorPeriodManifestRepository
            {
                Id = "repository-b",
                RepositoryPath = "repositories/beta",
                Heads =
                [
                    new ChangeAuthorPeriodManifestHead
                    {
                        Id = "default",
                        ObjectId = new string('c', 40),
                    },
                ],
            },
        ],
    };
}
