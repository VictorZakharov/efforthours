using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed record ChangeComponentDraft(
    ChangeComponentInput Input,
    EffortRange Effort,
    IReadOnlyList<CategoryEstimate> Categories,
    IReadOnlyList<ChangePathEvidence> Paths);

internal static class ChangeReconciler
{
    private static readonly HashSet<EffortCategory> SharedCategories =
    [
        EffortCategory.SpecificationComprehensionAndDomainLearning,
        EffortCategory.RepositoryAndSolutionSetup,
        EffortCategory.ArchitectureAndTechnicalDesign,
        EffortCategory.SelfReviewAndSystemIntegration,
    ];

    public static ChangeReconciliation Reconcile(
        ChangeSelection selection,
        EffortRange normalized,
        IReadOnlyList<CategoryEstimate> normalizedCategories,
        IReadOnlyList<ChangePathEvidence> normalizedPaths,
        IReadOnlyList<ChangeComponentDraft> drafts)
    {
        EffortRange isolated = ContractValidation.Sum(drafts.Select(draft => draft.Effort));
        decimal[] allocations = AllocateExpected(
            [.. drafts.Select(draft => draft.Effort.Expected)],
            normalized.Expected);
        HashSet<string> finalLogicalPaths = normalizedPaths
            .Where(path => path.Represented)
            .SelectMany(path => path.PreviousPath is null ? [path.Path] : new[] { path.Path, path.PreviousPath })
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, int> touches = drafts
            .SelectMany(draft => draft.Paths.Where(path => path.Represented))
            .SelectMany(path => path.PreviousPath is null ? [path.Path] : new[] { path.Path, path.PreviousPath })
            .GroupBy(path => path, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        string[] overlapPaths = [.. touches
            .Where(pair => pair.Value > 1)
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)];
        string[] revertedPaths = [.. drafts
            .SelectMany(draft => draft.Paths.Where(path => path.Represented))
            .Select(path => path.Path)
            .Where(path => !finalLogicalPaths.Contains(path))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        decimal isolatedShared = drafts.Sum(draft => draft.Categories
            .Where(category => SharedCategories.Contains(category.Category))
            .Sum(category => category.Hours.Expected));
        decimal normalizedShared = normalizedCategories
            .Where(category => SharedCategories.Contains(category.Category))
            .Sum(category => category.Hours.Expected);
        decimal sharedExcess = Math.Max(0m, isolatedShared - normalizedShared);
        SignedEffortRange delta = new()
        {
            Low = normalized.Low - isolated.Low,
            Expected = normalized.Expected - isolated.Expected,
            High = normalized.High - isolated.High,
        };

        List<Cause> causes = [];
        if (delta.Expected < 0m && sharedExcess > 0m)
        {
            causes.Add(new Cause(
                ChangeAdjustmentKind.SharedSetup,
                sharedExcess,
                "Repeated specification, architecture/setup, or review capability work in isolated " +
                    "components is shared by the coherent final delta."));
        }

        if (delta.Expected < 0m && overlapPaths.Length > 0)
        {
            causes.Add(new Cause(
                ChangeAdjustmentKind.Overlap,
                overlapPaths.Length,
                $"{overlapPaths.Length} path(s) are touched by more than one isolated component; " +
                    "overlapping final work is normalized once."));
        }

        if (delta.Expected < 0m && revertedPaths.Length > 0)
        {
            causes.Add(new Cause(
                ChangeAdjustmentKind.Revert,
                revertedPaths.Length,
                $"{revertedPaths.Length} component path effect(s) are absent from the final normalized delta; " +
                    "discarded or reverted states do not create represented EHE."));
        }

        ChangeComponentEstimate[] components = [.. drafts.Select((draft, index) =>
        {
            string id = ComponentId(selection, draft.Input, index);
            HashSet<string> componentPaths = draft.Paths
                .Select(path => path.Path)
                .ToHashSet(StringComparer.Ordinal);
            string[] evidenceIds = [.. normalizedPaths
                .Where(path => componentPaths.Contains(path.Path) ||
                    (path.PreviousPath is not null && componentPaths.Contains(path.PreviousPath)))
                .Select(path => path.Id)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];
            return new ChangeComponentEstimate
            {
                Id = id,
                Kind = draft.Input.Kind,
                Selector = draft.Input.Selector,
                BaseObjectId = draft.Input.BaseObjectId,
                HeadObjectId = draft.Input.HeadObjectId,
                IsolatedEffort = draft.Effort,
                AllocatedExpectedHours = allocations[index],
                EvidenceIds = evidenceIds,
            };
        })];
        ChangeAdjustment[] adjustments = BuildAdjustments(
            selection,
            delta,
            causes,
            components,
            normalizedPaths,
            overlapPaths,
            revertedPaths);
        ChangeNormalizationSummary? normalization = ChangeNormalizationCalculator.Calculate(
            selection,
            isolated,
            normalized,
            components,
            adjustments);
        decimal tolerance = Math.Max(1m, decimal.Round(
            isolated.Expected * 0.10m,
            2,
            MidpointRounding.AwayFromZero));
        decimal expectedDifference = normalized.Expected - isolated.Expected;
        string assessment = Math.Abs(expectedDifference) <= tolerance
            ? "within-no-rework-tolerance"
            : expectedDifference < 0m
                ? "normalized-below-isolated-components"
                : "normalized-above-isolated-components";
        return new ChangeReconciliation
        {
            IsolatedComponentSum = isolated,
            NormalizedEffort = normalized,
            AdditivityToleranceHours = tolerance,
            ExpectedDifferenceHours = expectedDifference,
            Assessment = assessment,
            AllocationMethod = "Proportional to isolated expected Change EHE with deterministic cent-level residual assignment; allocations are attribution, not historical hours.",
            Components = components,
            Adjustments = adjustments,
            Normalization = normalization,
        };
    }

    private static ChangeAdjustment[] BuildAdjustments(
        ChangeSelection selection,
        SignedEffortRange delta,
        IReadOnlyList<Cause> structuralCauses,
        IReadOnlyList<ChangeComponentEstimate> components,
        IReadOnlyList<ChangePathEvidence> normalizedPaths,
        IReadOnlyCollection<string> overlapPaths,
        IReadOnlyCollection<string> revertedPaths)
    {
        if (delta.Low == 0m && delta.Expected == 0m && delta.High == 0m)
        {
            return [];
        }

        List<Cause> causes = [.. structuralCauses];
        causes.Add(new Cause(
            ChangeAdjustmentKind.Interaction,
            structuralCauses.Count == 0 ? 1m : Math.Max(1m, structuralCauses.Sum(cause => cause.Weight) * 0.25m),
            "Residual interaction reconciles the independently estimated components with the authoritative " +
                "coherent final base-to-head artifact delta."));
        decimal totalWeight = causes.Sum(cause => cause.Weight);
        decimal usedLow = 0m;
        decimal usedExpected = 0m;
        decimal usedHigh = 0m;
        List<ChangeAdjustment> adjustments = [];
        for (int index = 0; index < causes.Count; index++)
        {
            Cause cause = causes[index];
            bool last = index == causes.Count - 1;
            decimal low = last
                ? delta.Low - usedLow
                : decimal.Round(delta.Low * cause.Weight / totalWeight, 2, MidpointRounding.AwayFromZero);
            decimal expected = last
                ? delta.Expected - usedExpected
                : decimal.Round(delta.Expected * cause.Weight / totalWeight, 2, MidpointRounding.AwayFromZero);
            decimal high = last
                ? delta.High - usedHigh
                : decimal.Round(delta.High * cause.Weight / totalWeight, 2, MidpointRounding.AwayFromZero);
            usedLow += low;
            usedExpected += expected;
            usedHigh += high;
            HashSet<string> causePaths = cause.Kind switch
            {
                ChangeAdjustmentKind.Overlap => overlapPaths.ToHashSet(StringComparer.Ordinal),
                ChangeAdjustmentKind.Revert => revertedPaths.ToHashSet(StringComparer.Ordinal),
                _ => [],
            };
            string[] evidenceIds = causePaths.Count == 0
                ? []
                : [.. normalizedPaths
                    .Where(path => causePaths.Contains(path.Path) ||
                        (path.PreviousPath is not null && causePaths.Contains(path.PreviousPath)))
                    .Select(path => path.Id)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)];
            adjustments.Add(new ChangeAdjustment
            {
                Id = AdjustmentId(selection, cause.Kind, index),
                Kind = cause.Kind,
                EffortDelta = new SignedEffortRange { Low = low, Expected = expected, High = high },
                Reason = cause.Reason +
                    " Point-level attribution is a transparent structural reconciliation heuristic, not calibrated causal accounting.",
                ComponentIds = [.. components.Select(component => component.Id).Order(StringComparer.Ordinal)],
                EvidenceIds = evidenceIds,
            });
        }

        return [.. adjustments];
    }

    private static decimal[] AllocateExpected(decimal[] isolated, decimal normalized)
    {
        decimal[] allocations = new decimal[isolated.Length];
        if (isolated.Length == 0 || normalized == 0m)
        {
            return allocations;
        }

        decimal total = isolated.Sum();
        decimal[] remainders = new decimal[isolated.Length];
        for (int index = 0; index < isolated.Length; index++)
        {
            decimal raw = total > 0m
                ? normalized * isolated[index] / total
                : normalized / isolated.Length;
            decimal floor = decimal.Floor(raw * 100m) / 100m;
            allocations[index] = floor;
            remainders[index] = raw - floor;
        }

        decimal residual = normalized - allocations.Sum();
        foreach (int index in Enumerable.Range(0, isolated.Length)
                     .OrderByDescending(index => remainders[index])
                     .ThenBy(index => index))
        {
            if (residual <= 0m)
            {
                break;
            }

            decimal increment = Math.Min(0.01m, residual);
            allocations[index] += increment;
            residual -= increment;
        }

        if (residual > 0m)
        {
            allocations[0] += residual;
        }

        return allocations;
    }

    private static string ComponentId(
        ChangeSelection selection,
        ChangeComponentInput input,
        int index) =>
        StableId(
            "change-component",
            selection.Base.ObjectId,
            selection.Head.ObjectId,
            input.BaseObjectId,
            input.HeadObjectId,
            input.Kind.ToString(),
            index.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static string AdjustmentId(
        ChangeSelection selection,
        ChangeAdjustmentKind kind,
        int index) =>
        StableId(
            "change-adjustment",
            selection.Base.ObjectId,
            selection.Head.ObjectId,
            kind.ToString(),
            index.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static string StableId(string prefix, params string[] values)
    {
        string digest = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\n', values))))
            .ToLowerInvariant();
        return $"{prefix}:{digest[..20]}";
    }

    private sealed record Cause(ChangeAdjustmentKind Kind, decimal Weight, string Reason);
}
