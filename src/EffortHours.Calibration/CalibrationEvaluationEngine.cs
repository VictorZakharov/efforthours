using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationEvaluationEngine
{
    public static CalibrationEvaluationReport Evaluate(
        CalibrationCorpus corpus,
        IReadOnlyList<CalibrationCandidateView> candidates,
        CalibrationPartition partition,
        bool expectChangeRecords,
        string evaluatorVersion,
        string metricVersion)
    {
        if (corpus.Records.Any(record => (record.Change is not null) != expectChangeRecords))
        {
            string expected = expectChangeRecords ? "Change EHE" : "repository EHE";
            throw new CalibrationEvaluationException(
                [$"Corpus '{corpus.Id}/{corpus.Version}' is not a {expected} corpus."]);
        }

        CalibrationRecord[] selectedRecords = [.. corpus.Records
            .Where(record => record.Partition == partition)
            .OrderBy(record => record.Id, StringComparer.Ordinal)];
        if (selectedRecords.Length == 0)
        {
            throw new CalibrationEvaluationException(
                [$"Corpus '{corpus.Id}/{corpus.Version}' has no '{partition}' records."]);
        }

        List<string> errors = [];
        Dictionary<CandidateKey, CalibrationCandidateView> candidateIndex = [];
        foreach (CalibrationCandidateView candidate in candidates)
        {
            CandidateKey key = new(candidate.SourceDigest, candidate.Profile, candidate.BaselineId);
            if (!candidateIndex.TryAdd(key, candidate))
            {
                errors.Add(
                    $"More than one candidate matches source digest '{key.SourceDigest}', " +
                    $"profile '{key.Profile}', and baseline '{key.BaselineId}'.");
            }
        }

        List<(CalibrationRecord Record, CalibrationCandidateView Candidate)> matches = [];
        HashSet<CandidateKey> usedKeys = [];
        foreach (CalibrationRecord record in selectedRecords)
        {
            CandidateKey key = new(
                record.Repository.SourceDigest,
                record.Profile,
                record.BaselineId);
            if (!candidateIndex.TryGetValue(key, out CalibrationCandidateView? candidate))
            {
                errors.Add(
                    $"No candidate matches calibration record '{record.Id}' with source digest " +
                    $"'{key.SourceDigest}', profile '{key.Profile}', and baseline '{key.BaselineId}'.");
                continue;
            }

            matches.Add((record, candidate));
            usedKeys.Add(key);
        }

        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }

        List<CalibrationRangeObservation> repositoryObservations = [];
        Dictionary<EffortCategory, List<CalibrationRangeObservation>> categoryObservations = [];
        List<CalibrationRangeObservation> workItemObservations = [];
        List<CalibrationRepositoryEvaluation> repositoryResults = [];

        int targetCount = 0;
        int matchedTargetCount = 0;
        int sourceReferenceCount = 0;
        int matchedSourceReferenceCount = 0;
        int candidateWorkItemCount = 0;
        int matchedCandidateWorkItemCount = 0;

        foreach ((CalibrationRecord record, CalibrationCandidateView candidate) in matches)
        {
            EffortRange reviewedTotal = ContractValidation.Sum(
                record.Targets.Select(target => target.Hours));
            repositoryObservations.Add(new CalibrationRangeObservation(
                reviewedTotal,
                candidate.TotalEffort));

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
                if (reviewed == ZeroRange && predicted == ZeroRange)
                {
                    continue;
                }

                if (!categoryObservations.TryGetValue(
                        category,
                        out List<CalibrationRangeObservation>? observations))
                {
                    observations = [];
                    categoryObservations.Add(category, observations);
                }

                observations.Add(new CalibrationRangeObservation(reviewed, predicted));
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

            foreach (CalibrationTarget target in record.Targets.OrderBy(
                         target => target.Id,
                         StringComparer.Ordinal))
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
                workItemObservations.Add(new CalibrationRangeObservation(target.Hours, predicted));
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
                CandidateEstimateDigest = candidate.EstimateDigest,
                ReviewedTotal = reviewedTotal,
                CandidateTotal = candidate.TotalEffort,
                ExpectedAbsoluteErrorHours = Round4(
                    decimal.Abs(candidate.TotalEffort.Expected - reviewedTotal.Expected)),
                ExpectedSignedErrorHours = Round4(
                    candidate.TotalEffort.Expected - reviewedTotal.Expected),
                ReviewedExpectedCovered = CalibrationMetricCalculator.ContainsExpected(
                    reviewedTotal,
                    candidate.TotalEffort),
                ReviewedRangeFullyCovered = CalibrationMetricCalculator.ContainsRange(
                    reviewedTotal,
                    candidate.TotalEffort),
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
            EvaluatorVersion = evaluatorVersion,
            MetricVersion = metricVersion,
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
            RepositoryTotals = CalibrationMetricCalculator.BuildMetrics(repositoryObservations),
            Categories = [.. categoryObservations
                .OrderBy(pair => pair.Key)
                .Select(pair => new CalibrationCategoryMetrics
                {
                    Category = pair.Key,
                    Metrics = CalibrationMetricCalculator.BuildMetrics(pair.Value),
                })],
            WorkItems = CalibrationMetricCalculator.BuildMetrics(workItemObservations),
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

    private static decimal? DivideOrNull(int numerator, int denominator) =>
        denominator == 0 ? null : Round4((decimal)numerator / denominator);

    private static decimal Round4(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);

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
}
