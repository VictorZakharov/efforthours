using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class ManualQaDecisionCompiler
{
    public const string CompilerVersion = ManualQaDecisionVersions.CompilerV1;

    public static CalibrationCorpus Compile(
        CalibrationCorpus sourceCorpus,
        ManualQaReviewPolicy reviewPolicy,
        string reviewPolicyDigest,
        ManualQaReviewManifest reviewManifest,
        IReadOnlyList<ManualQaReviewPacket> packets,
        ManualQaDecisionCompilerPolicy compilerPolicy,
        string compilerPolicyDigest,
        ManualQaDecisionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(compilerPolicyDigest);
        ManualQaDecisionBoundaryContext boundary = ManualQaDecisionBoundary.Validate(
            sourceCorpus,
            reviewPolicy,
            reviewPolicyDigest,
            reviewManifest,
            packets,
            compilerPolicy);

        List<string> errors = [.. ContractValidation.Validate(plan)];
        if (plan.Status != ManualQaDecisionPlanStatus.Completed)
        {
            errors.Add("Manual-QA decision compiler accepts completed plans only.");
        }

        ManualQaDecisionPlan template = ManualQaDecisionAuthoring.CreateTemplate(
            sourceCorpus,
            reviewPolicy,
            reviewPolicyDigest,
            reviewManifest,
            packets,
            compilerPolicy,
            compilerPolicyDigest);
        ValidateStaticPlanBoundary(template, plan, boundary, errors);
        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }

        string planDigest = CalibrationDigest.Compute(NormalizePlan(plan));
        Dictionary<string, ManualQaDecisionPlanRecord> plannedRecords = plan.Records.ToDictionary(
            record => record.SourceRecordId,
            StringComparer.Ordinal);
        CalibrationRecord[] records =
        [
            .. sourceCorpus.Records.OrderBy(record => record.Id, StringComparer.Ordinal)
                .Select(source => CompileRecord(
                    source,
                    plannedRecords[source.Id],
                    boundary.Packets[source.Id],
                    plan,
                    planDigest)),
        ];
        CalibrationCorpus output = new()
        {
            Id = compilerPolicy.OutputCorpus.Id,
            Version = compilerPolicy.OutputCorpus.Version,
            Description = compilerPolicy.OutputCorpus.Description,
            Rubric = compilerPolicy.OutputCorpus.Rubric,
            Records = records,
        };

        int outputTargetCount = records.Sum(record => record.Targets.Count);
        if (outputTargetCount != compilerPolicy.ExpectedOutputTargetCount)
        {
            errors.Add(
                $"Compiled corpus has {outputTargetCount} targets; policy requires " +
                $"{compilerPolicy.ExpectedOutputTargetCount}.");
        }

        errors.AddRange(ContractValidation.Validate(output).Select(error =>
            $"Compiled manual-QA corpus: {error}"));
        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }

        return output;
    }

    private static ManualQaDecisionPlan NormalizePlan(ManualQaDecisionPlan plan) => plan with
    {
        Records =
        [
            .. plan.Records.OrderBy(record => record.SourceRecordId, StringComparer.Ordinal)
                .Select(record => record with
                {
                    Review = record.Review is null
                        ? null
                        : record.Review with
                        {
                            Reviewers =
                            [
                                .. record.Review.Reviewers.OrderBy(reviewer => reviewer.Role)
                                    .ThenBy(reviewer => reviewer.Id, StringComparer.Ordinal),
                            ],
                        },
                    Decisions =
                    [
                        .. record.Decisions.OrderBy(
                                decision => decision.SourceTargetId,
                                StringComparer.Ordinal)
                            .Select(decision => decision with
                            {
                                EvidenceIds =
                                [
                                    .. decision.EvidenceIds.Order(StringComparer.Ordinal),
                                ],
                                UncertaintyReasons =
                                [
                                    .. decision.UncertaintyReasons.Order(StringComparer.Ordinal),
                                ],
                            }),
                    ],
                }),
        ],
    };

    private static void ValidateStaticPlanBoundary(
        ManualQaDecisionPlan template,
        ManualQaDecisionPlan plan,
        ManualQaDecisionBoundaryContext boundary,
        List<string> errors)
    {
        if (plan.PlanVersion != template.PlanVersion ||
            plan.CompilerVersion != template.CompilerVersion ||
            plan.Id != template.Id ||
            plan.Version != template.Version ||
            plan.Description != template.Description ||
            plan.Policy != template.Policy ||
            plan.SourceCorpus != template.SourceCorpus ||
            plan.ReviewManifest != template.ReviewManifest ||
            plan.ReviewRubric != template.ReviewRubric ||
            plan.OutputCorpus != template.OutputCorpus ||
            plan.Partition != template.Partition ||
            plan.Profile != template.Profile ||
            plan.BaselineId != template.BaselineId ||
            plan.RecordCount != template.RecordCount ||
            plan.DecisionCount != template.DecisionCount ||
            !plan.Instructions.SequenceEqual(template.Instructions))
        {
            errors.Add("Completed manual-QA plan changes the frozen template identity or instructions.");
        }

        Dictionary<string, ManualQaDecisionPlanRecord> expectedRecords = template.Records.ToDictionary(
            record => record.SourceRecordId,
            StringComparer.Ordinal);
        Dictionary<string, ManualQaDecisionPlanRecord> suppliedRecords = IndexRecords(plan, errors);
        foreach (string missing in expectedRecords.Keys.Except(
                     suppliedRecords.Keys,
                     StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            errors.Add($"Completed manual-QA plan omits source record '{missing}'.");
        }

        foreach (string unknown in suppliedRecords.Keys.Except(
                     expectedRecords.Keys,
                     StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            errors.Add($"Completed manual-QA plan contains unknown source record '{unknown}'.");
        }

        foreach (string recordId in expectedRecords.Keys.Intersect(
                     suppliedRecords.Keys,
                     StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            ValidateRecordBoundary(
                expectedRecords[recordId],
                suppliedRecords[recordId],
                boundary.SourceRecords[recordId],
                boundary.Packets[recordId],
                errors);
        }
    }

    private static Dictionary<string, ManualQaDecisionPlanRecord> IndexRecords(
        ManualQaDecisionPlan plan,
        List<string> errors)
    {
        Dictionary<string, ManualQaDecisionPlanRecord> records = new(StringComparer.Ordinal);
        foreach (ManualQaDecisionPlanRecord record in plan.Records)
        {
            if (!records.TryAdd(record.SourceRecordId, record))
            {
                errors.Add($"Completed manual-QA plan repeats source record '{record.SourceRecordId}'.");
            }
        }

        return records;
    }

    private static void ValidateRecordBoundary(
        ManualQaDecisionPlanRecord expected,
        ManualQaDecisionPlanRecord supplied,
        CalibrationRecord source,
        ManualQaReviewPacket packet,
        List<string> errors)
    {
        string recordId = expected.SourceRecordId;
        if (supplied.PacketDigest != expected.PacketDigest ||
            supplied.LineageDigest != expected.LineageDigest)
        {
            errors.Add($"Completed manual-QA record '{recordId}' changes packet or lineage identity.");
        }

        if (supplied.Review is not null && supplied.Review.CompletedOn < source.Review.CompletedOn)
        {
            errors.Add($"Completed manual-QA record '{recordId}' predates its source review.");
        }

        ValidateReviewerReuse(source, supplied, errors);
        Dictionary<string, ManualQaDecision> expectedDecisions = expected.Decisions.ToDictionary(
            decision => decision.SourceTargetId,
            StringComparer.Ordinal);
        Dictionary<string, ManualQaDecision> suppliedDecisions = supplied.Decisions
            .GroupBy(decision => decision.SourceTargetId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        if (!expectedDecisions.Keys.Order(StringComparer.Ordinal).SequenceEqual(
                suppliedDecisions.Keys.Order(StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            errors.Add($"Completed manual-QA record '{recordId}' changes exact target coverage.");
            return;
        }

        Dictionary<string, ManualQaReviewTarget> packetTargets = packet.Targets.ToDictionary(
            target => target.SourceTargetId,
            StringComparer.Ordinal);
        foreach ((string targetId, ManualQaDecision expectedDecision) in expectedDecisions)
        {
            ManualQaDecision suppliedDecision = suppliedDecisions[targetId];
            ManualQaReviewTarget packetTarget = packetTargets[targetId];
            if (suppliedDecision.SourceLineageDigest != expectedDecision.SourceLineageDigest ||
                suppliedDecision.OverlapGroupId != expectedDecision.OverlapGroupId)
            {
                errors.Add(
                    $"Completed manual-QA decision '{recordId}/{targetId}' changes frozen lineage or overlap identity.");
            }

            foreach (string evidenceId in suppliedDecision.EvidenceIds.Except(
                         packetTarget.EvidenceIds,
                         StringComparer.Ordinal))
            {
                errors.Add(
                    $"Completed manual-QA decision '{recordId}/{targetId}' cites unknown evidence '{evidenceId}'.");
            }
        }
    }

    private static void ValidateReviewerReuse(
        CalibrationRecord source,
        ManualQaDecisionPlanRecord planned,
        List<string> errors)
    {
        if (planned.Review is null)
        {
            return;
        }

        Dictionary<(string Id, CalibrationReviewerRole Role), CalibrationReviewer> prior =
            source.Review.Reviewers.ToDictionary(reviewer => (reviewer.Id, reviewer.Role));
        foreach (CalibrationReviewer reviewer in planned.Review.Reviewers)
        {
            if (prior.TryGetValue((reviewer.Id, reviewer.Role), out CalibrationReviewer? existing) &&
                existing != reviewer)
            {
                errors.Add(
                    $"Manual-QA record '{source.Id}' reuses reviewer '{reviewer.Id}' with changed provenance.");
            }
        }
    }

    private static CalibrationRecord CompileRecord(
        CalibrationRecord source,
        ManualQaDecisionPlanRecord planned,
        ManualQaReviewPacket packet,
        ManualQaDecisionPlan plan,
        string planDigest)
    {
        Dictionary<string, ManualQaReviewTarget> packetTargets = packet.Targets.ToDictionary(
            target => target.SourceTargetId,
            StringComparer.Ordinal);
        Dictionary<string, CalibrationTarget> sourceTargets = source.Targets.ToDictionary(
            target => target.Id,
            StringComparer.Ordinal);
        CalibrationTarget[] replacementQa =
        [
            .. planned.Decisions.OrderBy(
                    decision => decision.SourceTargetId,
                    StringComparer.Ordinal)
                .Select(decision => CreateQaTarget(
                    source.Id,
                    sourceTargets[decision.SourceTargetId],
                    packetTargets[decision.SourceTargetId],
                    decision)),
        ];
        CalibrationTarget[] targets =
        [
            .. source.Targets.Where(target =>
                    target.Category != EffortCategory.ManualValidationDebuggingAndHardening)
                .Concat(replacementQa)
                .OrderBy(target => target.Category)
                .ThenBy(target => target.Scope, StringComparer.Ordinal)
                .ThenBy(target => target.Id, StringComparer.Ordinal),
        ];

        return source with
        {
            Review = MergeReview(source.Review, planned.Review!, plan, planDigest, planned.PacketDigest),
            Targets = targets,
        };
    }

    private static CalibrationTarget CreateQaTarget(
        string recordId,
        CalibrationTarget source,
        ManualQaReviewTarget packet,
        ManualQaDecision decision) => new()
        {
            Id = ManualQaSourceWorkItemLineage.CreateReviewedTargetId(recordId, source.Id),
            Category = EffortCategory.ManualValidationDebuggingAndHardening,
            Title = $"Manually validate, debug, and harden: {packet.Title}",
            Scope = packet.Scope,
            SourceWorkItemIds =
            [
                .. source.SourceWorkItemIds
                    .Select(ManualQaSourceWorkItemLineage.CreateCandidateWorkItemId)
                    .Order(StringComparer.Ordinal),
            ],
            EvidenceIds = [.. packet.EvidenceIds.Order(StringComparer.Ordinal)],
            Hours = decision.Hours!,
            Rationale = $"{decision.Rationale}\n\nOverlap allocation: {decision.OverlapAllocation}",
            UncertaintyReasons = [.. decision.UncertaintyReasons.Order(StringComparer.Ordinal)],
            SizeException = decision.SizeException,
        };

    private static CalibrationReviewProvenance MergeReview(
        CalibrationReviewProvenance source,
        CalibrationReviewProvenance qa,
        ManualQaDecisionPlan plan,
        string planDigest,
        string packetDigest)
    {
        CalibrationReviewer[] reviewers =
        [
            .. source.Reviewers.Concat(qa.Reviewers)
                .GroupBy(reviewer => (reviewer.Id, reviewer.Role))
                .Select(group => group.First())
                .OrderBy(reviewer => reviewer.Role)
                .ThenBy(reviewer => reviewer.Id, StringComparer.Ordinal),
        ];
        string provenance =
            $"Manual-QA category re-review: {qa.Notes} Plan {plan.Id}/{plan.Version} " +
            $"digest {planDigest}; packet digest {packetDigest}; compiler {CompilerVersion}.";
        return new CalibrationReviewProvenance
        {
            Status = CalibrationReviewStatus.TeacherEstimate,
            CompletedOn = DateOnly.FromDayNumber(int.Max(
                source.CompletedOn.DayNumber,
                qa.CompletedOn.DayNumber)),
            Reviewers = reviewers,
            Notes = string.IsNullOrWhiteSpace(source.Notes)
                ? provenance
                : $"{source.Notes}\n\n{provenance}",
        };
    }
}
