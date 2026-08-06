using Fairbill.Contracts.V1;

namespace Fairbill.Contracts;

public static partial class ContractValidation
{
    private const string ChangeCalibrationRubricId = "change-ehe-work-item";

    private static void ValidateChangeCalibrationReference(
        ChangeCalibrationReference? change,
        CalibrationRepositoryReference repository,
        string rubricId,
        string path,
        List<string> errors)
    {
        if (change is null)
        {
            if (string.Equals(rubricId, ChangeCalibrationRubricId, StringComparison.Ordinal))
            {
                errors.Add($"{path}.change is required by rubric '{ChangeCalibrationRubricId}'.");
            }

            return;
        }

        if (!string.Equals(rubricId, ChangeCalibrationRubricId, StringComparison.Ordinal))
        {
            errors.Add(
                $"{path}.change requires rubric '{ChangeCalibrationRubricId}', not '{rubricId}'.");
        }

        RequireText(change.Id, $"{path}.change.id", errors);
        RequireText(change.BaseObjectId, $"{path}.change.baseObjectId", errors);
        RequireText(change.HeadObjectId, $"{path}.change.headObjectId", errors);
        RequireDigest(change.BaseEvidenceDigest, $"{path}.change.baseEvidenceDigest", errors);
        RequireDigest(change.HeadEvidenceDigest, $"{path}.change.headEvidenceDigest", errors);
        RequireDigest(change.FinalDeltaDigest, $"{path}.change.finalDeltaDigest", errors);
        RequireUniqueText(change.CoverageTags, $"{path}.change.coverageTags", errors);
        if (change.CoverageTags.Count == 0)
        {
            errors.Add($"{path}.change.coverageTags must contain at least one review stratum.");
        }

        foreach (string tag in change.CoverageTags)
        {
            if (!IsKebabCaseTag(tag))
            {
                errors.Add(
                    $"{path}.change.coverageTags value '{tag}' must be lowercase kebab-case.");
            }
        }

        if (!string.Equals(
                change.FinalDeltaDigest,
                repository.SourceDigest,
                StringComparison.Ordinal))
        {
            errors.Add(
                $"{path}.change.finalDeltaDigest must equal repository.sourceDigest.");
        }
    }

    private static void ValidateCalibrationSubjectConsistency(
        string rubricId,
        IEnumerable<ChangeCalibrationReference?> references,
        string path,
        List<string> errors)
    {
        bool[] kinds = [.. references.Select(reference => reference is not null).Distinct()];
        if (kinds.Length > 1)
        {
            errors.Add($"{path} cannot mix repository and Change EHE records.");
        }

        bool changeRubric = string.Equals(
            rubricId,
            ChangeCalibrationRubricId,
            StringComparison.Ordinal);
        if (kinds.Length == 1 && kinds[0] != changeRubric)
        {
            errors.Add(
                $"{path} rubric '{rubricId}' does not agree with its calibration subject.");
        }
    }

    private static bool IsKebabCaseTag(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value[0] == '-' || value[^1] == '-')
        {
            return false;
        }

        bool previousHyphen = false;
        foreach (char character in value)
        {
            if (character == '-')
            {
                if (previousHyphen)
                {
                    return false;
                }

                previousHyphen = true;
                continue;
            }

            if (!char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character))
            {
                return false;
            }

            previousHyphen = false;
        }

        return true;
    }
}
