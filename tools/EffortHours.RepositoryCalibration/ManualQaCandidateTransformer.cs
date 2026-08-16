using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class ManualQaCandidateTransformer
{
    public const string PolicyVersion = "manual-qa-coding-ratio-policy/1.0.0";
    public const string PolicyId = "efforthours-manual-qa-coding-ratio";
    public const string CandidateId = "manual-qa-coding-ratio/0.1.0";
    public const string EstimatorVersion =
        "candidate-manual-qa-coding-ratio/0.1.0+seed-rules/0.4.0";
    public const string BaselineEstimatorVersion = "seed-rules/0.4.0";
    public const string FeatureContractVersion = EligibleCodingEffortVersions.V1;
    public const string EffectiveDate = "2026-08-16";
    public const string Maturity = "development-only-unvalidated";
    public const string Basis = "eligible-source-work-item-expected-hours";
    public const string Projection = "one-manual-qa-item-per-eligible-source-work-item";
    public const decimal LowRatio = 0.30m;
    public const decimal ExpectedRatio = 0.40m;
    public const decimal HighRatio = 0.50m;
    public const decimal MaximumConfidence = 0.50m;

    public static readonly EffortCategory[] EligibleCategories =
        [.. EligibleCodingEffortVersions.Categories];

    private static readonly HashSet<EffortCategory> EligibleCategorySet =
        [.. EligibleCategories];

    public static EstimateReport Transform(
        EstimateReport source,
        ManualQaCandidatePolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(policy);
        ValidatePolicy(policy);
        if (source.EstimatorVersion != policy.BaselineEstimatorVersion ||
            source.Profile != EstimationProfile.Implementation)
        {
            throw new InvalidDataException(
                $"Candidate '{policy.CandidateId}' supports implementation-profile " +
                $"'{policy.BaselineEstimatorVersion}' reports only.");
        }

        List<WorkItem> projected = new(source.WorkItems.Count * 2);
        int replacedManualItems = 0;
        int eligibleCodingItems = 0;
        foreach (WorkItem item in source.WorkItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.Category == EffortCategory.ManualValidationDebuggingAndHardening)
            {
                replacedManualItems++;
                continue;
            }

            projected.Add(item);
            if (!EligibleCategorySet.Contains(item.Category) || item.Hours.Expected <= 0m)
            {
                continue;
            }

            projected.Add(CreateManualQaItem(item, policy));
            eligibleCodingItems++;
        }

        WorkItem[] ordered =
        [
            .. projected.OrderBy(item => item.Category)
                .ThenBy(item => item.Scope, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal),
        ];
        return CandidateEstimateProjection.Create(
            source,
            policy.EstimatorVersion,
            ordered,
            [
                "Development-only manual-QA candidate; this estimate is not admitted for product use.",
                $"Replaced {replacedManualItems} seed manual-validation item(s) with " +
                $"{eligibleCodingItems} dependency-linked item(s) derived from eligible expected coding effort.",
                "The 30/40/50 percent manual-QA component is centered on 40 percent of expected coding effort; source low/high values are not compounded into it.",
            ]);
    }

    internal static void ValidatePolicy(ManualQaCandidatePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.PolicyVersion != PolicyVersion ||
            policy.Id != PolicyId ||
            policy.CandidateId != CandidateId ||
            policy.EstimatorVersion != EstimatorVersion ||
            policy.BaselineEstimatorVersion != BaselineEstimatorVersion ||
            policy.FeatureContractVersion != FeatureContractVersion ||
            policy.EffectiveDate != EffectiveDate ||
            policy.LicenseExpression != "MIT" ||
            policy.Maturity != Maturity ||
            policy.Basis != Basis ||
            policy.Projection != Projection ||
            policy.LowRatio != LowRatio ||
            policy.ExpectedRatio != ExpectedRatio ||
            policy.HighRatio != HighRatio ||
            policy.MaximumConfidence != MaximumConfidence ||
            !policy.EligibleCategories.SequenceEqual(EligibleCategories) ||
            policy.Limitations.Count == 0)
        {
            throw new InvalidDataException(
                "Manual-QA candidate policy identity or frozen configuration differs.");
        }
    }

    private static WorkItem CreateManualQaItem(
        WorkItem source,
        ManualQaCandidatePolicy policy) => new()
        {
            Id = $"work:manual-qa-coding-ratio:{StableToken(source.Id)}:part-0001",
            Category = EffortCategory.ManualValidationDebuggingAndHardening,
            Title = $"Manually validate, debug, and harden: {source.Title}",
            Scope = source.Scope,
            EvidenceIds = Normalize(source.EvidenceIds),
            Quantity = 1m,
            Complexity = source.Complexity,
            Hours = new EffortRange
            {
                Low = source.Hours.Expected * policy.LowRatio,
                Expected = source.Hours.Expected * policy.ExpectedRatio,
                High = source.Hours.Expected * policy.HighRatio,
            },
            Confidence = decimal.Min(source.Confidence, policy.MaximumConfidence),
            Reason = string.Format(
                CultureInfo.InvariantCulture,
                "Candidate '{0}' applies 30%/40%/50% to the {1} expected coding hours " +
                "in eligible source item '{2}' ({3}). All three points use expected coding " +
                "effort so source interval width is not compounded.",
                policy.CandidateId,
                source.Hours.Expected,
                source.Id,
                source.Category),
            Estimator = new EstimatorReference
            {
                Id = "candidate-manual-qa-coding-ratio",
                Version = "0.1.0",
                Kind = EstimatorKind.Rule,
            },
            Profiles = source.Profiles,
            DependencyIds = [source.Id],
            CorrelationGroup = source.CorrelationGroup is null
            ? $"manual-qa:{StableToken(source.Scope)}"
            : $"manual-qa:{source.CorrelationGroup}",
            Assumptions = Normalize(
        [
            .. source.Assumptions,
            "A competent senior contractor performs reasonable manual validation while recreating the represented coding work.",
        ]),
            Exclusions = Normalize(
        [
            .. source.Exclusions,
            "Specification, discovery, architecture, repository setup, documentation, manual QA itself, self-review, professionalization gaps, and pricing are outside the coding basis.",
        ]),
            UncertaintyReasons = Normalize(
        [
            .. source.UncertaintyReasons,
            "The 30/40/50 percent ratio is an experience-based prior without fresh blind validation.",
        ]),
        };

    private static string[] Normalize(IEnumerable<string> values) =>
    [
        .. values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    private static string StableToken(string value)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(digest.AsSpan(0, 10)).ToLowerInvariant();
    }
}
