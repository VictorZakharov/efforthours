using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Calibration;

public static class CalibrationCorpusReviewCompiler
{
    public const string CompilerVersion = "calibration-corpus-review-compiler/0.1.0";

    public static CalibrationCorpus Compile(
        CalibrationCorpusReviewPlan plan,
        CalibrationCorpus sourceCorpus)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(sourceCorpus);

        List<string> errors = [.. ContractValidation.Validate(plan)];
        errors.AddRange(ContractValidation.Validate(sourceCorpus).Select(error =>
            $"Source corpus: {error}"));
        if (!string.Equals(plan.CompilerVersion, CompilerVersion, StringComparison.Ordinal))
        {
            errors.Add(
                $"Corpus review plan requires compiler '{plan.CompilerVersion}', but this build " +
                $"provides '{CompilerVersion}'.");
        }

        string sourceDigest = CalibrationDigest.Compute(sourceCorpus);
        if (!string.Equals(plan.SourceCorpus.Id, sourceCorpus.Id, StringComparison.Ordinal) ||
            !string.Equals(plan.SourceCorpus.Version, sourceCorpus.Version, StringComparison.Ordinal) ||
            !string.Equals(plan.SourceCorpus.Digest, sourceDigest, StringComparison.Ordinal))
        {
            errors.Add(
                $"Corpus review plan expects source corpus '{plan.SourceCorpus.Id}/" +
                $"{plan.SourceCorpus.Version}' at digest '{plan.SourceCorpus.Digest}', but received " +
                $"'{sourceCorpus.Id}/{sourceCorpus.Version}' at digest '{sourceDigest}'.");
        }

        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }

        Dictionary<string, CalibrationRecord> sourceRecords = sourceCorpus.Records
            .ToDictionary(record => record.Id, StringComparer.Ordinal);
        Dictionary<string, CalibrationCorpusReviewPlanRecord> plannedRecords = plan.Records
            .ToDictionary(record => record.SourceRecordId, StringComparer.Ordinal);
        foreach (string missing in sourceRecords.Keys
                     .Except(plannedRecords.Keys, StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            errors.Add($"Corpus review plan has no decision set for source record '{missing}'.");
        }

        foreach (string unknown in plannedRecords.Keys
                     .Except(sourceRecords.Keys, StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            errors.Add($"Corpus review plan references unknown source record '{unknown}'.");
        }

        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }

        List<CalibrationRecord> records = [];
        foreach (CalibrationRecord source in sourceCorpus.Records.OrderBy(
                     record => record.Id,
                     StringComparer.Ordinal))
        {
            CalibrationCorpusReviewPlanRecord planned = plannedRecords[source.Id];
            ValidateProgression(source, planned, errors);

            Dictionary<string, CalibrationTarget> sourceTargets = source.Targets
                .ToDictionary(target => target.Id, StringComparer.Ordinal);
            Dictionary<string, CalibrationCorpusReviewTargetDecision> decisions = planned.Targets
                .ToDictionary(target => target.SourceTargetId, StringComparer.Ordinal);
            foreach (string missing in sourceTargets.Keys
                         .Except(decisions.Keys, StringComparer.Ordinal)
                         .Order(StringComparer.Ordinal))
            {
                errors.Add($"Corpus review record '{source.Id}' has no decision for target '{missing}'.");
            }

            foreach (string unknown in decisions.Keys
                         .Except(sourceTargets.Keys, StringComparer.Ordinal)
                         .Order(StringComparer.Ordinal))
            {
                errors.Add($"Corpus review record '{source.Id}' references unknown target '{unknown}'.");
            }

            if (errors.Count > 0)
            {
                continue;
            }

            CalibrationReviewer[] combinedReviewers =
            [
                .. source.Review.Reviewers,
                .. planned.Reviewers
                    .OrderBy(reviewer => reviewer.Role)
                    .ThenBy(reviewer => reviewer.Id, StringComparer.Ordinal),
            ];
            CalibrationRecord revised = source with
            {
                Review = new CalibrationReviewProvenance
                {
                    Status = planned.ResultStatus,
                    CompletedOn = planned.CompletedOn,
                    Reviewers = combinedReviewers,
                    Notes = MergeNotes(source.Review.Notes, planned.Notes),
                },
                Targets = [.. source.Targets
                    .OrderBy(target => target.Id, StringComparer.Ordinal)
                    .Select(target => Apply(decisions[target.Id], target))],
            };
            records.Add(revised);
        }

        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }

        CalibrationCorpus corpus = new()
        {
            Id = plan.Id,
            Version = plan.Version,
            Description = plan.Description,
            Rubric = sourceCorpus.Rubric,
            Records = records,
        };
        IReadOnlyList<string> corpusErrors = ContractValidation.Validate(corpus);
        if (corpusErrors.Count > 0)
        {
            throw new CalibrationEvaluationException([.. corpusErrors.Select(error =>
                $"Compiled reviewed corpus: {error}")]);
        }

        return corpus;
    }

    private static void ValidateProgression(
        CalibrationRecord source,
        CalibrationCorpusReviewPlanRecord planned,
        List<string> errors)
    {
        if (source.Review.Status == CalibrationReviewStatus.Adjudicated)
        {
            errors.Add($"Source record '{source.Id}' is already adjudicated and cannot advance further.");
        }
        else if (source.Review.Status == CalibrationReviewStatus.Reviewed &&
                 planned.ResultStatus != CalibrationReviewStatus.Adjudicated)
        {
            errors.Add($"Reviewed source record '{source.Id}' can advance only to adjudicated.");
        }

        if (planned.CompletedOn < source.Review.CompletedOn)
        {
            errors.Add(
                $"Corpus review record '{source.Id}' completes before its source review date " +
                $"'{source.Review.CompletedOn:yyyy-MM-dd}'.");
        }

        HashSet<string> priorReviewerIds = source.Review.Reviewers
            .Select(reviewer => reviewer.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (CalibrationReviewer reviewer in planned.Reviewers)
        {
            if (priorReviewerIds.Contains(reviewer.Id))
            {
                errors.Add(
                    $"Corpus review record '{source.Id}' reuses prior reviewer identity " +
                    $"'{reviewer.Id}'; subsequent review identities must be distinct.");
            }
        }

        CalibrationReviewer[] combined = [.. source.Review.Reviewers, .. planned.Reviewers];
        if (planned.ResultStatus is CalibrationReviewStatus.Reviewed or CalibrationReviewStatus.Adjudicated &&
            !combined.Any(reviewer => reviewer.Role == CalibrationReviewerRole.Reviewer))
        {
            errors.Add($"Corpus review record '{source.Id}' requires a reviewer role in combined provenance.");
        }

        if (planned.ResultStatus == CalibrationReviewStatus.Adjudicated &&
            !combined.Any(reviewer => reviewer.Role == CalibrationReviewerRole.Adjudicator))
        {
            errors.Add($"Corpus review record '{source.Id}' requires an adjudicator role in combined provenance.");
        }
    }

    private static CalibrationTarget Apply(
        CalibrationCorpusReviewTargetDecision decision,
        CalibrationTarget source)
    {
        if (decision.Action == CalibrationCorpusReviewAction.Accept)
        {
            return source;
        }

        return source with
        {
            Hours = decision.Hours!,
            Rationale = decision.Rationale!,
            UncertaintyReasons = [.. decision.UncertaintyReasons.Order(StringComparer.Ordinal)],
            SizeException = decision.SizeException,
        };
    }

    private static string MergeNotes(string? source, string subsequent) =>
        string.IsNullOrWhiteSpace(source)
            ? subsequent
            : $"{source}\n\nSubsequent review: {subsequent}";
}
