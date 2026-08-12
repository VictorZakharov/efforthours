using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class ChangeWorkItemBuilder
{
    private static Dictionary<string, Capability> Capabilities(
        EstimateReport report,
        Dictionary<string, EvidenceFact> facts) => report.WorkItems
        .GroupBy(item => CapabilityId(item.Id), StringComparer.Ordinal)
        .ToDictionary(
            group => group.Key,
            group =>
            {
                WorkItem first = group.OrderBy(item => item.Id, StringComparer.Ordinal).First();
                string[] evidenceIds = [.. group
                    .SelectMany(item => item.EvidenceIds)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)];
                HashSet<string> paths = evidenceIds
                    .SelectMany(id => facts.TryGetValue(id, out EvidenceFact? fact) ? FactPaths(fact) : [])
                    .Distinct(StringComparer.Ordinal)
                    .ToHashSet(StringComparer.Ordinal);
                return new Capability(
                    group.Key,
                    first.Category,
                    StripPartTitle(first.Title),
                    first.Scope,
                    first.Complexity,
                    ContractValidation.Sum(group.Select(item => item.Hours)),
                    group.Min(item => item.Confidence),
                    paths,
                    evidenceIds,
                    [.. group.SelectMany(item => item.UncertaintyReasons)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)]);
            },
            StringComparer.Ordinal);

    private static IEnumerable<string> FactPaths(EvidenceFact fact)
    {
        foreach (EvidenceLocation location in fact.Locations)
        {
            yield return NormalizePath(location.Path);
        }

        if (fact.Kind == EvidenceKinds.File)
        {
            yield return NormalizePath(fact.Scope);
        }
    }

    private static bool Touches(IReadOnlySet<string> capabilityPaths, ChangePathEvidence path) =>
        capabilityPaths.Contains(path.Path) ||
        (path.PreviousPath is not null && capabilityPaths.Contains(path.PreviousPath));

    private static EffortRange PositiveDifference(EffortRange baseHours, EffortRange headHours)
    {
        decimal expected = Math.Max(0m, headHours.Expected - baseHours.Expected);
        decimal low = Math.Clamp(headHours.Low - baseHours.Low, 0m, expected);
        decimal high = Math.Max(expected, headHours.High - baseHours.High);
        return new EffortRange { Low = low, Expected = expected, High = high };
    }

    private static EffortRange RemovalRange(decimal removedCapabilityExpected)
    {
        decimal expected = RoundQuarter(Math.Clamp(removedCapabilityExpected * 0.25m, 0.5m, 16m));
        return RangeFromExpected(expected, 0.62m);
    }

    private static EffortRange ModificationRange(
        EffortCategory category,
        ChangePathEvidence[] paths)
    {
        decimal factor = category switch
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
            EffortCategory.SpecificationComprehensionAndDomainLearning or
            EffortCategory.RepositoryAndSolutionSetup or
            EffortCategory.ArchitectureAndTechnicalDesign or
            EffortCategory.ManualValidationDebuggingAndHardening or
            EffortCategory.SelfReviewAndSystemIntegration => 0.5m,
            _ => 1m,
        };
        decimal statusFactor = paths.Max(path => StatusFactor(path.Status));
        decimal marginalFactor = factor * statusFactor;
        int logicalUnits = LogicalChangeUnits(paths);
        decimal raw = MarginalPathHours(
            logicalUnits,
            marginalFactor,
            marginalFactor * 0.5m,
            marginalFactor * 0.15m,
            8m);
        decimal expected = RoundQuarter(Math.Clamp(raw, 0.5m, 8m));
        return RangeFromExpected(expected, 0.7m);
    }

    private static EffortRange FallbackRange(
        ChangePathEvidence[] paths,
        EffortCategory category)
    {
        decimal factor = category switch
        {
            EffortCategory.DataModelingPersistenceAndMigrations or
            EffortCategory.ExternalIntegrationsAndProtocols or
            EffortCategory.SecurityAndAccessibility => 1.25m,
            EffortCategory.Documentation => 0.5m,
            EffortCategory.UnitTesting or
            EffortCategory.IntegrationContractAndComponentTesting or
            EffortCategory.EndToEndAndUiTesting => 0.75m,
            _ => 1m,
        };
        decimal statusFactor = StatusFactor(paths[0].Status);
        int logicalUnits = LogicalChangeUnits(paths);
        decimal expected = RoundQuarter(MarginalPathHours(
            logicalUnits,
            factor * statusFactor,
            factor * 0.5m * statusFactor,
            factor * 0.15m * statusFactor,
            8m));
        return RangeFromExpected(Math.Max(0.5m, expected), 0.58m);
    }

    private static int LogicalChangeUnits(IEnumerable<ChangePathEvidence> paths) => paths.Sum(path =>
        path.EditRegions switch
        {
            <= 1 => 1,
            <= 3 => 2,
            <= 15 => 3,
            _ => 4,
        });

    private static EffortCategory FallbackCategory(ChangePathEvidence path)
    {
        string role = path.Tags
            .FirstOrDefault(tag => tag.StartsWith("role:", StringComparison.Ordinal))?[5..] ?? string.Empty;
        string sqlRole = path.Tags
            .FirstOrDefault(tag => tag.StartsWith("sql-role:", StringComparison.Ordinal))?[9..] ?? string.Empty;
        string scriptRole = path.Tags
            .FirstOrDefault(tag => tag.StartsWith("script-role:", StringComparison.Ordinal))?[12..] ?? string.Empty;
        string lowerPath = path.Path.ToLowerInvariant();
        if (sqlRole == "test-fixture")
        {
            return EffortCategory.IntegrationContractAndComponentTesting;
        }

        if (sqlRole == "delivery")
        {
            return EffortCategory.PackagingDeploymentAndReleaseArtifacts;
        }

        if (scriptRole == "delivery")
        {
            return EffortCategory.PackagingDeploymentAndReleaseArtifacts;
        }

        if (scriptRole is "ci" or "infrastructure")
        {
            return EffortCategory.CiCdAndInfrastructureAsCode;
        }

        if (scriptRole == "build")
        {
            return EffortCategory.BuildConfigurationAndDeveloperTooling;
        }

        if (scriptRole == "test")
        {
            return lowerPath.Contains("integration", StringComparison.Ordinal) ||
                lowerPath.Contains("smoke", StringComparison.Ordinal)
                    ? EffortCategory.IntegrationContractAndComponentTesting
                    : EffortCategory.UnitTesting;
        }

        if (role == "test")
        {
            if (lowerPath.Contains("e2e", StringComparison.Ordinal) ||
                lowerPath.Contains("endtoend", StringComparison.Ordinal) ||
                lowerPath.Contains("playwright", StringComparison.Ordinal) ||
                lowerPath.Contains("cypress", StringComparison.Ordinal))
            {
                return EffortCategory.EndToEndAndUiTesting;
            }

            return lowerPath.Contains("integration", StringComparison.Ordinal)
                ? EffortCategory.IntegrationContractAndComponentTesting
                : EffortCategory.UnitTesting;
        }

        if (role == "documentation")
        {
            return EffortCategory.Documentation;
        }

        if (role is "ci-configuration" or "infrastructure" or "container-configuration")
        {
            return EffortCategory.CiCdAndInfrastructureAsCode;
        }

        if (role is "project" or "solution" or "package-manifest" or "build-configuration" or "configuration")
        {
            return EffortCategory.BuildConfigurationAndDeveloperTooling;
        }
        if (role == "delivery")
        {
            return EffortCategory.PackagingDeploymentAndReleaseArtifacts;
        }

        if (lowerPath.Contains("migration", StringComparison.Ordinal))
        {
            return EffortCategory.DataModelingPersistenceAndMigrations;
        }

        if (Path.GetExtension(lowerPath).Equals(".sql", StringComparison.OrdinalIgnoreCase))
        {
            return EffortCategory.DataModelingPersistenceAndMigrations;
        }

        return EffortCategory.ProductionImplementation;
    }

    private static string FallbackTitle(
        ChangePathEvidence[] paths,
        EffortCategory category)
    {
        ChangePathEvidence path = paths[0];
        string verb = path.Status switch
        {
            ChangePathStatus.Added => "Add",
            ChangePathStatus.Modified => "Modify",
            ChangePathStatus.Removed => "Remove",
            ChangePathStatus.Moved => "Move",
            _ => "Change",
        };
        return paths.Length == 1
            ? $"{verb} maintained artifact '{path.Path}'"
            : $"{verb} {paths.Length} correlated {CategoryName(category)} artifacts";
    }

    private static string CategoryName(EffortCategory category) => category switch
    {
        EffortCategory.ProductionImplementation => "production",
        EffortCategory.UnitTesting => "unit-test",
        EffortCategory.IntegrationContractAndComponentTesting => "integration-test",
        EffortCategory.EndToEndAndUiTesting => "end-to-end-test",
        EffortCategory.Documentation => "documentation",
        EffortCategory.BuildConfigurationAndDeveloperTooling => "build-configuration",
        EffortCategory.CiCdAndInfrastructureAsCode => "delivery-configuration",
        _ => "maintained",
    };

    private static List<WorkItem> CreateItems(
        ChangeSelection selection,
        string rule,
        string capabilityIdentity,
        EffortCategory category,
        string title,
        string scope,
        ComplexityLevel complexity,
        EffortRange hours,
        decimal confidence,
        string reason,
        IReadOnlyList<string> evidenceIds,
        EstimationProfile profile,
        decimal quantity,
        IReadOnlyList<string> uncertaintyReasons)
    {
        int partCount = hours.Expected <= 1.5m
            ? 1
            : Math.Max(2, (int)decimal.Ceiling(hours.Expected));
        while (hours.Expected / partCount > 8m)
        {
            partCount++;
        }

        string capabilityId = WorkItemId(selection, rule, capabilityIdentity, category, scope);
        List<WorkItem> items = [];
        decimal usedLow = 0m;
        decimal usedExpected = 0m;
        decimal usedHigh = 0m;
        for (int index = 0; index < partCount; index++)
        {
            decimal low = Part(hours.Low, partCount, index, ref usedLow);
            decimal expected = Part(hours.Expected, partCount, index, ref usedExpected);
            decimal high = Part(hours.High, partCount, index, ref usedHigh);
            low = Math.Min(low, expected);
            high = Math.Max(high, expected);
            (string partTitle, string partReason) = DescribeLogicalPart(
                category,
                title,
                reason,
                index,
                partCount);
            items.Add(new WorkItem
            {
                Id = partCount == 1 ? capabilityId : $"{capabilityId}:part-{index + 1:D4}",
                Category = category,
                Title = partTitle,
                Scope = string.IsNullOrWhiteSpace(scope) ? "." : scope,
                EvidenceIds = [.. evidenceIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
                Quantity = Math.Max(0.01m, quantity / partCount),
                Complexity = complexity,
                Hours = new EffortRange { Low = low, Expected = expected, High = high },
                Confidence = Math.Clamp(confidence, 0m, 1m),
                Reason = partReason,
                Estimator = new EstimatorReference
                {
                    Id = $"change-rule:{rule}",
                    Version = EstimatorVersion,
                    Kind = EstimatorKind.Rule,
                },
                Profiles = [profile],
                CorrelationGroup = $"change:{rule}:{scope}",
                Assumptions =
                [
                    "The final base-to-head artifact delta is valued once with modern 2026-equivalent technology.",
                    "Commit count, elapsed time, authorship, messages, and intermediate churn are not effort multipliers.",
                ],
                Exclusions =
                [
                    "Historical rework and discarded intermediate states are excluded.",
                ],
                UncertaintyReasons = [.. uncertaintyReasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            });
        }

        return items;
    }

    private static decimal Part(decimal total, int count, int index, ref decimal used)
    {
        decimal value = index == count - 1
            ? total - used
            : decimal.Round(total / count, 2, MidpointRounding.AwayFromZero);
        used += value;
        return value;
    }

    private static EffortRange RangeFromExpected(decimal expected, decimal confidence)
    {
        decimal uncertainty = 1m - Math.Clamp(confidence, 0m, 1m);
        decimal lowFactor = 0.65m - uncertainty * 0.25m;
        decimal highFactor = 1.55m + uncertainty * 1.25m;
        decimal low = Math.Min(expected, RoundQuarter(expected * lowFactor));
        decimal high = Math.Max(expected, RoundQuarter(expected * highFactor));
        return new EffortRange { Low = low, Expected = expected, High = high };
    }

    private static decimal MarginalPathHours(
        int quantity,
        decimal firstTier,
        decimal secondTier,
        decimal remainingTier,
        decimal cap)
    {
        int firstCount = Math.Min(quantity, 2);
        int secondCount = Math.Min(Math.Max(0, quantity - firstCount), 6);
        int remainingCount = Math.Max(0, quantity - firstCount - secondCount);
        return Math.Min(
            cap,
            firstCount * firstTier + secondCount * secondTier + remainingCount * remainingTier);
    }

    private static string WorkItemId(
        ChangeSelection selection,
        string rule,
        string capabilityIdentity,
        EffortCategory category,
        string scope)
    {
        string identity = string.Join(
            '\n',
            selection.Base.ObjectId,
            selection.Head.ObjectId,
            rule,
            capabilityIdentity,
            category,
            scope);
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant();
        return $"change-item:{rule}:{digest[..20]}";
    }

    private static string CapabilityId(string workItemId)
    {
        int marker = workItemId.LastIndexOf(":part-", StringComparison.Ordinal);
        return marker > 0 && int.TryParse(workItemId.AsSpan(marker + 6), out int part) && part > 0
            ? workItemId[..marker]
            : workItemId;
    }

    private static string StripPartTitle(string title)
    {
        int marker = title.LastIndexOf(" (part ", StringComparison.Ordinal);
        return marker > 0 && title.EndsWith(')') ? title[..marker] : title;
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static decimal RoundQuarter(decimal value) =>
        decimal.Round(value * 4m, 0, MidpointRounding.AwayFromZero) / 4m;

    private static EffortRange Zero() => new() { Low = 0m, Expected = 0m, High = 0m };

    private sealed record Capability(
        string Id,
        EffortCategory Category,
        string Title,
        string Scope,
        ComplexityLevel Complexity,
        EffortRange Hours,
        decimal Confidence,
        IReadOnlySet<string> Paths,
        IReadOnlyList<string> EvidenceIds,
        IReadOnlyList<string> UncertaintyReasons);
}
