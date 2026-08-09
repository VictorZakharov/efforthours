using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class ChangeCalibrationAuthoring
{
    public const string AuthoringVersion = "change-calibration-authoring/0.1.0";
    public const string RubricId = "change-ehe-work-item";
    public const string RubricVersion = "1.0.0";
    public const string Warning =
        "UNREVIEWED CHANGE EHE: candidate values are reference material, not calibration labels. " +
        "Review the normalized final delta without using history activity as effort evidence.";

    public static CalibrationAuthoringPacket Scaffold(
        ChangeEstimateReport estimate,
        string repositoryFamilyId,
        string caseId,
        IReadOnlyList<string> coverageTags,
        bool blind = false)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryFamilyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentNullException.ThrowIfNull(coverageTags);

        List<string> errors = [.. ContractValidation.Validate(estimate)
            .Select(error => $"Source change estimate: {error}")];
        if (coverageTags.Count == 0)
        {
            errors.Add("At least one Change calibration coverage tag is required.");
        }

        if (estimate.RateCard is not null || estimate.TotalCost is not null)
        {
            errors.Add("Change calibration authoring requires an effort-only estimate produced with --no-rate.");
        }

        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }

        ChangeCalibrationReference change = ChangeCalibrationIdentity.CreateReference(
            estimate,
            caseId,
            coverageTags);
        CalibrationCandidateVisibility visibility = blind
            ? CalibrationCandidateVisibility.Blind
            : CalibrationCandidateVisibility.Reference;
        CalibrationAuthoringPacket packet = new()
        {
            AuthoringVersion = AuthoringVersion,
            Status = CalibrationAuthoringStatus.Unreviewed,
            Warning = Warning,
            Rubric = new CalibrationRubricReference
            {
                Id = RubricId,
                Version = RubricVersion,
            },
            Repository = new CalibrationRepositoryReference
            {
                Id = repositoryFamilyId,
                Name = estimate.Repository.Name,
                SourceDigest = change.FinalDeltaDigest,
            },
            Change = change,
            Profile = estimate.Profile,
            BaselineId = estimate.Baseline.Id,
            CandidateVisibility = visibility,
            Candidate = new CalibrationAuthoringCandidate
            {
                EstimatorVersion = estimate.EstimatorVersion,
                EstimateDigest = CalibrationDigest.Compute(estimate),
                TotalHours = blind ? null : estimate.TotalEffort,
                Categories = blind ? [] : estimate.Categories,
            },
            Targets = [.. estimate.WorkItems
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => CreateTarget(item, blind))],
            ProfessionalizationGapWorkItemIds = [.. estimate.ProfessionalizationGap
                .Select(item => item.Id)
                .Order(StringComparer.Ordinal)],
            Instructions =
            [
                "Confirm repository-family identity, immutable base/head provenance, license, redistribution, and partition before compilation.",
                "Estimate only the normalized final functional and quality delta; intermediate churn, commit count, author, and elapsed time are forbidden signals.",
                "Use the source Change report and explain query to verify evidence, exclusions, and final-delta reconciliation without using candidate hours when blind.",
                "Fill review hours and rationale independently for every represented target; use an exact 0/0/0 range plus a size exception for a reviewed false positive, while an empty target list is valid only for a zero-EHE final delta.",
                "Keep expected targets near 0.5 to 8 hours, record uncertainty explicitly, and reconcile the final total only after target review.",
                "Compile teacher labels before a distinct reviewer receives a blind second-pass packet; packet generation alone does not advance maturity.",
            ],
        };

        IReadOnlyList<string> packetErrors = ContractValidation.Validate(packet);
        if (packetErrors.Count > 0)
        {
            throw new CalibrationEvaluationException([.. packetErrors.Select(error =>
                $"Change calibration packet: {error}")]);
        }

        return packet;
    }

    private static CalibrationAuthoringTarget CreateTarget(WorkItem item, bool blind) => new()
    {
        Id = $"target:{item.Id}",
        SourceCapabilityId = CalibrationAuthoring.GetSourceCapabilityId(item.Id),
        Category = item.Category,
        Title = item.Title,
        Scope = item.Scope,
        SourceWorkItemIds = [item.Id],
        EvidenceIds = item.EvidenceIds,
        Candidate = new CalibrationAuthoringSuggestion
        {
            Hours = blind ? null : item.Hours,
            Confidence = blind ? null : item.Confidence,
            Reason = item.Reason,
        },
        Review = new CalibrationAuthoringReviewFields(),
        Assumptions = item.Assumptions,
        Exclusions = item.Exclusions,
        UncertaintyReasons = item.UncertaintyReasons,
    };
}
