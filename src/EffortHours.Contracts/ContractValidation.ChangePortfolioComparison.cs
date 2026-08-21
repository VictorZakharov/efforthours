using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(ChangePortfolioBucketManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        List<string> errors = [];
        RequireVersion(manifest.SchemaVersion, "change portfolio bucket manifest", errors);
        if (manifest.Buckets.Count is < 1 or > ChangePortfolioComparisonLimits.MaximumBuckets)
        {
            errors.Add(
                $"A bucket manifest must contain between 1 and " +
                $"{ChangePortfolioComparisonLimits.MaximumBuckets} buckets.");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (ChangePortfolioBucketDefinition bucket in manifest.Buckets)
        {
            ValidatePublicId(bucket.Id, "bucket.id", errors);
            RequireCanonicalText(
                bucket.Label,
                $"bucket[{bucket.Id}].label",
                ChangePortfolioComparisonLimits.MaximumTitleLength,
                errors);
            if (!ids.Add(bucket.Id))
            {
                errors.Add($"Bucket ID '{bucket.Id}' is duplicated.");
            }

            if (bucket.SinceInclusive >= bucket.UntilExclusive)
            {
                errors.Add($"Bucket '{bucket.Id}' must have a non-empty closed interval.");
            }
        }

        ChangePortfolioBucketDefinition[] ordered =
            [.. manifest.Buckets.OrderBy(bucket => bucket.SinceInclusive).ThenBy(bucket => bucket.Id, StringComparer.Ordinal)];
        for (int index = 1; index < ordered.Length; index++)
        {
            if (ordered[index - 1].UntilExclusive > ordered[index].SinceInclusive)
            {
                errors.Add(
                    $"Buckets '{ordered[index - 1].Id}' and '{ordered[index].Id}' overlap.");
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(ChangePortfolioCapacityManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        List<string> errors = [];
        RequireVersion(manifest.SchemaVersion, "change portfolio capacity manifest", errors);
        RequireCanonicalText(
            manifest.CalendarPolicy,
            "calendarPolicy",
            ChangePortfolioComparisonLimits.MaximumCalendarPolicyLength,
            errors);
        if (manifest.Entries.Count is < 1 or > ChangePortfolioComparisonLimits.MaximumCapacityEntries)
        {
            errors.Add(
                $"A capacity manifest must contain between 1 and " +
                $"{ChangePortfolioComparisonLimits.MaximumCapacityEntries} entries.");
        }

        HashSet<(string BucketId, string ContributorId)> keys = [];
        foreach (ChangePortfolioCapacityEntry entry in manifest.Entries)
        {
            ValidatePublicId(entry.BucketId, "capacity.bucketId", errors);
            ValidatePublicId(entry.ContributorId, "capacity.contributorId", errors);
            if (entry.Hours <= 0m)
            {
                errors.Add(
                    $"Capacity entry '{entry.BucketId}/{entry.ContributorId}' must be positive.");
            }

            if (!keys.Add((entry.BucketId, entry.ContributorId)))
            {
                errors.Add(
                    $"Capacity entry '{entry.BucketId}/{entry.ContributorId}' is duplicated.");
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(ChangePortfolioComparisonReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "change portfolio comparison report", errors);
        RequireCanonicalText(
            report.Title,
            "title",
            ChangePortfolioComparisonLimits.MaximumTitleLength,
            errors);
        RequireText(report.CliVersion, "cliVersion", errors);
        RequireText(report.EstimatorVersion, "estimatorVersion", errors);
        RequireText(report.SourceChangeEstimatorVersion, "sourceChangeEstimatorVersion", errors);
        ValidateHostDiscovery(report, errors);
        if (!Enum.IsDefined(report.Status) || !Enum.IsDefined(report.View))
        {
            errors.Add("Comparison status and view must use recognized values.");
        }

        errors.AddRange(Validate(report.Selection).Select(error => $"selection: {error}"));
        if (report.SourcePortfolio is not null)
        {
            errors.AddRange(Validate(report.SourcePortfolio).Select(error => $"sourcePortfolio: {error}"));
            if (!string.Equals(
                    ContractJson.SerializeCompact(report.SourcePortfolio.Selection),
                    ContractJson.SerializeCompact(report.Selection),
                    StringComparison.Ordinal) ||
                report.SourcePortfolio.Profile != report.Profile ||
                report.SourcePortfolio.EstimatorVersion != report.EstimatorVersion ||
                report.SourcePortfolio.SourceChangeEstimatorVersion != report.SourceChangeEstimatorVersion)
            {
                errors.Add("The comparison identity must match its canonical source portfolio.");
            }
        }
        ValidateComparisonBucketPolicy(report.BucketPolicy, errors);
        ValidateComparisonBuckets(report, errors);
        ValidateComparisonSeries(report, errors);
        ValidateComparisonExecution(report, errors);
        ValidateDigest(report.Verification.SemanticDigest, "verification.semanticDigest", errors);
        if (report.Verification.SourcePortfolioDigest is not null)
        {
            ValidateDigest(
                report.Verification.SourcePortfolioDigest,
                "verification.sourcePortfolioDigest",
                errors);
        }
        if (report.Verification.BucketAllocationPolicy !=
            ChangePortfolioComparisonIdentity.ContributorSeriesPolicy(
                report.BucketPolicy.ContributorNormalization))
        {
            errors.Add("The comparison report uses an unsupported bucket-allocation policy.");
        }

        if (report.Status == ChangePortfolioComparisonStatus.Complete &&
            (report.SourcePortfolio is null || !report.Verification.CompleteAggregates ||
             report.Execution.Failures.Count > 0 || report.Verification.SourcePortfolioDigest is null))
        {
            errors.Add("A complete comparison requires a source portfolio, complete aggregates, its digest, and no execution failures.");
        }

        if (report.Status == ChangePortfolioComparisonStatus.Incomplete &&
            (report.SourcePortfolio is not null || report.Series.Count > 0 ||
             report.Verification.CompleteAggregates || report.Execution.Failures.Count == 0 ||
             report.Verification.SourcePortfolioDigest is not null))
        {
            errors.Add("An incomplete comparison must omit source/series aggregates and retain at least one failure.");
        }

        return errors;
    }

    private static void ValidateComparisonBucketPolicy(
        ChangePortfolioComparisonBucketPolicy policy,
        List<string> errors)
    {
        if (!Enum.IsDefined(policy.Kind) || !Enum.IsDefined(policy.ContributorNormalization))
        {
            errors.Add("bucketPolicy kind and contributor normalization must use recognized values.");
        }

        RequireText(policy.Policy, "bucketPolicy.policy", errors);
        ValidateDigest(policy.InputDigest, "bucketPolicy.inputDigest", errors);
        if (policy.RollingWindowBucketCount !=
            ChangePortfolioComparisonPolicies.RollingWindowBucketCount)
        {
            errors.Add("bucketPolicy.rollingWindowBucketCount must be 3.");
        }

        if ((policy.CapacityCalendarPolicy is null) != (policy.CapacityInputDigest is null))
        {
            errors.Add("Capacity calendar policy and input digest must be present together.");
        }

        if (policy.CapacityInputDigest is not null)
        {
            ValidateDigest(policy.CapacityInputDigest, "bucketPolicy.capacityInputDigest", errors);
        }
    }

    private static void ValidateComparisonBuckets(
        ChangePortfolioComparisonReport report,
        List<string> errors)
    {
        if (report.Buckets.Count is < 1 or > ChangePortfolioComparisonLimits.MaximumBuckets)
        {
            errors.Add("The comparison report must contain a supported number of buckets.");
            return;
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        DateTimeOffset? previousUntil = null;
        foreach (ChangePortfolioComparisonBucket bucket in report.Buckets)
        {
            ValidatePublicId(bucket.Id, "bucket.id", errors);
            RequireCanonicalText(
                bucket.Label,
                $"bucket[{bucket.Id}].label",
                ChangePortfolioComparisonLimits.MaximumTitleLength,
                errors);
            if (!ids.Add(bucket.Id))
            {
                errors.Add($"Comparison bucket ID '{bucket.Id}' is duplicated.");
            }

            if (bucket.SinceInclusive >= bucket.UntilExclusive)
            {
                errors.Add($"Comparison bucket '{bucket.Id}' has an empty interval.");
            }

            if (previousUntil is not null && previousUntil != bucket.SinceInclusive)
            {
                errors.Add("Comparison buckets must be in chronological order without gaps.");
            }

            previousUntil = bucket.UntilExclusive;
        }

        ChangePortfolioAuthorPeriodManifestSelection? selection =
            report.Selection.AuthorPeriodManifest;
        if (selection is null ||
            report.Buckets[0].SinceInclusive != selection.SinceInclusive ||
            report.Buckets[^1].UntilExclusive != selection.UntilExclusive)
        {
            errors.Add("Comparison buckets must partition the source portfolio interval exactly.");
        }
    }

    private static void ValidateComparisonSeries(
        ChangePortfolioComparisonReport report,
        List<string> errors)
    {
        if (report.Status == ChangePortfolioComparisonStatus.Incomplete)
        {
            if (report.Series.Count > 0)
            {
                errors.Add("An incomplete comparison cannot publish effort series.");
            }

            return;
        }

        ChangePortfolioComparisonSeries[] portfolioSeries =
            [.. report.Series.Where(series => series.Kind == ChangePortfolioSeriesKind.Portfolio)];
        if (portfolioSeries.Length != 1)
        {
            errors.Add("A comparison report must contain exactly one portfolio series.");
            return;
        }

        HashSet<string> seriesIds = new(StringComparer.Ordinal);
        foreach (ChangePortfolioComparisonSeries series in report.Series)
        {
            ValidatePublicId(series.Id, "series.id", errors);
            if (!seriesIds.Add(series.Id))
            {
                errors.Add($"Comparison series ID '{series.Id}' is duplicated.");
            }

            if (!Enum.IsDefined(series.Kind))
            {
                errors.Add($"Series '{series.Id}' has an unrecognized kind.");
            }

            if (series.Points.Count != report.Buckets.Count ||
                !series.Points.Select(point => point.BucketId)
                    .SequenceEqual(report.Buckets.Select(bucket => bucket.Id), StringComparer.Ordinal))
            {
                errors.Add($"Series '{series.Id}' must contain one canonical point per bucket.");
            }

            ValidateRange(series.TotalEffort, $"series[{series.Id}].totalEffort", errors);
            foreach (ChangePortfolioComparisonPoint point in series.Points)
            {
                ValidateRange(point.Effort, $"series[{series.Id}].point[{point.BucketId}]", errors);
                if (point.SelectedChangeCount < 0)
                {
                    errors.Add($"Series '{series.Id}' selected-change counts cannot be negative.");
                }

                ValidateRatioPair(point.CapacityHours, point.CapacityRatio, series.Id, errors);
            }

            ValidateRatioPair(series.TotalCapacityHours, series.TotalCapacityRatio, series.Id, errors);
            if (Sum(series.Points.Select(point => point.Effort)) != series.TotalEffort)
            {
                errors.Add($"Series '{series.Id}' bucket effort does not equal its total effort.");
            }

            if ((series.TotalCapacityHours is null) != (series.Trend is null))
            {
                errors.Add($"Series '{series.Id}' capacity and trend statistics must be present together.");
            }

            if (series.Trend is not null)
            {
                ValidateTrend(series, errors);
            }
        }

        ChangePortfolioComparisonSeries portfolio = portfolioSeries[0];
        if (portfolio.TotalEffort != report.SourcePortfolio!.TotalEffort)
        {
            errors.Add("The portfolio series total must equal sourcePortfolio.totalEffort.");
        }

        if (report.BucketPolicy.ContributorNormalization ==
            ChangePortfolioContributorNormalization.Isolated)
        {
            ValidateIsolatedContributorSeries(report, errors);
            return;
        }

        ChangePortfolioComparisonSeries[] additive =
            [.. report.Series.Where(series => series.AdditiveToPortfolio)];
        if (Sum(additive.Select(series => series.TotalEffort)) != portfolio.TotalEffort)
        {
            errors.Add("Additive contributor-match series must sum exactly to the portfolio total.");
        }
        foreach (int bucketIndex in Enumerable.Range(0, report.Buckets.Count))
        {
            if (Sum(additive.Select(series => series.Points[bucketIndex].Effort)) !=
                portfolio.Points[bucketIndex].Effort)
            {
                errors.Add(
                    $"Additive series do not reconcile in bucket '{report.Buckets[bucketIndex].Id}'.");
            }
        }
    }

    private static void ValidateIsolatedContributorSeries(
        ChangePortfolioComparisonReport report,
        List<string> errors)
    {
        ChangePortfolioComparisonSeries[] contributors =
        [.. report.Series.Where(series => series.Kind == ChangePortfolioSeriesKind.ContributorIsolated)];
        string[] expectedIds =
        [.. report.Selection.AuthorPeriodManifest!.ContributorIds.Order(StringComparer.Ordinal)];
        string[] actualIds =
        [.. contributors
            .Where(series => series.ContributorIds.Count == 1)
            .Select(series => series.ContributorIds[0])
            .Order(StringComparer.Ordinal)];
        if (!actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal) ||
            report.Series.Any(series =>
                series.Kind is not (ChangePortfolioSeriesKind.Portfolio or
                    ChangePortfolioSeriesKind.ContributorIsolated)))
        {
            errors.Add(
                "Isolated normalization requires exactly one isolated series per requested contributor.");
            return;
        }

        foreach (ChangePortfolioComparisonSeries series in contributors)
        {
            if (series.AdditiveToPortfolio || series.ContributorIds.Count != 1)
            {
                errors.Add(
                    $"Isolated contributor series '{series.Id}' must be non-additive and name one contributor.");
                continue;
            }

            string contributorId = series.ContributorIds[0];
            ChangePortfolioItemEstimate[] items =
            [.. report.SourcePortfolio!.Items.Where(item =>
                item.Attribution.ContributorMatches?.Any(match =>
                    match.ContributorId == contributorId) == true)];
            if (Sum(items.Select(item => item.IsolatedEffort)) != series.TotalEffort)
            {
                errors.Add(
                    $"Isolated contributor series '{series.Id}' does not equal its canonical item estimates.");
            }

            for (int index = 0; index < report.Buckets.Count; index++)
            {
                ChangePortfolioComparisonBucket bucket = report.Buckets[index];
                ChangePortfolioItemEstimate[] bucketItems =
                [.. items.Where(item =>
                    item.Attribution.SelectedTimestamp is DateTimeOffset timestamp &&
                    timestamp >= bucket.SinceInclusive &&
                    timestamp < bucket.UntilExclusive)];
                if (Sum(bucketItems.Select(item => item.IsolatedEffort)) !=
                        series.Points[index].Effort ||
                    bucketItems.Length != series.Points[index].SelectedChangeCount)
                {
                    errors.Add(
                        $"Isolated contributor series '{series.Id}' does not match bucket '{bucket.Id}'.");
                }
            }
        }
    }

    private static void ValidateRatioPair(
        decimal? capacity,
        ChangePortfolioRatioRange? ratio,
        string seriesId,
        List<string> errors)
    {
        if ((capacity is null) != (ratio is null))
        {
            errors.Add($"Series '{seriesId}' capacity and capacity ratio must be present together.");
            return;
        }

        if (capacity is <= 0m)
        {
            errors.Add($"Series '{seriesId}' supplied capacity must be positive.");
        }

        if (ratio is not null &&
            (ratio.Low < 0m || ratio.Low > ratio.Expected || ratio.Expected > ratio.High))
        {
            errors.Add($"Series '{seriesId}' capacity ratios must satisfy low <= expected <= high.");
        }
    }

    private static void ValidateTrend(
        ChangePortfolioComparisonSeries series,
        List<string> errors)
    {
        ChangePortfolioTrendStatistics trend = series.Trend!;
        if (trend.ObservationCount != series.Points.Count ||
            trend.FirstBucketId != series.Points[0].BucketId ||
            trend.LatestBucketId != series.Points[^1].BucketId)
        {
            errors.Add($"Series '{series.Id}' trend boundaries do not match its points.");
        }

        if (trend.RSquared is < 0m or > 1m)
        {
            errors.Add($"Series '{series.Id}' R-squared must be between zero and one.");
        }

        foreach (ChangePortfolioRollingPoint point in trend.CapacityWeightedRollingWindow)
        {
            if (point.WindowBucketCount != ChangePortfolioComparisonPolicies.RollingWindowBucketCount ||
                point.ExpectedRatio < 0m)
            {
                errors.Add($"Series '{series.Id}' contains an invalid rolling-window point.");
            }
        }
    }

}
