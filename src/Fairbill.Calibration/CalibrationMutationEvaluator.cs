using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Calibration;

public static class CalibrationMutationEvaluator
{
    public const string EvaluatorVersion = "calibration-mutation-evaluator/0.1.0";
    public const string MetricVersion = "calibration-mutation-metrics/1.0.0";

    public static CalibrationMutationReport Evaluate(
        CalibrationMutationSuite suite,
        IReadOnlyList<EstimateReport> candidates)
    {
        ArgumentNullException.ThrowIfNull(suite);
        ArgumentNullException.ThrowIfNull(candidates);

        List<string> errors = [.. ContractValidation.Validate(suite)];
        if (!string.Equals(suite.MetricVersion, MetricVersion, StringComparison.Ordinal))
        {
            errors.Add(
                $"Mutation suite uses metric version '{suite.MetricVersion}', but this build supports " +
                $"'{MetricVersion}'.");
        }

        Dictionary<CandidateKey, EstimateReport> candidateIndex = [];
        foreach (EstimateReport candidate in candidates)
        {
            errors.AddRange(ContractValidation.Validate(candidate).Select(error =>
                $"Candidate '{candidate.Repository.Name}/{candidate.Profile}': {error}"));
            if (string.IsNullOrWhiteSpace(candidate.Repository.SourceDigest))
            {
                errors.Add(
                    $"Candidate '{candidate.Repository.Name}/{candidate.Profile}' requires " +
                    "repository.sourceDigest.");
                continue;
            }

            CandidateKey key = new(
                candidate.Repository.SourceDigest,
                candidate.Profile,
                candidate.Baseline.Id);
            if (!candidateIndex.TryAdd(key, candidate))
            {
                errors.Add(
                    $"More than one candidate matches digest '{key.SourceDigest}', profile " +
                    $"'{key.Profile}', and baseline '{key.BaselineId}'.");
            }
        }

        Dictionary<string, EstimateReport> matched = new(StringComparer.Ordinal);
        foreach (CalibrationMutationCase mutationCase in suite.Cases)
        {
            CandidateKey key = new(
                mutationCase.SourceDigest,
                mutationCase.Profile,
                mutationCase.BaselineId);
            if (!candidateIndex.TryGetValue(key, out EstimateReport? candidate))
            {
                errors.Add(
                    $"No candidate matches mutation case '{mutationCase.Id}' with digest " +
                    $"'{key.SourceDigest}', profile '{key.Profile}', and baseline '{key.BaselineId}'.");
                continue;
            }

            matched[mutationCase.Id] = candidate;
        }

        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }

        List<CalibrationMutationAssertionResult> results = [];
        foreach (CalibrationMutationAssertion assertion in suite.Assertions.OrderBy(
                     item => item.Id,
                     StringComparer.Ordinal))
        {
            decimal subject = ReadPoint(matched[assertion.SubjectCaseId], assertion);
            decimal reference = ReadPoint(matched[assertion.ReferenceCaseId], assertion);
            decimal difference = subject - reference;
            bool passed =
                (assertion.MinimumDifferenceHours is null ||
                 difference >= assertion.MinimumDifferenceHours) &&
                (assertion.MaximumDifferenceHours is null ||
                 difference <= assertion.MaximumDifferenceHours);
            results.Add(new CalibrationMutationAssertionResult
            {
                Id = assertion.Id,
                Family = assertion.Family,
                SubjectCaseId = assertion.SubjectCaseId,
                ReferenceCaseId = assertion.ReferenceCaseId,
                Point = assertion.Point,
                Scope = assertion.Scope,
                Category = assertion.Category,
                SubjectHours = subject,
                ReferenceHours = reference,
                DifferenceHours = difference,
                MinimumDifferenceHours = assertion.MinimumDifferenceHours,
                MaximumDifferenceHours = assertion.MaximumDifferenceHours,
                Passed = passed,
                Rationale = assertion.Rationale,
            });
        }

        int passedCount = results.Count(result => result.Passed);
        HashSet<EstimateReport> matchedCandidates = [.. matched.Values];
        CalibrationMutationReport report = new()
        {
            EvaluatorVersion = EvaluatorVersion,
            MetricVersion = MetricVersion,
            SuiteId = suite.Id,
            SuiteVersion = suite.Version,
            CaseCount = suite.Cases.Count,
            AssertionCount = results.Count,
            PassedCount = passedCount,
            FailedCount = results.Count - passedCount,
            AllPassed = passedCount == results.Count,
            IgnoredCandidateCount = candidates.Count - matchedCandidates.Count,
            CandidateEstimatorVersions = [.. matched.Values
                .Select(candidate => candidate.EstimatorVersion)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            Cases = [.. suite.Cases
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => CreateCaseResult(item.Id, matched[item.Id]))],
            Assertions = results,
        };

        IReadOnlyList<string> reportErrors = ContractValidation.Validate(report);
        if (reportErrors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Calibration mutation evaluation produced an invalid report: " +
                string.Join("; ", reportErrors));
        }

        return report;
    }

    private static CalibrationMutationCaseResult CreateCaseResult(
        string caseId,
        EstimateReport candidate) => new()
        {
            CaseId = caseId,
            CandidateEstimatorVersion = candidate.EstimatorVersion,
            CandidateEstimateDigest = CalibrationDigest.Compute(candidate),
            TotalHours = candidate.TotalEffort,
        };

    private static decimal ReadPoint(
        EstimateReport report,
        CalibrationMutationAssertion assertion)
    {
        EffortRange range = assertion.Scope == CalibrationMutationScope.RepositoryTotal
            ? report.TotalEffort
            : report.Categories
                .FirstOrDefault(category => category.Category == assertion.Category)?.Hours
                ?? new EffortRange { Low = 0m, Expected = 0m, High = 0m };
        return assertion.Point switch
        {
            CalibrationMutationPoint.Low => range.Low,
            CalibrationMutationPoint.Expected => range.Expected,
            CalibrationMutationPoint.High => range.High,
            _ => throw new ArgumentOutOfRangeException(nameof(assertion)),
        };
    }

    private readonly record struct CandidateKey(
        string SourceDigest,
        EstimationProfile Profile,
        string BaselineId);
}
