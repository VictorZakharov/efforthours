using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Calibration;

public static class CalibrationAuthoring
{
    private static readonly Regex PartSuffix = new(
        @":part-\d+$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public const string AuthoringVersion = "calibration-authoring/0.1.0";
    public const string Warning =
        "UNREVIEWED: candidate values are reference material, not calibration labels. " +
        "Estimate each target from evidence under the referenced rubric before copying it into a corpus.";

    public static CalibrationAuthoringPacket Scaffold(
        EstimateReport estimate,
        bool blind = false)
    {
        ArgumentNullException.ThrowIfNull(estimate);

        List<string> estimateErrors = [.. ContractValidation.Validate(estimate)];
        if (string.IsNullOrWhiteSpace(estimate.Repository.SourceDigest))
        {
            estimateErrors.Add("repository.sourceDigest is required for calibration authoring.");
        }

        if (estimateErrors.Count > 0)
        {
            throw new CalibrationEvaluationException(
                [.. estimateErrors.Select(error => $"Source estimate: {error}")]);
        }

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
                Id = "ehe-work-item",
                Version = "1.0.0",
            },
            Repository = new CalibrationRepositoryReference
            {
                Id = SuggestRepositoryId(estimate.Repository.Name),
                Name = estimate.Repository.Name,
                SourceDigest = estimate.Repository.SourceDigest!,
            },
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
                .OrderBy(id => id, StringComparer.Ordinal)],
            Instructions =
            [
                "Confirm repository identity, profile, baseline, source provenance, license, and partition outside this packet.",
                "Review represented behavior and evidence; exclude duplication, dead, generated, vendored, and accidental volume.",
                "Fill review.hours and review.rationale independently for every retained target; regroup targets when needed.",
                "Keep expected effort near 0.5 to 8 hours or fill review.sizeException with a concrete reason.",
                "Reconcile category and repository totals only after target review; do not force a preferred total.",
                "Create a calibration corpus record only after review is complete and reviewer provenance is recorded.",
            ],
        };

        IReadOnlyList<string> packetErrors = ContractValidation.Validate(packet);
        if (packetErrors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Calibration authoring produced an invalid packet: {string.Join("; ", packetErrors)}");
        }

        return packet;
    }

    private static CalibrationAuthoringTarget CreateTarget(WorkItem item, bool blind) => new()
    {
        Id = $"target:{item.Id}",
        SourceCapabilityId = GetSourceCapabilityId(item.Id),
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

    public static string GetSourceCapabilityId(string workItemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workItemId);
        return PartSuffix.Replace(workItemId, string.Empty);
    }

    private static string SuggestRepositoryId(string name)
    {
        StringBuilder slug = new();
        bool previousSeparator = false;
        foreach (char character in name.Normalize(NormalizationForm.FormKC))
        {
            char lower = char.ToLower(character, CultureInfo.InvariantCulture);
            if (char.IsAsciiLetterOrDigit(lower))
            {
                slug.Append(lower);
                previousSeparator = false;
            }
            else if (!previousSeparator && slug.Length > 0)
            {
                slug.Append('-');
                previousSeparator = true;
            }
        }

        while (slug.Length > 0 && slug[^1] == '-')
        {
            slug.Length--;
        }

        return slug.Length == 0
            ? "repository:review-required"
            : $"repository:{slug}";
    }
}
