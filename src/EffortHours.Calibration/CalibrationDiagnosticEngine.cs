using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationDiagnosticEngine
{
    public static CalibrationDiagnosticReport Diagnose(
        CalibrationCorpus corpus,
        IReadOnlyList<CalibrationCandidateView> candidates,
        CalibrationPartition partition,
        decimal materialContributionThreshold,
        string diagnosticVersion)
    {
        if (corpus.Records.Any(record => record.Change is not null))
        {
            throw new CalibrationEvaluationException(
                [$"Corpus '{corpus.Id}/{corpus.Version}' is not a repository EHE corpus."]);
        }

        CalibrationRecord[] selectedRecords = [.. corpus.Records
            .Where(record => record.Partition == partition)
            .OrderBy(record => record.Id, StringComparer.Ordinal)];
        if (selectedRecords.Length == 0)
        {
            throw new CalibrationEvaluationException(
                [$"Corpus '{corpus.Id}/{corpus.Version}' has no '{partition}' records."]);
        }

        Dictionary<CandidateKey, CalibrationCandidateView> candidateIndex = [];
        List<string> errors = [];
        foreach (CalibrationCandidateView candidate in candidates)
        {
            CandidateKey key = Key(candidate);
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
            CandidateKey key = Key(record);
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

        CalibrationRepositoryDiagnostic[] unordered = [.. matches.Select(match =>
            CalibrationRepositoryDiagnosticBuilder.Build(
                match.Record,
                match.Candidate,
                materialContributionThreshold))];
        CalibrationRepositoryDiagnostic[] repositories = [.. unordered
            .OrderByDescending(repository => repository.Range.ExpectedAbsoluteErrorHours)
            .ThenBy(repository => repository.RecordId, StringComparer.Ordinal)
            .Select((repository, index) => repository with { Rank = index + 1 })];

        EffortRange reviewedTotal = CalibrationDiagnosticMath.Sum(
            repositories.Select(repository => repository.Range.Reviewed));
        EffortRange candidateTotal = CalibrationDiagnosticMath.Sum(
            repositories.Select(repository => repository.Range.Candidate));
        CalibrationRangeDiagnostic totalRange = CalibrationDiagnosticMath.Describe(
            reviewedTotal,
            candidateTotal);
        CalibrationSignedEffortRange repositoryReconciliation =
            CalibrationDiagnosticMath.Round4(CalibrationDiagnosticMath.Difference(
                CalibrationDiagnosticMath.Sum(
                    repositories.Select(repository => repository.Range.SignedError)),
                totalRange.SignedError));

        (int rawCount, decimal? rawCorrelation) = WidthCorrelation(repositories, normalize: false);
        (int normalizedCount, decimal? normalizedCorrelation) =
            WidthCorrelation(repositories, normalize: true);

        CalibrationDiagnosticReport report = new()
        {
            DiagnosticVersion = diagnosticVersion,
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
            MaterialContributionThreshold = materialContributionThreshold,
            Summary = new CalibrationDiagnosticSummary
            {
                Range = totalRange,
                GrossRepositoryExpectedErrorHours = repositories.Sum(repository =>
                    repository.Range.ExpectedAbsoluteErrorHours),
                RepositoryCancellationHours = repositories.Sum(repository =>
                    repository.Range.ExpectedAbsoluteErrorHours) -
                    totalRange.ExpectedAbsoluteErrorHours,
                GrossCategoryExpectedErrorHours = repositories.Sum(repository =>
                    repository.GrossCategoryExpectedErrorHours),
                GrossComponentExpectedErrorHours = repositories.Sum(repository =>
                    repository.GrossComponentExpectedErrorHours),
                ComponentCount = repositories.Sum(repository => repository.Components.Count),
                MaterialComponentCount = repositories.Sum(repository =>
                    repository.MaterialComponentCount),
                ReviewedSizeExceptionTargetCount = repositories.Sum(repository =>
                    repository.ReviewedSizeExceptionTargetCount),
                OversizedReviewedTargetCount = repositories.Sum(repository =>
                    repository.OversizedReviewedTargetCount),
                ReviewedExpectedCoveredRepositoryCount = repositories.Count(repository =>
                    repository.Range.ReviewedExpectedStatus == CalibrationIntervalStatus.Covered),
                CandidateTooHighRepositoryCount = repositories.Count(repository =>
                    repository.Range.ReviewedExpectedStatus == CalibrationIntervalStatus.CandidateTooHigh),
                CandidateTooLowRepositoryCount = repositories.Count(repository =>
                    repository.Range.ReviewedExpectedStatus == CalibrationIntervalStatus.CandidateTooLow),
                CandidateSymmetricRepositoryCount = repositories.Count(repository =>
                    repository.Range.CandidateSymmetric),
                RawRepositoryWidthCorrelationSampleCount = rawCount,
                RawRepositoryWidthCorrelation = rawCorrelation,
                NormalizedRepositoryWidthCorrelationSampleCount = normalizedCount,
                NormalizedRepositoryWidthCorrelation = normalizedCorrelation,
                RepositoryReconciliationDelta = repositoryReconciliation,
            },
            Repositories = repositories,
        };

        IReadOnlyList<string> reportErrors = ContractValidation.Validate(report);
        if (reportErrors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Calibration diagnostic produced an invalid report: {string.Join("; ", reportErrors)}");
        }

        return report;
    }

    private static (int Count, decimal? Correlation) WidthCorrelation(
        IReadOnlyList<CalibrationRepositoryDiagnostic> repositories,
        bool normalize)
    {
        List<(decimal Left, decimal Right)> observations = [];
        foreach (CalibrationRepositoryDiagnostic repository in repositories)
        {
            CalibrationRangeDiagnostic range = repository.Range;
            if (normalize &&
                (range.Candidate.Expected == 0m || range.Reviewed.Expected == 0m))
            {
                continue;
            }

            observations.Add(normalize
                ? (range.CandidateWidthHours / range.Candidate.Expected,
                    range.ReviewedWidthHours / range.Reviewed.Expected)
                : (range.CandidateWidthHours, range.ReviewedWidthHours));
        }

        return (observations.Count, CalibrationDiagnosticMath.Correlation(observations));
    }

    private static CandidateKey Key(CalibrationCandidateView candidate) => new(
        candidate.SourceDigest,
        candidate.Profile,
        candidate.BaselineId);

    private static CandidateKey Key(CalibrationRecord record) => new(
        record.Repository.SourceDigest,
        record.Profile,
        record.BaselineId);

    private readonly record struct CandidateKey(
        string SourceDigest,
        EstimationProfile Profile,
        string BaselineId);
}
