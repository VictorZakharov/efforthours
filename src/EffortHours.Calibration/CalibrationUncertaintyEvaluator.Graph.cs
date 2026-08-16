using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static partial class CalibrationUncertaintyEvaluator
{
    public const string GraphEvaluatorVersion = CalibrationUncertaintyVersions.GraphEvaluatorV1;

    public static CalibrationUncertaintyGraphEvaluationReport EvaluateGraphDevelopment(
        CalibrationCorpus corpus,
        IReadOnlyList<CalibrationUncertaintyGraphFeatureReport> featureReports)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(featureReports);
        if (corpus.Records.Any(record => record.Partition != CalibrationPartition.Development))
        {
            throw new CalibrationEvaluationException(
                [
                    "Graph uncertainty evaluation v1 accepts a development-only corpus; " +
                    "validation and test records must remain outside this diagnostic input.",
                ]);
        }

        CalibrationEvaluator.ThrowIfInvalid(corpus);
        CalibrationUncertaintyGraphEvaluationPolicy policy =
            CalibrationUncertaintyGraphEvaluationPolicyCatalog.Current;
        List<string> errors = ValidateGraphInputs(corpus, featureReports, policy);
        CalibrationRecord[] records = [.. corpus.Records
            .Where(record => record.Partition == CalibrationPartition.Development)
            .OrderBy(record => record.Id, StringComparer.Ordinal)];
        if (records.Length == 0)
        {
            errors.Add($"Corpus '{corpus.Id}/{corpus.Version}' has no development records.");
        }

        if (records.Select(record => record.Repository.Id)
            .Distinct(StringComparer.Ordinal).Count() < 3)
        {
            errors.Add(
                "Graph uncertainty evaluation requires at least three development repositories " +
                "for repository-held-out folds.");
        }

        Dictionary<ReportKey, CalibrationUncertaintyGraphFeatureReport> index = [];
        foreach (CalibrationUncertaintyGraphFeatureReport report in featureReports)
        {
            ReportKey key = Key(report);
            if (!index.TryAdd(key, report))
            {
                errors.Add(
                    $"More than one graph feature report matches source digest " +
                    $"'{key.SourceDigest}', profile '{key.Profile}', and baseline " +
                    $"'{key.BaselineId}'.");
            }
        }

        List<(CalibrationRecord Record, CalibrationUncertaintyGraphFeatureReport Report)> matches =
            [];
        HashSet<ReportKey> used = [];
        foreach (CalibrationRecord record in records)
        {
            ReportKey key = Key(record);
            if (!index.TryGetValue(key, out CalibrationUncertaintyGraphFeatureReport? report))
            {
                errors.Add(
                    $"No graph feature report matches development record '{record.Id}' with " +
                    $"source digest '{key.SourceDigest}', profile '{key.Profile}', and baseline " +
                    $"'{key.BaselineId}'.");
                continue;
            }

            matches.Add((record, report));
            used.Add(key);
        }

        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors.Distinct(StringComparer.Ordinal));
        }

        CalibrationUncertaintyMatchedData matched =
            CalibrationUncertaintyGraphEvaluationMatcher.Match(matches, policy);
        if (matched.Observations.Count == 0)
        {
            throw new CalibrationEvaluationException(
                ["No complete category-matched development targets were available for evaluation."]);
        }

        CalibrationUncertaintyObservation[] observations =
            AddRepositoryHeldOutBaselines(matched.Observations);
        CalibrationUncertaintyGraphFeatureReport first = matches[0].Report;
        CalibrationUncertaintyIntervalPerformance baseline =
            CalibrationUncertaintyEvaluationMath.Performance(
                observations,
                observation => observation.BaselineRange!);
        IReadOnlyList<CalibrationUncertaintyFeatureEvaluation> measurements =
            BuildFeatureEvaluations(
                observations,
                first.FeatureContract.Features,
                baseline,
                (definition, value) => CalibrationUncertaintyGraphEvaluationPolicyCatalog.Bucket(
                    definition.Id,
                    value));
        IReadOnlyList<CalibrationUncertaintyRepositoryEvaluation> repositories =
            BuildRepositoryEvaluations(matched.Repositories, observations);
        CalibrationUncertaintyGraphEvaluationReport result = new()
        {
            EvaluatorVersion = GraphEvaluatorVersion,
            MetricVersion = CalibrationUncertaintyVersions.EvaluationMetricV1,
            CorpusId = corpus.Id,
            CorpusVersion = corpus.Version,
            CorpusDigest = CalibrationDigest.Compute(corpus),
            Partition = CalibrationPartition.Development,
            FeatureContractVersion = first.FeatureContract.Version,
            FeatureContractDigest = first.FeatureContractDigest,
            IntervalPolicyVersion = first.FeatureContract.IntervalPolicy.Version,
            EvaluationPolicy = policy,
            EvaluationPolicyDigest = CalibrationDigest.Compute(policy),
            ProjectorVersions = [.. matches.Select(match => match.Report.ProjectorVersion)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            EstimatorVersions = [.. matches.Select(match => match.Report.EstimatorVersion)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            Protocol = GraphProtocol,
            Summary = new CalibrationUncertaintyEvaluationSummary
            {
                RecordCount = records.Length,
                RepositoryCount = records.Select(record => record.Repository.Id)
                    .Distinct(StringComparer.Ordinal).Count(),
                FeatureReportCount = featureReports.Count,
                IgnoredFeatureReportCount = featureReports.Count - used.Count,
                TargetCount = matched.TargetCount,
                MatchedTargetCount = observations.Length,
                SourceWorkItemReferenceCount = matched.SourceWorkItemReferenceCount,
                MatchedSourceWorkItemReferenceCount =
                    matched.MatchedSourceWorkItemReferenceCount,
            },
            CurrentIntervals = CalibrationUncertaintyEvaluationMath.Performance(
                observations,
                observation => observation.CandidateRange),
            CrossValidatedBaseline = baseline,
            Features = BuildGraphFeatureEvaluations(measurements, repositories),
            Slices = BuildSlices(observations),
            Repositories = repositories,
            Targets = [.. observations.Select(ToGraphTargetEvaluation)],
        };

        IReadOnlyList<string> reportErrors = ContractValidation.Validate(result);
        if (reportErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "Graph uncertainty evaluator produced an invalid report: " +
                string.Join("; ", reportErrors));
        }

        return result;
    }

    private static readonly CalibrationUncertaintyGraphEvaluationProtocol GraphProtocol = new()
    {
        Version = CalibrationUncertaintyVersions.GraphEvaluationProtocolV1,
        Partition = CalibrationPartition.Development,
        FoldUnit = "repository",
        TargetMetric = "absolute-reviewed-expected-residual",
        Normalization = "candidate-expected-with-0.5-hour-floor",
        NormalizationFloorHours = CalibrationUncertaintyEvaluationMath.NormalizationFloorHours,
        IntendedCoverageTarget = CalibrationUncertaintyEvaluationMath.CoverageTarget,
        QuantileMethod = "nearest-rank",
        MinimumBucketObservationCount =
            CalibrationUncertaintyEvaluationMath.MinimumBucketObservationCount,
        MinimumBucketRepositoryCount =
            CalibrationUncertaintyEvaluationMath.MinimumBucketRepositoryCount,
        EvaluationPolicyVersion = CalibrationUncertaintyVersions.GraphEvaluationPolicyV1,
        BucketAssignment = "fixed-feature-specific-inclusive-maximum",
        TargetNodePopulation = "union-of-unique-mapped-node-ids-per-target",
        UnmappedWorkItemPolicy = "retain-target-and-aggregate-mapped-nodes",
        PublicInterfaceAvailabilityPolicy =
            "any-unavailable-selected-node-makes-interface-features-unavailable",
        RepositoryIsolated = true,
        DevelopmentOnly = true,
        FitsProductionModel = false,
        FormalProbabilityInterval = false,
    };

    private static IReadOnlyList<CalibrationUncertaintyGraphFeatureEvaluation>
        BuildGraphFeatureEvaluations(
            IReadOnlyList<CalibrationUncertaintyFeatureEvaluation> measurements,
            IReadOnlyList<CalibrationUncertaintyRepositoryEvaluation> repositories)
    {
        Dictionary<string, decimal?> baselineCoverage = repositories.ToDictionary(
            repository => repository.RecordId,
            repository => repository.CrossValidatedBaseline.ReviewedExpectedCoverage,
            StringComparer.Ordinal);
        return
        [
            .. measurements.Select(measurement =>
            {
                CalibrationUncertaintyGraphEvaluationRule rule =
                    CalibrationUncertaintyGraphEvaluationPolicyCatalog.Current.Rules.Single(
                        candidate => candidate.FeatureId == measurement.FeatureId);
                bool directionMatches = measurement.NormalizedAbsoluteResidualSpearman > 0m;
                int bucketViolations = CountGraphDirectionBucketViolations(
                    measurement.Buckets);
                int repositoryRegressions = measurement.RepositoryFolds.Count(fold =>
                    CoverageRegressed(
                        fold.Intervals.ReviewedExpectedCoverage,
                        baselineCoverage[fold.RecordId]));
                bool pooledGate = measurement.ConditionedPredictionCount > 0 &&
                    measurement.CoverageDeltaFromBaseline is >= 0m &&
                    measurement.MeanNormalizedWidthDeltaFromBaseline <= 0m &&
                    measurement.MeanIntervalMissDeltaFromBaseline <= 0m;
                return new CalibrationUncertaintyGraphFeatureEvaluation
                {
                    FeatureId = measurement.FeatureId,
                    TargetAggregation = rule.TargetAggregation,
                    ExpectedResidualDirection = rule.ExpectedResidualDirection,
                    Evaluation = measurement,
                    ExpectedDirectionCorrelationMatches = directionMatches,
                    ExpectedDirectionBucketViolationCount = bucketViolations,
                    RepositoryCoverageRegressionCount = repositoryRegressions,
                    MeetsPooledIncrementalGate = pooledGate,
                };
            }),
        ];
    }

    private static int CountGraphDirectionBucketViolations(
        IReadOnlyList<CalibrationUncertaintyFeatureBucketEvaluation> buckets)
    {
        int result = 0;
        for (int index = 1; index < buckets.Count; index++)
        {
            if (buckets[index].Empirical80thPercentileNormalizedResidual <
                buckets[index - 1].Empirical80thPercentileNormalizedResidual)
            {
                result++;
            }
        }

        return result;
    }

    private static CalibrationUncertaintyGraphTargetEvaluation ToGraphTargetEvaluation(
        CalibrationUncertaintyObservation observation) => new()
        {
            Source = ToTargetEvaluation(observation),
            NodeIds = observation.GraphNodeIds,
            Features =
            [
                .. CalibrationUncertaintyGraphEvaluationPolicyCatalog.Current.Rules.Select(rule =>
                    ToFeatureValue(rule.FeatureId, observation.Features[rule.FeatureId])),
            ],
        };

    private static List<string> ValidateGraphInputs(
        CalibrationCorpus corpus,
        IReadOnlyList<CalibrationUncertaintyGraphFeatureReport> reports,
        CalibrationUncertaintyGraphEvaluationPolicy policy)
    {
        List<string> errors = [];
        if (corpus.Records.Any(record => record.Change is not null))
        {
            errors.Add($"Corpus '{corpus.Id}/{corpus.Version}' is not a repository EHE corpus.");
        }

        if (reports.Count == 0)
        {
            errors.Add("At least one graph uncertainty feature report is required.");
            return errors;
        }

        foreach (CalibrationUncertaintyGraphFeatureReport report in reports)
        {
            errors.AddRange(ContractValidation.Validate(report).Select(error =>
                $"Graph feature report '{report.RepositorySourceDigest}/{report.Profile}': " +
                error));
            if (report.FeatureContractDigest != CalibrationDigest.Compute(report.FeatureContract))
            {
                errors.Add(
                    $"Graph feature report '{report.RepositorySourceDigest}/{report.Profile}' " +
                    "has a feature-contract digest mismatch.");
            }
        }

        string canonicalContractDigest = CalibrationDigest.Compute(
            CalibrationUncertaintyGraphFeatureCatalog.Current);
        if (canonicalContractDigest != CalibrationUncertaintyVersions.GraphFeatureContractDigestV1)
        {
            errors.Add("The built-in graph uncertainty feature catalog differs from its v1 digest.");
        }

        if (CalibrationDigest.Compute(policy) !=
            CalibrationUncertaintyVersions.GraphEvaluationPolicyDigestV1)
        {
            errors.Add("The built-in graph uncertainty evaluation policy differs from its v1 digest.");
        }

        CalibrationUncertaintyGraphFeatureReport first = reports[0];
        if (first.FeatureContract.Version != CalibrationUncertaintyGraphFeatureCatalog.Version ||
            first.FeatureContractDigest !=
                CalibrationUncertaintyVersions.GraphFeatureContractDigestV1)
        {
            errors.Add(
                "Graph uncertainty evaluation v1 requires the canonical frozen feature " +
                $"contract '{CalibrationUncertaintyGraphFeatureCatalog.Version}' with digest " +
                $"'{CalibrationUncertaintyVersions.GraphFeatureContractDigestV1}'.");
        }

        foreach (CalibrationUncertaintyGraphFeatureReport report in reports.Skip(1))
        {
            if (report.FeatureContract.Version != first.FeatureContract.Version ||
                report.FeatureContractDigest != first.FeatureContractDigest ||
                report.FeatureContract.IntervalPolicy.Version !=
                    first.FeatureContract.IntervalPolicy.Version)
            {
                errors.Add(
                    "All graph feature reports must use the same feature contract, contract " +
                    "digest, and interval policy.");
                break;
            }
        }

        if (!first.FeatureContract.Features.Select(feature => feature.Id).SequenceEqual(
                policy.Rules.Select(rule => rule.FeatureId),
                StringComparer.Ordinal))
        {
            errors.Add(
                "The graph evaluation policy must cover the frozen feature-contract order " +
                "exactly.");
        }

        return errors;
    }

    private static ReportKey Key(CalibrationUncertaintyGraphFeatureReport report) => new(
        report.RepositorySourceDigest,
        report.Profile,
        report.BaselineId);
}
