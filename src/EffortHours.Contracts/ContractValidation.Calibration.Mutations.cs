using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(CalibrationMutationSuite suite)
    {
        ArgumentNullException.ThrowIfNull(suite);

        List<string> errors = [];
        RequireVersion(suite.SchemaVersion, "calibration mutation suite", errors);
        RequireText(suite.MetricVersion, "metricVersion", errors);
        RequireText(suite.Id, "suite.id", errors);
        RequireText(suite.Version, "suite.version", errors);
        RequireText(suite.Description, "suite.description", errors);

        if (suite.Cases.Count < 2)
        {
            errors.Add("A calibration mutation suite must contain at least two cases.");
        }

        HashSet<string> caseIds = new(StringComparer.Ordinal);
        HashSet<(string SourceDigest, EstimationProfile Profile, string BaselineId)> caseKeys = [];
        foreach (CalibrationMutationCase mutationCase in suite.Cases)
        {
            string path = $"case[{mutationCase.Id}]";
            RequireText(mutationCase.Id, "case.id", errors);
            RequireText(mutationCase.Description, $"{path}.description", errors);
            RequireDigest(mutationCase.SourceDigest, $"{path}.sourceDigest", errors);
            RequireText(mutationCase.BaselineId, $"{path}.baselineId", errors);
            if (!caseIds.Add(mutationCase.Id))
            {
                errors.Add($"Mutation case '{mutationCase.Id}' is duplicated.");
            }

            if (!caseKeys.Add((mutationCase.SourceDigest, mutationCase.Profile, mutationCase.BaselineId)))
            {
                errors.Add($"More than one mutation case selects the candidate for '{mutationCase.SourceDigest}'.");
            }
        }

        if (suite.Assertions.Count == 0)
        {
            errors.Add("A calibration mutation suite must contain at least one assertion.");
        }

        HashSet<string> assertionIds = new(StringComparer.Ordinal);
        foreach (CalibrationMutationAssertion assertion in suite.Assertions)
        {
            string path = $"assertion[{assertion.Id}]";
            RequireText(assertion.Id, "assertion.id", errors);
            RequireText(assertion.Family, $"{path}.family", errors);
            RequireText(assertion.SubjectCaseId, $"{path}.subjectCaseId", errors);
            RequireText(assertion.ReferenceCaseId, $"{path}.referenceCaseId", errors);
            RequireText(assertion.Rationale, $"{path}.rationale", errors);
            if (!assertionIds.Add(assertion.Id))
            {
                errors.Add($"Mutation assertion '{assertion.Id}' is duplicated.");
            }

            if (!caseIds.Contains(assertion.SubjectCaseId))
            {
                errors.Add($"{path} references unknown subject case '{assertion.SubjectCaseId}'.");
            }

            if (!caseIds.Contains(assertion.ReferenceCaseId))
            {
                errors.Add($"{path} references unknown reference case '{assertion.ReferenceCaseId}'.");
            }

            if (string.Equals(assertion.SubjectCaseId, assertion.ReferenceCaseId, StringComparison.Ordinal))
            {
                errors.Add($"{path} must compare two different cases.");
            }

            ValidateMutationScope(assertion.Scope, assertion.Category, path, errors);
            if (assertion.MinimumDifferenceHours is null && assertion.MaximumDifferenceHours is null)
            {
                errors.Add($"{path} requires at least one difference bound.");
            }

            if (assertion.MinimumDifferenceHours is not null &&
                assertion.MaximumDifferenceHours is not null &&
                assertion.MinimumDifferenceHours > assertion.MaximumDifferenceHours)
            {
                errors.Add($"{path}.minimumDifferenceHours cannot exceed maximumDifferenceHours.");
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(CalibrationMutationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "calibration mutation report", errors);
        RequireText(report.EvaluatorVersion, "evaluatorVersion", errors);
        RequireText(report.MetricVersion, "metricVersion", errors);
        RequireText(report.SuiteId, "suiteId", errors);
        RequireText(report.SuiteVersion, "suiteVersion", errors);
        RequireUniqueText(report.CandidateEstimatorVersions, "candidateEstimatorVersions", errors);
        if (report.CandidateEstimatorVersions.Count == 0)
        {
            errors.Add("A mutation report requires at least one candidate estimator version.");
        }

        if (report.CaseCount != report.Cases.Count || report.CaseCount < 2)
        {
            errors.Add("Mutation report caseCount must equal the case result count and be at least two.");
        }

        HashSet<string> caseIds = new(StringComparer.Ordinal);
        foreach (CalibrationMutationCaseResult mutationCase in report.Cases)
        {
            string path = $"case[{mutationCase.CaseId}]";
            RequireText(mutationCase.CaseId, "case.caseId", errors);
            RequireText(mutationCase.CandidateEstimatorVersion, $"{path}.candidateEstimatorVersion", errors);
            RequireDigest(mutationCase.CandidateEstimateDigest, $"{path}.candidateEstimateDigest", errors);
            ValidateRange(mutationCase.TotalHours, $"{path}.totalHours", errors);
            if (!caseIds.Add(mutationCase.CaseId))
            {
                errors.Add($"Mutation case result '{mutationCase.CaseId}' is duplicated.");
            }
        }

        if (report.AssertionCount != report.Assertions.Count || report.AssertionCount < 1)
        {
            errors.Add("Mutation report assertionCount must equal the assertion result count and be positive.");
        }

        HashSet<string> assertionIds = new(StringComparer.Ordinal);
        foreach (CalibrationMutationAssertionResult assertion in report.Assertions)
        {
            string path = $"assertion[{assertion.Id}]";
            RequireText(assertion.Id, "assertion.id", errors);
            RequireText(assertion.Family, $"{path}.family", errors);
            RequireText(assertion.SubjectCaseId, $"{path}.subjectCaseId", errors);
            RequireText(assertion.ReferenceCaseId, $"{path}.referenceCaseId", errors);
            RequireText(assertion.Rationale, $"{path}.rationale", errors);
            if (!assertionIds.Add(assertion.Id))
            {
                errors.Add($"Mutation assertion result '{assertion.Id}' is duplicated.");
            }

            if (!caseIds.Contains(assertion.SubjectCaseId) || !caseIds.Contains(assertion.ReferenceCaseId))
            {
                errors.Add($"{path} references a case not present in the report.");
            }

            ValidateMutationScope(assertion.Scope, assertion.Category, path, errors);
            if (assertion.MinimumDifferenceHours is null && assertion.MaximumDifferenceHours is null)
            {
                errors.Add($"{path} requires at least one difference bound.");
            }

            if (assertion.MinimumDifferenceHours is not null &&
                assertion.MaximumDifferenceHours is not null &&
                assertion.MinimumDifferenceHours > assertion.MaximumDifferenceHours)
            {
                errors.Add($"{path}.minimumDifferenceHours cannot exceed maximumDifferenceHours.");
            }

            if (assertion.SubjectHours < 0m || assertion.ReferenceHours < 0m)
            {
                errors.Add($"{path} contains negative compared effort.");
            }

            decimal difference = assertion.SubjectHours - assertion.ReferenceHours;
            if (assertion.DifferenceHours != difference)
            {
                errors.Add($"{path}.differenceHours does not equal subjectHours minus referenceHours.");
            }

            bool expectedPass =
                (assertion.MinimumDifferenceHours is null || difference >= assertion.MinimumDifferenceHours) &&
                (assertion.MaximumDifferenceHours is null || difference <= assertion.MaximumDifferenceHours);
            if (assertion.Passed != expectedPass)
            {
                errors.Add($"{path}.passed does not agree with its difference bounds.");
            }
        }

        int passed = report.Assertions.Count(assertion => assertion.Passed);
        int failed = report.Assertions.Count - passed;
        if (report.PassedCount != passed ||
            report.FailedCount != failed ||
            report.AllPassed != (failed == 0) ||
            report.IgnoredCandidateCount < 0)
        {
            errors.Add("Mutation report pass/fail or ignored-candidate summary is inconsistent.");
        }

        return errors;
    }

}
