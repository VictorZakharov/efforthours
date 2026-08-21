using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    private static readonly DateTimeOffset Since = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Until = new(2026, 4, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void InputContractsAreSchemaValidOrderIndependentAndRejectIncompleteCapacity()
    {
        ChangePortfolioBucketManifest buckets = Buckets();
        ChangePortfolioCapacityManifest capacity = Capacity();

        AssertSchema(SchemaNames.ChangePortfolioBucketManifest, ContractJson.Serialize(buckets));
        AssertSchema(SchemaNames.ChangePortfolioCapacityManifest, ContractJson.Serialize(capacity));
        Assert.Empty(ContractValidation.Validate(buckets));
        Assert.Empty(ContractValidation.Validate(capacity));
        Assert.Equal(
            ChangePortfolioComparisonIdentity.ComputeBucketDigest(buckets),
            ChangePortfolioComparisonIdentity.ComputeBucketDigest(
                buckets with { Buckets = [.. buckets.Buckets.Reverse()] }));
        Assert.Equal(
            ChangePortfolioComparisonIdentity.ComputeCapacityDigest(capacity),
            ChangePortfolioComparisonIdentity.ComputeCapacityDigest(
                capacity with { Entries = [.. capacity.Entries.Reverse()] }));

        ChangePortfolioCapacityManifest invalid = capacity with
        {
            Entries = [.. capacity.Entries, capacity.Entries[0]],
        };
        Assert.Contains(
            ContractValidation.Validate(invalid),
            error => error.Contains("duplicated", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ComparisonAllocatesOnePortfolioAcrossBucketsAndRendersBothViews()
    {
        ChangeAuthorPeriodManifest manifest = Manifest();
        ChangePortfolioReport source = await SourceReportAsync(manifest);
        ChangePortfolioComparisonReport report = ChangePortfolioComparisonBuilder.Build(
            source,
            BuildOptions(manifest));

        Assert.Empty(ContractValidation.Validate(report));
        string json = new ChangePortfolioComparisonJsonRenderer().Render(report);
        AssertSchema(SchemaNames.ChangePortfolioComparisonReport, json);
        Assert.DoesNotContain("person-a@example.test", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-repository-path", json, StringComparison.OrdinalIgnoreCase);

        ChangePortfolioComparisonSeries portfolio = Assert.Single(
            report.Series,
            series => series.Kind == ChangePortfolioSeriesKind.Portfolio);
        Assert.Equal(source.TotalEffort, portfolio.TotalEffort);
        Assert.Equal(4, portfolio.Points.Count);
        Assert.Equal(0, portfolio.Points[1].SelectedChangeCount);
        Assert.Equal(new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero), report.Buckets[^1].UntilExclusive);
        Assert.True(report.Buckets[^1].PartialEnd);
        Assert.Equal(2, portfolio.Trend!.CapacityWeightedRollingWindow.Count);
        Assert.Equal(
            portfolio.TotalEffort,
            Sum(report.Series.Where(series => series.AdditiveToPortfolio)
                .Select(series => series.TotalEffort)));

        string markdown = ChangePortfolioComparisonMarkdownRenderer.Render(report);
        Assert.DoesNotContain('\r', markdown);
        Assert.Contains("# Synthetic portfolio trend", markdown, StringComparison.Ordinal);
        Assert.Contains("```mermaid", markdown, StringComparison.Ordinal);
        Assert.Contains("Numeric fallback:", markdown, StringComparison.Ordinal);
        Assert.Contains("Contributor comparison matrix", markdown, StringComparison.Ordinal);
        Assert.Contains("Partial-period note", markdown, StringComparison.Ordinal);
        Assert.Contains("Series order: `portfolio`, `contributor-contributor-a`, `contributor-contributor-b`", markdown, StringComparison.Ordinal);
        Assert.Contains("repositories explicitly supplied in the manifest", markdown, StringComparison.Ordinal);
        Assert.Contains("association", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("person-a@example.test", markdown, StringComparison.OrdinalIgnoreCase);

        ChangePortfolioComparisonReport findings = report with
        {
            View = ChangePortfolioComparisonView.Findings,
            Title = "Synthetic engineering findings",
        };
        string findingsMarkdown = ChangePortfolioComparisonMarkdownRenderer.Render(findings);
        Assert.DoesNotContain('\r', findingsMarkdown);
        Assert.Contains("# Synthetic engineering findings", findingsMarkdown, StringComparison.Ordinal);
        Assert.Contains("Version and environment boundary", findingsMarkdown, StringComparison.Ordinal);
        Assert.Contains("Repository outcomes", findingsMarkdown, StringComparison.Ordinal);
        Assert.Contains("Phase timings and last progress", findingsMarkdown, StringComparison.Ordinal);
        Assert.Contains("Reuse and processing volume", findingsMarkdown, StringComparison.Ordinal);
        Assert.Contains("Sanitized reproduction shape", findingsMarkdown, StringComparison.Ordinal);
        Assert.Contains("Data-handling notes", findingsMarkdown, StringComparison.Ordinal);
        Assert.Contains("repositories explicitly supplied in the manifest", findingsMarkdown, StringComparison.Ordinal);
        Assert.DoesNotContain("private-repository-path", findingsMarkdown, StringComparison.OrdinalIgnoreCase);
        // Whole-document golden digests freeze every generated heading, table, chart,
        // fallback series, calculation, caveat, and findings section without a bulky fixture.
        Assert.Equal(
            "sha256:7d2d1e24a38846a996fede842f127fe9a7b3139fcfad4af820c2c2063f122af8",
            ChangePortfolioComparisonIdentity.ComputeTextDigest(markdown));
        Assert.Equal(
            "sha256:e3f592a25a7877c00326810405b7d53ad26ae3799912985f89c1abf93054bd29",
            ChangePortfolioComparisonIdentity.ComputeTextDigest(findingsMarkdown));
    }

    private static ChangePortfolioComparisonBuildOptions BuildOptions(
        ChangeAuthorPeriodManifest manifest) => new()
        {
            View = ChangePortfolioComparisonView.Trend,
            Title = "Synthetic portfolio trend",
            GeneratedAt = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero),
            CliVersion = "0.10.0-test",
            Profile = EstimationProfile.Implementation,
            BucketKind = ChangePortfolioBucketPolicyKind.Custom,
            BucketPolicy = ChangePortfolioComparisonPolicies.CustomClosedBucketsV1,
            BucketManifest = Buckets(),
            Buckets =
            [
                .. Buckets().Buckets.Select((bucket, index) => new ChangePortfolioComparisonBucket
                {
                    Id = bucket.Id,
                    Label = bucket.Label,
                    SinceInclusive = bucket.SinceInclusive,
                    UntilExclusive = bucket.UntilExclusive,
                    PartialStart = false,
                    PartialEnd = index == 3,
                }),
            ],
            CapacityManifest = Capacity(),
            SourceManifest = manifest,
            ExecutionTelemetry = new ChangePortfolioExecutionTelemetry(),
            ExecutionStatistics = new ChangePortfolioExecutionStatistics(),
            ExecutionOverride = FixedExecution(manifest),
        };

    private static ChangePortfolioComparisonExecution FixedExecution(
        ChangeAuthorPeriodManifest manifest)
    {
        DateTimeOffset observedAt = new(2026, 8, 20, 11, 59, 0, TimeSpan.Zero);
        ChangePortfolioComparisonProgress progress = new()
        {
            ObservedAt = observedAt,
            Phase = ChangePortfolioExecutionPhases.StaticAnalysis,
            ProcessedUnits = 6,
            TotalUnits = 6,
            AnalysisCacheRequests = 12,
            AnalysisCacheHits = 7,
            ElapsedMilliseconds = 45.25m,
            WorkingSetBytes = 96 * 1024 * 1024,
            PeakWorkingSetBytes = 112 * 1024 * 1024,
        };
        ChangePortfolioComparisonPhaseTiming[] timings =
        [
            new()
            {
                Phase = ChangePortfolioExecutionPhases.HistoryUnion,
                ElapsedMilliseconds = 12.5m,
            },
            new()
            {
                Phase = ChangePortfolioExecutionPhases.StaticAnalysis,
                ElapsedMilliseconds = 45.25m,
            },
        ];
        return new ChangePortfolioComparisonExecution
        {
            RuntimeFramework = "test-runtime/1.0",
            OperatingSystemFamily = "other",
            ProcessArchitecture = "test-architecture",
            LogicalProcessorCount = 8,
            ShardPolicy = ChangePortfolioComparisonPolicies.RepositoryEvidenceShardsV1,
            Checkpoint = new ChangePortfolioComparisonCheckpoint
            {
                Enabled = true,
                HitCount = manifest.Repositories.Count,
            },
            RepositoryShardCount = manifest.Repositories.Count,
            Repositories = [.. manifest.Repositories
                .OrderBy(repository => repository.Id, StringComparer.Ordinal)
                .Select(repository => new ChangePortfolioComparisonRepositoryExecution
                {
                    RepositoryId = repository.Id,
                    Status = ChangePortfolioRepositoryExecutionStatus.Reused,
                    CheckpointDisposition = ChangePortfolioCheckpointDisposition.Hit,
                    SelectedChangeCount = repository.Id == "repository-a" ? 3 : 0,
                    ElapsedMilliseconds = 57.75m,
                    InputDigest = ChangePortfolioComparisonIdentity.ComputeRepositoryInputDigest(
                        manifest,
                        repository.Id,
                        EstimationProfile.Implementation,
                        ChangeEstimator.Version),
                    PhaseTimings = timings,
                    LastProgress = progress,
                })],
            PhaseTimings = timings,
            LastProgress = progress,
            Reuse = new ChangePortfolioComparisonReuse
            {
                SnapshotAnalysisRequests = 12,
                SnapshotAnalysisHits = 7,
                UniqueSnapshotAnalysisKeys = 5,
                AnalysisArtifactRequests = 20,
                AnalysisArtifactHits = 14,
                UniqueAnalysisArtifactKeys = 6,
                SnapshotInventoryRequests = 12,
                SnapshotInventoryHits = 8,
                UniqueSnapshotInventoryObjects = 4,
                BlobRequests = 30,
                BlobCacheHits = 24,
                UniqueBlobObjects = 6,
                BlobReadBytes = 4096,
                PeakWorkingSetBytes = progress.PeakWorkingSetBytes,
            },
            Failures = [],
        };
    }

    private static async Task<ChangePortfolioReport> SourceReportAsync(
        ChangeAuthorPeriodManifest manifest)
    {
        List<ChangePortfolioCandidate> candidates = [];
        candidates.Add(await CandidateAsync(
            "alpha",
            Since.AddDays(10),
            [Match("contributor-a", ChangePortfolioContributorMatchKind.DirectAuthor)]));
        candidates.Add(await CandidateAsync(
            "beta",
            new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero),
            [Match("contributor-b", ChangePortfolioContributorMatchKind.DirectAuthor)]));
        candidates.Add(await CandidateAsync(
            "shared",
            new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero),
            [
                Match("contributor-a", ChangePortfolioContributorMatchKind.DirectAuthor),
                Match("contributor-b", ChangePortfolioContributorMatchKind.Coauthor),
            ]));
        return ChangePortfolioReconciler.Reconcile(
            Selection(manifest),
            candidates,
            EstimationProfile.Implementation);
    }

    private static async Task<ChangePortfolioCandidate> CandidateAsync(
        string id,
        DateTimeOffset timestamp,
        IReadOnlyList<ChangePortfolioContributorMatch> matches)
    {
        const string project =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n";
        InMemoryChangeSnapshot before = new(("Demo.csproj", project));
        InMemoryChangeSnapshot after = new(
            ("Demo.csproj", project),
            ($"{id}.cs", $"namespace Demo; public sealed class {id} {{ public int Value => {id.Length}; }}\n"));
        ChangeEstimateReport isolated = await new ChangeEstimator().EstimateAsync(
            new GitChangePlan
            {
                RepositoryPath = "private-repository-path",
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
        return new ChangePortfolioCandidate
        {
            RepositoryId = "repository-a",
            SelectorId = $"repository-a:commit:{id}",
            Report = isolated,
            Attribution = new ChangePortfolioAttribution
            {
                Kind = matches.Any(match =>
                    match.Kind == ChangePortfolioContributorMatchKind.DirectAuthor)
                    ? ChangePortfolioAttributionKind.DirectAuthor
                    : ChangePortfolioAttributionKind.Coauthor,
                SelectedTimestamp = timestamp,
                ParentCount = 1,
                ContributorMatches = matches,
                HeadIds = ["default"],
            },
        };
    }

    private static ChangePortfolioContributorMatch Match(
        string id,
        ChangePortfolioContributorMatchKind kind) => new()
        {
            ContributorId = id,
            Kind = kind,
        };

    private static ChangePortfolioSelection Selection(ChangeAuthorPeriodManifest manifest) =>
        ChangeAuthorPeriodManifestIdentity.CreateReportSelection(manifest);

    private static ChangeAuthorPeriodManifest Manifest() => new()
    {
        Selection = new ChangeAuthorPeriodManifestSelection
        {
            SinceInclusive = Since,
            UntilExclusive = Until,
            TimeZone = "UTC",
            DateField = ChangePortfolioDateField.Author,
            MergePolicy = ChangePortfolioMergePolicy.Exclude,
            CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include,
        },
        Contributors =
        [
            new ChangeAuthorPeriodManifestContributor
            {
                Id = "contributor-a",
                Aliases = ["person-a@example.test"],
            },
            new ChangeAuthorPeriodManifestContributor
            {
                Id = "contributor-b",
                Aliases = ["person-b@example.test"],
            },
        ],
        Repositories =
        [
            new ChangeAuthorPeriodManifestRepository
            {
                Id = "repository-a",
                RepositoryPath = "private-repository-path",
                Heads =
                [
                    new ChangeAuthorPeriodManifestHead
                    {
                        Id = "default",
                        ObjectId = new string('a', 40),
                    },
                ],
            },
        ],
    };

    private static ChangePortfolioBucketManifest Buckets() => new()
    {
        Buckets =
        [
            Bucket("2026-01", "January 2026", Since, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)),
            Bucket("2026-02", "February 2026", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)),
            Bucket("2026-03", "March 2026", new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero)),
            Bucket("2026-04", "April 2026 partial", new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), Until),
        ],
    };

    private static ChangePortfolioBucketDefinition Bucket(
        string id,
        string label,
        DateTimeOffset since,
        DateTimeOffset until) => new()
        {
            Id = id,
            Label = label,
            SinceInclusive = since,
            UntilExclusive = until,
        };

    private static ChangePortfolioCapacityManifest Capacity() => new()
    {
        CalendarPolicy = "Synthetic weekday capacity; final month prorated through April 14.",
        Entries =
        [
            .. Buckets().Buckets.SelectMany(bucket => new[]
            {
                new ChangePortfolioCapacityEntry
                {
                    BucketId = bucket.Id,
                    ContributorId = "contributor-a",
                    Hours = bucket.Id == "2026-04" ? 80m : 160m,
                },
                new ChangePortfolioCapacityEntry
                {
                    BucketId = bucket.Id,
                    ContributorId = "contributor-b",
                    Hours = bucket.Id == "2026-04" ? 80m : 160m,
                },
            }),
        ],
    };

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.GitTree,
    };

    private static EffortRange Sum(IEnumerable<EffortRange> ranges) => ranges.Aggregate(
        new EffortRange { Low = 0m, Expected = 0m, High = 0m },
        (left, right) => new EffortRange
        {
            Low = left.Low + right.Low,
            Expected = left.Expected + right.Expected,
            High = left.High + right.High,
        });

    private static void AssertSchema(string name, string json)
    {
        SchemaValidationResult result = ContractSchemaValidator.Validate(name, json);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }
}
