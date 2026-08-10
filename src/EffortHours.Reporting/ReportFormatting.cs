using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

internal static class ReportFormatting
{
    public static string Display(EffortCategory category) => category switch
    {
        EffortCategory.SpecificationComprehensionAndDomainLearning => "Specification and domain learning",
        EffortCategory.RepositoryAndSolutionSetup => "Repository and solution setup",
        EffortCategory.ArchitectureAndTechnicalDesign => "Architecture and technical design",
        EffortCategory.ProductionImplementation => "Production implementation",
        EffortCategory.UiImplementationAndRepresentedUxDecisions => "UI implementation and UX decisions",
        EffortCategory.DataModelingPersistenceAndMigrations => "Data modeling, persistence, and migrations",
        EffortCategory.ExternalIntegrationsAndProtocols => "External integrations and protocols",
        EffortCategory.UnitTesting => "Unit testing",
        EffortCategory.IntegrationContractAndComponentTesting => "Integration, contract, and component testing",
        EffortCategory.EndToEndAndUiTesting => "End-to-end and UI testing",
        EffortCategory.ManualValidationDebuggingAndHardening => "Manual validation, debugging, and hardening",
        EffortCategory.Documentation => "Documentation",
        EffortCategory.BuildConfigurationAndDeveloperTooling => "Build configuration and developer tooling",
        EffortCategory.CiCdAndInfrastructureAsCode => "CI/CD and infrastructure as code",
        EffortCategory.SecurityAndAccessibility => "Security and accessibility",
        EffortCategory.PackagingDeploymentAndReleaseArtifacts => "Packaging, deployment, and release artifacts",
        EffortCategory.SelfReviewAndSystemIntegration => "Self-review and system integration",
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    public static string Kebab<T>(T value)
        where T : struct, Enum =>
        System.Text.Json.JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());

    public static string Hours(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Money(decimal value) =>
        value.ToString("#,0.00", CultureInfo.InvariantCulture);

    public static string Percent(decimal value) =>
        value.ToString("P0", CultureInfo.InvariantCulture);

    public static string SharePercent(decimal value) =>
        value.ToString("0.##%", CultureInfo.InvariantCulture);

    public static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
