using Fairbill.Contracts.V1;

namespace Fairbill.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(CalibrationCorpusReviewPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        List<string> errors = [];
        RequireVersion(packet.SchemaVersion, "calibration corpus review packet", errors);
        RequireText(packet.AuthoringVersion, "authoringVersion", errors);
        RequireText(packet.Warning, "warning", errors);
        ValidateCorpusReference(packet.SourceCorpus, "sourceCorpus", errors);
        RequireText(packet.Rubric.Id, "rubric.id", errors);
        RequireText(packet.Rubric.Version, "rubric.version", errors);

        if (packet.Records.Count == 0)
        {
            errors.Add("The calibration corpus review packet must contain at least one record.");
        }

        HashSet<string> recordIds = new(StringComparer.Ordinal);
        foreach (CalibrationCorpusReviewRecord record in packet.Records)
        {
            string recordPath = $"record[{record.SourceRecordId}]";
            RequireText(record.SourceRecordId, "record.sourceRecordId", errors);
            if (!recordIds.Add(record.SourceRecordId))
            {
                errors.Add($"Corpus review record '{record.SourceRecordId}' is duplicated.");
            }

            RequireText(record.Repository.Id, $"{recordPath}.repository.id", errors);
            RequireText(record.Repository.Name, $"{recordPath}.repository.name", errors);
            RequireDigest(record.Repository.SourceDigest, $"{recordPath}.repository.sourceDigest", errors);
            ValidateChangeCalibrationReference(
                record.Change,
                record.Repository,
                packet.Rubric.Id,
                recordPath,
                errors);
            RequireText(record.BaselineId, $"{recordPath}.baselineId", errors);

            if (packet.CandidateVisibility == CalibrationCandidateVisibility.Reference)
            {
                if (record.CandidateTotalHours is null)
                {
                    errors.Add($"{recordPath} requires candidateTotalHours in reference mode.");
                }
            }
            else if (record.CandidateTotalHours is not null)
            {
                errors.Add($"{recordPath} must hide candidateTotalHours in blind mode.");
            }

            if (record.CandidateTotalHours is not null)
            {
                ValidateRange(record.CandidateTotalHours, $"{recordPath}.candidateTotalHours", errors);
            }

            if (record.Targets.Count == 0 && record.Change is null)
            {
                errors.Add($"{recordPath}.targets must contain at least one target.");
            }

            HashSet<string> targetIds = new(StringComparer.Ordinal);
            foreach (CalibrationCorpusReviewTarget target in record.Targets)
            {
                string targetPath = $"{recordPath}.target[{target.SourceTargetId}]";
                RequireText(target.SourceTargetId, $"{recordPath}.target.sourceTargetId", errors);
                RequireText(target.Title, $"{targetPath}.title", errors);
                RequireText(target.Scope, $"{targetPath}.scope", errors);
                if (!targetIds.Add(target.SourceTargetId))
                {
                    errors.Add($"Source target '{target.SourceTargetId}' is duplicated in '{record.SourceRecordId}'.");
                }

                if (target.SourceWorkItemIds.Count == 0)
                {
                    errors.Add($"{targetPath}.sourceWorkItemIds must contain at least one ID.");
                }

                if (target.EvidenceIds.Count == 0)
                {
                    errors.Add($"{targetPath}.evidenceIds must contain at least one ID.");
                }

                RequireUniqueText(target.SourceWorkItemIds, $"{targetPath}.sourceWorkItemIds", errors);
                RequireUniqueText(target.EvidenceIds, $"{targetPath}.evidenceIds", errors);

                if (packet.CandidateVisibility == CalibrationCandidateVisibility.Reference)
                {
                    if (target.Candidate.Hours is null)
                    {
                        errors.Add($"{targetPath} requires candidate hours in reference mode.");
                    }

                    RequireText(target.Candidate.Rationale, $"{targetPath}.candidate.rationale", errors);
                }
                else if (target.Candidate.Hours is not null ||
                         target.Candidate.Rationale is not null ||
                         target.Candidate.UncertaintyReasons.Count > 0 ||
                         target.Candidate.SizeException is not null)
                {
                    errors.Add($"{targetPath} must hide prior target judgments in blind mode.");
                }

                if (target.Candidate.Hours is not null)
                {
                    ValidateRange(target.Candidate.Hours, $"{targetPath}.candidate.hours", errors);
                }

                RequireUniqueText(
                    target.Candidate.UncertaintyReasons,
                    $"{targetPath}.candidate.uncertaintyReasons",
                    errors);

                if (target.Review.Action is not null ||
                    target.Review.Hours is not null ||
                    target.Review.Rationale is not null ||
                    target.Review.UncertaintyReasons.Count > 0 ||
                    target.Review.SizeException is not null)
                {
                    errors.Add($"{targetPath}.review must remain empty in an unreviewed packet.");
                }
            }
        }

        ValidateCalibrationSubjectConsistency(
            packet.Rubric.Id,
            packet.Records.Select(record => record.Change),
            "corpus review packet",
            errors);

        RequireUniqueText(packet.Instructions, "instructions", errors);
        if (packet.Instructions.Count == 0)
        {
            errors.Add("Calibration corpus review instructions must not be empty.");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(CalibrationCorpusReviewPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        List<string> errors = [];
        RequireVersion(plan.SchemaVersion, "calibration corpus review plan", errors);
        RequireText(plan.CompilerVersion, "plan.compilerVersion", errors);
        ValidateCorpusReference(plan.SourceCorpus, "plan.sourceCorpus", errors);
        RequireText(plan.Id, "plan.id", errors);
        RequireText(plan.Version, "plan.version", errors);
        RequireText(plan.Description, "plan.description", errors);

        if (plan.Records.Count == 0)
        {
            errors.Add("The calibration corpus review plan must contain at least one record.");
        }

        HashSet<string> recordIds = new(StringComparer.Ordinal);
        foreach (CalibrationCorpusReviewPlanRecord record in plan.Records)
        {
            string recordPath = $"record[{record.SourceRecordId}]";
            RequireText(record.SourceRecordId, "record.sourceRecordId", errors);
            if (!recordIds.Add(record.SourceRecordId))
            {
                errors.Add($"Corpus review-plan record '{record.SourceRecordId}' is duplicated.");
            }

            if (record.ResultStatus == CalibrationReviewStatus.TeacherEstimate)
            {
                errors.Add($"{recordPath}.resultStatus must advance to reviewed or adjudicated.");
            }

            if (record.CompletedOn == default)
            {
                errors.Add($"{recordPath}.completedOn is required.");
            }

            RequireText(record.Notes, $"{recordPath}.notes", errors);
            if (record.Reviewers.Count == 0)
            {
                errors.Add($"{recordPath}.reviewers must contain at least one subsequent reviewer.");
            }

            HashSet<string> reviewerIds = new(StringComparer.Ordinal);
            foreach (CalibrationReviewer reviewer in record.Reviewers)
            {
                RequireText(reviewer.Id, $"{recordPath}.reviewer.id", errors);
                if (!reviewerIds.Add(reviewer.Id))
                {
                    errors.Add($"Reviewer '{reviewer.Id}' is duplicated in {recordPath}.");
                }

                if (reviewer.Role == CalibrationReviewerRole.Teacher)
                {
                    errors.Add($"{recordPath} may add only reviewer or adjudicator roles.");
                }

                if (reviewer.Kind == CalibrationReviewerKind.HostAi)
                {
                    RequireText(reviewer.ModelId, $"{recordPath}.reviewer[{reviewer.Id}].modelId", errors);
                    RequireText(
                        reviewer.ModelVersion,
                        $"{recordPath}.reviewer[{reviewer.Id}].modelVersion",
                        errors);
                }
            }

            if (record.ResultStatus == CalibrationReviewStatus.Reviewed &&
                !record.Reviewers.Any(reviewer => reviewer.Role == CalibrationReviewerRole.Reviewer))
            {
                errors.Add($"{recordPath} requires a reviewer role to produce reviewed labels.");
            }

            if (record.ResultStatus == CalibrationReviewStatus.Adjudicated &&
                !record.Reviewers.Any(reviewer => reviewer.Role == CalibrationReviewerRole.Adjudicator))
            {
                errors.Add($"{recordPath} requires an adjudicator role to produce adjudicated labels.");
            }

            HashSet<string> targetIds = new(StringComparer.Ordinal);
            foreach (CalibrationCorpusReviewTargetDecision target in record.Targets)
            {
                string targetPath = $"{recordPath}.target[{target.SourceTargetId}]";
                RequireText(target.SourceTargetId, $"{recordPath}.target.sourceTargetId", errors);
                if (!targetIds.Add(target.SourceTargetId))
                {
                    errors.Add($"Source target '{target.SourceTargetId}' is duplicated in {recordPath}.");
                }

                RequireUniqueText(
                    target.UncertaintyReasons,
                    $"{targetPath}.uncertaintyReasons",
                    errors);
                if (target.Action == CalibrationCorpusReviewAction.Accept)
                {
                    if (target.Hours is not null ||
                        target.Rationale is not null ||
                        target.UncertaintyReasons.Count > 0 ||
                        target.SizeException is not null)
                    {
                        errors.Add($"{targetPath} accepts the source target and cannot contain replacement values.");
                    }

                    continue;
                }

                if (target.Hours is null)
                {
                    errors.Add($"{targetPath}.hours is required for a replacement decision.");
                    continue;
                }

                RequireText(target.Rationale, $"{targetPath}.rationale", errors);
                ValidateReviewedRange(
                    target.Hours,
                    $"{targetPath}.hours",
                    target.SizeException,
                    $"{targetPath}.sizeException",
                    errors);
            }
        }

        return errors;
    }

}
