using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static class SnapshotCategoryGrouping
{
    public static string Group(EffortCategory category) => category switch
    {
        EffortCategory.SpecificationComprehensionAndDomainLearning or EffortCategory.ArchitectureAndTechnicalDesign => "design",
        EffortCategory.RepositoryAndSolutionSetup or EffortCategory.BuildConfigurationAndDeveloperTooling or
            EffortCategory.CiCdAndInfrastructureAsCode or EffortCategory.PackagingDeploymentAndReleaseArtifacts => "delivery",
        EffortCategory.UnitTesting or EffortCategory.IntegrationContractAndComponentTesting or EffortCategory.EndToEndAndUiTesting or
            EffortCategory.ManualValidationDebuggingAndHardening or EffortCategory.SelfReviewAndSystemIntegration => "validation",
        EffortCategory.Documentation => "documentation",
        EffortCategory.ProductionImplementation or EffortCategory.UiImplementationAndRepresentedUxDecisions or
            EffortCategory.DataModelingPersistenceAndMigrations or EffortCategory.ExternalIntegrationsAndProtocols or
            EffortCategory.SecurityAndAccessibility => "implementation",
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    public static IReadOnlyList<SnapshotCategoryGroup> Aggregate(IReadOnlyList<CategoryEstimate> categories) => [.. categories
        .GroupBy(c => Group(c.Category)).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new SnapshotCategoryGroup(g.Key,
            new EffortRange { Low = g.Sum(c => c.Hours.Low), Expected = g.Sum(c => c.Hours.Expected), High = g.Sum(c => c.Hours.High) }))];
}
