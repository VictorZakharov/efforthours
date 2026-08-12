using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class ChangeWorkItemBuilder
{
    private static CapabilityRolePartition[] PartitionCapabilityRoleBudget(
        EffortCategory primaryCategory,
        ChangePathEvidence[] paths,
        IReadOnlyList<string> fallbackEvidenceIds,
        EffortRange hours)
    {
        if (paths.Length == 0)
        {
            return
            [
                new CapabilityRolePartition(
                    primaryCategory,
                    [],
                    [.. fallbackEvidenceIds],
                    hours),
            ];
        }

        IGrouping<EffortCategory, ChangePathEvidence>[] groups = [.. paths
            .GroupBy(path => ExplicitRoleCategory(path) ?? primaryCategory)
            .OrderBy(group => group.Key)];
        if (groups.Length == 1)
        {
            ChangePathEvidence[] onlyPaths = [.. groups[0].OrderBy(path => path.Path, StringComparer.Ordinal)];
            return
            [
                new CapabilityRolePartition(
                    groups[0].Key,
                    onlyPaths,
                    EvidenceIds(onlyPaths),
                    hours),
            ];
        }

        decimal[] weights = [.. groups.Select(group =>
            Math.Max(1, LogicalChangeUnits(group)) * RoleWeight(group.Key))];
        decimal totalWeight = weights.Sum();
        decimal usedLow = 0m;
        decimal usedExpected = 0m;
        decimal usedHigh = 0m;
        List<CapabilityRolePartition> partitions = [];
        for (int index = 0; index < groups.Length; index++)
        {
            ChangePathEvidence[] partitionPaths = [.. groups[index]
                .OrderBy(path => path.Path, StringComparer.Ordinal)];
            partitions.Add(new CapabilityRolePartition(
                groups[index].Key,
                partitionPaths,
                EvidenceIds(partitionPaths),
                new EffortRange
                {
                    Low = WeightedPart(hours.Low, weights[index], totalWeight, index, groups.Length, ref usedLow),
                    Expected = WeightedPart(
                        hours.Expected,
                        weights[index],
                        totalWeight,
                        index,
                        groups.Length,
                        ref usedExpected),
                    High = WeightedPart(
                        hours.High,
                        weights[index],
                        totalWeight,
                        index,
                        groups.Length,
                        ref usedHigh),
                }));
        }

        return [.. partitions];
    }

    private static EffortCategory? ExplicitRoleCategory(ChangePathEvidence path)
    {
        string role = path.Tags
            .FirstOrDefault(tag => tag.StartsWith("role:", StringComparison.Ordinal))?[5..] ?? string.Empty;
        return role switch
        {
            "test" => FallbackCategory(path),
            "documentation" => EffortCategory.Documentation,
            "ci-configuration" or "infrastructure" =>
                EffortCategory.CiCdAndInfrastructureAsCode,
            "container-configuration" => EffortCategory.PackagingDeploymentAndReleaseArtifacts,
            "delivery" => EffortCategory.PackagingDeploymentAndReleaseArtifacts,
            "project" or "solution" or "package-manifest" or "build-configuration" or "configuration" =>
                EffortCategory.BuildConfigurationAndDeveloperTooling,
            _ => null,
        };
    }

    private static decimal RoleWeight(EffortCategory category) => category switch
    {
        EffortCategory.UiImplementationAndRepresentedUxDecisions or
        EffortCategory.DataModelingPersistenceAndMigrations or
        EffortCategory.ExternalIntegrationsAndProtocols or
        EffortCategory.SecurityAndAccessibility => 1.25m,
        EffortCategory.UnitTesting or
        EffortCategory.IntegrationContractAndComponentTesting or
        EffortCategory.EndToEndAndUiTesting or
        EffortCategory.Documentation or
        EffortCategory.BuildConfigurationAndDeveloperTooling or
        EffortCategory.CiCdAndInfrastructureAsCode or
        EffortCategory.PackagingDeploymentAndReleaseArtifacts => 0.75m,
        _ => 1m,
    };

    private static string[] EvidenceIds(IEnumerable<ChangePathEvidence> paths) => [.. paths
        .Select(path => path.Id)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)];

    private static decimal WeightedPart(
        decimal total,
        decimal weight,
        decimal totalWeight,
        int index,
        int count,
        ref decimal used)
    {
        decimal value = index == count - 1
            ? total - used
            : decimal.Round(total * weight / totalWeight, 2, MidpointRounding.AwayFromZero);
        used += value;
        return value;
    }

    private sealed record CapabilityRolePartition(
        EffortCategory Category,
        IReadOnlyList<ChangePathEvidence> Paths,
        IReadOnlyList<string> EvidenceIds,
        EffortRange Hours);
}
