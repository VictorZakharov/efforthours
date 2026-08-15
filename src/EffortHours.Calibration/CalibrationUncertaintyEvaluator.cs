using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static partial class CalibrationUncertaintyEvaluator
{
    public const string EvaluatorVersion = "uncertainty-feature-evaluator/1.0.0";

    public static CalibrationUncertaintyEvaluationReport EvaluateDevelopment(
        CalibrationCorpus corpus,
        IReadOnlyList<CalibrationUncertaintyFeatureReport> featureReports)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(featureReports);
        if (corpus.Records.Any(record =>
                record.Partition != CalibrationPartition.Development))
        {
            throw new CalibrationEvaluationException(
                [
                    "Uncertainty feature evaluation v1 accepts a development-only corpus; " +
                    "validation and test records must remain outside this diagnostic input.",
                ]);
        }

        CalibrationEvaluator.ThrowIfInvalid(corpus);

        List<string> errors = ValidateInputs(corpus, featureReports);
        CalibrationRecord[] records = [.. corpus.Records
            .Where(record => record.Partition == CalibrationPartition.Development)
            .OrderBy(record => record.Id, StringComparer.Ordinal)];
        if (records.Length == 0)
        {
            errors.Add(
                $"Corpus '{corpus.Id}/{corpus.Version}' has no development records.");
        }

        if (records.Select(record => record.Repository.Id)
            .Distinct(StringComparer.Ordinal).Count() < 3)
        {
            errors.Add(
                "Uncertainty feature evaluation requires at least three development repositories " +
                "for repository-held-out folds.");
        }

        Dictionary<ReportKey, CalibrationUncertaintyFeatureReport> index = [];
        foreach (CalibrationUncertaintyFeatureReport report in featureReports)
        {
            ReportKey key = Key(report);
            if (!index.TryAdd(key, report))
            {
                errors.Add(
                    $"More than one feature report matches source digest '{key.SourceDigest}', " +
                    $"profile '{key.Profile}', and baseline '{key.BaselineId}'.");
            }
        }

        List<(CalibrationRecord Record, CalibrationUncertaintyFeatureReport Report)> matches = [];
        HashSet<ReportKey> used = [];
        foreach (CalibrationRecord record in records)
        {
            ReportKey key = Key(record);
            if (!index.TryGetValue(key, out CalibrationUncertaintyFeatureReport? report))
            {
                errors.Add(
                    $"No feature report matches development record '{record.Id}' with source " +
                    $"digest '{key.SourceDigest}', profile '{key.Profile}', and baseline " +
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
            CalibrationUncertaintyEvaluationMatcher.Match(matches);
        if (matched.Observations.Count == 0)
        {
            throw new CalibrationEvaluationException(
                ["No complete category-matched development targets were available for evaluation."]);
        }

        CalibrationUncertaintyObservation[] observations =
            AddRepositoryHeldOutBaselines(matched.Observations);
        CalibrationUncertaintyFeatureReport first = matches[0].Report;
        CalibrationUncertaintyIntervalPerformance baseline =
            CalibrationUncertaintyEvaluationMath.Performance(
                observations,
                observation => observation.BaselineRange!);
        CalibrationUncertaintyEvaluationReport result = new()
        {
            EvaluatorVersion = EvaluatorVersion,
            MetricVersion = CalibrationUncertaintyVersions.EvaluationMetricV1,
            CorpusId = corpus.Id,
            CorpusVersion = corpus.Version,
            CorpusDigest = CalibrationDigest.Compute(corpus),
            Partition = CalibrationPartition.Development,
            FeatureContractVersion = first.FeatureContract.Version,
            FeatureContractDigest = first.FeatureContractDigest,
            IntervalPolicyVersion = first.FeatureContract.IntervalPolicy.Version,
            ProjectorVersions = [.. matches.Select(match => match.Report.ProjectorVersion)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            EstimatorVersions = [.. matches.Select(match => match.Report.EstimatorVersion)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            Protocol = Protocol,
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
            Features = BuildFeatureEvaluations(
                observations,
                first.FeatureContract.Features,
                baseline),
            Slices = BuildSlices(observations),
            Repositories = BuildRepositoryEvaluations(matched.Repositories, observations),
            Targets = [.. observations.Select(ToTargetEvaluation)],
        };

        IReadOnlyList<string> reportErrors = ContractValidation.Validate(result);
        if (reportErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "Uncertainty feature evaluator produced an invalid report: " +
                string.Join("; ", reportErrors));
        }

        return result;
    }

    private static readonly CalibrationUncertaintyEvaluationProtocol Protocol = new()
    {
        Version = CalibrationUncertaintyVersions.EvaluationProtocolV1,
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
        RepositoryIsolated = true,
        DevelopmentOnly = true,
        FitsProductionModel = false,
        FormalProbabilityInterval = false,
    };

    private static List<string> ValidateInputs(
        CalibrationCorpus corpus,
        IReadOnlyList<CalibrationUncertaintyFeatureReport> reports)
    {
        List<string> errors = [];
        if (corpus.Records.Any(record => record.Change is not null))
        {
            errors.Add(
                $"Corpus '{corpus.Id}/{corpus.Version}' is not a repository EHE corpus.");
        }

        if (reports.Count == 0)
        {
            errors.Add("At least one uncertainty feature report is required.");
            return errors;
        }

        foreach (CalibrationUncertaintyFeatureReport report in reports)
        {
            errors.AddRange(ContractValidation.Validate(report).Select(error =>
                $"Feature report '{report.RepositorySourceDigest}/{report.Profile}': {error}"));
            if (report.FeatureContractDigest != CalibrationDigest.Compute(report.FeatureContract))
            {
                errors.Add(
                    $"Feature report '{report.RepositorySourceDigest}/{report.Profile}' has a " +
                    "feature-contract digest mismatch.");
            }
        }

        string contractVersion = reports[0].FeatureContract.Version;
        string contractDigest = reports[0].FeatureContractDigest;
        string policyVersion = reports[0].FeatureContract.IntervalPolicy.Version;
        string canonicalContractDigest = CalibrationDigest.Compute(
            CalibrationUncertaintyFeatureCatalog.Current);
        if (canonicalContractDigest != CalibrationUncertaintyVersions.FeatureContractDigestV1)
        {
            errors.Add(
                "The built-in uncertainty feature catalog differs from its frozen v1 digest.");
        }

        if (contractVersion != CalibrationUncertaintyFeatureCatalog.Version ||
            contractDigest != CalibrationUncertaintyVersions.FeatureContractDigestV1)
        {
            errors.Add(
                "Uncertainty feature evaluation v1 requires the canonical frozen feature contract " +
                $"'{CalibrationUncertaintyFeatureCatalog.Version}' with digest " +
                $"'{CalibrationUncertaintyVersions.FeatureContractDigestV1}'.");
        }

        foreach (CalibrationUncertaintyFeatureReport report in reports.Skip(1))
        {
            if (report.FeatureContract.Version != contractVersion ||
                report.FeatureContractDigest != contractDigest ||
                report.FeatureContract.IntervalPolicy.Version != policyVersion)
            {
                errors.Add(
                    "All uncertainty feature reports must use the same feature contract, " +
                    "contract digest, and interval policy.");
                break;
            }
        }

        if (reports[0].FeatureContract.Features.Any(feature =>
                feature.ValueKind == CalibrationUncertaintyFeatureValueKind.Distribution))
        {
            errors.Add(
                "Evaluation protocol v1 cannot evaluate active distribution-valued features.");
        }

        return errors;
    }

    private static CalibrationUncertaintyObservation[] AddRepositoryHeldOutBaselines(
        IReadOnlyList<CalibrationUncertaintyObservation> observations)
    {
        Dictionary<string, decimal> factors = observations
            .Select(observation => observation.RepositoryId)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                repositoryId => repositoryId,
                repositoryId => CalibrationUncertaintyEvaluationMath.Quantile80(
                    observations.Where(observation => !string.Equals(
                            observation.RepositoryId,
                            repositoryId,
                            StringComparison.Ordinal))
                        .Select(observation => observation.NormalizedAbsoluteResidual)),
                StringComparer.Ordinal);
        return
        [
            .. observations.Select(observation => observation with
            {
                BaselineRange = CalibrationUncertaintyEvaluationMath.PredictRange(
                    observation.CandidateRange.Expected,
                    factors[observation.RepositoryId]),
            }),
        ];
    }

    private static CalibrationUncertaintyTargetEvaluation ToTargetEvaluation(
        CalibrationUncertaintyObservation observation) => new()
        {
            RecordId = observation.RecordId,
            RepositoryId = observation.RepositoryId,
            TargetId = observation.TargetId,
            Category = observation.Category,
            Ecosystems = observation.Ecosystems,
            ExpectedSizeBand = observation.ExpectedSizeBand,
            CandidateRange = observation.CandidateRange,
            ReviewedRange = observation.ReviewedRange,
            AbsoluteExpectedResidualHours =
            CalibrationUncertaintyEvaluationMath.Round4(observation.AbsoluteResidualHours),
            NormalizedAbsoluteResidual =
            CalibrationUncertaintyEvaluationMath.Round4(observation.NormalizedAbsoluteResidual),
            CurrentReviewedExpectedCovered = CalibrationUncertaintyEvaluationMath.Covers(
            observation.CandidateRange,
            observation.ReviewedRange.Expected),
            CrossValidatedBaselineRange = observation.BaselineRange!,
            BaselineReviewedExpectedCovered = CalibrationUncertaintyEvaluationMath.Covers(
            observation.BaselineRange!,
            observation.ReviewedRange.Expected),
        };

    private static ReportKey Key(CalibrationRecord record) => new(
        record.Repository.SourceDigest,
        record.Profile,
        record.BaselineId);

    private static ReportKey Key(CalibrationUncertaintyFeatureReport report) => new(
        report.RepositorySourceDigest,
        report.Profile,
        report.BaselineId);

    private readonly record struct ReportKey(
        string SourceDigest,
        EstimationProfile Profile,
        string BaselineId);
}
