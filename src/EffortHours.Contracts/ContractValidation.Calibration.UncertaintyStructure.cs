using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(
        CalibrationUncertaintyStructuralFeatureReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> errors = [];
        RequireVersion(
            report.SchemaVersion,
            "calibration uncertainty structural feature report",
            errors);
        if (report.ProjectorVersion != CalibrationUncertaintyVersions.StructuralProjectorV1)
        {
            errors.Add(
                $"Unsupported uncertainty structural feature projector '{report.ProjectorVersion}'.");
        }

        RequireDigest(report.FeatureContractDigest, "featureContractDigest", errors);
        if (report.FeatureContractDigest !=
            CalibrationUncertaintyVersions.StructuralFeatureContractDigestV1)
        {
            errors.Add(
                "The uncertainty structural feature report does not pin the canonical v1 contract.");
        }

        RequireDigest(report.EstimateDigest, "estimateDigest", errors);
        RequireDigest(report.EvidenceDigest, "evidenceDigest", errors);
        RequireDigest(report.RepositorySourceDigest, "repositorySourceDigest", errors);
        RequireText(report.EstimatorVersion, "estimatorVersion", errors);
        RequireText(report.BaselineId, "baselineId", errors);
        RequireUniqueText(report.Ecosystems, "ecosystems", errors);

        ValidateStructuralFeatureContract(report.FeatureContract, errors);
        ValidateStructuralWorkItems(report, errors);
        ValidateStructuralSummary(report, errors);
        return errors;
    }

    private static void ValidateStructuralFeatureContract(
        CalibrationUncertaintyFeatureContract contract,
        List<string> errors)
    {
        if (contract.Version != CalibrationUncertaintyVersions.StructuralFeatureContractV1)
        {
            errors.Add(
                $"Unsupported uncertainty structural feature contract '{contract.Version}'.");
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
            errors.Add("The uncertainty structural feature contract must be label-independent.");
        }

        ValidateUncertaintyPolicy(contract.IntervalPolicy, errors);
        if (contract.Features.Count == 0)
        {
            errors.Add("The structural feature contract must contain at least one offline feature.");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (CalibrationUncertaintyFeatureDefinition feature in contract.Features)
        {
            ValidateUncertaintyFeatureDefinition(feature, "feature", errors);
            if (feature.Stage != CalibrationUncertaintyFeatureStage.AvailableOffline ||
                feature.Monotonicity != CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly)
            {
                errors.Add(
                    $"Structural feature '{feature.Id}' must be available offline and diagnostic-only before evaluation.");
            }

            if (feature.ValueKind is not CalibrationUncertaintyFeatureValueKind.Count and not
                CalibrationUncertaintyFeatureValueKind.Ratio)
            {
                errors.Add($"Structural feature '{feature.Id}' must be a scalar count or ratio.");
            }

            if (!ids.Add(feature.Id))
            {
                errors.Add($"Uncertainty structural feature ID '{feature.Id}' is duplicated.");
            }
        }

        foreach (CalibrationUncertaintyFeatureDefinition feature in contract.DeferredCandidates)
        {
            ValidateUncertaintyFeatureDefinition(feature, "deferredCandidate", errors);
            if (feature.Stage != CalibrationUncertaintyFeatureStage.DeferredEvidence ||
                feature.Monotonicity != CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly)
            {
                errors.Add(
                    $"Deferred structural candidate '{feature.Id}' must remain diagnostic deferred evidence.");
            }

            if (!ids.Add(feature.Id))
            {
                errors.Add($"Uncertainty structural feature ID '{feature.Id}' is duplicated.");
            }
        }
    }

    private static void ValidateStructuralWorkItems(
        CalibrationUncertaintyStructuralFeatureReport report,
        List<string> errors)
    {
        string[] featureIds = [.. report.FeatureContract.Features.Select(feature => feature.Id)];
        HashSet<string> workItemIds = new(StringComparer.Ordinal);
        foreach (CalibrationUncertaintyStructuralWorkItemFeatures item in report.WorkItems)
        {
            string path = $"workItem[{item.WorkItemId}]";
            RequireText(item.WorkItemId, "workItem.id", errors);
            if (!workItemIds.Add(item.WorkItemId))
            {
                errors.Add($"Uncertainty structural work-item ID '{item.WorkItemId}' is duplicated.");
            }

            RequireUniqueText(item.Ecosystems, $"{path}.ecosystems", errors);
            ValidateRange(item.SourceRange, $"{path}.sourceRange", errors);
            if (item.ExpectedHours != item.SourceRange.Expected)
            {
                errors.Add($"{path}.expectedHours must equal sourceRange.expected.");
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
            RequireUniqueText(item.StructuralEvidenceIds, $"{path}.structuralEvidenceIds", errors);
            RequireUniqueText(
                item.IncompatibleStructuralEvidenceIds,
                $"{path}.incompatibleStructuralEvidenceIds",
                errors);
            if (item.ResolvedEvidenceIds.Intersect(
                    item.UnresolvedEvidenceIds,
                    StringComparer.Ordinal).Any())
            {
                errors.Add($"{path} cannot resolve and leave unresolved the same evidence ID.");
            }

            HashSet<string> resolved = item.ResolvedEvidenceIds.ToHashSet(StringComparer.Ordinal);
            if (item.StructuralEvidenceIds.Any(id => !resolved.Contains(id)) ||
                item.IncompatibleStructuralEvidenceIds.Any(id => !resolved.Contains(id)) ||
                item.StructuralEvidenceIds.Intersect(
                    item.IncompatibleStructuralEvidenceIds,
                    StringComparer.Ordinal).Any())
            {
                errors.Add(
                    $"{path} structural evidence IDs must be disjoint subsets of resolved evidence IDs.");
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

            ValidateStructuralStatus(item, path, errors);
        }
    }

    private static void ValidateStructuralStatus(
        CalibrationUncertaintyStructuralWorkItemFeatures item,
        string path,
        List<string> errors)
    {
        bool hasAvailable = item.Features.Any(feature =>
            feature.Availability == CalibrationUncertaintyFeatureAvailability.Available);
        bool hasUnavailable = item.Features.Any(feature =>
            feature.Availability == CalibrationUncertaintyFeatureAvailability.Unavailable);
        if (item.CoverageStatus == CalibrationUncertaintyStructuralCoverageStatus.Complete &&
            (!hasAvailable || hasUnavailable))
        {
            errors.Add($"{path}.coverageStatus complete requires available, non-unavailable features.");
        }

        if (item.CoverageStatus == CalibrationUncertaintyStructuralCoverageStatus.Partial &&
            !hasAvailable)
        {
            errors.Add($"{path}.coverageStatus partial requires at least one available feature.");
        }

        if (item.CoverageStatus == CalibrationUncertaintyStructuralCoverageStatus.Unavailable &&
            !hasUnavailable)
        {
            errors.Add($"{path}.coverageStatus unavailable requires an unavailable feature.");
        }

        if (item.CoverageStatus == CalibrationUncertaintyStructuralCoverageStatus.NotApplicable &&
            hasUnavailable)
        {
            errors.Add($"{path}.coverageStatus not-applicable cannot contain unavailable features.");
        }
    }

    private static void ValidateStructuralSummary(
        CalibrationUncertaintyStructuralFeatureReport report,
        List<string> errors)
    {
        CalibrationUncertaintyStructuralFeatureSummary summary = report.Summary;
        if (summary.WorkItemCount != report.WorkItems.Count ||
            summary.FeatureCount != report.FeatureContract.Features.Count ||
            summary.CompleteWorkItemCount != CountStructuralStatus(
                report,
                CalibrationUncertaintyStructuralCoverageStatus.Complete) ||
            summary.PartialWorkItemCount != CountStructuralStatus(
                report,
                CalibrationUncertaintyStructuralCoverageStatus.Partial) ||
            summary.NotApplicableWorkItemCount != CountStructuralStatus(
                report,
                CalibrationUncertaintyStructuralCoverageStatus.NotApplicable) ||
            summary.UnavailableWorkItemCount != CountStructuralStatus(
                report,
                CalibrationUncertaintyStructuralCoverageStatus.Unavailable) ||
            summary.ResolvedEvidenceReferenceCount != report.WorkItems.Sum(item =>
                item.ResolvedEvidenceIds.Count) ||
            summary.UnresolvedEvidenceReferenceCount != report.WorkItems.Sum(item =>
                item.UnresolvedEvidenceIds.Count) ||
            summary.StructuralEvidenceReferenceCount != report.WorkItems.Sum(item =>
                item.StructuralEvidenceIds.Count) ||
            summary.IncompatibleStructuralEvidenceReferenceCount != report.WorkItems.Sum(item =>
                item.IncompatibleStructuralEvidenceIds.Count))
        {
            errors.Add(
                "Uncertainty structural feature summary does not reconcile to work items and contract.");
        }
    }

    private static int CountStructuralStatus(
        CalibrationUncertaintyStructuralFeatureReport report,
        CalibrationUncertaintyStructuralCoverageStatus status) =>
        report.WorkItems.Count(item => item.CoverageStatus == status);
}
