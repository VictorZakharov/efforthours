using System.Globalization;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class CandidateOperationalGateAssessment
{
    public static IReadOnlyList<CandidatePreflightGate> Build(
        SamplingPlan plan,
        CalibrationCorpus corpus,
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs,
        IReadOnlyList<EstimateReport> candidates,
        CalibrationEvaluationReport seed,
        CalibrationEvaluationReport candidate,
        LogicalCandidateModel model,
        string modelJson,
        string modelDigest)
    {
        return
        [
            BuildStratumGate(plan, seed, candidate),
            BuildCategoryGate(seed, candidate),
            BuildShapeAndSizeGate(plan, seed, candidate),
            BuildSchemaAndExplanationGate(inputs, candidates, model),
            BuildSafetyGate(plan, corpus, inputs, candidates, model, modelJson, modelDigest),
        ];
    }

    private static CandidatePreflightGate BuildStratumGate(
        SamplingPlan plan,
        CalibrationEvaluationReport seed,
        CalibrationEvaluationReport candidate)
    {
        SliceMetric[] metrics =
        [
            .. DevelopmentFamilies(plan)
                .GroupBy(family => family.PrimaryStratum, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => Slice(group.Key, group.Select(item => item.Id), seed, candidate)),
        ];
        bool passed = metrics.Length == 3 && metrics.All(metric =>
            metric.FamilyCount >= 5 &&
            metric.CandidateWape <= 0.30m &&
            metric.CandidateWape <= metric.SeedWape + 0.03m &&
            decimal.Abs(metric.CandidateBias) <= 0.15m);
        return Gate(
            "ecosystem-stratum-agreement",
            passed,
            "Every 5-family development stratum has expected WAPE <= 0.30, no more than 0.03 worse than seed, and absolute bias <= 0.15.",
            string.Join("; ", metrics.Select(Describe)));
    }

    private static CandidatePreflightGate BuildCategoryGate(
        CalibrationEvaluationReport seed,
        CalibrationEvaluationReport candidate) => BuildCategoryGate(
            seed.Categories,
            candidate.Categories);

    internal static CandidatePreflightGate BuildCategoryGate(
        IReadOnlyList<CalibrationCategoryMetrics> seedCategories,
        IReadOnlyList<CalibrationCategoryMetrics> candidateCategories)
    {
        Dictionary<EffortCategory, CalibrationCategoryMetrics> seedIndex = seedCategories
            .ToDictionary(item => item.Category);
        CalibrationCategoryMetrics[] material =
        [
            .. candidateCategories.Where(item =>
                    item.Metrics.Expected.SampleCount >= 5 &&
                    item.Metrics.Expected.ReviewedHours >= 20m)
                .OrderBy(item => item.Category),
        ];
        decimal reviewed = material.Sum(item => item.Metrics.Expected.ReviewedHours);
        bool computable = material.Length > 0 && reviewed > 0m && material.All(item =>
            seedIndex.ContainsKey(item.Category) &&
            item.Metrics.Expected.WeightedAbsolutePercentageError is not null &&
            item.Metrics.Expected.AggregateBiasRate is not null &&
            seedIndex[item.Category].Metrics.Expected.WeightedAbsolutePercentageError is not null);
        decimal seedWape = computable
            ? material.Sum(item =>
                Require(seedIndex[item.Category].Metrics.Expected.WeightedAbsolutePercentageError) *
                item.Metrics.Expected.ReviewedHours) / reviewed
            : decimal.MaxValue;
        decimal candidateWape = computable
            ? material.Sum(item =>
                Require(item.Metrics.Expected.WeightedAbsolutePercentageError) *
                item.Metrics.Expected.ReviewedHours) / reviewed
            : decimal.MaxValue;
        string[] violations = computable
            ?
            [
                .. material.Where(item =>
                        Require(item.Metrics.Expected.WeightedAbsolutePercentageError) >
                            Require(seedIndex[item.Category].Metrics.Expected.WeightedAbsolutePercentageError) + 0.05m ||
                        decimal.Abs(Require(item.Metrics.Expected.AggregateBiasRate)) > 0.20m)
                    .Select(item =>
                        $"{item.Category}[wape={Format(Require(item.Metrics.Expected.WeightedAbsolutePercentageError))}," +
                        $"seed={Format(Require(seedIndex[item.Category].Metrics.Expected.WeightedAbsolutePercentageError))}," +
                        $"bias={Format(Require(item.Metrics.Expected.AggregateBiasRate))}]")
                    .Order(StringComparer.Ordinal),
            ]
            : [];
        bool passed = computable &&
            candidateWape <= 0.35m &&
            seedWape > 0m &&
            candidateWape <= seedWape * 0.90m &&
            material.All(item =>
                Require(item.Metrics.Expected.WeightedAbsolutePercentageError) <=
                    Require(seedIndex[item.Category].Metrics.Expected.WeightedAbsolutePercentageError) + 0.05m &&
                decimal.Abs(Require(item.Metrics.Expected.AggregateBiasRate)) <= 0.20m);
        string categories = string.Join(",", material.Select(item =>
            $"{item.Category}:{item.Metrics.Expected.SampleCount}/{Format(item.Metrics.Expected.ReviewedHours)}h"));
        return Gate(
            "material-category-agreement",
            passed,
            "Pooled material-category WAPE <= 0.35 and at least 10% below seed; no material category regresses seed by > 0.05 or exceeds 0.20 absolute bias.",
            computable
                ? $"material={material.Length}[{categories}]; pooled-wape={Format(candidateWape)}; " +
                  $"seed={Format(seedWape)}; violations={violations.Length}[{string.Join(",", violations)}]"
                : "required material-category metrics were not computable");
    }

    private static CandidatePreflightGate BuildShapeAndSizeGate(
        SamplingPlan plan,
        CalibrationEvaluationReport seed,
        CalibrationEvaluationReport candidate)
    {
        SamplingFamily[] families = [.. DevelopmentFamilies(plan)];
        List<SliceMetric> metrics = [];
        foreach (string tag in plan.RequiredShapeTags.Order(StringComparer.Ordinal))
        {
            SamplingFamily[] members = [.. families.Where(family => family.ShapeTags.Contains(
                tag,
                StringComparer.Ordinal))];
            if (members.Length >= 3)
            {
                metrics.Add(Slice($"shape:{tag}", members.Select(item => item.Id), seed, candidate));
            }
        }

        foreach (IGrouping<string, SamplingFamily> group in families
                     .GroupBy(family => family.Size.Band, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            if (group.Count() >= 3)
            {
                metrics.Add(Slice($"size:{group.Key}", group.Select(item => item.Id), seed, candidate));
            }
        }

        bool passed = metrics.Count > 0 && metrics.All(metric =>
            metric.FamilyCount >= 3 && metric.CandidateWape <= metric.SeedWape + 0.05m);
        return Gate(
            "shape-and-size-slice-regression",
            passed,
            "Every development shape or size slice with at least three families has candidate expected WAPE no more than 0.05 worse than seed.",
            string.Join("; ", metrics.Select(Describe)));
    }

    private static CandidatePreflightGate BuildSchemaAndExplanationGate(
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs,
        IReadOnlyList<EstimateReport> candidates,
        LogicalCandidateModel model)
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
            $"reports={candidates.Count}; adjusted-items={adjusted}; canonical-round-trip={(passed ? "passed" : "failed")}");
    }

    private static CandidatePreflightGate BuildSafetyGate(
        SamplingPlan plan,
        CalibrationCorpus corpus,
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs,
        IReadOnlyList<EstimateReport> candidates,
        LogicalCandidateModel model,
        string modelJson,
        string modelDigest)
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
        Dictionary<string, SamplingFamily> familyIndex = DevelopmentFamilies(plan)
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
            $"development-replays={inputs.Count}; ood=seed-fallback; model-digest={modelDigest}; limits=" +
            $"{LogicalCandidateProjectionRunner.MaximumWorkItems}/" +
            $"{LogicalCandidateProjectionRunner.MaximumEvidenceFacts}/" +
            LogicalCandidateProjectionRunner.MaximumEvidenceReferences);
    }

    private static IEnumerable<SamplingFamily> DevelopmentFamilies(SamplingPlan plan) =>
        plan.Families.Where(family => family.Partition == "development");

    private static SliceMetric Slice(
        string id,
        IEnumerable<string> repositoryIds,
        CalibrationEvaluationReport seed,
        CalibrationEvaluationReport candidate)
    {
        HashSet<string> ids = repositoryIds.ToHashSet(StringComparer.Ordinal);
        CalibrationRepositoryEvaluation[] seedRows =
            [.. seed.Repositories.Where(item => ids.Contains(item.RepositoryId))];
        CalibrationRepositoryEvaluation[] candidateRows =
            [.. candidate.Repositories.Where(item => ids.Contains(item.RepositoryId))];
        if (seedRows.Length != ids.Count || candidateRows.Length != ids.Count)
        {
            throw new InvalidDataException($"Operational slice '{id}' does not map every family.");
        }

        decimal reviewed = candidateRows.Sum(item => item.ReviewedTotal.Expected);
        if (reviewed <= 0m)
        {
            throw new InvalidDataException($"Operational slice '{id}' has no reviewed expected hours.");
        }

        return new SliceMetric(
            id,
            ids.Count,
            seedRows.Sum(item => item.ExpectedAbsoluteErrorHours) / reviewed,
            candidateRows.Sum(item => item.ExpectedAbsoluteErrorHours) / reviewed,
            candidateRows.Sum(item => item.ExpectedSignedErrorHours) / reviewed);
    }

    private static CandidatePreflightGate Gate(
        string id,
        bool passed,
        string requirement,
        string observed) => new()
        {
            Id = id,
            Status = passed ? "passed" : "failed",
            Passed = passed,
            Requirement = requirement,
            Observed = observed,
            Rationale = passed
                ? "The exact development candidate satisfies this operational gate."
                : "The operational gate failed or was not computable, so candidate freeze remains blocked.",
        };

    private static string Describe(SliceMetric metric) =>
        $"{metric.Id}[n={metric.FamilyCount}]:wape={Format(metric.CandidateWape)}," +
        $"seed={Format(metric.SeedWape)},bias={Format(metric.CandidateBias)}";

    private static decimal Require(decimal? value) =>
        value ?? throw new InvalidDataException("A required operational metric is not computable.");

    private static string Format(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero)
            .ToString("0.####", CultureInfo.InvariantCulture);

    private static CandidateKey Key(EstimateReport estimate) => new(
        estimate.Repository.SourceDigest
            ?? throw new InvalidDataException("A candidate source digest is missing."),
        estimate.Profile,
        estimate.Baseline.Id);

    private readonly record struct CandidateKey(
        string SourceDigest,
        EstimationProfile Profile,
        string BaselineId);

    private sealed record SliceMetric(
        string Id,
        int FamilyCount,
        decimal SeedWape,
        decimal CandidateWape,
        decimal CandidateBias);
}
