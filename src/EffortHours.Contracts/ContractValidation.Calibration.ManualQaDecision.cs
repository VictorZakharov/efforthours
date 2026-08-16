using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(ManualQaDecisionCompilerPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        List<string> errors = [];
        RequireVersion(policy.SchemaVersion, "manual-QA decision policy", errors);
        RequireText(policy.PolicyVersion, "policyVersion", errors);
        RequireText(policy.Id, "id", errors);
        RequireText(policy.CompilerVersion, "compilerVersion", errors);
        RequireText(policy.AuthoringVersion, "authoringVersion", errors);
        RequireText(policy.LicenseExpression, "licenseExpression", errors);
        RequireText(policy.Maturity, "maturity", errors);
        RequireText(policy.PlanId, "planId", errors);
        RequireText(policy.PlanVersion, "planVersion", errors);
        RequireText(policy.PlanDescription, "planDescription", errors);
        ValidateCorpusReference(policy.SourceCorpus, "sourceCorpus", errors);
        ValidatePolicyReference(policy.ReviewPolicy, "reviewPolicy", errors);
        ValidateManualQaManifestReference(policy.ReviewManifest, "reviewManifest", errors);
        ValidateRubric(policy.ReviewRubric, "reviewRubric", errors);
        ValidateManualQaOutputCorpus(policy.OutputCorpus, "outputCorpus", errors);
        RequireText(policy.BaselineId, "baselineId", errors);
        RequireText(policy.SourceWorkItemLineageVersion, "sourceWorkItemLineageVersion", errors);
        RequireUniqueText(policy.RequiredDecisionPractices, "requiredDecisionPractices", errors);
        RequireUniqueText(policy.Limitations, "limitations", errors);

        if (policy.Partition != CalibrationPartition.Development)
        {
            errors.Add("Manual-QA decision policy partition must be development.");
        }

        if (policy.Profile != EstimationProfile.Implementation)
        {
            errors.Add("Manual-QA decision policy profile must be implementation.");
        }

        if (policy.ReplacedCategory != EffortCategory.ManualValidationDebuggingAndHardening)
        {
            errors.Add("Manual-QA decision policy may replace only the manual-validation category.");
        }

        if (policy.ExpectedRecordCount <= 0 ||
            policy.ExpectedDecisionCount <= 0 ||
            policy.ExpectedRemovedTargetCount <= 0 ||
            policy.ExpectedPreservedTargetCount <= 0 ||
            policy.ExpectedOutputTargetCount <= 0)
        {
            errors.Add("Manual-QA decision policy expected counts must all be positive.");
        }

        if (policy.ExpectedOutputTargetCount !=
            policy.ExpectedPreservedTargetCount + policy.ExpectedDecisionCount)
        {
            errors.Add("expectedOutputTargetCount must equal preserved targets plus decisions.");
        }

        if (policy.RequiredDecisionPractices.Count == 0)
        {
            errors.Add("requiredDecisionPractices must not be empty.");
        }

        if (policy.Limitations.Count == 0)
        {
            errors.Add("limitations must not be empty.");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(ManualQaDecisionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        List<string> errors = [];
        RequireVersion(plan.SchemaVersion, "manual-QA decision plan", errors);
        RequireText(plan.PlanVersion, "planVersion", errors);
        RequireText(plan.CompilerVersion, "compilerVersion", errors);
        RequireText(plan.Id, "id", errors);
        RequireText(plan.Version, "version", errors);
        RequireText(plan.Description, "description", errors);
        ValidatePolicyReference(plan.Policy, "policy", errors);
        ValidateCorpusReference(plan.SourceCorpus, "sourceCorpus", errors);
        ValidateManualQaManifestReference(plan.ReviewManifest, "reviewManifest", errors);
        ValidateRubric(plan.ReviewRubric, "reviewRubric", errors);
        ValidateManualQaOutputCorpus(plan.OutputCorpus, "outputCorpus", errors);
        RequireText(plan.BaselineId, "baselineId", errors);
        RequireUniqueText(plan.Instructions, "instructions", errors);

        if (plan.Partition != CalibrationPartition.Development)
        {
            errors.Add("Manual-QA decision plan partition must be development.");
        }

        if (plan.Profile != EstimationProfile.Implementation)
        {
            errors.Add("Manual-QA decision plan profile must be implementation.");
        }

        if (plan.RecordCount <= 0 || plan.RecordCount != plan.Records.Count)
        {
            errors.Add("recordCount must be positive and equal records.Count.");
        }

        if (plan.DecisionCount <= 0 ||
            plan.DecisionCount != plan.Records.Sum(record => record.Decisions.Count))
        {
            errors.Add("decisionCount must be positive and equal the decision sum.");
        }

        if (plan.Instructions.Count == 0)
        {
            errors.Add("Manual-QA decision plan instructions must not be empty.");
        }

        HashSet<string> recordIds = new(StringComparer.Ordinal);
        foreach (ManualQaDecisionPlanRecord record in plan.Records)
        {
            ValidateManualQaDecisionRecord(plan.Status, record, recordIds, errors);
        }

        return errors;
    }

    private static void ValidateManualQaDecisionRecord(
        ManualQaDecisionPlanStatus status,
        ManualQaDecisionPlanRecord record,
        HashSet<string> recordIds,
        List<string> errors)
    {
        string path = $"record[{record.SourceRecordId}]";
        RequireText(record.SourceRecordId, "record.sourceRecordId", errors);
        RequireDigest(record.PacketDigest, $"{path}.packetDigest", errors);
        RequireDigest(record.LineageDigest, $"{path}.lineageDigest", errors);
        if (!recordIds.Add(record.SourceRecordId))
        {
            errors.Add($"Manual-QA decision record '{record.SourceRecordId}' is duplicated.");
        }

        if (record.Decisions.Count == 0)
        {
            errors.Add($"{path}.decisions must not be empty.");
        }

        if (status == ManualQaDecisionPlanStatus.Unreviewed)
        {
            if (record.Review is not null)
            {
                errors.Add($"{path}.review must remain null in an unreviewed template.");
            }
        }
        else if (record.Review is null)
        {
            errors.Add($"{path}.review is required in a completed plan.");
        }
        else
        {
            ValidateCalibrationReview(record.Review, path, errors);
            RequireText(record.Review.Notes, $"{path}.review.notes", errors);
            if (record.Review.Status != CalibrationReviewStatus.TeacherEstimate)
            {
                errors.Add($"{path}.review.status must remain teacher-estimate weak supervision.");
            }
        }

        HashSet<string> targetIds = new(StringComparer.Ordinal);
        foreach (ManualQaDecision decision in record.Decisions)
        {
            ValidateManualQaDecision(status, path, decision, targetIds, errors);
        }

        if (status == ManualQaDecisionPlanStatus.Completed &&
            targetIds.Count == record.Decisions.Count)
        {
            ValidateDuplicateOwners(path, record.Decisions, errors);
        }
    }

    private static void ValidateManualQaDecision(
        ManualQaDecisionPlanStatus status,
        string recordPath,
        ManualQaDecision decision,
        HashSet<string> targetIds,
        List<string> errors)
    {
        string path = $"{recordPath}.decision[{decision.SourceTargetId}]";
        RequireText(decision.SourceTargetId, $"{recordPath}.decision.sourceTargetId", errors);
        RequireDigest(decision.SourceLineageDigest, $"{path}.sourceLineageDigest", errors);
        RequireText(decision.OverlapGroupId, $"{path}.overlapGroupId", errors);
        RequireUniqueText(decision.EvidenceIds, $"{path}.evidenceIds", errors);
        RequireUniqueText(decision.UncertaintyReasons, $"{path}.uncertaintyReasons", errors);
        if (!targetIds.Add(decision.SourceTargetId))
        {
            errors.Add($"Manual-QA decision target '{decision.SourceTargetId}' is duplicated.");
        }

        if (status == ManualQaDecisionPlanStatus.Unreviewed)
        {
            if (decision.Disposition is not null ||
                decision.Hours is not null ||
                decision.Rationale is not null ||
                decision.EvidenceIds.Count > 0 ||
                decision.UncertaintyReasons.Count > 0 ||
                decision.OverlapAllocation is not null ||
                decision.DuplicateOfSourceTargetId is not null ||
                decision.SizeException is not null)
            {
                errors.Add($"{path} must contain no answer fields in an unreviewed template.");
            }

            return;
        }

        if (decision.Disposition is null)
        {
            errors.Add($"{path}.disposition is required in a completed plan.");
            return;
        }

        if (decision.Hours is null)
        {
            errors.Add($"{path}.hours is required in a completed plan.");
            return;
        }

        RequireText(decision.Rationale, $"{path}.rationale", errors);
        RequireText(decision.OverlapAllocation, $"{path}.overlapAllocation", errors);
        if (decision.EvidenceIds.Count == 0)
        {
            errors.Add($"{path}.evidenceIds must contain at least one reviewed evidence ID.");
        }

        ValidateReviewedRange(
            decision.Hours,
            $"{path}.hours",
            decision.SizeException,
            $"{path}.sizeException",
            errors);

        if (decision.Disposition == ManualQaDecisionDisposition.Estimate)
        {
            if (decision.Hours.Expected <= 0m)
            {
                errors.Add($"{path}.hours.expected must be positive for an estimate.");
            }

            if (decision.UncertaintyReasons.Count == 0)
            {
                errors.Add($"{path}.uncertaintyReasons must describe the reviewed range.");
            }

            if (decision.DuplicateOfSourceTargetId is not null)
            {
                errors.Add($"{path}.duplicateOfSourceTargetId applies only to duplicate decisions.");
            }

            if (decision.Hours.Expected is >= 0.5m and <= 8m &&
                decision.SizeException is not null)
            {
                errors.Add($"{path}.sizeException must be null inside the normal review band.");
            }

            return;
        }

        if (decision.Hours.Low != 0m ||
            decision.Hours.Expected != 0m ||
            decision.Hours.High != 0m)
        {
            errors.Add($"{path}.hours must be exactly 0/0/0 for an exclusion or duplicate.");
        }

        if (decision.UncertaintyReasons.Count > 0)
        {
            errors.Add($"{path}.uncertaintyReasons must be empty for a zero decision.");
        }

        if (decision.Disposition == ManualQaDecisionDisposition.Exclude &&
            decision.DuplicateOfSourceTargetId is not null)
        {
            errors.Add($"{path}.duplicateOfSourceTargetId must be null for an exclusion.");
        }

        if (decision.Disposition == ManualQaDecisionDisposition.Duplicate)
        {
            RequireText(
                decision.DuplicateOfSourceTargetId,
                $"{path}.duplicateOfSourceTargetId",
                errors);
        }
    }

    private static void ValidateDuplicateOwners(
        string recordPath,
        IReadOnlyList<ManualQaDecision> decisions,
        List<string> errors)
    {
        Dictionary<string, ManualQaDecision> byId = decisions.ToDictionary(
            decision => decision.SourceTargetId,
            StringComparer.Ordinal);
        foreach (ManualQaDecision duplicate in decisions.Where(decision =>
                     decision.Disposition == ManualQaDecisionDisposition.Duplicate))
        {
            string ownerId = duplicate.DuplicateOfSourceTargetId!;
            string path = $"{recordPath}.decision[{duplicate.SourceTargetId}]";
            if (string.Equals(ownerId, duplicate.SourceTargetId, StringComparison.Ordinal))
            {
                errors.Add($"{path} cannot duplicate itself.");
            }
            else if (!byId.TryGetValue(ownerId, out ManualQaDecision? owner))
            {
                errors.Add($"{path} references unknown duplicate owner '{ownerId}'.");
            }
            else if (owner.Disposition != ManualQaDecisionDisposition.Estimate)
            {
                errors.Add($"{path} duplicate owner '{ownerId}' must be an estimated target.");
            }
        }
    }

    private static void ValidateManualQaManifestReference(
        ManualQaReviewManifestReference reference,
        string path,
        List<string> errors)
    {
        RequireText(reference.Version, $"{path}.version", errors);
        RequireDigest(reference.Digest, $"{path}.digest", errors);
    }

    private static void ValidateManualQaOutputCorpus(
        ManualQaDecisionOutputCorpus output,
        string path,
        List<string> errors)
    {
        RequireText(output.Id, $"{path}.id", errors);
        RequireText(output.Version, $"{path}.version", errors);
        RequireText(output.Description, $"{path}.description", errors);
        ValidateRubric(output.Rubric, $"{path}.rubric", errors);
    }
}
