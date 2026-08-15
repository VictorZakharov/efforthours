using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class CandidateOperationalIntegrityGateAssessment
{
    public static CandidatePreflightGate BuildSchemaAndExplanationGate(
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs,
        IReadOnlyList<EstimateReport> candidates,
        LogicalCandidateModel model,
        string partition)
    {
        Dictionary<CandidateKey, EstimateReport> candidateIndex = candidates.ToDictionary(Key);
        bool passed = inputs.Count == candidates.Count;
        int adjusted = 0;
        foreach (LogicalCandidateDevelopmentInput input in inputs)
        {
            if (!candidateIndex.TryGetValue(Key(input.Estimate), out EstimateReport? candidate))
            {
                passed = false;
                continue;
            }

            Dictionary<string, WorkItem> sourceItems = input.Estimate.WorkItems.ToDictionary(item => item.Id);
            passed &= ContractValidation.Validate(candidate).Count == 0;
            passed &= input.Estimate.WorkItems.Select(item => item.Id).SequenceEqual(
                candidate.WorkItems.Select(item => item.Id),
                StringComparer.Ordinal);
            passed &= ContractValidation.Sum(candidate.Categories.Select(item => item.Hours)) ==
                candidate.TotalEffort;
            string serialized = ContractJson.Serialize(candidate);
            passed &= ContractJson.Serialize(ContractJson.Deserialize<EstimateReport>(serialized)) == serialized;
            foreach (WorkItem item in candidate.WorkItems)
            {
                WorkItem source = sourceItems[item.Id];
                passed &= source.EvidenceIds.SequenceEqual(item.EvidenceIds, StringComparer.Ordinal);
                passed &= !item.Reason.Contains(input.DirectoryPath, StringComparison.OrdinalIgnoreCase);
                if (source.Hours != item.Hours)
                {
                    adjusted++;
                    passed &= item.Reason.Contains(model.CandidateId, StringComparison.Ordinal) &&
                        item.Reason.Contains(model.Point.ScorerVersion, StringComparison.Ordinal) &&
                        item.Reason.Contains(item.Id, StringComparison.Ordinal);
                }
            }
        }

        return Gate(
            "schema-lineage-and-saved-explanation",
            passed && adjusted > 0,
            "All projections validate and round-trip, preserve stable work/evidence IDs, reconcile exactly, and explain every adjusted item without host paths.",
            $"reports={candidates.Count}; adjusted-items={adjusted}; canonical-round-trip={(passed ? "passed" : "failed")}",
            partition);
    }

    public static CandidatePreflightGate BuildSafetyGate(
        SamplingPlan plan,
        CalibrationCorpus corpus,
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs,
        IReadOnlyList<EstimateReport> candidates,
        LogicalCandidateModel model,
        string modelJson,
        string modelDigest,
        string partition)
    {
        bool passed = inputs.Count > 0;
        LogicalCandidateProjectionRunner.ValidateArtifactDigest(modelJson, modelDigest);
        try
        {
            LogicalCandidateProjectionRunner.ValidateArtifactDigest(modelJson + " ", modelDigest);
            passed = false;
        }
        catch (InvalidDataException)
        {
        }

        Dictionary<CandidateKey, EstimateReport> candidateIndex = candidates.ToDictionary(Key);
        Dictionary<string, SamplingFamily> familyIndex = plan.Families
            .Where(family => family.Partition == partition)
            .ToDictionary(family => family.Id, StringComparer.Ordinal);
        Dictionary<string, string> repositoryIds = corpus.Records.ToDictionary(
            record => record.Repository.SourceDigest,
            record => record.Repository.Id,
            StringComparer.Ordinal);
        foreach (LogicalCandidateDevelopmentInput input in inputs)
        {
            string sourceDigest = input.Estimate.Repository.SourceDigest!;
            SamplingFamily family = familyIndex[repositoryIds[sourceDigest]];
            LogicalCandidateProjectionResult projected = LogicalCandidateProjectionRunner.Project(
                input.Estimate,
                input.Evidence,
                model,
                family.PrimaryStratum);
            passed &= projected.Diagnostic is null &&
                ContractJson.Serialize(projected.Estimate) ==
                    ContractJson.Serialize(candidateIndex[Key(input.Estimate)]);
        }

        LogicalCandidateDevelopmentInput first = inputs[0];
        LogicalCandidateProjectionResult outside = LogicalCandidateProjectionRunner.Project(
            first.Estimate,
            first.Evidence,
            model,
            "outside-policy");
        passed &= ContractJson.Serialize(outside.Estimate) == ContractJson.Serialize(first.Estimate) &&
            outside.Diagnostic?.Contains(model.BaselineEstimatorVersion, StringComparison.Ordinal) == true;
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        try
        {
            LogicalCandidateProjectionRunner.Project(
                first.Estimate,
                first.Evidence,
                model,
                familyIndex.Values.First().PrimaryStratum,
                cancelled.Token);
            passed = false;
        }
        catch (OperationCanceledException)
        {
        }

        return Gate(
            "offline-safety-ood-and-tamper",
            passed,
            "The bounded saved-artifact projector is cancellable, fails closed on model tamper, and visibly retains the complete seed for unsupported strata without target execution, history, network, or external processes.",
            $"{partition}-replays={inputs.Count}; ood=seed-fallback; model-digest={modelDigest}; limits=" +
            $"{LogicalCandidateProjectionRunner.MaximumWorkItems}/" +
            $"{LogicalCandidateProjectionRunner.MaximumEvidenceFacts}/" +
            LogicalCandidateProjectionRunner.MaximumEvidenceReferences,
            partition);
    }

    private static CandidatePreflightGate Gate(
        string id,
        bool passed,
        string requirement,
        string observed,
        string partition) => new()
        {
            Id = id,
            Status = passed ? "passed" : "failed",
            Passed = passed,
            Requirement = requirement,
            Observed = observed,
            Rationale = passed
                ? $"The exact {partition} candidate satisfies this operational gate."
                : partition == "development"
                    ? "The operational gate failed or was not computable, so candidate freeze remains blocked."
                    : $"The {partition} operational gate failed or was not computable, so candidate selection is blocked.",
        };

    private static CandidateKey Key(EstimateReport estimate) => new(
        estimate.Repository.SourceDigest
            ?? throw new InvalidDataException("A candidate source digest is missing."),
        estimate.Profile,
        estimate.Baseline.Id);

    private readonly record struct CandidateKey(
        string SourceDigest,
        EstimationProfile Profile,
        string BaselineId);
}
