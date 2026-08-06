using System.Security.Cryptography;
using System.Text;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Calibration;

public static class CalibrationEvaluator
{
    public const string EvaluatorVersion = "calibration-evaluator/0.1.0";
    public const string MetricVersion = "calibration-metrics/1.0.0";

    public static CalibrationValidationSummary Summarize(CalibrationCorpus corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ThrowIfInvalid(corpus);

        CalibrationValidationSummary summary = new()
        {
            CorpusId = corpus.Id,
            CorpusVersion = corpus.Version,
            Valid = true,
            RecordCount = corpus.Records.Count,
            RepositoryCount = corpus.Records
                .Select(record => record.Repository.Id)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            Partitions = [.. corpus.Records
                .GroupBy(record => record.Partition)
                .OrderBy(group => group.Key)
                .Select(group => new CalibrationPartitionSummary
                {
                    Partition = group.Key,
                    RecordCount = group.Count(),
                    RepositoryCount = group
                        .Select(record => record.Repository.Id)
                        .Distinct(StringComparer.Ordinal)
                        .Count(),
                })],
        };

        IReadOnlyList<string> errors = ContractValidation.Validate(summary);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Calibration validation summary is invalid: {string.Join("; ", errors)}");
        }

        return summary;
    }

    public static CalibrationEvaluationReport Evaluate(
        CalibrationCorpus corpus,
        IReadOnlyList<EstimateReport> candidates,
        CalibrationPartition partition)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(candidates);
        ThrowIfInvalid(corpus);

        CalibrationRecord[] selectedRecords = [.. corpus.Records
            .Where(record => record.Partition == partition)
            .OrderBy(record => record.Id, StringComparer.Ordinal)];
        if (selectedRecords.Length == 0)
        {
            throw new CalibrationEvaluationException(
                [$"Corpus '{corpus.Id}/{corpus.Version}' has no '{partition}' records."]);
        }

        List<string> candidateErrors = [];
        Dictionary<CandidateKey, EstimateReport> candidateIndex = [];
        foreach (EstimateReport candidate in candidates)
        {
            IReadOnlyList<string> errors = ContractValidation.Validate(candidate);
            candidateErrors.AddRange(errors.Select(error =>
                $"Candidate '{candidate.Repository.Name}/{candidate.Profile}': {error}"));

            if (string.IsNullOrWhiteSpace(candidate.Repository.SourceDigest))
            {
                candidateErrors.Add(
                    $"Candidate '{candidate.Repository.Name}/{candidate.Profile}' requires repository.sourceDigest.");
                continue;
            }

            CandidateKey key = new(
                candidate.Repository.SourceDigest,
                candidate.Profile,
                candidate.Baseline.Id);
            if (!candidateIndex.TryAdd(key, candidate))
            {
                candidateErrors.Add(
                    $"More than one candidate matches source digest '{key.SourceDigest}', " +
                    $"profile '{key.Profile}', and baseline '{key.BaselineId}'.");
            }
        }

        if (candidateErrors.Count > 0)
        {
            throw new CalibrationEvaluationException(candidateErrors);
        }

        List<(CalibrationRecord Record, EstimateReport Candidate)> matches = [];
        HashSet<CandidateKey> usedKeys = [];
        foreach (CalibrationRecord record in selectedRecords)
        {
            CandidateKey key = new(
                record.Repository.SourceDigest,
                record.Profile,
                record.BaselineId);
            if (!candidateIndex.TryGetValue(key, out EstimateReport? candidate))
            {
                candidateErrors.Add(
                    $"No candidate matches calibration record '{record.Id}' with source digest " +
                    $"'{key.SourceDigest}', profile '{key.Profile}', and baseline '{key.BaselineId}'.");
                continue;
            }

            matches.Add((record, candidate));
            usedKeys.Add(key);
        }

        if (candidateErrors.Count > 0)
        {
            throw new CalibrationEvaluationException(candidateErrors);
        }

        List<RangeObservation> repositoryObservations = [];
        Dictionary<EffortCategory, List<RangeObservation>> categoryObservations = [];
        List<RangeObservation> workItemObservations = [];
        List<CalibrationRepositoryEvaluation> repositoryResults = [];

        int targetCount = 0;
        int matchedTargetCount = 0;
        int sourceReferenceCount = 0;
        int matchedSourceReferenceCount = 0;
        int candidateWorkItemCount = 0;
        int matchedCandidateWorkItemCount = 0;

        foreach ((CalibrationRecord record, EstimateReport candidate) in matches)
        {
            EffortRange reviewedTotal = ContractValidation.Sum(
                record.Targets.Select(target => target.Hours));
            repositoryObservations.Add(new RangeObservation(reviewedTotal, candidate.TotalEffort));

            Dictionary<EffortCategory, EffortRange> reviewedCategories = record.Targets
                .GroupBy(target => target.Category)
                .ToDictionary(
                    group => group.Key,
                    group => ContractValidation.Sum(group.Select(target => target.Hours)));
            Dictionary<EffortCategory, EffortRange> candidateCategories = candidate.Categories
                .GroupBy(category => category.Category)
                .ToDictionary(
                    group => group.Key,
                    group => ContractValidation.Sum(group.Select(category => category.Hours)));

            foreach (EffortCategory category in Enum.GetValues<EffortCategory>())
            {
                EffortRange reviewed = reviewedCategories.GetValueOrDefault(category, ZeroRange);
                EffortRange predicted = candidateCategories.GetValueOrDefault(category, ZeroRange);
                if (reviewed != ZeroRange || predicted != ZeroRange)
                {
                    if (!categoryObservations.TryGetValue(category, out List<RangeObservation>? observations))
                    {
                        observations = [];
                        categoryObservations.Add(category, observations);
                    }

                    observations.Add(new RangeObservation(reviewed, predicted));
                }
            }

            Dictionary<string, WorkItem> candidateItems = candidate.WorkItems.ToDictionary(
                item => item.Id,
                StringComparer.Ordinal);
            HashSet<string> fullyMatchedCandidateIds = new(StringComparer.Ordinal);
            List<string> unmatchedTargetIds = [];
            List<string> categoryMismatchTargetIds = [];
            int recordMatchedReferences = 0;
            int recordSourceReferences = 0;
            int recordMatchedTargets = 0;

            foreach (CalibrationTarget target in record.Targets.OrderBy(target => target.Id, StringComparer.Ordinal))
            {
                recordSourceReferences += target.SourceWorkItemIds.Count;
                WorkItem[] resolvedItems = [.. target.SourceWorkItemIds
                    .Where(candidateItems.ContainsKey)
                    .Select(id => candidateItems[id])];
                recordMatchedReferences += resolvedItems.Length;

                bool complete = resolvedItems.Length == target.SourceWorkItemIds.Count;
                bool categoryMatches = complete &&
                    resolvedItems.All(item => item.Category == target.Category);
                if (!categoryMatches)
                {
                    unmatchedTargetIds.Add(target.Id);
                    if (complete)
                    {
                        categoryMismatchTargetIds.Add(target.Id);
                    }

                    continue;
                }

                EffortRange predicted = ContractValidation.Sum(resolvedItems.Select(item => item.Hours));
                workItemObservations.Add(new RangeObservation(target.Hours, predicted));
                recordMatchedTargets++;
                foreach (WorkItem item in resolvedItems)
                {
                    fullyMatchedCandidateIds.Add(item.Id);
                }
            }

            string[] unmatchedCandidateIds = [.. candidateItems.Keys
                .Where(id => !fullyMatchedCandidateIds.Contains(id))
                .Order(StringComparer.Ordinal)];

            targetCount += record.Targets.Count;
            matchedTargetCount += recordMatchedTargets;
            sourceReferenceCount += recordSourceReferences;
            matchedSourceReferenceCount += recordMatchedReferences;
            candidateWorkItemCount += candidate.WorkItems.Count;
            matchedCandidateWorkItemCount += fullyMatchedCandidateIds.Count;

            repositoryResults.Add(new CalibrationRepositoryEvaluation
            {
                RecordId = record.Id,
                RepositoryId = record.Repository.Id,
                SourceDigest = record.Repository.SourceDigest,
                Profile = record.Profile,
                BaselineId = record.BaselineId,
                CandidateEstimatorVersion = candidate.EstimatorVersion,
                CandidateEstimateDigest = ComputeDigest(candidate),
                ReviewedTotal = reviewedTotal,
                CandidateTotal = candidate.TotalEffort,
                ExpectedAbsoluteErrorHours = Round4(
                    decimal.Abs(candidate.TotalEffort.Expected - reviewedTotal.Expected)),
                ExpectedSignedErrorHours = Round4(
                    candidate.TotalEffort.Expected - reviewedTotal.Expected),
                ReviewedExpectedCovered = ContainsExpected(reviewedTotal, candidate.TotalEffort),
                ReviewedRangeFullyCovered = ContainsRange(reviewedTotal, candidate.TotalEffort),
                TargetCount = record.Targets.Count,
                MatchedTargetCount = recordMatchedTargets,
                CandidateWorkItemCount = candidate.WorkItems.Count,
                MatchedCandidateWorkItemCount = fullyMatchedCandidateIds.Count,
                UnmatchedTargetIds = [.. unmatchedTargetIds.Order(StringComparer.Ordinal)],
                UnmatchedCandidateWorkItemIds = unmatchedCandidateIds,
                CategoryMismatchTargetIds = [.. categoryMismatchTargetIds.Order(StringComparer.Ordinal)],
            });
        }

        CalibrationEvaluationReport report = new()
        {
            EvaluatorVersion = EvaluatorVersion,
            MetricVersion = MetricVersion,
            CorpusId = corpus.Id,
            CorpusVersion = corpus.Version,
            Partition = partition,
            RecordCount = selectedRecords.Length,
            RepositoryCount = selectedRecords
                .Select(record => record.Repository.Id)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            IgnoredCandidateCount = candidates.Count - usedKeys.Count,
            CandidateEstimatorVersions = [.. matches
                .Select(match => match.Candidate.EstimatorVersion)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            RepositoryTotals = BuildMetrics(repositoryObservations),
            Categories = [.. categoryObservations
                .OrderBy(pair => pair.Key)
                .Select(pair => new CalibrationCategoryMetrics
                {
                    Category = pair.Key,
                    Metrics = BuildMetrics(pair.Value),
                })],
            WorkItems = BuildMetrics(workItemObservations),
            Match = new CalibrationMatchSummary
            {
                TargetCount = targetCount,
                MatchedTargetCount = matchedTargetCount,
                TargetMatchRate = DivideOrNull(matchedTargetCount, targetCount),
                SourceWorkItemReferenceCount = sourceReferenceCount,
                MatchedSourceWorkItemReferenceCount = matchedSourceReferenceCount,
                SourceWorkItemReferenceMatchRate = DivideOrNull(
                    matchedSourceReferenceCount,
                    sourceReferenceCount),
                CandidateWorkItemCount = candidateWorkItemCount,
                MatchedCandidateWorkItemCount = matchedCandidateWorkItemCount,
                CandidateWorkItemMatchRate = DivideOrNull(
                    matchedCandidateWorkItemCount,
                    candidateWorkItemCount),
            },
            Repositories = repositoryResults,
        };

        IReadOnlyList<string> reportErrors = ContractValidation.Validate(report);
        if (reportErrors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Calibration evaluator produced an invalid report: {string.Join("; ", reportErrors)}");
        }

        return report;
    }

    private static void ThrowIfInvalid(CalibrationCorpus corpus)
    {
        IReadOnlyList<string> errors = ContractValidation.Validate(corpus);
        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }
    }

    private static CalibrationRangeMetrics BuildMetrics(IReadOnlyList<RangeObservation> observations) =>
        new()
        {
            Low = BuildPointMetrics(observations, range => range.Low),
            Expected = BuildPointMetrics(observations, range => range.Expected),
            High = BuildPointMetrics(observations, range => range.High),
            Interval = BuildIntervalMetrics(observations),
        };

    private static CalibrationPointMetrics BuildPointMetrics(
        IReadOnlyList<RangeObservation> observations,
        Func<EffortRange, decimal> selector)
    {
        if (observations.Count == 0)
        {
            return new CalibrationPointMetrics
            {
                SampleCount = 0,
                ReviewedHours = 0m,
                CandidateHours = 0m,
                MeanAbsoluteErrorHours = 0m,
                MedianAbsoluteErrorHours = 0m,
                RootMeanSquaredErrorHours = 0m,
                MeanSignedErrorHours = 0m,
            };
        }

        decimal[] reviewed = [.. observations.Select(observation => selector(observation.Reviewed))];
        decimal[] candidate = [.. observations.Select(observation => selector(observation.Candidate))];
        decimal[] errors = [.. candidate.Zip(reviewed, (prediction, target) => prediction - target)];
        decimal[] absoluteErrors = [.. errors.Select(decimal.Abs).Order()];
        decimal reviewedHours = reviewed.Sum();
        decimal candidateHours = candidate.Sum();
        decimal absoluteErrorHours = absoluteErrors.Sum();
        decimal squaredErrorMean = errors.Sum(error => error * error) / observations.Count;

        return new CalibrationPointMetrics
        {
            SampleCount = observations.Count,
            ReviewedHours = Round4(reviewedHours),
            CandidateHours = Round4(candidateHours),
            MeanAbsoluteErrorHours = Round4(absoluteErrorHours / observations.Count),
            MedianAbsoluteErrorHours = Round4(Median(absoluteErrors)),
            RootMeanSquaredErrorHours = Round4((decimal)Math.Sqrt((double)squaredErrorMean)),
            MeanSignedErrorHours = Round4(errors.Sum() / observations.Count),
            WeightedAbsolutePercentageError = DivideOrNull(absoluteErrorHours, reviewedHours),
            AggregateBiasRate = DivideOrNull(candidateHours - reviewedHours, reviewedHours),
        };
    }

    private static CalibrationIntervalMetrics BuildIntervalMetrics(
        IReadOnlyList<RangeObservation> observations)
    {
        if (observations.Count == 0)
        {
            return new CalibrationIntervalMetrics
            {
                SampleCount = 0,
                ReviewedExpectedCoveredCount = 0,
                ReviewedRangeFullyCoveredCount = 0,
                MeanCandidateWidthHours = 0m,
                MeanReviewedWidthHours = 0m,
            };
        }

        int expectedCovered = observations.Count(observation =>
            ContainsExpected(observation.Reviewed, observation.Candidate));
        int rangeCovered = observations.Count(observation =>
            ContainsRange(observation.Reviewed, observation.Candidate));

        return new CalibrationIntervalMetrics
        {
            SampleCount = observations.Count,
            ReviewedExpectedCoveredCount = expectedCovered,
            ReviewedExpectedCoverage = DivideOrNull(expectedCovered, observations.Count),
            ReviewedRangeFullyCoveredCount = rangeCovered,
            ReviewedRangeFullyCoveredRate = DivideOrNull(rangeCovered, observations.Count),
            MeanCandidateWidthHours = Round4(
                observations.Average(observation =>
                    observation.Candidate.High - observation.Candidate.Low)),
            MeanReviewedWidthHours = Round4(
                observations.Average(observation =>
                    observation.Reviewed.High - observation.Reviewed.Low)),
        };
    }

    private static bool ContainsExpected(EffortRange reviewed, EffortRange candidate) =>
        candidate.Low <= reviewed.Expected && reviewed.Expected <= candidate.High;

    private static bool ContainsRange(EffortRange reviewed, EffortRange candidate) =>
        candidate.Low <= reviewed.Low && candidate.High >= reviewed.High;

    private static decimal Median(decimal[] orderedValues)
    {
        int middle = orderedValues.Length / 2;
        return orderedValues.Length % 2 == 0
            ? (orderedValues[middle - 1] + orderedValues[middle]) / 2m
            : orderedValues[middle];
    }

    private static decimal? DivideOrNull(decimal numerator, decimal denominator) =>
        denominator == 0m ? null : Round4(numerator / denominator);

    private static decimal? DivideOrNull(int numerator, int denominator) =>
        denominator == 0 ? null : Round4((decimal)numerator / denominator);

    private static decimal Round4(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    private static string ComputeDigest(EstimateReport candidate)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(ContractJson.SerializeCompact(candidate));
        return $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
    }

    private static EffortRange ZeroRange { get; } = new()
    {
        Low = 0m,
        Expected = 0m,
        High = 0m,
    };

    private readonly record struct CandidateKey(
        string SourceDigest,
        EstimationProfile Profile,
        string BaselineId);

    private sealed record RangeObservation(EffortRange Reviewed, EffortRange Candidate);
}
