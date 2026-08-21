using System.Runtime.InteropServices;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record ChangePortfolioComparisonBuildOptions
{
    public required ChangePortfolioComparisonView View { get; init; }

    public required string Title { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }

    public DateTimeOffset? AsOf { get; init; }

    public ChangePortfolioHostDiscovery? Discovery { get; init; }

    public required string CliVersion { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required ChangePortfolioBucketPolicyKind BucketKind { get; init; }

    public required string BucketPolicy { get; init; }

    public ChangePortfolioContributorNormalization ContributorNormalization { get; init; } =
        ChangePortfolioContributorNormalization.Joint;

    public required ChangePortfolioBucketManifest BucketManifest { get; init; }

    public IReadOnlyList<ChangePortfolioComparisonBucket> Buckets { get; init; } = [];

    public ChangePortfolioCapacityManifest? CapacityManifest { get; init; }

    public required ChangeAuthorPeriodManifest SourceManifest { get; init; }

    public ChangePortfolioExecutionTelemetry? ExecutionTelemetry { get; init; }

    public ChangePortfolioExecutionStatistics? ExecutionStatistics { get; init; }

    public ChangePortfolioComparisonExecution? ExecutionOverride { get; init; }
}

public static partial class ChangePortfolioComparisonBuilder
{
    public static ChangePortfolioComparisonReport Build(
        ChangePortfolioReport source,
        ChangePortfolioComparisonBuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        IReadOnlyList<string> sourceErrors = ContractValidation.Validate(source);
        if (sourceErrors.Count > 0)
        {
            throw new ArgumentException(
                "The source portfolio report is invalid: " + string.Join(" ", sourceErrors),
                nameof(source));
        }

        if (source.Aggregation is null || source.Selection.AuthorPeriodManifest is null)
        {
            throw new ArgumentException(
                "Time-bucketed comparison requires a manifest author-period portfolio.",
                nameof(source));
        }

        Dictionary<string, ChangePortfolioItemEstimate> items = source.Items.ToDictionary(
            item => item.Id,
            StringComparer.Ordinal);
        IReadOnlyList<ChangePortfolioComparisonSeries> additive = BuildAdditiveSeries(
            source,
            options,
            items);
        ChangePortfolioComparisonSeries portfolio = BuildPortfolioSeries(
            source,
            options,
            additive);
        IReadOnlyList<ChangePortfolioComparisonSeries> contributorSeries =
            options.ContributorNormalization == ChangePortfolioContributorNormalization.Isolated
                ? BuildIsolatedContributorSeries(source, options)
                : additive;
        IReadOnlyList<ChangePortfolioComparisonSeries> series =
        [
            portfolio,
            .. contributorSeries.OrderBy(value => value.Kind).ThenBy(value => value.Id, StringComparer.Ordinal),
        ];
        ChangePortfolioComparisonBucketPolicy bucketPolicy = new()
        {
            Kind = options.BucketKind,
            Policy = options.BucketPolicy,
            InputDigest = ChangePortfolioComparisonIdentity.ComputeBucketDigest(options.BucketManifest),
            ContributorNormalization = options.ContributorNormalization,
            CapacityCalendarPolicy = options.CapacityManifest?.CalendarPolicy,
            CapacityInputDigest = options.CapacityManifest is null
                ? null
                : ChangePortfolioComparisonIdentity.ComputeCapacityDigest(options.CapacityManifest),
        };
        string sourceDigest = ChangePortfolioComparisonIdentity.ComputePortfolioDigest(source);
        ChangePortfolioComparisonReport report = new()
        {
            Status = ChangePortfolioComparisonStatus.Complete,
            View = options.View,
            Title = options.Title,
            GeneratedAt = options.GeneratedAt.ToUniversalTime(),
            AsOf = options.AsOf?.ToUniversalTime(),
            Discovery = options.Discovery,
            CliVersion = options.CliVersion,
            EstimatorVersion = source.EstimatorVersion,
            SourceChangeEstimatorVersion = source.SourceChangeEstimatorVersion,
            Profile = source.Profile,
            Selection = source.Selection,
            BucketPolicy = bucketPolicy,
            SourcePortfolio = source,
            Buckets = options.Buckets,
            Series = series,
            Execution = options.ExecutionOverride ?? BuildExecution(source, options),
            Diagnostics = ComparisonDiagnostics(options),
            Verification = new ChangePortfolioComparisonVerification
            {
                SemanticDigest = ChangePortfolioComparisonIdentity.ComputeSemanticDigest(
                    source,
                    bucketPolicy,
                    options.Buckets,
                    series),
                SourcePortfolioDigest = sourceDigest,
                BucketAllocationPolicy = ChangePortfolioComparisonIdentity.ContributorSeriesPolicy(
                    options.ContributorNormalization),
                CompleteAggregates = true,
                ExecutionOnlyPathsExcluded = true,
                RawAliasesExcluded = true,
                Note = "Execution timings and resource samples are operational observations and are excluded from the semantic digest.",
            },
        };
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "The time-bucketed comparison report is invalid: " + string.Join(" ", errors));
        }

        return report;
    }

    private static List<ChangePortfolioComparisonSeries> BuildAdditiveSeries(
        ChangePortfolioReport source,
        ChangePortfolioComparisonBuildOptions options,
        IReadOnlyDictionary<string, ChangePortfolioItemEstimate> items)
    {
        List<ChangePortfolioComparisonSeries> result = [];
        foreach (ChangePortfolioContributorGroup group in source.Aggregation!.ContributorGroups)
        {
            Dictionary<string, EffortRange> effort = options.Buckets.ToDictionary(
                bucket => bucket.Id,
                _ => Zero(),
                StringComparer.Ordinal);
            Dictionary<string, int> counts = options.Buckets.ToDictionary(
                bucket => bucket.Id,
                _ => 0,
                StringComparer.Ordinal);
            foreach (ChangePortfolioContributorRepositoryAllocation allocation in
                group.RepositoryAllocations)
            {
                AllocateRepositoryGroup(options.Buckets, allocation, items, effort, counts);
            }

            bool single = group.Kind == ChangePortfolioContributorGroupKind.SingleContributor;
            string id = single
                ? "contributor-" + group.ContributorIds[0]
                : "shared-" + group.Id[(group.Id.LastIndexOf(':') + 1)..];
            string? contributorId = single ? group.ContributorIds[0] : null;
            ChangePortfolioComparisonPoint[] points =
            [.. options.Buckets.Select(bucket => CreatePoint(
                bucket.Id,
                counts[bucket.Id],
                effort[bucket.Id],
                Capacity(options.CapacityManifest, bucket.Id, contributorId)))];
            result.Add(CreateSeries(
                id,
                single
                    ? ChangePortfolioSeriesKind.ContributorExclusive
                    : ChangePortfolioSeriesKind.SharedContributors,
                group.ContributorIds,
                additiveToPortfolio: true,
                points,
                single && options.CapacityManifest is not null));
        }

        return result;
    }

    private static void AllocateRepositoryGroup(
        IReadOnlyList<ChangePortfolioComparisonBucket> buckets,
        ChangePortfolioContributorRepositoryAllocation allocation,
        IReadOnlyDictionary<string, ChangePortfolioItemEstimate> items,
        Dictionary<string, EffortRange> effort,
        Dictionary<string, int> counts)
    {
        Dictionary<string, List<ChangePortfolioItemEstimate>> byBucket = buckets.ToDictionary(
            bucket => bucket.Id,
            _ => new List<ChangePortfolioItemEstimate>(),
            StringComparer.Ordinal);
        foreach (string itemId in allocation.ItemIds)
        {
            ChangePortfolioItemEstimate item = items[itemId];
            DateTimeOffset timestamp = item.Attribution.SelectedTimestamp ??
                throw new InvalidOperationException(
                    $"Portfolio item '{item.Id}' lacks its selected timestamp.");
            ChangePortfolioComparisonBucket bucket = buckets.Single(candidate =>
                timestamp >= candidate.SinceInclusive && timestamp < candidate.UntilExclusive);
            byBucket[bucket.Id].Add(item);
        }

        ChangePortfolioAggregateAllocationInput[] inputs =
        [.. buckets.Select(bucket => new ChangePortfolioAggregateAllocationInput(
            bucket.Id,
            byBucket[bucket.Id].Sum(item => item.AllocatedExpectedHours),
            byBucket[bucket.Id].Sum(item => item.IsolatedEffort.High)))];
        IReadOnlyDictionary<string, EffortRange> allocated =
            ChangePortfolioAggregateAllocation.Allocate(allocation.NormalizedEffort, inputs);
        foreach (ChangePortfolioComparisonBucket bucket in buckets)
        {
            effort[bucket.Id] = Add(effort[bucket.Id], allocated[bucket.Id]);
            counts[bucket.Id] += byBucket[bucket.Id].Count;
        }
    }

    private static ChangePortfolioComparisonSeries BuildPortfolioSeries(
        ChangePortfolioReport source,
        ChangePortfolioComparisonBuildOptions options,
        IReadOnlyList<ChangePortfolioComparisonSeries> additive)
    {
        ChangePortfolioComparisonPoint[] points =
        [.. options.Buckets.Select((bucket, index) => CreatePoint(
            bucket.Id,
            additive.Sum(series => series.Points[index].SelectedChangeCount),
            Sum(additive.Select(series => series.Points[index].Effort)),
            options.CapacityManifest?.Entries
                    .Where(entry => entry.BucketId == bucket.Id)
                    .Sum(entry => entry.Hours)))];
        ChangePortfolioComparisonSeries result = CreateSeries(
            "portfolio",
            ChangePortfolioSeriesKind.Portfolio,
            source.Selection.AuthorPeriodManifest!.ContributorIds,
            additiveToPortfolio: false,
            points,
            options.CapacityManifest is not null);
        if (result.TotalEffort != source.TotalEffort)
        {
            throw new InvalidOperationException(
                "Time-bucket allocations do not reconcile to the source portfolio total.");
        }

        return result;
    }

    private static ChangePortfolioComparisonPoint CreatePoint(
        string bucketId,
        int count,
        EffortRange effort,
        decimal? capacity) => new()
        {
            BucketId = bucketId,
            SelectedChangeCount = count,
            Effort = effort,
            CapacityHours = capacity,
            CapacityRatio = capacity is null ? null : Ratio(effort, capacity.Value),
        };

    private static ChangePortfolioComparisonSeries CreateSeries(
        string id,
        ChangePortfolioSeriesKind kind,
        IReadOnlyList<string> contributorIds,
        bool additiveToPortfolio,
        IReadOnlyList<ChangePortfolioComparisonPoint> points,
        bool withCapacity)
    {
        EffortRange total = Sum(points.Select(point => point.Effort));
        decimal? capacity = withCapacity
            ? points.Sum(point => point.CapacityHours!.Value)
            : null;
        return new ChangePortfolioComparisonSeries
        {
            Id = id,
            Kind = kind,
            ContributorIds = contributorIds,
            AdditiveToPortfolio = additiveToPortfolio,
            Points = points,
            TotalEffort = total,
            TotalCapacityHours = capacity,
            TotalCapacityRatio = capacity is null ? null : Ratio(total, capacity.Value),
            Trend = capacity is null ? null : Trend(points),
        };
    }

    private static ChangePortfolioTrendStatistics Trend(
        IReadOnlyList<ChangePortfolioComparisonPoint> points)
    {
        decimal[] values = [.. points.Select(point => point.CapacityRatio!.Expected)];
        int count = values.Length;
        decimal sumX = count * (count - 1m) / 2m;
        decimal sumY = values.Sum();
        decimal sumXX = Enumerable.Range(0, count).Sum(index => (decimal)index * index);
        decimal sumXY = Enumerable.Range(0, count).Sum(index => index * values[index]);
        decimal denominator = count * sumXX - sumX * sumX;
        decimal? slope = denominator == 0m
            ? null
            : Round((count * sumXY - sumX * sumY) / denominator, 8);
        decimal? rSquared = slope is null ? null : RSquared(values, slope.Value);
        List<ChangePortfolioRollingPoint> rolling = [];
        int window = ChangePortfolioComparisonPolicies.RollingWindowBucketCount;
        for (int end = window - 1; end < points.Count; end++)
        {
            IReadOnlyList<ChangePortfolioComparisonPoint> selected =
                [.. points.Skip(end - window + 1).Take(window)];
            rolling.Add(new ChangePortfolioRollingPoint
            {
                BucketId = points[end].BucketId,
                WindowBucketCount = window,
                ExpectedRatio = Round(
                    selected.Sum(point => point.Effort.Expected) /
                    selected.Sum(point => point.CapacityHours!.Value),
                    6),
            });
        }

        decimal first = values[0];
        decimal latest = values[^1];
        return new ChangePortfolioTrendStatistics
        {
            ObservationCount = count,
            FirstBucketId = points[0].BucketId,
            LatestBucketId = points[^1].BucketId,
            FirstExpectedRatio = first,
            LatestExpectedRatio = latest,
            PercentageChange = first == 0m
                ? null
                : Round((latest - first) / first * 100m, 4),
            OrdinaryLeastSquaresSlope = slope,
            RSquared = rSquared,
            CapacityWeightedRollingWindow = rolling,
        };
    }

    private static decimal RSquared(decimal[] values, decimal slope)
    {
        decimal meanX = (values.Length - 1m) / 2m;
        decimal meanY = values.Average();
        decimal intercept = meanY - slope * meanX;
        decimal total = values.Sum(value => Square(value - meanY));
        decimal residual = Enumerable.Range(0, values.Length)
            .Sum(index => Square(values[index] - (intercept + slope * index)));
        return total == 0m ? 1m : Round(Math.Clamp(1m - residual / total, 0m, 1m), 8);
    }

    private static ChangePortfolioRatioRange Ratio(EffortRange effort, decimal capacity) => new()
    {
        Low = Round(effort.Low / capacity, 6),
        Expected = Round(effort.Expected / capacity, 6),
        High = Round(effort.High / capacity, 6),
    };

    private static decimal? Capacity(
        ChangePortfolioCapacityManifest? manifest,
        string bucketId,
        string? contributorId) => manifest is null || contributorId is null
            ? null
            : manifest.Entries.Single(entry =>
                entry.BucketId == bucketId && entry.ContributorId == contributorId).Hours;

    private static ChangePortfolioComparisonExecution BuildExecution(
        ChangePortfolioReport source,
        ChangePortfolioComparisonBuildOptions options)
    {
        ChangePortfolioProgress? progress = options.ExecutionTelemetry?.GetLastProgress();
        ChangePortfolioExecutionStatistics statistics = options.ExecutionStatistics ?? new();
        Dictionary<string, int> selected = source.Items
            .GroupBy(item => item.RepositoryId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        return new ChangePortfolioComparisonExecution
        {
            RuntimeFramework = RuntimeInformation.FrameworkDescription,
            OperatingSystemFamily = OperatingSystem.IsWindows()
                ? "windows"
                : OperatingSystem.IsLinux()
                    ? "linux"
                    : OperatingSystem.IsMacOS() ? "macos" : "other",
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            LogicalProcessorCount = Environment.ProcessorCount,
            ShardPolicy = ChangePortfolioComparisonPolicies.RepositoryEvidenceShardsV1,
            Checkpoint = new ChangePortfolioComparisonCheckpoint
            {
                Enabled = false,
            },
            RepositoryShardCount = options.SourceManifest.Repositories.Count,
            Repositories = [.. options.SourceManifest.Repositories
                .OrderBy(repository => repository.Id, StringComparer.Ordinal)
                .Select(repository => new ChangePortfolioComparisonRepositoryExecution
                {
                    RepositoryId = repository.Id,
                    Status = ChangePortfolioRepositoryExecutionStatus.Complete,
                    CheckpointDisposition = ChangePortfolioCheckpointDisposition.Disabled,
                    SelectedChangeCount = selected.GetValueOrDefault(repository.Id),
                    ElapsedMilliseconds = 0m,
                    InputDigest = ChangePortfolioComparisonIdentity.ComputeRepositoryInputDigest(
                        options.SourceManifest,
                        repository.Id,
                        source.Profile,
                        source.EstimatorVersion),
                })],
            PhaseTimings = options.ExecutionTelemetry is null
                ? []
                : [.. options.ExecutionTelemetry.GetTimings().Select(timing =>
                    new ChangePortfolioComparisonPhaseTiming
                    {
                        Phase = timing.Phase,
                        ElapsedMilliseconds = Round((decimal)timing.Elapsed.TotalMilliseconds, 3),
                    })],
            LastProgress = progress is null ? null : new ChangePortfolioComparisonProgress
            {
                ObservedAt = progress.ObservedAt.ToUniversalTime(),
                Phase = progress.Phase,
                ProcessedUnits = progress.ProcessedUnits,
                TotalUnits = progress.TotalUnits,
                AnalysisCacheRequests = progress.AnalysisCacheRequests,
                AnalysisCacheHits = progress.AnalysisCacheHits,
                ElapsedMilliseconds = Round((decimal)progress.Elapsed.TotalMilliseconds, 3),
                WorkingSetBytes = progress.WorkingSetBytes,
                PeakWorkingSetBytes = progress.PeakWorkingSetBytes,
            },
            Reuse = new ChangePortfolioComparisonReuse
            {
                SnapshotAnalysisRequests = statistics.SnapshotAnalysisRequests,
                SnapshotAnalysisHits = statistics.SnapshotAnalysisHits,
                UniqueSnapshotAnalysisKeys = statistics.UniqueSnapshotAnalysisKeys,
                AnalysisArtifactRequests = statistics.AnalysisArtifactRequests,
                AnalysisArtifactHits = statistics.AnalysisArtifactHits,
                UniqueAnalysisArtifactKeys = statistics.UniqueAnalysisArtifactKeys,
                SnapshotInventoryRequests = statistics.SnapshotInventoryRequests,
                SnapshotInventoryHits = statistics.SnapshotInventoryHits,
                UniqueSnapshotInventoryObjects = statistics.UniqueSnapshotInventoryObjects,
                BlobRequests = statistics.BlobRequests,
                BlobCacheHits = statistics.BlobCacheHits,
                UniqueBlobObjects = statistics.UniqueBlobObjects,
                BlobReadBytes = statistics.BlobReadBytes,
                PeakWorkingSetBytes = progress?.PeakWorkingSetBytes ?? 0,
            },
            Failures = [],
        };
    }

    private static IReadOnlyList<Diagnostic> ComparisonDiagnostics(
        ChangePortfolioComparisonBuildOptions options) =>
    [
        new Diagnostic
        {
            Code = "FB5330",
            Severity = DiagnosticSeverity.Information,
            Message = "Time buckets are one alternative decomposition of the jointly reconciled portfolio. Bucket and contributor counts do not multiply EHE.",
        },
        new Diagnostic
        {
            Code = "FB5331",
            Severity = DiagnosticSeverity.Warning,
            Message = options.CapacityManifest is null
                ? "No reference capacity was supplied; capacity ratios and trend statistics are omitted."
                : "Reference capacity is a caller-supplied comparison denominator, not recorded labor, productivity, compensation, or authorship evidence.",
        },
        new Diagnostic
        {
            Code = "FB5335",
            Severity = DiagnosticSeverity.Information,
            Message = options.ContributorNormalization == ChangePortfolioContributorNormalization.Joint
                ? "Contributor series use jointly normalized exact-match-set allocations and can change when report membership changes."
                : "Contributor series use membership-stable isolated commit estimates. They can overlap on shared commits, are not additive, and do not replace the jointly normalized portfolio total.",
        },
    ];

    private static EffortRange Sum(IEnumerable<EffortRange> values) => values.Aggregate(Zero(), Add);

    private static EffortRange Add(EffortRange left, EffortRange right) => new()
    {
        Low = left.Low + right.Low,
        Expected = left.Expected + right.Expected,
        High = left.High + right.High,
    };

    private static EffortRange Zero() => new() { Low = 0m, Expected = 0m, High = 0m };

    private static decimal Square(decimal value) => value * value;

    private static decimal Round(decimal value, int digits) =>
        decimal.Round(value, digits, MidpointRounding.AwayFromZero);
}
