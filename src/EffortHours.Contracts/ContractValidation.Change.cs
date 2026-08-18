using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(ChangeEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        List<string> errors = [];
        RequireVersion(evidence.SchemaVersion, "change evidence", errors);
        ValidateChangeSelection(evidence.Selection, errors);
        RequireText(evidence.Repository.Name, "repository.name", errors);
        RequireText(evidence.Repository.Scope, "repository.scope", errors);
        RequireText(evidence.BaseEvidenceDigest, "baseEvidenceDigest", errors);
        RequireText(evidence.HeadEvidenceDigest, "headEvidenceDigest", errors);
        if (evidence.UnchangedContextPathCount < 0)
        {
            errors.Add("unchangedContextPathCount cannot be negative.");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> paths = new(StringComparer.Ordinal);
        foreach (ChangePathEvidence path in evidence.Paths)
        {
            RequireText(path.Id, "changePath.id", errors);
            RequireText(path.Path, $"changePath[{path.Id}].path", errors);
            RequireText(path.Reason, $"changePath[{path.Id}].reason", errors);
            if (!ids.Add(path.Id))
            {
                errors.Add($"Change-path evidence ID '{path.Id}' is duplicated.");
            }

            if (!paths.Add(path.Path))
            {
                errors.Add($"Changed destination path '{path.Path}' is duplicated.");
            }

            if (path.EditRegions < 0)
            {
                errors.Add($"Change path '{path.Id}' has a negative edit-region count.");
            }

            if (path.Represented && path.Classification != ChangePathClassification.Represented)
            {
                errors.Add($"Change path '{path.Id}' is represented but has exclusion classification '{path.Classification}'.");
            }

            if (!path.Represented && path.Classification == ChangePathClassification.Represented)
            {
                errors.Add($"Change path '{path.Id}' is excluded but has represented classification.");
            }

            if (path.Represented && path.EditRegions == 0)
            {
                errors.Add($"Represented change path '{path.Id}' must contain at least one edit region.");
            }

            switch (path.Status)
            {
                case ChangePathStatus.Added when path.BaseObjectId is not null || path.HeadObjectId is null:
                    errors.Add($"Added path '{path.Id}' must have only a head object ID.");
                    break;
                case ChangePathStatus.Removed when path.BaseObjectId is null || path.HeadObjectId is not null:
                    errors.Add($"Removed path '{path.Id}' must have only a base object ID.");
                    break;
                case ChangePathStatus.Modified when path.BaseObjectId is null || path.HeadObjectId is null:
                    errors.Add($"Modified path '{path.Id}' must have base and head object IDs.");
                    break;
                case ChangePathStatus.Moved when
                    path.BaseObjectId is null || path.HeadObjectId is null ||
                    string.IsNullOrWhiteSpace(path.PreviousPath):
                    errors.Add($"Moved path '{path.Id}' must have base/head object IDs and a previous path.");
                    break;
            }
        }

        ValidatePullRequestVerification(evidence, errors);

        return errors;
    }

    public static IReadOnlyList<string> Validate(ChangeEstimateReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> errors = [.. Validate(report.Evidence)];
        RequireVersion(report.SchemaVersion, "change estimate report", errors);
        RequireVersion(report.ChangeEvidenceSchemaVersion, "change estimate evidence", errors);
        RequireText(report.EstimatorVersion, "estimatorVersion", errors);
        RequireText(report.SourceEstimatorVersion, "sourceEstimatorVersion", errors);
        if (report.Selection != report.Evidence.Selection)
        {
            errors.Add("The report selection does not equal the embedded change-evidence selection.");
        }

        ValidateRange(report.TotalEffort, "totalEffort", errors);
        HashSet<string> evidenceIds = report.Evidence.Paths
            .Select(path => path.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> itemIds = new(StringComparer.Ordinal);
        foreach (WorkItem item in report.WorkItems.Concat(report.ProfessionalizationGap))
        {
            RequireVersion(item.SchemaVersion, $"work item '{item.Id}'", errors);
            RequireText(item.Id, "workItem.id", errors);
            RequireText(item.Title, $"workItem[{item.Id}].title", errors);
            RequireText(item.Scope, $"workItem[{item.Id}].scope", errors);
            RequireText(item.Reason, $"workItem[{item.Id}].reason", errors);
            ValidateRange(item.Hours, $"workItem[{item.Id}].hours", errors);
            if (item.Quantity <= 0m)
            {
                errors.Add($"Work item '{item.Id}' quantity must be positive.");
            }

            if (item.Confidence is < 0m or > 1m)
            {
                errors.Add($"Work item '{item.Id}' confidence must be between 0 and 1.");
            }

            if (!itemIds.Add(item.Id))
            {
                errors.Add($"Work item ID '{item.Id}' is duplicated.");
            }

            foreach (string evidenceId in item.EvidenceIds)
            {
                if (!evidenceIds.Contains(evidenceId))
                {
                    errors.Add($"Work item '{item.Id}' references unknown change evidence '{evidenceId}'.");
                }
            }
        }

        if (Sum(report.WorkItems.Select(item => item.Hours)) != report.TotalEffort)
        {
            errors.Add("The change total does not equal the sum of represented work items.");
        }

        if (Sum(report.Categories.Select(category => category.Hours)) != report.TotalEffort)
        {
            errors.Add("The change total does not equal the sum of category estimates.");
        }

        Dictionary<EffortCategory, EffortRange> expectedCategories = report.WorkItems
            .GroupBy(item => item.Category)
            .ToDictionary(group => group.Key, group => Sum(group.Select(item => item.Hours)));
        HashSet<EffortCategory> categoryIds = [];
        foreach (CategoryEstimate category in report.Categories)
        {
            ValidateRange(category.Hours, $"category[{category.Category}].hours", errors);
            if (!categoryIds.Add(category.Category))
            {
                errors.Add($"Change category '{category.Category}' is duplicated.");
            }

            if (!expectedCategories.TryGetValue(category.Category, out EffortRange? expected) ||
                expected != category.Hours)
            {
                errors.Add($"Change category '{category.Category}' does not equal its work-item sum.");
            }
        }

        foreach (EffortCategory category in expectedCategories.Keys.Except(categoryIds))
        {
            errors.Add($"Change category '{category}' is missing from the category ledger.");
        }

        ChangeReconciliation reconciliation = report.Reconciliation;
        ValidateRange(reconciliation.IsolatedComponentSum, "reconciliation.isolatedComponentSum", errors);
        ValidateRange(reconciliation.NormalizedEffort, "reconciliation.normalizedEffort", errors);
        if (reconciliation.NormalizedEffort != report.TotalEffort)
        {
            errors.Add("Reconciliation normalized effort does not equal the report total.");
        }

        if (reconciliation.AdditivityToleranceHours < 0m)
        {
            errors.Add("Reconciliation additivity tolerance cannot be negative.");
        }

        decimal expectedTolerance = Math.Max(1m, decimal.Round(
            reconciliation.IsolatedComponentSum.Expected * 0.10m,
            2,
            MidpointRounding.AwayFromZero));
        if (reconciliation.AdditivityToleranceHours != expectedTolerance)
        {
            errors.Add("Reconciliation additivity tolerance is inconsistent with the v1 rule.");
        }

        RequireText(reconciliation.Assessment, "reconciliation.assessment", errors);
        RequireText(reconciliation.AllocationMethod, "reconciliation.allocationMethod", errors);
        EffortRange componentSum = Sum(reconciliation.Components.Select(component => component.IsolatedEffort));
        if (componentSum != reconciliation.IsolatedComponentSum)
        {
            errors.Add("Reconciliation component estimates do not equal the isolated component sum.");
        }

        decimal allocationSum = reconciliation.Components.Sum(component => component.AllocatedExpectedHours);
        if (allocationSum != report.TotalEffort.Expected)
        {
            errors.Add("Per-component expected allocations do not equal normalized expected effort.");
        }

        if (reconciliation.Components.Any(component => component.AllocatedExpectedHours < 0m))
        {
            errors.Add("Per-component expected allocations cannot be negative.");
        }

        HashSet<string> componentIds = new(StringComparer.Ordinal);
        foreach (ChangeComponentEstimate component in reconciliation.Components)
        {
            RequireText(component.Id, "reconciliation.component.id", errors);
            RequireText(component.Selector, $"reconciliation.component[{component.Id}].selector", errors);
            RequireText(component.BaseObjectId, $"reconciliation.component[{component.Id}].baseObjectId", errors);
            RequireText(component.HeadObjectId, $"reconciliation.component[{component.Id}].headObjectId", errors);
            ValidateRange(component.IsolatedEffort, $"reconciliation.component[{component.Id}].isolatedEffort", errors);
            if (!componentIds.Add(component.Id))
            {
                errors.Add($"Change component ID '{component.Id}' is duplicated.");
            }


            foreach (string evidenceId in component.EvidenceIds.Where(id => !evidenceIds.Contains(id)))
            {
                errors.Add($"Change component '{component.Id}' references unknown evidence '{evidenceId}'.");
            }
        }

        if (reconciliation.Components.Count == 0)
        {
            errors.Add("Change reconciliation must contain at least one component.");
        }

        HashSet<string> adjustmentIds = new(StringComparer.Ordinal);
        foreach (ChangeAdjustment adjustment in reconciliation.Adjustments)
        {
            RequireText(adjustment.Id, "reconciliation.adjustment.id", errors);
            RequireText(adjustment.Reason, $"reconciliation.adjustment[{adjustment.Id}].reason", errors);
            if (!adjustmentIds.Add(adjustment.Id))
            {
                errors.Add($"Change adjustment ID '{adjustment.Id}' is duplicated.");
            }

            foreach (string componentId in adjustment.ComponentIds.Where(id => !componentIds.Contains(id)))
            {
                errors.Add($"Change adjustment '{adjustment.Id}' references unknown component '{componentId}'.");
            }

            foreach (string evidenceId in adjustment.EvidenceIds.Where(id => !evidenceIds.Contains(id)))
            {
                errors.Add($"Change adjustment '{adjustment.Id}' references unknown evidence '{evidenceId}'.");
            }
        }

        decimal adjustmentLow = reconciliation.Adjustments.Sum(adjustment => adjustment.EffortDelta.Low);
        decimal adjustmentExpected = reconciliation.Adjustments.Sum(adjustment => adjustment.EffortDelta.Expected);
        decimal adjustmentHigh = reconciliation.Adjustments.Sum(adjustment => adjustment.EffortDelta.High);
        if (reconciliation.IsolatedComponentSum.Low + adjustmentLow != report.TotalEffort.Low ||
            reconciliation.IsolatedComponentSum.Expected + adjustmentExpected != report.TotalEffort.Expected ||
            reconciliation.IsolatedComponentSum.High + adjustmentHigh != report.TotalEffort.High)
        {
            errors.Add("Reconciliation adjustments do not bridge isolated and normalized effort.");
        }

        if (reconciliation.ExpectedDifferenceHours !=
            report.TotalEffort.Expected - reconciliation.IsolatedComponentSum.Expected)
        {
            errors.Add("Reconciliation expectedDifferenceHours is inconsistent.");
        }

        string expectedAssessment = Math.Abs(reconciliation.ExpectedDifferenceHours) <=
            reconciliation.AdditivityToleranceHours
                ? "within-no-rework-tolerance"
                : reconciliation.ExpectedDifferenceHours < 0m
                    ? "normalized-below-isolated-components"
                    : "normalized-above-isolated-components";
        if (!string.Equals(reconciliation.Assessment, expectedAssessment, StringComparison.Ordinal))
        {
            errors.Add("Reconciliation assessment is inconsistent with its expected difference and tolerance.");
        }

        ValidateChangeNormalization(report, componentIds, adjustmentIds, errors);

        ValidateRateAndCost(report.RateCard, report.TotalCost, report.TotalEffort, "totalCost", errors);
        return errors;
    }

    private static void ValidateChangeSelection(ChangeSelection selection, List<string> errors)
    {
        RequireVersion(selection.SchemaVersion, "change selection", errors);
        RequireText(selection.Base.Selector, "selection.base.selector", errors);
        RequireText(selection.Base.ObjectId, "selection.base.objectId", errors);
        RequireText(selection.Head.Selector, "selection.head.selector", errors);
        RequireText(selection.Head.ObjectId, "selection.head.objectId", errors);

        switch (selection.Kind)
        {
            case ChangeSelectionKind.BaseHead:
                if (selection.Commit is not null || selection.Parent is not null ||
                    selection.Range is not null || selection.PullRequest is not null)
                {
                    errors.Add("A base-head selection cannot contain commit, parent, range, or pull-request metadata.");
                }

                break;

            case ChangeSelectionKind.Commit:
                RequireText(selection.Commit, "selection.commit", errors);
                if (selection.Range is not null || selection.PullRequest is not null)
                {
                    errors.Add("A commit selection cannot contain range or pull-request metadata.");
                }

                break;

            case ChangeSelectionKind.Range:
                RequireText(selection.Range, "selection.range", errors);
                if (selection.Commit is not null || selection.Parent is not null ||
                    selection.PullRequest is not null)
                {
                    errors.Add("A range selection cannot contain commit, parent, or pull-request metadata.");
                }

                break;

            case ChangeSelectionKind.PullRequest:
                if (selection.PullRequest is null)
                {
                    errors.Add("A pull-request selection requires pullRequest metadata.");
                }
                else
                {
                    PullRequestReference pullRequest = selection.PullRequest;
                    RequireText(pullRequest.Input, "selection.pullRequest.input", errors);
                    if (pullRequest.Number <= 0)
                    {
                        errors.Add("selection.pullRequest.number must be positive.");
                    }

                    if (pullRequest.ProviderChangedFileCount < 0)
                    {
                        errors.Add("selection.pullRequest.providerChangedFileCount cannot be negative.");
                    }

                    bool hasComparisonProvenance = pullRequest.ProviderBaseObjectId is not null ||
                        pullRequest.ComparisonBasePolicy is not null ||
                        pullRequest.ObjectAcquisition is not null;
                    if (hasComparisonProvenance)
                    {
                        RequireText(
                            pullRequest.ProviderBaseObjectId,
                            "selection.pullRequest.providerBaseObjectId",
                            errors);
                        if (pullRequest.ComparisonBasePolicy is null ||
                            pullRequest.ObjectAcquisition is null)
                        {
                            errors.Add(
                                "Pull-request comparison provenance requires its provider base, policy, and object-acquisition mode together.");
                        }
                    }
                }

                if (selection.Commit is not null || selection.Parent is not null || selection.Range is not null)
                {
                    errors.Add("A pull-request selection cannot contain commit, parent, or range metadata.");
                }

                break;
        }
    }

}
