using System.Globalization;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal sealed record CandidatePreflightAssessment
{
    public required CandidatePreflightMetrics Metrics { get; init; }

    public required IReadOnlyList<CandidatePreflightGate> Gates { get; init; }

    public static CandidatePreflightAssessment Build(
        CalibrationCorpus corpus,
        IReadOnlyList<EstimateReport> candidates,
        CalibrationEvaluationReport seed,
        CalibrationEvaluationReport candidate)
    {
        DerivedIntervals intervals = BuildIntervals(corpus, candidates, candidate);
        CandidatePreflightMetrics metrics = BuildMetrics(seed, candidate, intervals);
        return new CandidatePreflightAssessment
        {
            Metrics = metrics,
            Gates = BuildGates(seed, metrics),
        };
    }

    private static CandidatePreflightMetrics BuildMetrics(
        CalibrationEvaluationReport seed,
        CalibrationEvaluationReport candidate,
        DerivedIntervals intervals)
    {
        decimal seedWape = Require(seed.RepositoryTotals.Expected.WeightedAbsolutePercentageError);
        decimal candidateWape = Require(
            candidate.RepositoryTotals.Expected.WeightedAbsolutePercentageError);
        return new CandidatePreflightMetrics
        {
            RepositoryExpectedWape = candidateWape,
            RelativeWapeImprovement = Round4((seedWape - candidateWape) / seedWape),
            AbsoluteAggregateBias = decimal.Abs(Require(
                candidate.RepositoryTotals.Expected.AggregateBiasRate)),
            MedianRepositoryAbsoluteErrorHours =
                candidate.RepositoryTotals.Expected.MedianAbsoluteErrorHours,
            FamilyMaximumErrorPassRate = intervals.FamilyMaximumErrorPassRate,
            FamilyOrdinaryErrorPassRate = intervals.FamilyOrdinaryErrorPassRate,
            LowWape = Require(candidate.RepositoryTotals.Low.WeightedAbsolutePercentageError),
            HighWape = Require(candidate.RepositoryTotals.High.WeightedAbsolutePercentageError),
            RepositoryExpectedCoverage = Require(
                candidate.RepositoryTotals.Interval.ReviewedExpectedCoverage),
            MeanRepositoryNormalizedWidth = intervals.MeanRepositoryNormalizedWidth,
            P90RepositoryNormalizedWidth = intervals.P90RepositoryNormalizedWidth,
            MeanWidthRelativeToSeed = Divide(
                candidate.RepositoryTotals.Interval.MeanCandidateWidthHours,
                seed.RepositoryTotals.Interval.MeanCandidateWidthHours),
            MeanWidthRelativeToReviewed = Divide(
                candidate.RepositoryTotals.Interval.MeanCandidateWidthHours,
                candidate.RepositoryTotals.Interval.MeanReviewedWidthHours),
            MatchedTargetExpectedCoverage = intervals.TargetExpectedCoverage,
            MatchedTargetMeanNormalizedWidth = intervals.TargetMeanNormalizedWidth,
            TargetMatchRate = Require(candidate.Match.TargetMatchRate),
            SourceReferenceMatchRate = Require(candidate.Match.SourceWorkItemReferenceMatchRate),
            CandidateItemMatchRate = Require(candidate.Match.CandidateWorkItemMatchRate),
            CategoryMismatchRate = intervals.CategoryMismatchRate,
        };
    }

    private static List<CandidatePreflightGate> BuildGates(
        CalibrationEvaluationReport seed,
        CandidatePreflightMetrics metrics)
    {
        decimal seedBias = decimal.Abs(Require(seed.RepositoryTotals.Expected.AggregateBiasRate));
        decimal seedLowWape = Require(seed.RepositoryTotals.Low.WeightedAbsolutePercentageError);
        decimal seedHighWape = Require(seed.RepositoryTotals.High.WeightedAbsolutePercentageError);
        decimal seedCoverage = Require(seed.RepositoryTotals.Interval.ReviewedExpectedCoverage);
        List<CandidatePreflightGate> gates =
        [
            Gate(
                "repository-expected-wape",
                metrics.RepositoryExpectedWape <= 0.20m && metrics.RelativeWapeImprovement >= 0.15m,
                "WAPE <= 0.20 and at least 15% lower than seed",
                $"wape={Format(metrics.RepositoryExpectedWape)}; relative-improvement={Format(metrics.RelativeWapeImprovement)}"),
            Gate(
                "aggregate-bias",
                metrics.AbsoluteAggregateBias <= 0.10m && metrics.AbsoluteAggregateBias <= seedBias,
                "absolute bias <= 0.10 and no worse than seed",
                $"candidate={Format(metrics.AbsoluteAggregateBias)}; seed={Format(seedBias)}"),
            Gate(
                "median-repository-error",
                metrics.MedianRepositoryAbsoluteErrorHours <=
                    seed.RepositoryTotals.Expected.MedianAbsoluteErrorHours,
                "median expected absolute error no greater than seed",
                $"candidate={Format(metrics.MedianRepositoryAbsoluteErrorHours)}h; " +
                $"seed={Format(seed.RepositoryTotals.Expected.MedianAbsoluteErrorHours)}h"),
            Gate(
                "per-family-maximum-error",
                metrics.FamilyMaximumErrorPassRate == 1m,
                "every family <= max(16h, 50%) error",
                Format(metrics.FamilyMaximumErrorPassRate)),
            Gate(
                "per-family-ordinary-error",
                metrics.FamilyOrdinaryErrorPassRate >= 0.90m,
                "at least 90% of families <= max(8h, 25%) error",
                Format(metrics.FamilyOrdinaryErrorPassRate)),
            Gate(
                "low-wape",
                metrics.LowWape <= 0.30m && metrics.LowWape <= seedLowWape + 0.03m,
                "low WAPE <= 0.30 and no more than 0.03 worse than seed",
                Format(metrics.LowWape)),
            Gate(
                "high-wape",
                metrics.HighWape <= 0.30m && metrics.HighWape <= seedHighWape + 0.03m,
                "high WAPE <= 0.30 and no more than 0.03 worse than seed",
                Format(metrics.HighWape)),
            Gate(
                "mapping",
                metrics.TargetMatchRate >= 0.95m &&
                metrics.SourceReferenceMatchRate >= 0.95m &&
                metrics.CandidateItemMatchRate >= 0.95m,
                "target, source-reference, and candidate-item match rates each >= 0.95",
                $"{Format(metrics.TargetMatchRate)}/{Format(metrics.SourceReferenceMatchRate)}/" +
                Format(metrics.CandidateItemMatchRate)),
            Gate(
                "category-mismatch",
                metrics.CategoryMismatchRate <= 0.02m,
                "category mismatch <= 0.02 of reviewed targets",
                Format(metrics.CategoryMismatchRate)),
            Gate(
                "repository-expected-coverage",
                metrics.RepositoryExpectedCoverage >= 0.80m &&
                metrics.RepositoryExpectedCoverage >= seedCoverage - 0.15m,
                "coverage >= 0.80 and no more than 0.15 below seed",
                Format(metrics.RepositoryExpectedCoverage)),
            Gate(
                "mean-repository-normalized-width",
                metrics.MeanRepositoryNormalizedWidth <= 0.50m,
                "mean repository normalized width <= 0.50",
                Format(metrics.MeanRepositoryNormalizedWidth)),
            Gate(
                "p90-repository-normalized-width",
                metrics.P90RepositoryNormalizedWidth <= 0.80m,
                "p90 repository normalized width <= 0.80",
                Format(metrics.P90RepositoryNormalizedWidth)),
            Gate(
                "mean-width-relative-to-seed",
                metrics.MeanWidthRelativeToSeed <= 0.75m,
                "mean width relative to seed <= 0.75",
                Format(metrics.MeanWidthRelativeToSeed)),
            Gate(
                "mean-width-relative-to-reviewed",
                metrics.MeanWidthRelativeToReviewed <= 1.25m,
                "mean width relative to reviewed ranges <= 1.25",
                Format(metrics.MeanWidthRelativeToReviewed)),
            Gate(
                "matched-target-expected-coverage",
                metrics.MatchedTargetExpectedCoverage >= 0.75m,
                "matched-target expected coverage >= 0.75",
                Format(metrics.MatchedTargetExpectedCoverage)),
            Gate(
                "matched-target-normalized-width",
                metrics.MatchedTargetMeanNormalizedWidth <= 0.75m,
                "matched-target mean normalized width <= 0.75",
                Format(metrics.MatchedTargetMeanNormalizedWidth)),
        ];
        string[] notEvaluated =
        [
            "ecosystem-stratum-agreement",
            "material-category-agreement",
            "shape-and-size-slice-regression",
            "public-mutation-suite",
            "cross-platform-determinism",
            "schema-lineage-and-saved-explanation",
            "offline-safety-ood-and-tamper",
            "median-latency-overhead",
            "slowest-latency-overhead",
            "peak-working-set-overhead",
            "installed-package-increase",
            "scanner-thresholds-and-target-fingerprints",
        ];
        gates.AddRange(notEvaluated.Select(NotEvaluated));
        return gates;
    }

    private static DerivedIntervals BuildIntervals(
        CalibrationCorpus corpus,
        IReadOnlyList<EstimateReport> candidates,
        CalibrationEvaluationReport evaluation)
    {
        Dictionary<CandidateKey, EstimateReport> index = candidates.ToDictionary(
            candidate => new CandidateKey(
                candidate.Repository.SourceDigest!,
                candidate.Profile,
                candidate.Baseline.Id));
        List<decimal> repositoryWidths = [];
        List<decimal> targetWidths = [];
        int targetCount = 0;
        int targetCovered = 0;
        foreach (CalibrationRecord record in corpus.Records.Where(record =>
                     record.Partition == CalibrationPartition.Development))
        {
            EstimateReport candidate = index[new CandidateKey(
                record.Repository.SourceDigest,
                record.Profile,
                record.BaselineId)];
            Dictionary<string, WorkItem> items = candidate.WorkItems.ToDictionary(item => item.Id);
            EffortRange reviewedTotal = ContractValidation.Sum(record.Targets.Select(target => target.Hours));
            repositoryWidths.Add(
                (candidate.TotalEffort.High - candidate.TotalEffort.Low) / reviewedTotal.Expected);
            foreach (CalibrationTarget target in record.Targets)
            {
                EffortRange predicted = ContractValidation.Sum(
                    target.SourceWorkItemIds.Select(id => items[id].Hours));
                targetCount++;
                if (predicted.Low <= target.Hours.Expected && target.Hours.Expected <= predicted.High)
                {
                    targetCovered++;
                }

                if (target.Hours.Expected > 0m)
                {
                    targetWidths.Add((predicted.High - predicted.Low) / target.Hours.Expected);
                }
            }
        }

        int maximumPass = evaluation.Repositories.Count(repository =>
            repository.ExpectedAbsoluteErrorHours <= decimal.Max(
                16m,
                repository.ReviewedTotal.Expected * 0.50m));
        int ordinaryPass = evaluation.Repositories.Count(repository =>
            repository.ExpectedAbsoluteErrorHours <= decimal.Max(
                8m,
                repository.ReviewedTotal.Expected * 0.25m));
        int mismatchCount = evaluation.Repositories.Sum(repository =>
            repository.CategoryMismatchTargetIds.Count);
        decimal[] orderedWidths = [.. repositoryWidths.Order()];
        return new DerivedIntervals(
            Round4(repositoryWidths.Average()),
            Round4(orderedWidths[(int)Math.Ceiling(orderedWidths.Length * 0.90m) - 1]),
            Round4((decimal)targetCovered / targetCount),
            Round4(targetWidths.Average()),
            Round4((decimal)maximumPass / evaluation.RepositoryCount),
            Round4((decimal)ordinaryPass / evaluation.RepositoryCount),
            Round4((decimal)mismatchCount / evaluation.Match.TargetCount));
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
                ? "The development preflight satisfies this numerical gate."
                : "The development preflight does not satisfy this gate; the candidate cannot advance.",
        };

    private static CandidatePreflightGate NotEvaluated(string id) => new()
    {
        Id = id,
        Status = "not-evaluated",
        Passed = false,
        Requirement = "Required by repository-model-admission/1.0.0 before validation selection.",
        Rationale =
            "The candidate already failed development preflight, so this gate was not run and fails closed.",
    };

    private static decimal Divide(decimal numerator, decimal denominator) =>
        denominator == 0m
            ? throw new InvalidDataException("A required candidate-preflight denominator is zero.")
            : Round4(numerator / denominator);

    private static decimal Require(decimal? value) =>
        value ?? throw new InvalidDataException("A required candidate-preflight metric is not computable.");

    private static string Format(decimal value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);

    private static decimal Round4(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    private readonly record struct CandidateKey(
        string SourceDigest,
        EstimationProfile Profile,
        string BaselineId);

    private sealed record DerivedIntervals(
        decimal MeanRepositoryNormalizedWidth,
        decimal P90RepositoryNormalizedWidth,
        decimal TargetExpectedCoverage,
        decimal TargetMeanNormalizedWidth,
        decimal FamilyMaximumErrorPassRate,
        decimal FamilyOrdinaryErrorPassRate,
        decimal CategoryMismatchRate);
}
