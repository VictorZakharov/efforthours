using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(CalibrationUncertaintyFeatureReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "calibration uncertainty feature report", errors);
        if (report.ProjectorVersion != CalibrationUncertaintyVersions.ProjectorV1)
        {
            errors.Add($"Unsupported uncertainty feature projector '{report.ProjectorVersion}'.");
        }
        RequireDigest(report.FeatureContractDigest, "featureContractDigest", errors);
        if (report.FeatureContractDigest != CalibrationUncertaintyVersions.FeatureContractDigestV1)
        {
            errors.Add("The uncertainty feature report does not pin the canonical v1 feature contract.");
        }
        RequireDigest(report.EstimateDigest, "estimateDigest", errors);
        RequireDigest(report.EvidenceDigest, "evidenceDigest", errors);
        RequireDigest(report.RepositorySourceDigest, "repositorySourceDigest", errors);
        RequireText(report.EstimatorVersion, "estimatorVersion", errors);
        RequireText(report.BaselineId, "baselineId", errors);
        RequireUniqueText(report.Ecosystems, "ecosystems", errors);

        ValidateUncertaintyFeatureContract(report.FeatureContract, errors);
        ValidateUncertaintyWorkItems(report, errors);
        ValidateUncertaintySummary(report, errors);
        return errors;
    }

    private static void ValidateUncertaintyFeatureContract(
        CalibrationUncertaintyFeatureContract contract,
        List<string> errors)
    {
        if (contract.Version != CalibrationUncertaintyVersions.FeatureContractV1)
        {
            errors.Add($"Unsupported uncertainty feature contract '{contract.Version}'.");
        }
        RequireText(contract.EffectiveDate, "featureContract.effectiveDate", errors);
        if (!DateOnly.TryParseExact(
                contract.EffectiveDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            errors.Add("featureContract.effectiveDate must use yyyy-MM-dd.");
        }

        if (!contract.LabelIndependent)
        {
            errors.Add("The uncertainty feature contract must be label-independent.");
        }

        ValidateUncertaintyPolicy(contract.IntervalPolicy, errors);
        if (contract.Features.Count == 0)
        {
            errors.Add("The uncertainty feature contract must contain at least one offline feature.");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (CalibrationUncertaintyFeatureDefinition feature in contract.Features)
        {
            ValidateUncertaintyFeatureDefinition(feature, "feature", errors);
            if (feature.Stage != CalibrationUncertaintyFeatureStage.AvailableOffline)
            {
                errors.Add($"Feature '{feature.Id}' must be available offline.");
            }

            if (feature.ValueKind == CalibrationUncertaintyFeatureValueKind.Distribution)
            {
                errors.Add($"Offline feature '{feature.Id}' must be a scalar in v1.");
            }

            if (!ids.Add(feature.Id))
            {
                errors.Add($"Uncertainty feature ID '{feature.Id}' is duplicated.");
            }
        }

        foreach (CalibrationUncertaintyFeatureDefinition feature in contract.DeferredCandidates)
        {
            ValidateUncertaintyFeatureDefinition(feature, "deferredCandidate", errors);
            if (feature.Stage != CalibrationUncertaintyFeatureStage.DeferredEvidence)
            {
                errors.Add($"Deferred candidate '{feature.Id}' must use deferred-evidence stage.");
            }

            if (feature.Monotonicity != CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly)
            {
                errors.Add($"Deferred candidate '{feature.Id}' cannot constrain interval width.");
            }

            if (!ids.Add(feature.Id))
            {
                errors.Add($"Uncertainty feature ID '{feature.Id}' is duplicated.");
            }
        }
    }

    private static void ValidateUncertaintyPolicy(
        CalibrationUncertaintyIntervalPolicy policy,
        List<string> errors)
    {
        if (policy.Version != CalibrationUncertaintyVersions.IntervalPolicyV1)
        {
            errors.Add($"Unsupported uncertainty interval policy '{policy.Version}'.");
        }

        if (policy.IntendedCoverageMetric !=
                CalibrationUncertaintyCoverageMetric.ReviewedExpectedPoint ||
            policy.IntendedCoverageTarget != 0.80m)
        {
            errors.Add("The v1 uncertainty interval policy must target 0.80 reviewed-expected coverage.");
        }

        if (policy.FormalProbabilityInterval ||
            !policy.SymmetricAroundExpected ||
            !policy.ZeroHourFloor ||
            !policy.DirectionalContingenciesSeparate ||
            !policy.MaterialUnresolvedFactsMustWiden ||
            !policy.ComparableWeakerEvidenceMustNotNarrow ||
            policy.MissingValuesWidenAutomatically)
        {
            errors.Add("The v1 uncertainty interval policy does not satisfy its safety invariants.");
        }
    }

    private static void ValidateUncertaintyFeatureDefinition(
        CalibrationUncertaintyFeatureDefinition feature,
        string path,
        List<string> errors)
    {
        RequireText(feature.Id, $"{path}.id", errors);
        RequireText(feature.OfflineSource, $"{path}[{feature.Id}].offlineSource", errors);
        RequireText(feature.Description, $"{path}[{feature.Id}].description", errors);
    }

    private static void ValidateUncertaintyWorkItems(
        CalibrationUncertaintyFeatureReport report,
        List<string> errors)
    {
        string[] featureIds = [.. report.FeatureContract.Features.Select(feature => feature.Id)];
        HashSet<string> workItemIds = new(StringComparer.Ordinal);
        foreach (CalibrationUncertaintyWorkItemFeatures item in report.WorkItems)
        {
            string path = $"workItem[{item.WorkItemId}]";
            RequireText(item.WorkItemId, "workItem.id", errors);
            if (!workItemIds.Add(item.WorkItemId))
            {
                errors.Add($"Uncertainty work-item ID '{item.WorkItemId}' is duplicated.");
            }

            RequireUniqueText(item.Ecosystems, $"{path}.ecosystems", errors);

            ValidateRange(item.SourceRange, $"{path}.sourceRange", errors);
            if (item.ExpectedHours != item.SourceRange.Expected)
            {
                errors.Add($"{path}.expectedHours must equal sourceRange.expected.");
            }

            bool compliant = IsSymmetricWithZeroFloor(item.SourceRange);
            if (item.SourceRangePolicyCompliant != compliant)
            {
                errors.Add($"{path}.sourceRangePolicyCompliant is inconsistent with sourceRange.");
            }

            if (item.ParentId is not null)
            {
                RequireText(item.ParentId, $"{path}.parentId", errors);
            }

            if (item.CorrelationGroup is not null)
            {
                RequireText(item.CorrelationGroup, $"{path}.correlationGroup", errors);
            }

            RequireUniqueText(item.ResolvedEvidenceIds, $"{path}.resolvedEvidenceIds", errors);
            RequireUniqueText(item.UnresolvedEvidenceIds, $"{path}.unresolvedEvidenceIds", errors);
            if (item.ResolvedEvidenceIds.Intersect(
                    item.UnresolvedEvidenceIds,
                    StringComparer.Ordinal).Any())
            {
                errors.Add($"{path} cannot resolve and leave unresolved the same evidence ID.");
            }

            string[] itemFeatureIds = [.. item.Features.Select(feature => feature.FeatureId)];
            if (!itemFeatureIds.SequenceEqual(featureIds, StringComparer.Ordinal))
            {
                errors.Add($"{path}.features must match feature-contract order exactly.");
            }

            HashSet<string> allowedEvidenceIds =
            [
                .. item.ResolvedEvidenceIds,
                .. item.UnresolvedEvidenceIds,
            ];

            foreach ((CalibrationUncertaintyFeatureValue value, int index) in
                     item.Features.Select((value, index) => (value, index)))
            {
                CalibrationUncertaintyFeatureDefinition? definition =
                    index < report.FeatureContract.Features.Count
                        ? report.FeatureContract.Features[index]
                        : null;
                ValidateUncertaintyFeatureValue(
                    value,
                    definition,
                    allowedEvidenceIds,
                    path,
                    errors);
            }
        }
    }

    private static void ValidateUncertaintyFeatureValue(
        CalibrationUncertaintyFeatureValue value,
        CalibrationUncertaintyFeatureDefinition? definition,
        HashSet<string> allowedEvidenceIds,
        string itemPath,
        List<string> errors)
    {
        string path = $"{itemPath}.feature[{value.FeatureId}]";
        RequireText(value.FeatureId, $"{itemPath}.feature.id", errors);
        RequireUniqueText(value.EvidenceIds, $"{path}.evidenceIds", errors);
        if (value.Availability == CalibrationUncertaintyFeatureAvailability.Available)
        {
            if (value.Value is null)
            {
                errors.Add($"{path}.value is required when available.");
            }

            if (value.ReasonCode is not null)
            {
                errors.Add($"{path}.reasonCode must be omitted when available.");
            }
        }
        else
        {
            if (value.Value is not null)
            {
                errors.Add($"{path}.value must be omitted when not available.");
            }

            RequireText(value.ReasonCode, $"{path}.reasonCode", errors);
        }

        if (value.EvidenceIds.Any(id => !allowedEvidenceIds.Contains(id)))
        {
            errors.Add($"{path}.evidenceIds must be supporting IDs on the work item.");
        }

        if (value.Value is null || definition is null)
        {
            return;
        }

        if (value.Value < 0m)
        {
            errors.Add($"{path}.value cannot be negative.");
        }

        if (definition.ValueKind == CalibrationUncertaintyFeatureValueKind.Ratio &&
            value.Value > 1m)
        {
            errors.Add($"{path}.value must be at most one for a ratio.");
        }

        if (definition.ValueKind is CalibrationUncertaintyFeatureValueKind.Count or
            CalibrationUncertaintyFeatureValueKind.Ordinal &&
            decimal.Truncate(value.Value.Value) != value.Value.Value)
        {
            errors.Add($"{path}.value must be an integer for {definition.ValueKind}.");
        }
    }

    private static void ValidateUncertaintySummary(
        CalibrationUncertaintyFeatureReport report,
        List<string> errors)
    {
        CalibrationUncertaintyFeatureSummary summary = report.Summary;
        HashSet<string> widthDrivers =
        [
            .. report.FeatureContract.Features
                .Where(feature => feature.Monotonicity !=
                    CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly)
                .Select(feature => feature.Id),
        ];
        if (summary.WorkItemCount != report.WorkItems.Count ||
            summary.FeatureCount != report.FeatureContract.Features.Count ||
            summary.WidthDriverFeatureCount != widthDrivers.Count ||
            summary.DeferredCandidateCount != report.FeatureContract.DeferredCandidates.Count ||
            summary.ResolvedEvidenceReferenceCount != report.WorkItems.Sum(item =>
                item.ResolvedEvidenceIds.Count) ||
            summary.UnresolvedEvidenceReferenceCount != report.WorkItems.Sum(item =>
                item.UnresolvedEvidenceIds.Count) ||
            summary.WidthDriverUnavailableWorkItemCount != report.WorkItems.Count(item =>
                item.Features.Any(feature => widthDrivers.Contains(feature.FeatureId) &&
                    feature.Availability == CalibrationUncertaintyFeatureAvailability.Unavailable)) ||
            summary.MaterialAccessGapWorkItemCount != CountFeaturePositive(
                report,
                "access.material-unresolved-count") ||
            summary.NonMaterialOfflineLimitationWorkItemCount != CountFeaturePositive(
                report,
                "analysis.non-material-offline-limitation-count") ||
            summary.SourcePolicyCompliantWorkItemCount != report.WorkItems.Count(item =>
                item.SourceRangePolicyCompliant))
        {
            errors.Add("Uncertainty feature summary does not reconcile to work items and contract.");
        }
    }

    private static int CountFeaturePositive(
        CalibrationUncertaintyFeatureReport report,
        string id) => report.WorkItems.Count(item => item.Features.Any(feature =>
            feature.FeatureId == id && feature.Value > 0m));

    private static bool IsSymmetricWithZeroFloor(EffortRange range)
    {
        decimal halfWidth = range.High - range.Expected;
        return range.Low >= 0m &&
            range.Low <= range.Expected &&
            halfWidth >= 0m &&
            range.Low == decimal.Max(0m, range.Expected - halfWidth);
    }
}
