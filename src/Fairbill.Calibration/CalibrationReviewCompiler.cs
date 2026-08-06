using System.Text.RegularExpressions;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Calibration;

public static class CalibrationReviewCompiler
{
    public const string CompilerVersion = "calibration-review-compiler/0.1.0";

    private static readonly Regex TitlePartSuffix = new(
        @" \(part \d+ of \d+\)$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static CalibrationCorpus Compile(
        CalibrationReviewPlan plan,
        IReadOnlyList<EstimateReport> sourceEstimates)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(sourceEstimates);

        List<string> errors = [.. ContractValidation.Validate(plan)];
        if (!string.Equals(plan.CompilerVersion, CompilerVersion, StringComparison.Ordinal))
        {
            errors.Add(
                $"Review plan requires compiler '{plan.CompilerVersion}', but this build provides " +
                $"'{CompilerVersion}'.");
        }

        Dictionary<CandidateKey, EstimateReport> candidates = [];
        foreach (EstimateReport estimate in sourceEstimates)
        {
            errors.AddRange(ContractValidation.Validate(estimate).Select(error =>
                $"Source estimate '{estimate.Repository.Name}/{estimate.Profile}': {error}"));

            if (string.IsNullOrWhiteSpace(estimate.Repository.SourceDigest))
            {
                errors.Add(
                    $"Source estimate '{estimate.Repository.Name}/{estimate.Profile}' requires " +
                    "repository.sourceDigest.");
                continue;
            }

            CandidateKey key = new(
                estimate.Repository.SourceDigest,
                estimate.Profile,
                estimate.Baseline.Id);
            if (!candidates.TryAdd(key, estimate))
            {
                errors.Add(
                    $"More than one source estimate matches digest '{key.SourceDigest}', " +
                    $"profile '{key.Profile}', and baseline '{key.BaselineId}'.");
            }
        }

        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }

        List<CalibrationRecord> records = [];
        foreach (CalibrationReviewPlanRecord planned in plan.Records
                     .OrderBy(record => record.Id, StringComparer.Ordinal))
        {
            CandidateKey key = new(
                planned.Repository.SourceDigest,
                planned.Profile,
                planned.BaselineId);
            if (!candidates.TryGetValue(key, out EstimateReport? estimate))
            {
                errors.Add(
                    $"No source estimate matches review-plan record '{planned.Id}' with digest " +
                    $"'{key.SourceDigest}', profile '{key.Profile}', and baseline '{key.BaselineId}'.");
                continue;
            }

            string actualDigest = CalibrationDigest.Compute(estimate);
            if (!string.Equals(
                    planned.SourceEstimatorVersion,
                    estimate.EstimatorVersion,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"Review-plan record '{planned.Id}' expects estimator " +
                    $"'{planned.SourceEstimatorVersion}', but the source estimate uses " +
                    $"'{estimate.EstimatorVersion}'.");
            }

            if (!string.Equals(planned.SourceEstimateDigest, actualDigest, StringComparison.Ordinal))
            {
                errors.Add(
                    $"Review-plan record '{planned.Id}' expects estimate digest " +
                    $"'{planned.SourceEstimateDigest}', but received '{actualDigest}'.");
            }

            Dictionary<string, WorkItem[]> capabilities = estimate.WorkItems
                .GroupBy(item => CalibrationAuthoring.GetSourceCapabilityId(item.Id))
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
                    StringComparer.Ordinal);
            Dictionary<string, CalibrationCapabilityReviewDecision> decisions = planned.Capabilities
                .ToDictionary(decision => decision.SourceCapabilityId, StringComparer.Ordinal);

            foreach (string missing in capabilities.Keys
                         .Except(decisions.Keys, StringComparer.Ordinal)
                         .OrderBy(id => id, StringComparer.Ordinal))
            {
                errors.Add(
                    $"Review-plan record '{planned.Id}' has no decision for source capability '{missing}'.");
            }

            foreach (string unknown in decisions.Keys
                         .Except(capabilities.Keys, StringComparer.Ordinal)
                         .OrderBy(id => id, StringComparer.Ordinal))
            {
                errors.Add(
                    $"Review-plan record '{planned.Id}' references unknown source capability '{unknown}'.");
            }

            if (errors.Count > 0)
            {
                continue;
            }

            List<CalibrationTarget> targets = [];
            foreach (CalibrationCapabilityReviewDecision decision in planned.Capabilities
                         .OrderBy(item => item.SourceCapabilityId, StringComparer.Ordinal))
            {
                WorkItem[] items = capabilities[decision.SourceCapabilityId];
                if (decision.Targets.Count > items.Length)
                {
                    errors.Add(
                        $"Capability '{decision.SourceCapabilityId}' in '{planned.Id}' has " +
                        $"{decision.Targets.Count} reviewed targets but only {items.Length} source work items.");
                    continue;
                }

                EffortCategory category = items[0].Category;
                string scope = items[0].Scope;
                if (items.Any(item => item.Category != category ||
                                      !string.Equals(item.Scope, scope, StringComparison.Ordinal)))
                {
                    errors.Add(
                        $"Source capability '{decision.SourceCapabilityId}' in '{planned.Id}' " +
                        "contains mixed categories or scopes and cannot be compiled automatically.");
                    continue;
                }

                string title = TitlePartSuffix.Replace(items[0].Title, string.Empty);
                int cursor = 0;
                for (int index = 0; index < decision.Targets.Count; index++)
                {
                    int remainingItems = items.Length - cursor;
                    int remainingTargets = decision.Targets.Count - index;
                    int take = (remainingItems + remainingTargets - 1) / remainingTargets;
                    WorkItem[] assigned = items[cursor..(cursor + take)];
                    cursor += take;

                    CalibrationReviewTargetDecision reviewed = decision.Targets[index];
                    targets.Add(new CalibrationTarget
                    {
                        Id = $"target:{decision.SourceCapabilityId}:review-{index + 1:D4}",
                        Category = category,
                        Title = decision.Targets.Count == 1
                            ? title
                            : $"{title} (review target {index + 1} of {decision.Targets.Count})",
                        Scope = scope,
                        SourceWorkItemIds = [.. assigned.Select(item => item.Id)],
                        EvidenceIds = [.. assigned
                            .SelectMany(item => item.EvidenceIds)
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(id => id, StringComparer.Ordinal)],
                        Hours = reviewed.Hours,
                        Rationale = decision.Rationale,
                        UncertaintyReasons = reviewed.UncertaintyReasons,
                        SizeException = reviewed.SizeException,
                    });
                }
            }

            records.Add(new CalibrationRecord
            {
                Id = planned.Id,
                Repository = planned.Repository,
                Profile = planned.Profile,
                BaselineId = planned.BaselineId,
                Partition = planned.Partition,
                SourceEstimatorVersion = planned.SourceEstimatorVersion,
                SourceEstimateDigest = planned.SourceEstimateDigest,
                Source = planned.Source,
                Review = planned.Review,
                Targets = targets,
            });
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
            Rubric = plan.Rubric,
            Records = records,
        };
        IReadOnlyList<string> corpusErrors = ContractValidation.Validate(corpus);
        if (corpusErrors.Count > 0)
        {
            throw new CalibrationEvaluationException([.. corpusErrors.Select(error =>
                $"Compiled corpus: {error}")]);
        }

        return corpus;
    }

    private readonly record struct CandidateKey(
        string SourceDigest,
        EstimationProfile Profile,
        string BaselineId);
}
