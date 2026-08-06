using Fairbill.Contracts.V1;

namespace Fairbill.Contracts;

public static partial class ContractValidation
{
    private static void ValidateCalibrationSource(
        CalibrationSourceProvenance source,
        string recordPath,
        List<string> errors)
    {
        RequireText(source.SourceReference, $"{recordPath}.source.sourceReference", errors);
        RequireText(source.Revision, $"{recordPath}.source.revision", errors);
        RequireText(source.LicenseExpression, $"{recordPath}.source.licenseExpression", errors);

        if (source.DataClassification == CalibrationDataClassification.Private &&
            source.RedistributionAllowed)
        {
            errors.Add($"{recordPath} is classified private but permits redistribution.");
        }

        if (source.DataClassification != CalibrationDataClassification.Private &&
            !source.RedistributionAllowed)
        {
            errors.Add($"{recordPath} is public or synthetic but does not permit redistribution.");
        }
    }

    private static void ValidateCalibrationReview(
        CalibrationReviewProvenance review,
        string recordPath,
        List<string> errors)
    {
        if (review.CompletedOn == default)
        {
            errors.Add($"{recordPath}.review.completedOn is required.");
        }

        if (review.Reviewers.Count == 0)
        {
            errors.Add($"{recordPath}.review.reviewers must contain at least one reviewer.");
        }

        HashSet<(string Id, CalibrationReviewerRole Role)> reviewers = [];
        foreach (CalibrationReviewer reviewer in review.Reviewers)
        {
            RequireText(reviewer.Id, $"{recordPath}.review.reviewer.id", errors);
            if (!reviewers.Add((reviewer.Id, reviewer.Role)))
            {
                errors.Add(
                    $"Reviewer '{reviewer.Id}' has duplicate role '{reviewer.Role}' in {recordPath}.");
            }

            if (reviewer.Kind == CalibrationReviewerKind.HostAi)
            {
                RequireText(reviewer.ModelId, $"{recordPath}.review.reviewer[{reviewer.Id}].modelId", errors);
                RequireText(
                    reviewer.ModelVersion,
                    $"{recordPath}.review.reviewer[{reviewer.Id}].modelVersion",
                    errors);
            }
        }

        if (!review.Reviewers.Any(reviewer => reviewer.Role == CalibrationReviewerRole.Teacher))
        {
            errors.Add($"{recordPath}.review requires a '{CalibrationReviewerRole.Teacher}' role.");
        }

        if (review.Status is CalibrationReviewStatus.Reviewed or CalibrationReviewStatus.Adjudicated &&
            !review.Reviewers.Any(reviewer => reviewer.Role == CalibrationReviewerRole.Reviewer))
        {
            errors.Add(
                $"{recordPath}.review status '{review.Status}' requires a " +
                $"'{CalibrationReviewerRole.Reviewer}' role.");
        }

        if (review.Status == CalibrationReviewStatus.Adjudicated &&
            !review.Reviewers.Any(reviewer => reviewer.Role == CalibrationReviewerRole.Adjudicator))
        {
            errors.Add(
                $"{recordPath}.review status '{review.Status}' requires an " +
                $"'{CalibrationReviewerRole.Adjudicator}' role.");
        }
    }

    private static void ValidateCalibrationTargets(
        CalibrationRecord record,
        string recordPath,
        List<string> errors)
    {
        if (record.Targets.Count == 0 && record.Change is null)
        {
            errors.Add($"{recordPath}.targets must contain at least one target.");
        }

        HashSet<string> targetIds = new(StringComparer.Ordinal);
        HashSet<string> sourceWorkItemIds = new(StringComparer.Ordinal);
        foreach (CalibrationTarget target in record.Targets)
        {
            string path = $"{recordPath}.target[{target.Id}]";
            RequireText(target.Id, $"{recordPath}.target.id", errors);
            RequireText(target.Title, $"{path}.title", errors);
            RequireText(target.Scope, $"{path}.scope", errors);
            RequireText(target.Rationale, $"{path}.rationale", errors);
            ValidateReviewedRange(
                target.Hours,
                $"{path}.hours",
                target.SizeException,
                $"{path}.sizeException",
                errors);

            if (!targetIds.Add(target.Id))
            {
                errors.Add($"Calibration target ID '{target.Id}' is duplicated in record '{record.Id}'.");
            }

            if (target.SourceWorkItemIds.Count == 0)
            {
                errors.Add($"{path}.sourceWorkItemIds must contain at least one ID.");
            }

            RequireUniqueText(target.SourceWorkItemIds, $"{path}.sourceWorkItemIds", errors);
            foreach (string sourceWorkItemId in target.SourceWorkItemIds)
            {
                if (!sourceWorkItemIds.Add(sourceWorkItemId))
                {
                    errors.Add(
                        $"Source work-item ID '{sourceWorkItemId}' is mapped to more than one target " +
                        $"in record '{record.Id}'.");
                }
            }

            if (target.EvidenceIds.Count == 0)
            {
                errors.Add($"{path}.evidenceIds must contain at least one ID.");
            }

            RequireUniqueText(target.EvidenceIds, $"{path}.evidenceIds", errors);
            RequireUniqueText(target.UncertaintyReasons, $"{path}.uncertaintyReasons", errors);
        }
    }

    private static void ValidateCalibrationMetrics(
        CalibrationRangeMetrics metrics,
        string path,
        List<string> errors)
    {
        ValidatePointMetrics(metrics.Low, $"{path}.low", errors);
        ValidatePointMetrics(metrics.Expected, $"{path}.expected", errors);
        ValidatePointMetrics(metrics.High, $"{path}.high", errors);

        CalibrationIntervalMetrics interval = metrics.Interval;
        if (interval.SampleCount < 0 ||
            interval.ReviewedExpectedCoveredCount < 0 ||
            interval.ReviewedExpectedCoveredCount > interval.SampleCount ||
            interval.ReviewedRangeFullyCoveredCount < 0 ||
            interval.ReviewedRangeFullyCoveredCount > interval.SampleCount ||
            interval.MeanCandidateWidthHours < 0m ||
            interval.MeanReviewedWidthHours < 0m)
        {
            errors.Add($"{path}.interval contains invalid counts or widths.");
        }

        ValidateUnitRatio(interval.ReviewedExpectedCoverage, $"{path}.interval.reviewedExpectedCoverage", errors);
        ValidateUnitRatio(
            interval.ReviewedRangeFullyCoveredRate,
            $"{path}.interval.reviewedRangeFullyCoveredRate",
            errors);
    }

    private static void ValidatePointMetrics(
        CalibrationPointMetrics metrics,
        string path,
        List<string> errors)
    {
        if (metrics.SampleCount < 0 ||
            metrics.ReviewedHours < 0m ||
            metrics.CandidateHours < 0m ||
            metrics.MeanAbsoluteErrorHours < 0m ||
            metrics.MedianAbsoluteErrorHours < 0m ||
            metrics.RootMeanSquaredErrorHours < 0m ||
            metrics.WeightedAbsolutePercentageError < 0m)
        {
            errors.Add($"{path} contains invalid counts, hours, or nonnegative error metrics.");
        }
    }

    private static void ValidateMatchSummary(CalibrationMatchSummary match, List<string> errors)
    {
        if (match.TargetCount < 0 ||
            match.MatchedTargetCount < 0 ||
            match.MatchedTargetCount > match.TargetCount ||
            match.SourceWorkItemReferenceCount < 0 ||
            match.MatchedSourceWorkItemReferenceCount < 0 ||
            match.MatchedSourceWorkItemReferenceCount > match.SourceWorkItemReferenceCount ||
            match.CandidateWorkItemCount < 0 ||
            match.MatchedCandidateWorkItemCount < 0 ||
            match.MatchedCandidateWorkItemCount > match.CandidateWorkItemCount)
        {
            errors.Add("Calibration aggregate match counts are invalid.");
        }

        ValidateUnitRatio(match.TargetMatchRate, "match.targetMatchRate", errors);
        ValidateUnitRatio(
            match.SourceWorkItemReferenceMatchRate,
            "match.sourceWorkItemReferenceMatchRate",
            errors);
        ValidateUnitRatio(match.CandidateWorkItemMatchRate, "match.candidateWorkItemMatchRate", errors);
    }

    private static void ValidateCorpusReference(
        CalibrationCorpusReference reference,
        string path,
        List<string> errors)
    {
        RequireText(reference.Id, $"{path}.id", errors);
        RequireText(reference.Version, $"{path}.version", errors);
        RequireDigest(reference.Digest, $"{path}.digest", errors);
    }

    private static void ValidateMutationScope(
        CalibrationMutationScope scope,
        EffortCategory? category,
        string path,
        List<string> errors)
    {
        if (scope == CalibrationMutationScope.Category && category is null)
        {
            errors.Add($"{path}.category is required for category scope.");
        }

        if (scope == CalibrationMutationScope.RepositoryTotal && category is not null)
        {
            errors.Add($"{path}.category must be omitted for repository-total scope.");
        }
    }

    private static void RequireDigest(string? value, string path, List<string> errors)
    {
        RequireText(value, path, errors);
        if (value is not null && !value.StartsWith("sha256:", StringComparison.Ordinal))
        {
            errors.Add($"{path} must start with 'sha256:'.");
        }
    }

    private static void RequireUniqueText(
        IReadOnlyList<string> values,
        string path,
        List<string> errors)
    {
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireText(value, path, errors);
            if (!unique.Add(value))
            {
                errors.Add($"{path} contains duplicate value '{value}'.");
            }
        }
    }

    private static void ValidateUnitRatio(decimal? value, string path, List<string> errors)
    {
        if (value is < 0m or > 1m)
        {
            errors.Add($"{path} must be between 0 and 1 when present.");
        }
    }

}
