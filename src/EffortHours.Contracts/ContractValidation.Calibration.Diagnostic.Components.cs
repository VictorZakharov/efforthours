using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateDiagnosticComponent(
        CalibrationResidualContribution component,
        int expectedRank,
        string repositoryPath,
        List<string> errors)
    {
        string path = $"{repositoryPath}.component[{component.Id}]";
        if (component.Rank != expectedRank)
        {
            errors.Add($"{path}.rank must follow component order.");
        }

        RequireText(component.Id, $"{repositoryPath}.component.id", errors);
        RequireText(component.Title, $"{path}.title", errors);
        RequireText(component.Scope, $"{path}.scope", errors);
        if (component.ReviewedRationale is not null)
        {
            RequireText(component.ReviewedRationale, $"{path}.reviewedRationale", errors);
        }

        if (component.ReviewedSizeException is not null)
        {
            RequireText(component.ReviewedSizeException, $"{path}.reviewedSizeException", errors);
        }

        if (component.CandidateLeafCount != component.CandidateLeaves.Count)
        {
            errors.Add($"{path}.candidateLeafCount does not match candidateLeaves.");
        }

        HashSet<string> candidateLeafIds = new(StringComparer.Ordinal);
        foreach (CalibrationCandidateLeafDiagnostic leaf in component.CandidateLeaves)
        {
            ValidateCandidateLeaf(leaf, component.CandidateReasons.Count, path, errors);
            if (!candidateLeafIds.Add(leaf.Id))
            {
                errors.Add($"{path}.candidateLeaves contains duplicate ID '{leaf.Id}'.");
            }
        }

        RequireUniqueText(
            component.MissingCandidateWorkItemIds,
            $"{path}.missingCandidateWorkItemIds",
            errors);
        RequireUniqueText(component.ReviewedEvidenceIds, $"{path}.reviewedEvidenceIds", errors);
        RequireUniqueText(component.CandidateEvidenceIds, $"{path}.candidateEvidenceIds", errors);
        RequireUniqueText(component.CandidateReasons, $"{path}.candidateReasons", errors);
        RequireUniqueText(
            component.ReviewedUncertaintyReasons,
            $"{path}.reviewedUncertaintyReasons",
            errors);
        RequireUniqueText(
            component.CandidateUncertaintyReasons,
            $"{path}.candidateUncertaintyReasons",
            errors);
        ValidateDiagnosticRange(component.Range, $"{path}.range", errors);
        ValidateDiagnosticShares(
            component.AbsoluteErrorShare,
            component.CumulativeAbsoluteErrorShare,
            path,
            errors);
        if (component.CandidateConfidence is < 0m or > 1m)
        {
            errors.Add($"{path}.candidateConfidence must be between zero and one when present.");
        }

        ValidateDiagnosticMapping(component, path, errors);
        ValidateCandidateLeafProjection(component, path, errors);
    }

    private static void ValidateCandidateLeaf(
        CalibrationCandidateLeafDiagnostic leaf,
        int candidateReasonCount,
        string componentPath,
        List<string> errors)
    {
        string path = $"{componentPath}.candidateLeaf[{leaf.Id}]";
        RequireText(leaf.Id, $"{componentPath}.candidateLeaf.id", errors);
        RequireText(leaf.Title, $"{path}.title", errors);
        RequireText(leaf.Scope, $"{path}.scope", errors);
        RequireDigest(leaf.EvidenceDigest, $"{path}.evidenceDigest", errors);
        ValidateRange(leaf.Hours, $"{path}.hours", errors);
        if (leaf.Confidence is < 0m or > 1m ||
            leaf.EvidenceCount < 0 ||
            leaf.ReasonIndex < 0 ||
            leaf.ReasonIndex >= candidateReasonCount ||
            leaf.AssumptionCount < 0 ||
            leaf.ExclusionCount < 0 ||
            leaf.UncertaintyReasonCount < 0)
        {
            errors.Add($"{path} contains invalid confidence, digest counts, or reason index.");
        }
    }

    private static void ValidateCandidateLeafProjection(
        CalibrationResidualContribution component,
        string path,
        List<string> errors)
    {
        EffortRange candidateLeafTotal = Sum(component.CandidateLeaves.Select(leaf => leaf.Hours));
        if (candidateLeafTotal != component.Range.Candidate)
        {
            errors.Add($"{path}.candidateLeaves do not equal the component candidate range.");
        }

        if (component.CandidateEvidenceIds.Count >
            component.CandidateLeaves.Sum(leaf => leaf.EvidenceCount))
        {
            errors.Add($"{path}.candidateEvidenceIds exceed candidate-leaf evidence counts.");
        }

        decimal? expectedConfidence = component.CandidateLeaves.Count == 0
            ? null
            : component.CandidateLeaves.Min(leaf => leaf.Confidence);
        if (component.CandidateConfidence != expectedConfidence)
        {
            errors.Add($"{path}.candidateConfidence does not equal minimum leaf confidence.");
        }
    }

    private static void ValidateDiagnosticMapping(
        CalibrationResidualContribution component,
        string path,
        List<string> errors)
    {
        bool unmatched = component.Kind == CalibrationResidualComponentKind.UnmatchedCandidateWorkItem;
        if (unmatched !=
            (component.MappingStatus == CalibrationResidualMappingStatus.UnmatchedCandidate))
        {
            errors.Add($"{path}.kind and mappingStatus are inconsistent.");
        }

        if (unmatched)
        {
            if (component.ReviewedTargetId is not null ||
                component.ReviewedRationale is not null ||
                component.CandidateLeaves.Count != 1 ||
                component.MissingCandidateWorkItemIds.Count != 0)
            {
                errors.Add($"{path} contains an invalid unmatched-candidate mapping.");
            }

            return;
        }

        RequireText(component.ReviewedTargetId, $"{path}.reviewedTargetId", errors);
        RequireText(component.ReviewedRationale, $"{path}.reviewedRationale", errors);
        if (component.MappingStatus == CalibrationResidualMappingStatus.Incomplete &&
            component.MissingCandidateWorkItemIds.Count == 0)
        {
            errors.Add($"{path} is incomplete but has no missing candidate work-item IDs.");
        }

        if (component.MappingStatus != CalibrationResidualMappingStatus.Incomplete &&
            component.MissingCandidateWorkItemIds.Count > 0)
        {
            errors.Add($"{path} has missing candidate work-item IDs but is not incomplete.");
        }
    }

    private static void ValidateDiagnosticRange(
        CalibrationRangeDiagnostic range,
        string path,
        List<string> errors)
    {
        ValidateRange(range.Reviewed, $"{path}.reviewed", errors);
        ValidateRange(range.Candidate, $"{path}.candidate", errors);
        if (range.SignedError != DiagnosticDifference(range.Candidate, range.Reviewed))
        {
            errors.Add($"{path}.signedError must equal candidate minus reviewed.");
        }

        decimal lower = range.Candidate.Expected - range.Candidate.Low;
        decimal upper = range.Candidate.High - range.Candidate.Expected;
        decimal width = range.Candidate.High - range.Candidate.Low;
        decimal reviewedWidth = range.Reviewed.High - range.Reviewed.Low;
        decimal asymmetry = decimal.Abs(upper - lower);
        if (range.ExpectedAbsoluteErrorHours != decimal.Abs(range.SignedError.Expected) ||
            range.CandidateWidthHours != width ||
            range.ReviewedWidthHours != reviewedWidth ||
            range.CandidateLowerDistanceHours != lower ||
            range.CandidateUpperDistanceHours != upper ||
            range.CandidateAbsoluteAsymmetryHours != asymmetry ||
            range.CandidateSymmetric != (lower == upper))
        {
            errors.Add($"{path} contains inconsistent error, width, or symmetry values.");
        }

        decimal expectedRelativeAsymmetry = width == 0m
            ? 0m
            : DiagnosticRound4(asymmetry / width);
        if (range.CandidateRelativeAsymmetry != expectedRelativeAsymmetry)
        {
            errors.Add($"{path}.candidateRelativeAsymmetry is inconsistent.");
        }

        ValidateDiagnosticInterval(range, path, errors);
    }

    private static void ValidateDiagnosticInterval(
        CalibrationRangeDiagnostic range,
        string path,
        List<string> errors)
    {
        CalibrationIntervalStatus expectedStatus = range.Reviewed.Expected < range.Candidate.Low
            ? CalibrationIntervalStatus.CandidateTooHigh
            : range.Reviewed.Expected > range.Candidate.High
                ? CalibrationIntervalStatus.CandidateTooLow
                : CalibrationIntervalStatus.Covered;
        decimal expectedMiss = expectedStatus switch
        {
            CalibrationIntervalStatus.CandidateTooHigh =>
                range.Candidate.Low - range.Reviewed.Expected,
            CalibrationIntervalStatus.CandidateTooLow =>
                range.Reviewed.Expected - range.Candidate.High,
            _ => 0m,
        };
        if (range.ReviewedExpectedStatus != expectedStatus || range.IntervalMissHours != expectedMiss)
        {
            errors.Add($"{path} contains an inconsistent reviewed-expected interval status.");
        }
    }
}
