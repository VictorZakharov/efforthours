using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static partial class CalibrationUncertaintyEvaluator
{
    public const string StructuralEvaluatorVersion =
        CalibrationUncertaintyVersions.StructuralEvaluatorV1;

    public static CalibrationUncertaintyStructuralEvaluationReport
        EvaluateStructuralDevelopment(
            CalibrationCorpus corpus,
            IReadOnlyList<CalibrationUncertaintyStructuralFeatureReport> featureReports)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(featureReports);
        if (corpus.Records.Any(record => record.Partition != CalibrationPartition.Development))
        {
            throw new CalibrationEvaluationException(
                [
                    "Structural uncertainty evaluation v1 accepts a development-only corpus; " +
                    "validation and test records must remain outside this diagnostic input.",
                ]);
        }

        CalibrationEvaluator.ThrowIfInvalid(corpus);
        CalibrationUncertaintyStructuralEvaluationPolicy policy =
            CalibrationUncertaintyStructuralEvaluationPolicyCatalog.Current;
        List<string> errors = ValidateStructuralInputs(corpus, featureReports, policy);
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
                "Structural uncertainty evaluation requires at least three development " +
                "repositories for repository-held-out folds.");
        }

        Dictionary<ReportKey, CalibrationUncertaintyStructuralFeatureReport> index = [];
        foreach (CalibrationUncertaintyStructuralFeatureReport report in featureReports)
        {
            ReportKey key = Key(report);
            if (!index.TryAdd(key, report))
            {
                errors.Add(
                    $"More than one structural feature report matches source digest " +
                    $"'{key.SourceDigest}', profile '{key.Profile}', and baseline " +
                    $"'{key.BaselineId}'.");
            }
        }

        List<(CalibrationRecord Record, CalibrationUncertaintyStructuralFeatureReport Report)>
            matches = [];
        HashSet<ReportKey> used = [];
        foreach (CalibrationRecord record in records)
        {
            ReportKey key = Key(record);
            if (!index.TryGetValue(
                    key,
                    out CalibrationUncertaintyStructuralFeatureReport? report))
            {
                errors.Add(
                    $"No structural feature report matches development record '{record.Id}' " +
                    $"with source digest '{key.SourceDigest}', profile '{key.Profile}', and " +
                    $"baseline '{key.BaselineId}'.");
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
            CalibrationUncertaintyStructuralEvaluationMatcher.Match(matches, policy);
        if (matched.Observations.Count == 0)
        {
            throw new CalibrationEvaluationException(
                ["No complete category-matched development targets were available for evaluation."]);
        }

        CalibrationUncertaintyObservation[] observations =
            AddRepositoryHeldOutBaselines(matched.Observations);
        CalibrationUncertaintyStructuralFeatureReport first = matches[0].Report;
        CalibrationUncertaintyIntervalPerformance baseline =
            CalibrationUncertaintyEvaluationMath.Performance(
                observations,
                observation => observation.BaselineRange!);
        IReadOnlyList<CalibrationUncertaintyFeatureEvaluation> measurements =
            BuildFeatureEvaluations(
                observations,
                first.FeatureContract.Features,
                baseline,
                (definition, value) =>
                    CalibrationUncertaintyStructuralEvaluationPolicyCatalog.Bucket(
                        definition.Id,
                        value));
        CalibrationUncertaintyRepositoryEvaluation[] repositories =
            [.. BuildRepositoryEvaluations(matched.Repositories, observations)];
        CalibrationUncertaintyStructuralEvaluationReport result = new()
        {
            EvaluatorVersion = StructuralEvaluatorVersion,
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
            Protocol = StructuralProtocol,
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
            Features = BuildStructuralFeatureEvaluations(measurements, repositories),
            Slices = BuildSlices(observations),
            Repositories = repositories,
            Targets = [.. observations.Select(ToStructuralTargetEvaluation)],
        };

        IReadOnlyList<string> reportErrors = ContractValidation.Validate(result);
        if (reportErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "Structural uncertainty evaluator produced an invalid report: " +
                string.Join("; ", reportErrors));
        }

        return result;
    }

    private static readonly CalibrationUncertaintyStructuralEvaluationProtocol
        StructuralProtocol = new()
        {
            Version = CalibrationUncertaintyVersions.StructuralEvaluationProtocolV1,
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
            EvaluationPolicyVersion =
                CalibrationUncertaintyVersions.StructuralEvaluationPolicyV1,
            BucketAssignment = "fixed-feature-specific-inclusive-maximum",
            RepositoryIsolated = true,
            DevelopmentOnly = true,
            FitsProductionModel = false,
            FormalProbabilityInterval = false,
        };

    private static IReadOnlyList<CalibrationUncertaintyStructuralFeatureEvaluation>
        BuildStructuralFeatureEvaluations(
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
                CalibrationUncertaintyStructuralEvaluationRule rule =
                    CalibrationUncertaintyStructuralEvaluationPolicyCatalog.Current.Rules.Single(
                        candidate => candidate.FeatureId == measurement.FeatureId);
                bool directionMatches = ExpectedDirectionMatches(
                    measurement.NormalizedAbsoluteResidualSpearman,
                    rule.ExpectedResidualDirection);
                int bucketViolations = CountExpectedDirectionBucketViolations(
                    measurement.Buckets,
                    rule.ExpectedResidualDirection);
                int repositoryRegressions = measurement.RepositoryFolds.Count(fold =>
                    CoverageRegressed(
                        fold.Intervals.ReviewedExpectedCoverage,
                        baselineCoverage[fold.RecordId]));
                bool pooledGate = measurement.ConditionedPredictionCount > 0 &&
                    measurement.CoverageDeltaFromBaseline is >= 0m &&
                    measurement.MeanNormalizedWidthDeltaFromBaseline <= 0m &&
                    measurement.MeanIntervalMissDeltaFromBaseline <= 0m;
                return new CalibrationUncertaintyStructuralFeatureEvaluation
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

    private static CalibrationUncertaintyStructuralTargetEvaluation
        ToStructuralTargetEvaluation(CalibrationUncertaintyObservation observation) => new()
        {
            Source = ToTargetEvaluation(observation),
            Features =
            [
                .. CalibrationUncertaintyStructuralEvaluationPolicyCatalog.Current.Rules.Select(
                    rule => ToFeatureValue(rule.FeatureId, observation.Features[rule.FeatureId])),
            ],
        };

    private static CalibrationUncertaintyFeatureValue ToFeatureValue(
        string featureId,
        CalibrationUncertaintyAggregatedFeature feature) => new()
        {
            FeatureId = featureId,
            Availability = feature.Availability,
            Value = feature.Value,
        };

    private static bool ExpectedDirectionMatches(
        decimal? correlation,
        CalibrationUncertaintyStructuralResidualDirection direction) => direction switch
        {
            CalibrationUncertaintyStructuralResidualDirection.HigherValueHigherResidual =>
                correlation > 0m,
            CalibrationUncertaintyStructuralResidualDirection.HigherValueLowerResidual =>
                correlation < 0m,
            _ => false,
        };

    private static int CountExpectedDirectionBucketViolations(
        IReadOnlyList<CalibrationUncertaintyFeatureBucketEvaluation> buckets,
        CalibrationUncertaintyStructuralResidualDirection direction)
    {
        int violations = 0;
        for (int index = 1; index < buckets.Count; index++)
        {
            decimal previous = buckets[index - 1].Empirical80thPercentileNormalizedResidual;
            decimal current = buckets[index].Empirical80thPercentileNormalizedResidual;
            bool violation = direction switch
            {
                CalibrationUncertaintyStructuralResidualDirection.HigherValueHigherResidual =>
                    current < previous,
                CalibrationUncertaintyStructuralResidualDirection.HigherValueLowerResidual =>
                    current > previous,
                _ => true,
            };
            if (violation)
            {
                violations++;
            }
        }

        return violations;
    }

    private static bool CoverageRegressed(decimal? conditioned, decimal? baseline) =>
        conditioned is not null && baseline is not null && conditioned < baseline;

    private static List<string> ValidateStructuralInputs(
        CalibrationCorpus corpus,
        IReadOnlyList<CalibrationUncertaintyStructuralFeatureReport> reports,
        CalibrationUncertaintyStructuralEvaluationPolicy policy)
    {
        List<string> errors = [];
        if (corpus.Records.Any(record => record.Change is not null))
        {
            errors.Add($"Corpus '{corpus.Id}/{corpus.Version}' is not a repository EHE corpus.");
        }

        if (reports.Count == 0)
        {
            errors.Add("At least one structural uncertainty feature report is required.");
            return errors;
        }

        foreach (CalibrationUncertaintyStructuralFeatureReport report in reports)
        {
            errors.AddRange(ContractValidation.Validate(report).Select(error =>
                $"Structural feature report '{report.RepositorySourceDigest}/{report.Profile}': " +
                error));
            if (report.FeatureContractDigest != CalibrationDigest.Compute(report.FeatureContract))
            {
                errors.Add(
                    $"Structural feature report '{report.RepositorySourceDigest}/" +
                    $"{report.Profile}' has a feature-contract digest mismatch.");
            }
        }

        string contractVersion = reports[0].FeatureContract.Version;
        string contractDigest = reports[0].FeatureContractDigest;
        string intervalPolicyVersion = reports[0].FeatureContract.IntervalPolicy.Version;
        string canonicalContractDigest = CalibrationDigest.Compute(
            CalibrationUncertaintyStructuralFeatureCatalog.Current);
        if (canonicalContractDigest !=
            CalibrationUncertaintyVersions.StructuralFeatureContractDigestV1)
        {
            errors.Add(
                "The built-in structural uncertainty feature catalog differs from its frozen " +
                "v1 digest.");
        }

        string policyDigest = CalibrationDigest.Compute(policy);
        if (policyDigest != CalibrationUncertaintyVersions.StructuralEvaluationPolicyDigestV1)
        {
            errors.Add(
                "The built-in structural uncertainty evaluation policy differs from its frozen " +
                "v1 digest.");
        }

        if (contractVersion != CalibrationUncertaintyStructuralFeatureCatalog.Version ||
            contractDigest != CalibrationUncertaintyVersions.StructuralFeatureContractDigestV1)
        {
            errors.Add(
                "Structural uncertainty evaluation v1 requires the canonical frozen feature " +
                $"contract '{CalibrationUncertaintyStructuralFeatureCatalog.Version}' with " +
                $"digest '{CalibrationUncertaintyVersions.StructuralFeatureContractDigestV1}'.");
        }

        foreach (CalibrationUncertaintyStructuralFeatureReport report in reports.Skip(1))
        {
            if (report.FeatureContract.Version != contractVersion ||
                report.FeatureContractDigest != contractDigest ||
                report.FeatureContract.IntervalPolicy.Version != intervalPolicyVersion)
            {
                errors.Add(
                    "All structural feature reports must use the same feature contract, contract " +
                    "digest, and interval policy.");
                break;
            }
        }

        if (!reports[0].FeatureContract.Features.Select(feature => feature.Id).SequenceEqual(
                policy.Rules.Select(rule => rule.FeatureId),
                StringComparer.Ordinal))
        {
            errors.Add(
                "The structural evaluation policy must cover the frozen feature-contract order " +
                "exactly.");
        }

        return errors;
    }

    private static ReportKey Key(CalibrationUncertaintyStructuralFeatureReport report) => new(
        report.RepositorySourceDigest,
        report.Profile,
        report.BaselineId);
}
