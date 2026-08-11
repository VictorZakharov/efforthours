using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class ChangeWorkItemBuilder
{
    private static (string Title, string Reason) DescribeLogicalPart(
        EffortCategory category,
        string title,
        string reason,
        int index,
        int count)
    {
        if (count == 1)
        {
            return (title, reason);
        }

        string phase = LogicalPhase(category, index);
        return (
            $"{phase}: {title}",
            $"{phase} as task {index + 1} of {count} in a granular decomposition of this logical change. " +
            reason);
    }

    private static string LogicalPhase(EffortCategory category, int index)
    {
        string[] phases = category switch
        {
            EffortCategory.SpecificationComprehensionAndDomainLearning =>
            [
                "Trace the affected contracts",
                "Resolve the behavioral boundary",
                "Confirm assumptions and constraints",
                "Consolidate the implementation specification",
            ],
            EffortCategory.ArchitectureAndTechnicalDesign =>
            [
                "Define the design boundary",
                "Select the implementation structure",
                "Reconcile compatibility constraints",
                "Finalize the technical design",
            ],
            EffortCategory.UnitTesting or
            EffortCategory.IntegrationContractAndComponentTesting or
            EffortCategory.EndToEndAndUiTesting =>
            [
                "Design focused test cases",
                "Implement primary regression coverage",
                "Cover boundary and failure behavior",
                "Stabilize fixtures and assertions",
                "Confirm cross-surface coverage",
            ],
            EffortCategory.Documentation =>
            [
                "Define the reader-facing change",
                "Author the maintained guidance",
                "Verify examples and compatibility notes",
                "Integrate the documentation update",
            ],
            EffortCategory.BuildConfigurationAndDeveloperTooling or
            EffortCategory.CiCdAndInfrastructureAsCode or
            EffortCategory.PackagingDeploymentAndReleaseArtifacts =>
            [
                "Define the delivery constraint",
                "Implement the configuration change",
                "Cover compatibility and failure behavior",
                "Verify the delivery integration",
            ],
            EffortCategory.ManualValidationDebuggingAndHardening =>
            [
                "Exercise the primary scenario",
                "Probe boundary and failure behavior",
                "Confirm integration and compatibility",
                "Resolve observed defects",
                "Complete final hardening",
            ],
            EffortCategory.SelfReviewAndSystemIntegration =>
            [
                "Review behavior and contracts",
                "Review edge cases and evidence",
                "Integrate the affected surfaces",
                "Confirm final consistency",
            ],
            _ =>
            [
                "Define the bounded behavior",
                "Implement the primary behavior",
                "Integrate affected contracts",
                "Handle boundary and compatibility cases",
                "Complete supporting behavior",
                "Verify internal consistency",
            ],
        };

        string phase = phases[index % phases.Length];
        int cycle = index / phases.Length;
        return cycle == 0 ? phase : $"{phase} ({cycle + 1})";
    }
}
