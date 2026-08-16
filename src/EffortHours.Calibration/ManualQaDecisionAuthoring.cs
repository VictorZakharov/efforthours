using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class ManualQaDecisionAuthoring
{
    public const string PolicyId = "efforthours-manual-qa-development-decision";
    public const string Maturity = "development-only-pre-answer-compiler-freeze";

    public static ManualQaDecisionPlan CreateTemplate(
        CalibrationCorpus sourceCorpus,
        ManualQaReviewPolicy reviewPolicy,
        string reviewPolicyDigest,
        ManualQaReviewManifest reviewManifest,
        IReadOnlyList<ManualQaReviewPacket> packets,
        ManualQaDecisionCompilerPolicy compilerPolicy,
        string compilerPolicyDigest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compilerPolicyDigest);
        ManualQaDecisionBoundaryContext boundary = ManualQaDecisionBoundary.Validate(
            sourceCorpus,
            reviewPolicy,
            reviewPolicyDigest,
            reviewManifest,
            packets,
            compilerPolicy);

        ManualQaDecisionPlan plan = new()
        {
            PlanVersion = ManualQaDecisionVersions.PlanV1,
            CompilerVersion = compilerPolicy.CompilerVersion,
            Status = ManualQaDecisionPlanStatus.Unreviewed,
            Id = compilerPolicy.PlanId,
            Version = compilerPolicy.PlanVersion,
            Description = compilerPolicy.PlanDescription,
            Policy = new ManualQaReviewPolicyReference
            {
                Id = compilerPolicy.Id,
                Version = compilerPolicy.PolicyVersion,
                Digest = compilerPolicyDigest,
            },
            SourceCorpus = compilerPolicy.SourceCorpus,
            ReviewManifest = compilerPolicy.ReviewManifest,
            ReviewRubric = compilerPolicy.ReviewRubric,
            OutputCorpus = compilerPolicy.OutputCorpus,
            Partition = compilerPolicy.Partition,
            Profile = compilerPolicy.Profile,
            BaselineId = compilerPolicy.BaselineId,
            RecordCount = compilerPolicy.ExpectedRecordCount,
            DecisionCount = compilerPolicy.ExpectedDecisionCount,
            Records =
            [
                .. reviewManifest.Packets.OrderBy(
                        entry => entry.SourceRecordId,
                        StringComparer.Ordinal)
                    .Select(entry => CreateRecord(
                        entry,
                        boundary.Packets[entry.SourceRecordId])),
            ],
            Instructions =
            [
                "Copy this frozen template; never edit the committed template artifact.",
                "Keep seed and candidate QA values, formulas, totals, and comparisons unavailable while authoring.",
                "Set status to completed only after every record review and every target decision is complete.",
                "Use estimate for distinct represented QA work, exclude for a non-QA or false-positive responsibility, and duplicate only when another estimated target owns the shared work.",
                "Cite packet evidence IDs, record overlap allocation explicitly, and follow manual-qa-work-item/1.0.0.",
                "Run the frozen compiler with an explicit completed-plan digest; validation and test inputs are forbidden.",
            ],
        };
        IReadOnlyList<string> errors = ContractValidation.Validate(plan);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Manual-QA decision authoring produced an invalid template: " +
                string.Join("; ", errors));
        }

        return plan;
    }

    private static ManualQaDecisionPlanRecord CreateRecord(
        ManualQaReviewManifestPacket entry,
        ManualQaReviewPacket packet) => new()
        {
            SourceRecordId = entry.SourceRecordId,
            PacketDigest = entry.PacketDigest,
            LineageDigest = entry.LineageDigest,
            Review = null,
            Decisions =
            [
                .. packet.Targets.OrderBy(
                        target => target.SourceTargetId,
                        StringComparer.Ordinal)
                    .Select(target => new ManualQaDecision
                    {
                        SourceTargetId = target.SourceTargetId,
                        SourceLineageDigest = target.SourceLineageDigest,
                        OverlapGroupId = target.OverlapGroupId,
                        Disposition = null,
                        Hours = null,
                        Rationale = null,
                        EvidenceIds = [],
                        UncertaintyReasons = [],
                        OverlapAllocation = null,
                        DuplicateOfSourceTargetId = null,
                        SizeException = null,
                    }),
            ],
        };
}
