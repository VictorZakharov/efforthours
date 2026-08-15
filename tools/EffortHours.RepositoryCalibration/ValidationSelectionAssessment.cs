using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class ValidationSelectionAssessment
{
    public static IReadOnlyList<CandidatePreflightGate> BuildBoundaryGates(
        VerifiedValidationSelection verified)
    {
        int targetCount = verified.Corpus.Records.Sum(record => record.Targets.Count);
        int zeroCount = verified.Corpus.Records.Sum(record =>
            record.Targets.Count(target => target.Hours.Expected == 0m));
        return
        [
            Passed(
                "candidate-and-selection-freeze",
                "The exact finite candidate set, artifact chain, and validation selection rule must match the pre-opening manifest.",
                $"manifest={verified.CandidateManifestDigest}; candidates=1; " +
                $"primary=repository-total-expected-wape; tolerance={Format(verified.WapeTieTolerance)}"),
            Passed(
                "validation-family-and-review-boundary",
                "Validation must contain the frozen 3-stratum x 3-size matrix, complete blind teacher labels, immutable public lineage, and no contamination.",
                $"families={verified.Corpus.Records.Count}; targets={targetCount}; " +
                $"zero-exclusions={zeroCount}; opening={verified.OpeningDigest}; corpus={verified.CorpusDigest}"),
            Passed(
                "seed-evidence-and-estimate-lineage",
                "Every validation record must reproduce the opening's exact seed estimate and evidence identities before candidate projection.",
                $"matched-seed-inputs={verified.Inputs.Count}/{verified.Corpus.Records.Count}"),
            Passed(
                "one-shot-output-and-test-seal",
                "All validation candidate/evaluation outputs must be fresh and the test partition must remain inaccessible until a committed selection authorizes it.",
                "fresh-validation-outputs=verified; test-source=not-accessed; test-labels=not-authored; test-candidate-output=not-generated"),
        ];
    }

    public static ValidationSelectionBaseline BuildBaseline(
        CalibrationEvaluationReport evaluation,
        string evaluationDigest)
    {
        decimal[] widths = [.. evaluation.Repositories
            .Where(repository => repository.ReviewedTotal.Expected > 0m)
            .Select(repository =>
                (repository.CandidateTotal.High - repository.CandidateTotal.Low) /
                repository.ReviewedTotal.Expected)
            .Order()];
        if (widths.Length == 0)
        {
            throw new InvalidDataException("Seed validation widths are not computable.");
        }

        return new ValidationSelectionBaseline
        {
            EstimatorVersion = evaluation.CandidateEstimatorVersions.Single(),
            Evaluation = Artifact(
                "efforthours-public-readiness-validation-seed",
                evaluation.EvaluatorVersion,
                evaluationDigest),
            RepositoryExpectedWape = Require(
                evaluation.RepositoryTotals.Expected.WeightedAbsolutePercentageError),
            AbsoluteAggregateBias = decimal.Abs(Require(
                evaluation.RepositoryTotals.Expected.AggregateBiasRate)),
            MedianRepositoryAbsoluteErrorHours =
                evaluation.RepositoryTotals.Expected.MedianAbsoluteErrorHours,
            RepositoryExpectedCoverage = Require(
                evaluation.RepositoryTotals.Interval.ReviewedExpectedCoverage),
            MeanRepositoryNormalizedWidth = Round4(widths.Average()),
            P90RepositoryNormalizedWidth =
                Round4(widths[(int)Math.Ceiling(widths.Length * 0.90m) - 1]),
        };
    }

    public static ValidationSelectionArtifact Artifact(
        string id,
        string version,
        string digest) => new()
        {
            Id = id,
            Version = version,
            Digest = digest,
        };

    private static CandidatePreflightGate Passed(
        string id,
        string requirement,
        string observed) => new()
        {
            Id = id,
            Status = "passed",
            Passed = true,
            Requirement = requirement,
            Observed = observed,
            Rationale = "The one-shot validation evaluator verified this boundary before projection.",
        };

    private static decimal Require(decimal? value) =>
        value ?? throw new InvalidDataException("A required validation metric is not computable.");

    private static decimal Round4(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    private static string Format(decimal value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);
}
