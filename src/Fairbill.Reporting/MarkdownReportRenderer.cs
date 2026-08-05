using System.Globalization;
using System.Text;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Reporting;

public sealed class MarkdownReportRenderer : IReportRenderer
{
    public string Render(EstimateReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The estimate report is invalid: " + string.Join(" ", errors),
                nameof(report));
        }

        StringBuilder markdown = new();
        markdown.Append("# Fairbill estimate - ").AppendLine(Escape(report.Repository.Name));
        markdown.AppendLine();
        markdown.AppendLine("> Equivalent Human Effort is counterfactual replacement effort, not a record of hours actually worked.");
        markdown.AppendLine();
        markdown.Append("Profile: `").Append(ToKebabCase(report.Profile)).AppendLine("`  ");
        markdown.Append("Estimator: `").Append(Escape(report.EstimatorVersion)).AppendLine("`  ");
        markdown.Append("Verification: `").Append(ToKebabCase(report.Verification.Mode)).Append("` / `")
            .Append(ToKebabCase(report.Verification.WorkingState)).AppendLine("`");
        markdown.AppendLine();

        markdown.AppendLine("## Summary");
        markdown.AppendLine();
        markdown.AppendLine("| Measure | Low | Expected | High |");
        markdown.AppendLine("| --- | ---: | ---: | ---: |");
        markdown.Append("| Human-hours | ").Append(Hours(report.TotalEffort.Low)).Append(" | ")
            .Append(Hours(report.TotalEffort.Expected)).Append(" | ")
            .Append(Hours(report.TotalEffort.High)).AppendLine(" |");

        if (report.TotalCost is not null)
        {
            markdown.Append("| Replacement cost (").Append(Escape(report.TotalCost.Currency)).Append(") | ")
                .Append(Money(report.TotalCost.Low)).Append(" | ")
                .Append(Money(report.TotalCost.Expected)).Append(" | ")
                .Append(Money(report.TotalCost.High)).AppendLine(" |");
        }

        markdown.AppendLine();
        markdown.AppendLine("## Categories");
        markdown.AppendLine();
        markdown.AppendLine("| Category | Low | Expected | High |");
        markdown.AppendLine("| --- | ---: | ---: | ---: |");
        foreach (CategoryEstimate category in report.Categories)
        {
            markdown.Append("| ").Append(Display(category.Category)).Append(" | ")
                .Append(Hours(category.Hours.Low)).Append(" | ")
                .Append(Hours(category.Hours.Expected)).Append(" | ")
                .Append(Hours(category.Hours.High)).AppendLine(" |");
        }

        markdown.AppendLine();
        markdown.AppendLine("## Work items");
        markdown.AppendLine();
        markdown.AppendLine("| Work item | Category | Expected | Confidence | Evidence |");
        markdown.AppendLine("| --- | --- | ---: | ---: | --- |");
        foreach (WorkItem item in report.WorkItems)
        {
            markdown.Append("| ").Append(Escape(item.Title)).Append(" | ")
                .Append(Display(item.Category)).Append(" | ")
                .Append(Hours(item.Hours.Expected)).Append(" | ")
                .Append(item.Confidence.ToString("P0", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(Escape(string.Join(", ", item.EvidenceIds))).AppendLine(" |");
        }

        AppendList(markdown, "Assumptions", report.Assumptions);

        if (report.Diagnostics.Count > 0)
        {
            AppendList(
                markdown,
                "Diagnostics",
                report.Diagnostics.Select(diagnostic =>
                    $"`{diagnostic.Code}` ({ToKebabCase(diagnostic.Severity)}): {diagnostic.Message}"));
        }

        if (report.ProfessionalizationGap.Count > 0)
        {
            AppendList(
                markdown,
                "Professionalization gap",
                report.ProfessionalizationGap.Select(item =>
                    $"{item.Title}: {Hours(item.Hours.Expected)} expected hours"));
        }

        if (!string.IsNullOrWhiteSpace(report.Verification.Note))
        {
            AppendList(markdown, "Verification note", [report.Verification.Note]);
        }

        return markdown.ToString().TrimEnd() + "\n";
    }

    private static void AppendList(StringBuilder markdown, string heading, IEnumerable<string> values)
    {
        string[] materialized = [.. values];
        if (materialized.Length == 0)
        {
            return;
        }

        markdown.AppendLine();
        markdown.Append("## ").AppendLine(heading);
        markdown.AppendLine();
        foreach (string value in materialized)
        {
            markdown.Append("- ").AppendLine(Escape(value));
        }
    }

    private static string Display(EffortCategory category) => category switch
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

    private static string ToKebabCase<T>(T value)
        where T : struct, Enum
    {
        return System.Text.Json.JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());
    }

    private static string Hours(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Money(decimal value) =>
        value.ToString("#,0.00", CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
