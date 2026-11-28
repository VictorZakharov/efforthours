using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class RepositoryPathExclusionTests
{
    [Theory]
    [InlineData("/Content/Pack/**")]
    [InlineData("Content/../Pack/**")]
    [InlineData("./Content")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("C:/Pack")]
    [InlineData("Content\\Pack")]
    [InlineData("Content//Pack")]
    [InlineData("Content/[Pp]ack")]
    [InlineData("!Content")]
    [InlineData("**")]
    [InlineData("**/*")]
    [InlineData("**/**")]
    public void InvalidOrEverythingGlobsAreRejected(string glob)
    {
        Assert.NotEmpty(RepositoryPathExclusion.Validate([glob]));
        Assert.Throws<InvalidDataException>(() => RepositoryPathExclusion.Create([glob]));
    }

    [Fact]
    public void TheMaximumNumberOfLongExactAndWildcardGlobsCompiles()
    {
        string[] globs = [.. Enumerable.Range(0, RepositoryPathExclusion.MaximumGlobs).Select(index => index % 2 == 0
            ? $"Content/Alpine_Mountains_Villa/Meshes/Group{index}/SM_asset_with_a_long_descriptive_name_{index}.uasset"
            : $"Content/__ExternalActors__/Pack{index}/**/*_?{index}.uasset")];
        RepositoryPathExclusion exclusion = RepositoryPathExclusion.Create(globs)!;
        Assert.True(exclusion.Excludes(globs[0]));
        Assert.True(exclusion.Excludes("Content/__ExternalActors__/Pack1/A/B/x_y1.uasset"));
        Assert.False(exclusion.Excludes("Content/Villa/BP_Door.uasset"));
    }

    [Fact]
    public void EmptyAndDuplicateListsAreRejectedAndAbsentExclusionIsNull()
    {
        Assert.NotEmpty(RepositoryPathExclusion.Validate([]));
        Assert.NotEmpty(RepositoryPathExclusion.Validate(["Pack/**", "Pack/**"]));
        Assert.Empty(RepositoryPathExclusion.Validate(null));
        Assert.Null(RepositoryPathExclusion.Create(null));
    }

    [Fact]
    public void GlobsUseAreaSelectorSemanticsAndOrderIndependentDigests()
    {
        RepositoryPathExclusion exclusion = RepositoryPathExclusion.Create(
            ["Content/__ExternalActors__/LocomotionAnimPack/**", "Content/Fab/MetaHuman", "Docs/*.txt", "*.md"])!;
        Assert.True(exclusion.Excludes("Content/__ExternalActors__/LocomotionAnimPack/A/B.uasset"));
        Assert.True(exclusion.Excludes("Content/Fab/MetaHuman/Face/M_Skin.uasset"));
        Assert.True(exclusion.Excludes("Docs/PackEdits.txt"));
        Assert.True(exclusion.Excludes("README.md"));
        Assert.False(exclusion.Excludes("Content/Fab/MetaHumanOther/A.uasset"));
        Assert.False(exclusion.Excludes("Docs/Nested/PackEdits.txt"));
        Assert.False(exclusion.Excludes("Docs/README.md"));
        Assert.False(exclusion.Excludes("Villa.uproject"));
        Assert.Equal(exclusion.Globs.Order(StringComparer.Ordinal), exclusion.Globs);
        RepositoryPathExclusion reordered = RepositoryPathExclusion.Create([.. exclusion.Globs.Reverse()])!;
        Assert.Equal(exclusion.Digest, reordered.Digest);
        Assert.NotEqual(exclusion.Digest, RepositoryPathExclusion.Create(["Docs/*.txt"])!.Digest);
    }

    [Fact]
    public void ArchiveAdmissionDropsExcludedFilesAndRecordsCountsAndBytes()
    {
        GitArchiveSnapshot archive = Snapshot(("src/Main.cs", "class Main {}"), ("Pack/A.cs", "class A {}"),
            ("Pack/Sub/B.txt", "bbbb"));
        RepositoryPathExclusion exclusion = RepositoryPathExclusion.Create(["Pack/**"])!;
        (GitArchiveSnapshot admitted, SnapshotExclusionSummary? summary) = SnapshotPortfolioExclusion.Admit(archive, exclusion);
        Assert.Equal(["src/Main.cs"], admitted.Files.Keys);
        Assert.Equal(Snapshot(("src/Main.cs", "class Main {}")).InputDigest, admitted.InputDigest);
        Assert.NotNull(summary);
        Assert.Equal(2, summary.ExcludedFileCount);
        Assert.Equal("class A {}".Length + "bbbb".Length, summary.ExcludedBytes);
        Assert.Equal(exclusion.Digest, summary.Digest);
        Assert.Equal(["Pack/**"], summary.Exclude);

        (GitArchiveSnapshot same, SnapshotExclusionSummary? none) = SnapshotPortfolioExclusion.Admit(archive, null);
        Assert.Same(archive, same);
        Assert.Null(none);

        SnapshotPlanningException empty = Assert.Throws<SnapshotPlanningException>(() =>
            SnapshotPortfolioExclusion.Admit(archive, RepositoryPathExclusion.Create(["src", "Pack"])));
        Assert.Equal("excluded-all-files", empty.Category);
        ChangeSnapshotFile[] inventory = [.. archive.Files.Keys.Select(path => new ChangeSnapshotFile
        {
            Path = path, ObjectId = new string('a', 40), Length = 1, Mode = "100644",
        })];
        Assert.Same(inventory, SnapshotPortfolioExclusion.Admit(inventory, null));
        Assert.Equal(["src/Main.cs"], SnapshotPortfolioExclusion.Admit(inventory, exclusion).Select(f => f.Path));
        Assert.Equal("excluded-all-files", Assert.Throws<SnapshotPlanningException>(() =>
            SnapshotPortfolioExclusion.Admit(inventory, RepositoryPathExclusion.Create(["src", "Pack"]))).Category);
    }

    [Fact]
    public async Task ExcludedUnrealPackagesLeaveTheDescriptorAndOwnPackagesValuedAndAreasExhaustive()
    {
        GitArchiveSnapshot archive = Snapshot(
            ("Villa.uproject", "{\"FileVersion\":3}"),
            ("Content/Villa/BP_Door.uasset", Pointer("door", 16 * 1024 + 2_560 * 300)),
            ("Content/Pack/SM_Rock.uasset", Pointer("rock", 40_000_000)),
            ("Content/Pack/M_Rock.uasset", Pointer("material", 9_000_000)));
        RepositoryPathExclusion exclusion = RepositoryPathExclusion.Create(["Content/Pack/**"])!;
        (GitArchiveSnapshot admitted, SnapshotExclusionSummary? summary) = SnapshotPortfolioExclusion.Admit(archive, exclusion);
        Assert.Equal(2, summary!.ExcludedFileCount);
        Assert.Contains("Villa.uproject", admitted.Files.Keys);

        SeedEstimator estimator = new();
        EstimateReport own = estimator.Estimate(await ScanAsync(admitted), EstimationProfile.Implementation, rateCard: null);
        EstimateReport all = estimator.Estimate(await ScanAsync(archive), EstimationProfile.Implementation, rateCard: null);
        Assert.True(own.TotalEffort.Expected > 0m);
        Assert.True(own.TotalEffort.Expected < all.TotalEffort.Expected);
        GitArchiveSnapshot authoredOnly = Snapshot(("Villa.uproject", "{\"FileVersion\":3}"),
            ("Content/Villa/BP_Door.uasset", Pointer("door", 16 * 1024 + 2_560 * 300)));
        Assert.Equal(authoredOnly.InputDigest, admitted.InputDigest);
        Assert.Equal(estimator.Estimate(await ScanAsync(authoredOnly), EstimationProfile.Implementation, rateCard: null).TotalEffort,
            own.TotalEffort);

        // Ownership is exhaustive over the admitted set: a catch-all area never sees excluded packages, and the
        // owned package still resolves through its context-only ancestor descriptor.
        IReadOnlyList<SnapshotAreaInput> areas = SnapshotAreaPartition.Partition(admitted,
            [new() { Id = "content", Include = ["Content/**"] }, new() { Id = "rest", Include = ["**"] }]);
        Assert.Equal(admitted.Files.Count, areas.Sum(area => area.OwnedFiles));
        Assert.Equal(["Villa.uproject"], areas[0].Snapshot.ContextOnlyPaths);
        Assert.DoesNotContain(areas.SelectMany(area => area.Snapshot.Files.Keys), path => path.StartsWith("Content/Pack/", StringComparison.Ordinal));
        SnapshotPlanningException unmatched = Assert.Throws<SnapshotPlanningException>(() => SnapshotAreaPartition.Partition(admitted,
            [new() { Id = "pack", Include = ["Content/Pack/**"] }, new() { Id = "rest", Include = ["**"] }]));
        Assert.Equal("unmatched-selector", unmatched.Category);
    }

    [Fact]
    public async Task UnrealDescriptorsWithAByteOrderMarkAreReadAsJson()
    {
        const string descriptor = "{\n\t\"FileVersion\": 3,\n\t\"Plugins\": [ { \"Name\": \"A\", \"Enabled\": true } ]\n}\n";
        GitArchiveSnapshot plain = Snapshot(("Villa.uproject", descriptor));
        GitArchiveSnapshot marked = new(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["Villa.uproject"] = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(descriptor)],
        });
        RepositoryEvidence before = await ScanAsync(plain);
        RepositoryEvidence after = await ScanAsync(marked);
        Assert.DoesNotContain(after.Diagnostics, diagnostic => diagnostic.Code == "FB8302");
        Assert.Equal(Plugins(before), Plugins(after));
        Assert.Equal(1m, Plugins(after));

        static decimal Plugins(RepositoryEvidence evidence) => evidence.Facts
            .SelectMany(fact => fact.Measurements).Where(item => item.Name == "enabled-plugins").Sum(item => item.Value);
    }

    [Fact]
    public void ManifestValidationRejectsUnsafeExclusionWithProjectContext()
    {
        SnapshotPortfolioManifest manifest = new()
        {
            Year = 2026, Timezone = "UTC", Profile = EstimationProfile.Implementation,
            Projects = [new() { Id = "villa", Ref = "main", Areas = [new() { Id = "all", Include = ["**"] }], Exclude = ["../outside/**"] }],
        };
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => SnapshotPortfolioValidation.Validate(manifest));
        Assert.Contains("villa", error.Message, StringComparison.Ordinal);
        SnapshotPortfolioValidation.Validate(manifest with { Projects = [manifest.Projects[0] with { Exclude = ["Content/Pack/**"] }] });
        SchemaValidationResult schema = ContractSchemaValidator.Validate("snapshot-portfolio-manifest.schema.json",
            ContractJson.Serialize(manifest with { Projects = [manifest.Projects[0] with { Exclude = ["Content/Pack/**"] }] }));
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
    }

    [Fact]
    public void UnexcludedIdentityReceiptAndMeasurementKeysKeepTheirPreviousSerialization()
    {
        MeasurementIdentity identity = SnapshotMeasurementIdentity.Create(EstimationProfile.Implementation);
        string json = ContractJson.SerializeCompact(identity);
        Assert.DoesNotContain("exclusion", json, StringComparison.OrdinalIgnoreCase);
        // The alpha.44 measurement-key and binding-key shapes, rebuilt independently of the new overloads.
        Assert.Equal(SnapshotMeasurementIdentity.Digest(new { inputDigest = Digest, identity, selected = 3, context = 1 }),
            SnapshotPortfolioStore.MeasurementKey(Digest, identity, 3, 1));
        Assert.Equal(SnapshotMeasurementIdentity.Digest(new { objectId = "abc", identity, areasDigest = "" }),
            SnapshotPortfolioRunner.BindingKey("abc", identity, ""));

        RepositoryPathExclusion exclusion = RepositoryPathExclusion.Create(["Pack/**"])!;
        MeasurementIdentity excluded = SnapshotMeasurementIdentity.Create(EstimationProfile.Implementation, exclusionDigest: exclusion.Digest);
        Assert.NotEqual(SnapshotPortfolioRunner.BindingKey("abc", identity, ""), SnapshotPortfolioRunner.BindingKey("abc", excluded, ""));
        SnapshotExclusionSummary summary = new() { Digest = exclusion.Digest, Exclude = exclusion.Globs, ExcludedFileCount = 2, ExcludedBytes = 9 };
        Assert.NotEqual(SnapshotPortfolioStore.MeasurementKey(Digest, excluded, 3, 0, summary),
            SnapshotPortfolioStore.MeasurementKey(Digest, excluded, 3, 0, summary with { ExcludedFileCount = 3 }));
        Assert.Equal(SnapshotMeasurementIdentity.Digest(identity), SnapshotMeasurementIdentity.Digest(excluded with { ExclusionDigest = null }));

        SnapshotMeasurementReceipt receipt = Receipt(identity, null);
        Assert.DoesNotContain("exclusion", ContractJson.SerializeCompact(receipt), StringComparison.OrdinalIgnoreCase);
        SnapshotPortfolioValidation.Validate(receipt);
        SnapshotMeasurementReceipt recorded = Receipt(excluded, summary);
        SnapshotPortfolioValidation.Validate(recorded);
        Assert.True(ContractSchemaValidator.Validate("snapshot-measurement-receipt.schema.json", ContractJson.Serialize(recorded)).IsValid);
        Assert.Throws<InvalidDataException>(() => SnapshotPortfolioValidation.Validate(Receipt(identity, summary)));
        Assert.Throws<InvalidDataException>(() => SnapshotPortfolioValidation.Validate(
            Receipt(excluded, summary with { Exclude = ["Other/**"] })));
    }

    [Fact]
    public void ChangeAdmissionComposesExclusionOnlyWhenConfigured()
    {
        Assert.Null(ChangePathAdmission.WithExclusion(null, null));
        ChangePathAdmission scope = EngineeringScopeProfile.Load().CreateAdmission("owner/repository");
        Assert.Same(scope, ChangePathAdmission.WithExclusion(scope, null));
        ChangePathAdmission excluded = ChangePathAdmission.WithExclusion(null, ["Pack/**"])!;
        Assert.False(excluded.Admits("Pack/Rock.cs"));
        Assert.True(excluded.Admits("src/Main.cs"));
        ChangePathAdmission scoped = ChangePathAdmission.WithExclusion(scope, ["Pack/**"])!;
        Assert.False(scoped.Admits("Pack/Rock.cs"));
        Assert.Equal(scope.Admits("src/Main.cs"), scoped.Admits("src/Main.cs"));
        Assert.NotEqual(scope.ProfileDigest, scoped.ProfileDigest);
        Assert.NotEqual(excluded.ProfileDigest, scoped.ProfileDigest);
        Assert.Equal(excluded.ProfileDigest, ChangePathAdmission.WithExclusion(null, ["Pack/**"])!.ProfileDigest);
        Assert.Throws<InvalidDataException>(() => ChangePathAdmission.WithExclusion(null, ["/abs"]));
    }

    [Fact]
    public void AuthorPeriodManifestBindsExclusionAndValidatesIt()
    {
        ChangeAuthorPeriodManifest manifest = AuthorPeriod(null);
        string plain = ChangeAuthorPeriodManifestIdentity.ComputeDigest(manifest);
        string excluded = ChangeAuthorPeriodManifestIdentity.ComputeDigest(AuthorPeriod(["Pack/**", "Fab/**"]));
        Assert.NotEqual(plain, excluded);
        Assert.Equal(excluded, ChangeAuthorPeriodManifestIdentity.ComputeDigest(AuthorPeriod(["Fab/**", "Pack/**"])));
        Assert.NotEqual(
            ChangePortfolioComparisonIdentity.ComputeRepositoryInputDigest(manifest, "synthetic", EstimationProfile.Implementation, "v", ""),
            ChangePortfolioComparisonIdentity.ComputeRepositoryInputDigest(AuthorPeriod(["Pack/**"]), "synthetic", EstimationProfile.Implementation, "v", ""));
        Assert.Empty(ContractValidation.Validate(AuthorPeriod(["Pack/**"])));
        Assert.Contains(ContractValidation.Validate(AuthorPeriod(["../Pack"])), error => error.Contains("exclude", StringComparison.Ordinal));
        Assert.Contains(ContractValidation.Validate(AuthorPeriod(["**"])), error => error.Contains("exclude", StringComparison.Ordinal));
        ChangeAuthorPeriodManifest rewritten = AuthorPeriod(["Pack/**"]);
        rewritten = rewritten with { Repositories = [rewritten.Repositories[0] with { RewriteEvents = [new ChangeRewriteEvent
        {
            OriginalObjectId = new string('b', 40), RewrittenObjectId = new string('c', 40),
            OldBaseObjectId = new string('d', 40), NewBaseObjectId = new string('e', 40),
        }] }] };
        Assert.Contains(ContractValidation.Validate(rewritten), error => error.Contains("rewrite or replay", StringComparison.Ordinal));
        Assert.DoesNotContain("\"exclude\":", ContractJson.SerializeCompact(manifest), StringComparison.Ordinal);
    }

    private static readonly string Digest = "sha256:" + new string('a', 64);

    private static SnapshotMeasurementReceipt Receipt(MeasurementIdentity identity, SnapshotExclusionSummary? exclusion)
    {
        SnapshotMeasurementReceipt receipt = new()
        {
            Id = "", InputDigest = Digest, Measurement = identity, ProducerVersion = "test",
            Hours = new() { Low = 0, Expected = 0, High = 0 }, Categories = [], SelectedFileCount = 1, ContextFileCount = 0,
            EvidenceDigest = Digest, Exclusion = exclusion,
        };
        return receipt with { Id = SnapshotPortfolioValidation.ReceiptId(receipt) };
    }

    private static ChangeAuthorPeriodManifest AuthorPeriod(IReadOnlyList<string>? exclude) => new()
    {
        Selection = new ChangeAuthorPeriodManifestSelection
        {
            SinceInclusive = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UntilExclusive = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
            TimeZone = "UTC",
            DateField = ChangePortfolioDateField.Committer,
            MergePolicy = ChangePortfolioMergePolicy.Exclude,
            CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include,
        },
        Contributors = [new ChangeAuthorPeriodManifestContributor { Id = "contributor", Aliases = ["synthetic@example.test"] }],
        Repositories = [new ChangeAuthorPeriodManifestRepository
        {
            Id = "synthetic", RepositoryPath = "virtual", Exclude = exclude,
            Heads = [new ChangeAuthorPeriodManifestHead { Id = "default", ObjectId = new string('a', 40) }],
        }],
    };

    private static Task<RepositoryEvidence> ScanAsync(GitArchiveSnapshot snapshot) =>
        new RepositoryAnalysisPipeline(snapshot).ScanAsync(snapshot.RootPath,
            new RepositoryScanOptions { ContextOnlyPaths = snapshot.ContextOnlyPaths });

    private static GitArchiveSnapshot Snapshot(params (string Path, string Text)[] files) =>
        new(files.ToDictionary(f => f.Path, f => Encoding.UTF8.GetBytes(f.Text), StringComparer.Ordinal));

    private static string Pointer(string seed, long size) =>
        "version https://git-lfs.github.com/spec/v1\noid sha256:" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed))).ToLowerInvariant() +
        $"\nsize {size}\n";
}
