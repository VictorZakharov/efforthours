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
        => Build(
            plan,
            corpus,
            inputs,
            candidates,
            seed,
            candidate,
            model,
            modelJson,
            modelDigest,
            CalibrationPartition.Development);

    public static IReadOnlyList<CandidatePreflightGate> Build(
        SamplingPlan plan,
        CalibrationCorpus corpus,
        IReadOnlyList<LogicalCandidateDevelopmentInput> inputs,
        IReadOnlyList<EstimateReport> candidates,
        CalibrationEvaluationReport seed,
        CalibrationEvaluationReport candidate,
        LogicalCandidateModel model,
        string modelJson,
        string modelDigest,
        CalibrationPartition partition)
    {
        string partitionName = PartitionName(partition);
        int minimumStratumFamilies = partition == CalibrationPartition.Development ? 5 : 3;
        return
        [
            BuildStratumGate(
                plan,
                seed,
                candidate,
                partitionName,
                minimumStratumFamilies),
            BuildCategoryGate(seed, candidate, partitionName),
            BuildShapeAndSizeGate(plan, seed, candidate, partitionName),
            CandidateOperationalIntegrityGateAssessment.BuildSchemaAndExplanationGate(
                inputs,
                candidates,
                model,
                partitionName),
            CandidateOperationalIntegrityGateAssessment.BuildSafetyGate(
                plan,
                corpus,
                inputs,
                candidates,
                model,
                modelJson,
                modelDigest,
                partitionName),
        ];
    }

    private static CandidatePreflightGate BuildStratumGate(
        SamplingPlan plan,
        CalibrationEvaluationReport seed,
        CalibrationEvaluationReport candidate,
        string partition,
        int minimumFamilies)
    {
        SliceMetric[] metrics =
        [
            .. PartitionFamilies(plan, partition)
                .GroupBy(family => family.PrimaryStratum, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => Slice(group.Key, group.Select(item => item.Id), seed, candidate)),
        ];
        bool passed = metrics.Length == 3 && metrics.All(metric =>
            metric.FamilyCount >= minimumFamilies &&
            metric.CandidateWape <= 0.30m &&
            metric.CandidateWape <= metric.SeedWape + 0.03m &&
            decimal.Abs(metric.CandidateBias) <= 0.15m);
        return Gate(
            "ecosystem-stratum-agreement",
            passed,
            $"Every {minimumFamilies}-family {partition} stratum has expected WAPE <= 0.30, no more than 0.03 worse than seed, and absolute bias <= 0.15.",
            string.Join("; ", metrics.Select(Describe)),
            partition);
    }

    private static CandidatePreflightGate BuildCategoryGate(
        CalibrationEvaluationReport seed,
        CalibrationEvaluationReport candidate,
        string partition)
    {
        CandidatePreflightGate gate = BuildCategoryGate(seed.Categories, candidate.Categories);
        return gate with
        {
            Rationale = gate.Passed
                ? $"The exact {partition} candidate satisfies this operational gate."
                : $"The {partition} operational gate failed or was not computable, so candidate selection is blocked.",
        };
    }

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
        CalibrationEvaluationReport candidate,
        string partition)
    {
        SamplingFamily[] families = [.. PartitionFamilies(plan, partition)];
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
            $"Every {partition} shape or size slice with at least three families has candidate expected WAPE no more than 0.05 worse than seed.",
            string.Join("; ", metrics.Select(Describe)),
            partition);
    }

    private static IEnumerable<SamplingFamily> PartitionFamilies(
        SamplingPlan plan,
        string partition) => plan.Families.Where(family => family.Partition == partition);

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
        string observed,
        string partition = "development") => new()
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

    private static string Describe(SliceMetric metric) =>
        $"{metric.Id}[n={metric.FamilyCount}]:wape={Format(metric.CandidateWape)}," +
        $"seed={Format(metric.SeedWape)},bias={Format(metric.CandidateBias)}";

    private static decimal Require(decimal? value) =>
        value ?? throw new InvalidDataException("A required operational metric is not computable.");

    private static string Format(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero)
            .ToString("0.####", CultureInfo.InvariantCulture);

    private static string PartitionName(CalibrationPartition partition) =>
        partition.ToString().ToLowerInvariant();

    private sealed record SliceMetric(
        string Id,
        int FamilyCount,
        decimal SeedWape,
        decimal CandidateWape,
        decimal CandidateBias);
}
