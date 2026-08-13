using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class CalibrationAuthoring
{
    private static readonly Regex PartSuffix = new(
        @":part-\d+$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex TitlePartSuffix = new(
        @" \(part \d+ of \d+\)$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public const string AuthoringVersion = "calibration-authoring/0.2.0";
    public const string RubricId = "ehe-work-item";
    public const string RubricVersion = "1.1.0";
    public const string Warning =
        "UNREVIEWED: candidate values are reference material, not calibration labels. " +
        "Estimate each target from evidence under the referenced rubric before copying it into a corpus.";

    public static CalibrationAuthoringPacket Scaffold(
        EstimateReport estimate,
        bool blind = false,
        string? repositoryFamilyId = null,
        string? repositoryName = null)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        if (repositoryFamilyId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repositoryFamilyId);
        }

        if (repositoryName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        }

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
                Id = RubricId,
                Version = RubricVersion,
            },
            Repository = new CalibrationRepositoryReference
            {
                Id = repositoryFamilyId ?? SuggestRepositoryId(estimate.Repository.Name),
                Name = repositoryName ?? estimate.Repository.Name,
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
                .GroupBy(item => GetSourceCapabilityId(item.Id), StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => CreateTarget(group.Key, group, blind))],
            ProfessionalizationGapWorkItemIds = blind
                ? []
                : [.. estimate.ProfessionalizationGap
                    .Select(item => item.Id)
                    .OrderBy(id => id, StringComparer.Ordinal)],
            Instructions =
            [
                "Confirm repository identity, profile, baseline, source provenance, license, and partition outside this packet.",
                "Review represented behavior and evidence; exclude duplication, dead, generated, vendored, and accidental volume.",
                "Review each capability independently; split review targets when needed and use exact 0/0/0 only for a rubric-qualified exclusion.",
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

    private static CalibrationAuthoringTarget CreateTarget(
        string capabilityId,
        IEnumerable<WorkItem> sourceItems,
        bool blind)
    {
        WorkItem[] items = [.. sourceItems.OrderBy(item => item.Id, StringComparer.Ordinal)];
        WorkItem first = items[0];
        if (items.Any(item => item.Category != first.Category ||
                              !string.Equals(item.Scope, first.Scope, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Source capability '{capabilityId}' contains mixed categories or scopes.");
        }

        return new CalibrationAuthoringTarget
        {
            Id = $"target:{capabilityId}",
            SourceCapabilityId = capabilityId,
            Category = first.Category,
            Title = TitlePartSuffix.Replace(first.Title, string.Empty),
            Scope = first.Scope,
            SourceWorkItemIds = blind ? [] : [.. items.Select(item => item.Id)],
            EvidenceIds = DistinctSorted(items.SelectMany(item => item.EvidenceIds)),
            Candidate = new CalibrationAuthoringSuggestion
            {
                Hours = blind ? null : ContractValidation.Sum(items.Select(item => item.Hours)),
                Confidence = blind ? null : WeightedConfidence(items),
                Reason = blind ? null : SummarizeCandidateReason(items),
            },
            Review = new CalibrationAuthoringReviewFields(),
            Assumptions = DistinctSorted(items.SelectMany(item => item.Assumptions)),
            Exclusions = DistinctSorted(items.SelectMany(item => item.Exclusions)),
            UncertaintyReasons = DistinctSorted(items.SelectMany(item => item.UncertaintyReasons)),
        };
    }

    private static decimal WeightedConfidence(WorkItem[] items)
    {
        decimal total = items.Sum(item => item.Hours.Expected);
        return total == 0m
            ? items.Average(item => item.Confidence)
            : items.Sum(item => item.Hours.Expected * item.Confidence) / total;
    }

    private static string SummarizeCandidateReason(WorkItem[] items)
    {
        string[] reasons = DistinctSorted(items.Select(item => item.Reason));
        return reasons.Length == 1
            ? reasons[0]
            : $"Aggregated candidate guidance from {items.Length} source work items; " +
              "inspect the source estimate for partition-level reasons.";
    }

    private static string[] DistinctSorted(IEnumerable<string> values) =>
        [.. values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)];

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
