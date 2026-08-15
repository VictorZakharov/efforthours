using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(CalibrationDiagnosticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "calibration diagnostic report", errors);
        RequireText(report.DiagnosticVersion, "diagnosticVersion", errors);
        RequireText(report.CorpusId, "corpusId", errors);
        RequireText(report.CorpusVersion, "corpusVersion", errors);
        if (report.RecordCount <= 0 || report.RepositoryCount <= 0)
        {
            errors.Add("Calibration diagnostic record and repository counts must be positive.");
        }

        if (report.IgnoredCandidateCount < 0)
        {
            errors.Add("ignoredCandidateCount cannot be negative.");
        }

        if (report.MaterialContributionThreshold is <= 0m or > 1m)
        {
            errors.Add("materialContributionThreshold must be greater than zero and at most one.");
        }

        RequireUniqueText(
            report.CandidateEstimatorVersions,
            "candidateEstimatorVersions",
            errors);
        if (report.CandidateEstimatorVersions.Count == 0)
        {
            errors.Add("At least one candidate estimator version is required.");
        }

        if (report.Repositories.Count != report.RecordCount)
        {
            errors.Add("Repository diagnostic count does not equal recordCount.");
        }

        ValidateDiagnosticSummary(report.Summary, report.RecordCount, errors);
        HashSet<string> recordIds = new(StringComparer.Ordinal);
        foreach ((CalibrationRepositoryDiagnostic repository, int index) in
                 report.Repositories.Select((repository, index) => (repository, index)))
        {
            ValidateRepositoryDiagnostic(repository, index + 1, errors);
            if (!recordIds.Add(repository.RecordId))
            {
                errors.Add($"Repository diagnostic record ID '{repository.RecordId}' is duplicated.");
            }
        }

        if (report.Repositories.Select(repository => repository.RepositoryId)
                .Distinct(StringComparer.Ordinal)
                .Count() != report.RepositoryCount)
        {
            errors.Add("Distinct repository diagnostic count does not equal repositoryCount.");
        }

        ValidateDiagnosticSummaryReconciliation(report, errors);
        return errors;
    }

    private static void ValidateDiagnosticSummary(
        CalibrationDiagnosticSummary summary,
        int recordCount,
        List<string> errors)
    {
        ValidateDiagnosticRange(summary.Range, "summary.range", errors);
        if (summary.GrossRepositoryExpectedErrorHours < 0m ||
            summary.RepositoryCancellationHours < 0m ||
            summary.GrossCategoryExpectedErrorHours < 0m ||
            summary.GrossComponentExpectedErrorHours < 0m ||
            summary.ComponentCount < 0 ||
            summary.MaterialComponentCount < 0 ||
            summary.MaterialComponentCount > summary.ComponentCount ||
            summary.ReviewedSizeExceptionTargetCount < 0 ||
            summary.OversizedReviewedTargetCount < 0 ||
            summary.OversizedReviewedTargetCount > summary.ReviewedSizeExceptionTargetCount)
        {
            errors.Add("Calibration diagnostic summary contains invalid errors or component counts.");
        }

        int statusCount = summary.ReviewedExpectedCoveredRepositoryCount +
            summary.CandidateTooHighRepositoryCount +
            summary.CandidateTooLowRepositoryCount;
        if (summary.ReviewedExpectedCoveredRepositoryCount < 0 ||
            summary.CandidateTooHighRepositoryCount < 0 ||
            summary.CandidateTooLowRepositoryCount < 0 ||
            statusCount != recordCount ||
            summary.CandidateSymmetricRepositoryCount < 0 ||
            summary.CandidateSymmetricRepositoryCount > recordCount)
        {
            errors.Add("Calibration diagnostic summary contains invalid repository status counts.");
        }

        ValidateCorrelation(
            summary.RawRepositoryWidthCorrelationSampleCount,
            summary.RawRepositoryWidthCorrelation,
            recordCount,
            "summary.rawRepositoryWidthCorrelation",
            errors);
        ValidateCorrelation(
            summary.NormalizedRepositoryWidthCorrelationSampleCount,
            summary.NormalizedRepositoryWidthCorrelation,
            recordCount,
            "summary.normalizedRepositoryWidthCorrelation",
            errors);
        if (!IsZeroDiagnosticDelta(summary.RepositoryReconciliationDelta))
        {
            errors.Add("summary.repositoryReconciliationDelta must be exactly zero.");
        }
    }

    private static void ValidateRepositoryDiagnostic(
        CalibrationRepositoryDiagnostic repository,
        int expectedRank,
        List<string> errors)
    {
        string path = $"repository[{repository.RecordId}]";
        if (repository.Rank != expectedRank)
        {
            errors.Add($"{path}.rank must follow report order.");
        }

        RequireText(repository.RecordId, "repository.recordId", errors);
        RequireText(repository.RepositoryId, $"{path}.repositoryId", errors);
        RequireDigest(repository.SourceDigest, $"{path}.sourceDigest", errors);
        RequireText(repository.BaselineId, $"{path}.baselineId", errors);
        RequireText(
            repository.CandidateEstimatorVersion,
            $"{path}.candidateEstimatorVersion",
            errors);
        RequireDigest(
            repository.CandidateEstimateDigest,
            $"{path}.candidateEstimateDigest",
            errors);
        ValidateDiagnosticRange(repository.Range, $"{path}.range", errors);

        int classifiedTargets = repository.MatchedTargetCount +
            repository.IncompleteTargetCount +
            repository.CategoryMismatchTargetCount;
        if (repository.TargetCount < 0 ||
            repository.CandidateWorkItemCount < 0 ||
            repository.MatchedTargetCount < 0 ||
            repository.IncompleteTargetCount < 0 ||
            repository.CategoryMismatchTargetCount < 0 ||
            classifiedTargets != repository.TargetCount ||
            repository.UnmatchedCandidateWorkItemCount < 0 ||
            repository.UnmatchedCandidateWorkItemCount > repository.CandidateWorkItemCount ||
            repository.ReviewedSizeExceptionTargetCount < 0 ||
            repository.ReviewedSizeExceptionTargetCount > repository.TargetCount ||
            repository.OversizedReviewedTargetCount < 0 ||
            repository.OversizedReviewedTargetCount > repository.ReviewedSizeExceptionTargetCount)
        {
            errors.Add($"{path} contains invalid target or work-item counts.");
        }

        ValidateDiagnosticBreakdown(repository, path, errors);
    }

    private static void ValidateDiagnosticBreakdown(
        CalibrationRepositoryDiagnostic repository,
        string path,
        List<string> errors)
    {
        if (repository.GrossCategoryExpectedErrorHours < 0m ||
            repository.CategoryCancellationHours < 0m ||
            repository.GrossComponentExpectedErrorHours < 0m ||
            repository.ComponentCancellationHours < 0m ||
            repository.MaterialCategoryCount < 0 ||
            repository.MaterialCategoryCount > repository.Categories.Count ||
            repository.MaterialComponentCount < 0 ||
            repository.MaterialComponentCount > repository.Components.Count)
        {
            errors.Add($"{path} contains invalid breakdown errors or material counts.");
        }

        HashSet<EffortCategory> categories = [];
        foreach ((CalibrationCategoryResidual category, int index) in
                 repository.Categories.Select((category, index) => (category, index)))
        {
            if (category.Rank != index + 1 || !categories.Add(category.Category))
            {
                errors.Add($"{path}.categories contains an invalid rank or duplicate category.");
            }

            ValidateDiagnosticRange(
                category.Range,
                $"{path}.category[{category.Category}].range",
                errors);
            ValidateDiagnosticShares(
                category.AbsoluteErrorShare,
                category.CumulativeAbsoluteErrorShare,
                $"{path}.category[{category.Category}]",
                errors);
        }

        HashSet<string> componentIds = new(StringComparer.Ordinal);
        foreach ((CalibrationResidualContribution component, int index) in
                 repository.Components.Select((component, index) => (component, index)))
        {
            ValidateDiagnosticComponent(component, index + 1, path, errors);
            if (!componentIds.Add(component.Id))
            {
                errors.Add($"{path}.component ID '{component.Id}' is duplicated.");
            }
        }

        ValidateRepositoryReconciliation(repository, path, errors);
    }

    private static void ValidateRepositoryReconciliation(
        CalibrationRepositoryDiagnostic repository,
        string path,
        List<string> errors)
    {
        decimal expectedAbsolute = repository.Range.ExpectedAbsoluteErrorHours;
        decimal categoryGross = repository.Categories.Sum(category =>
            category.Range.ExpectedAbsoluteErrorHours);
        decimal componentGross = repository.Components.Sum(component =>
            component.Range.ExpectedAbsoluteErrorHours);
        if (repository.GrossCategoryExpectedErrorHours != categoryGross ||
            repository.CategoryCancellationHours != categoryGross - expectedAbsolute ||
            repository.GrossComponentExpectedErrorHours != componentGross ||
            repository.ComponentCancellationHours != componentGross - expectedAbsolute)
        {
            errors.Add($"{path} gross error or cancellation does not reconcile.");
        }

        CalibrationSignedEffortRange categoryDelta = DiagnosticRound4(DiagnosticSubtract(
            DiagnosticSum(repository.Categories.Select(category => category.Range.SignedError)),
            repository.Range.SignedError));
        CalibrationSignedEffortRange componentDelta = DiagnosticRound4(DiagnosticSubtract(
            DiagnosticSum(repository.Components.Select(component => component.Range.SignedError)),
            repository.Range.SignedError));
        if (repository.CategoryReconciliationDelta != categoryDelta ||
            !IsZeroDiagnosticDelta(categoryDelta) ||
            repository.ComponentReconciliationDelta != componentDelta ||
            !IsZeroDiagnosticDelta(componentDelta))
        {
            errors.Add($"{path} category or component residuals do not reconcile exactly.");
        }

        if (repository.MaterialCategoryCount !=
                repository.Categories.Count(category => category.MaterialContributor) ||
            repository.MaterialComponentCount !=
                repository.Components.Count(component => component.MaterialContributor))
        {
            errors.Add($"{path} material contributor counts do not match the breakdown.");
        }

        if (repository.CandidateWorkItemCount !=
                repository.Components.Sum(component => component.CandidateLeafCount) ||
            repository.ReviewedSizeExceptionTargetCount != repository.Components.Count(component =>
                component.Kind == CalibrationResidualComponentKind.ReviewedTarget &&
                component.ReviewedSizeException is not null) ||
            repository.OversizedReviewedTargetCount != repository.Components.Count(component =>
                component.Kind == CalibrationResidualComponentKind.ReviewedTarget &&
                component.Range.Reviewed.Expected > 8m))
        {
            errors.Add($"{path} candidate leaf or reviewed size-exception counts do not reconcile.");
        }
    }

    private static void ValidateDiagnosticSummaryReconciliation(
        CalibrationDiagnosticReport report,
        List<string> errors)
    {
        CalibrationDiagnosticSummary summary = report.Summary;
        EffortRange reviewed = Sum(report.Repositories.Select(repository => repository.Range.Reviewed));
        EffortRange candidate = Sum(report.Repositories.Select(repository => repository.Range.Candidate));
        if (summary.Range.Reviewed != reviewed || summary.Range.Candidate != candidate)
        {
            errors.Add("Diagnostic summary ranges do not equal repository ranges.");
        }

        decimal repositoryGross = report.Repositories.Sum(repository =>
            repository.Range.ExpectedAbsoluteErrorHours);
        int coveredCount = report.Repositories.Count(repository =>
            repository.Range.ReviewedExpectedStatus == CalibrationIntervalStatus.Covered);
        int candidateTooHighCount = report.Repositories.Count(repository =>
            repository.Range.ReviewedExpectedStatus == CalibrationIntervalStatus.CandidateTooHigh);
        int candidateTooLowCount = report.Repositories.Count(repository =>
            repository.Range.ReviewedExpectedStatus == CalibrationIntervalStatus.CandidateTooLow);
        int symmetricCount = report.Repositories.Count(repository =>
            repository.Range.CandidateSymmetric);
        if (summary.GrossRepositoryExpectedErrorHours != repositoryGross ||
            summary.RepositoryCancellationHours !=
                repositoryGross - summary.Range.ExpectedAbsoluteErrorHours ||
            summary.GrossCategoryExpectedErrorHours != report.Repositories.Sum(repository =>
                repository.GrossCategoryExpectedErrorHours) ||
            summary.GrossComponentExpectedErrorHours != report.Repositories.Sum(repository =>
                repository.GrossComponentExpectedErrorHours) ||
            summary.ComponentCount != report.Repositories.Sum(repository => repository.Components.Count) ||
            summary.MaterialComponentCount != report.Repositories.Sum(repository =>
                repository.MaterialComponentCount) ||
            summary.ReviewedSizeExceptionTargetCount != report.Repositories.Sum(repository =>
                repository.ReviewedSizeExceptionTargetCount) ||
            summary.OversizedReviewedTargetCount != report.Repositories.Sum(repository =>
                repository.OversizedReviewedTargetCount) ||
            summary.ReviewedExpectedCoveredRepositoryCount != coveredCount ||
            summary.CandidateTooHighRepositoryCount != candidateTooHighCount ||
            summary.CandidateTooLowRepositoryCount != candidateTooLowCount ||
            summary.CandidateSymmetricRepositoryCount != symmetricCount)
        {
            errors.Add("Diagnostic summary errors, counts, or statuses do not reconcile.");
        }
    }

    private static void ValidateDiagnosticShares(
        decimal share,
        decimal cumulativeShare,
        string path,
        List<string> errors)
    {
        if (share is < 0m or > 1m || cumulativeShare is < 0m or > 1m)
        {
            errors.Add($"{path} absolute error shares must be between zero and one.");
        }
    }

    private static void ValidateCorrelation(
        int sampleCount,
        decimal? correlation,
        int maximumCount,
        string path,
        List<string> errors)
    {
        if (sampleCount < 0 || sampleCount > maximumCount || correlation is < -1m or > 1m)
        {
            errors.Add($"{path} contains an invalid sample count or coefficient.");
        }

        if (sampleCount < 2 && correlation is not null)
        {
            errors.Add($"{path} must be absent with fewer than two observations.");
        }
    }

    private static CalibrationSignedEffortRange DiagnosticDifference(
        EffortRange left,
        EffortRange right) => new()
        {
            Low = left.Low - right.Low,
            Expected = left.Expected - right.Expected,
            High = left.High - right.High,
        };

    private static CalibrationSignedEffortRange DiagnosticSum(
        IEnumerable<CalibrationSignedEffortRange> ranges) => new()
        {
            Low = ranges.Sum(range => range.Low),
            Expected = ranges.Sum(range => range.Expected),
            High = ranges.Sum(range => range.High),
        };

    private static CalibrationSignedEffortRange DiagnosticSubtract(
        CalibrationSignedEffortRange left,
        CalibrationSignedEffortRange right) => new()
        {
            Low = left.Low - right.Low,
            Expected = left.Expected - right.Expected,
            High = left.High - right.High,
        };

    private static CalibrationSignedEffortRange DiagnosticRound4(
        CalibrationSignedEffortRange value) => new()
        {
            Low = DiagnosticRound4(value.Low),
            Expected = DiagnosticRound4(value.Expected),
            High = DiagnosticRound4(value.High),
        };

    private static bool IsZeroDiagnosticDelta(CalibrationSignedEffortRange value) =>
        value.Low == 0m && value.Expected == 0m && value.High == 0m;

    private static decimal DiagnosticRound4(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
