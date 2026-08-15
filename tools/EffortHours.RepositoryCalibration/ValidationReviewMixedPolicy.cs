using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static partial class DevelopmentReviewPolicy
{
    private static ReviewJudgment CleanArchitectureExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        bool testScope = IsTestScope(target.Scope);
        if ((kind is "data-persistence" or "external-integration") && testScope)
        {
            return Exclude(
                "the semantic evidence belongs to test infrastructure and is already represented by functional-test authoring.");
        }

        if (kind == "application-entry-point" && testScope)
        {
            return Exclude(
                "the test application host is supporting test infrastructure rather than another product entry point.");
        }

        decimal supportingFactor = testScope ? 0.65m : 1m;
        decimal expected = kind switch
        {
            "specification-comprehension" => 20m,
            "solution-coordination" => 12m,
            "self-review" => 24m,
            "build-tooling" => 12m,
            "ci-infrastructure" => 5m,
            "documentation" => 24m,
            "api-surface" => ApiEffort(target, evidence, 1m),
            "application-entry-point" => EntryPoint(target, evidence, 1m),
            "architecture-design" => Architecture(target, supportingFactor, 14m),
            "data-persistence" => DataEffort(target, evidence, 0.9m),
            "dotnet-source-backbone" => DotNetSource(
                target,
                evidence,
                CleanArchitectureSourceFactor(target.Scope)),
            "end-to-end-tests" => TestEffort(target, evidence, 1.25m),
            "external-integration" => IntegrationEffort(target, evidence, 1m),
            "integration-tests" => TestEffort(target, evidence, 1.2m),
            "javascript-source-backbone" => JavaScriptSource(target, evidence, 1m),
            "manual-validation" => ManualValidation(target, supportingFactor, 14m),
            "project-setup" => ProjectSetup(target, supportingFactor),
            "security-surface" => SecurityEffort(target, evidence, 1m),
            "ui-surface" => UiEffort(target, evidence, 1m),
            "unit-tests" => TestEffort(target, evidence, 1.2m),
            "validation-surface" => ValidationEffort(target, evidence, 1m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static decimal CleanArchitectureSourceFactor(string scope) => scope switch
    {
        "src/Application/Application.csproj" => 1.2m,
        "src/Infrastructure/Infrastructure.csproj" => 1.15m,
        "src/Web/Web.csproj" => 1.1m,
        _ => 1m,
    };

    private static ReviewJudgment ElectronNetExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        bool testScope = IsTestScope(target.Scope);
        bool buildScope = target.Scope.StartsWith("nuke/", StringComparison.Ordinal) ||
            target.Scope == ".";
        if (buildScope &&
            kind is "application-entry-point" or "dotnet-source-backbone")
        {
            return Exclude(
                "root or Nuke entry/source evidence is repository build tooling already represented by build and release work.");
        }

        if (kind == "ui-surface" &&
            target.Scope.StartsWith("nuke/", StringComparison.Ordinal))
        {
            return Exclude(
                "command-like Nuke build evidence is build tooling rather than a product UI surface.");
        }

        if (kind == "external-integration" &&
            (testScope || target.Scope == "."))
        {
            return Exclude(
                "the integration signal belongs to the integration-test project or root release tooling and is already represented elsewhere.");
        }

        decimal supportingFactor = ElectronSampleScope(target.Scope) ? 0.65m : 1m;
        decimal expected = kind switch
        {
            "specification-comprehension" => 35m,
            "solution-coordination" => 20m,
            "self-review" => 40m,
            "build-tooling" => 20m,
            "ci-infrastructure" => 8m,
            "documentation" => 90m,
            "packaging-release" => 8m,
            "api-surface" => ApiEffort(target, evidence, 0.7m),
            "application-entry-point" => EntryPoint(target, evidence, supportingFactor),
            "architecture-design" => Architecture(
                target,
                supportingFactor * ElectronCoreFactor(target.Scope),
                18m),
            "dotnet-source-backbone" => DotNetSource(
                target,
                evidence,
                supportingFactor * ElectronDotNetFactor(target.Scope)),
            "external-integration" => IntegrationEffort(target, evidence, 1.2m),
            "javascript-source-backbone" => JavaScriptSource(
                target,
                evidence,
                supportingFactor * ElectronJavaScriptFactor(target.Scope)),
            "manual-validation" => ManualValidation(
                target,
                supportingFactor * ElectronCoreFactor(target.Scope),
                18m),
            "polyglot-source-backbone" => PolyglotSource(target, evidence, 0.7m),
            "project-setup" => ProjectSetup(target, supportingFactor),
            "security-surface" => SecurityEffort(target, evidence, 0.8m),
            "ui-surface" => UiEffort(target, evidence, supportingFactor),
            "unit-tests" => TestEffort(target, evidence, 1.25m),
            "validation-surface" => ValidationEffort(target, evidence, 0.8m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static bool ElectronSampleScope(string scope) =>
        scope.Contains(".Samples.", StringComparison.Ordinal) ||
        scope.StartsWith("src/ElectronNET.WebApp", StringComparison.Ordinal);

    private static decimal ElectronDotNetFactor(string scope) => scope switch
    {
        "src/ElectronNET.API/ElectronNET.API.csproj" => 1.4m,
        "src/ElectronNET.AspNet/ElectronNET.AspNet.csproj" => 1.2m,
        "src/ElectronNET.Build/ElectronNET.Build.csproj" => 1.1m,
        _ => 1m,
    };

    private static decimal ElectronJavaScriptFactor(string scope) => scope switch
    {
        "src/ElectronNET.Host" => 1.3m,
        "src/ElectronNET.Host/ElectronHostHook" => 1.15m,
        _ => 1m,
    };

    private static decimal ElectronCoreFactor(string scope) => scope switch
    {
        "src/ElectronNET.API/ElectronNET.API.csproj" => 1.3m,
        "src/ElectronNET.Host" => 1.2m,
        _ => 1m,
    };

    private static ReviewJudgment OrchardCoreExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        bool testScope = IsTestScope(target.Scope);
        if ((kind is "api-surface" or "data-persistence" or "external-integration" or
             "security-surface" or "ui-surface" or "validation-surface") &&
            testScope)
        {
            return Exclude(
                "the specialized semantic evidence belongs to tests or test applications and is already represented by test authoring.");
        }

        if ((kind is "application-entry-point" or
             "dotnet-source-backbone" or
             "javascript-source-backbone") &&
            testScope)
        {
            return Exclude(
                "test-host or test-helper implementation is represented by the retained test capability rather than as a parallel product component.");
        }

        decimal supportingFactor = testScope
            ? 0.6m
            : OrchardSampleScope(target.Scope)
                ? 0.7m
                : 1m;
        decimal expected = kind switch
        {
            "specification-comprehension" => 140m,
            "solution-coordination" => 120m,
            "self-review" => 180m,
            "build-tooling" => 80m,
            "ci-infrastructure" => 25m,
            "container-deployment" => 15m,
            "documentation" => 300m,
            "packaging-release" => 20m,
            "api-surface" => ApiEffort(target, evidence, 1m),
            "application-entry-point" => EntryPoint(target, evidence, supportingFactor),
            "architecture-design" => Architecture(
                target,
                supportingFactor * OrchardCoreFactor(target.Scope),
                24m),
            "background-work" => Clamp(
                3m + (target.EvidenceIds.Count * 0.75m),
                3m,
                24m),
            "data-persistence" => DataEffort(target, evidence, 1.05m),
            "dotnet-source-backbone" => DotNetSource(
                target,
                evidence,
                supportingFactor * OrchardSourceFactor(target.Scope)),
            "end-to-end-tests" => TestEffort(target, evidence, 1.35m),
            "external-integration" => IntegrationEffort(target, evidence, 1.2m),
            "integration-tests" => TestEffort(target, evidence, 1.2m),
            "javascript-source-backbone" => JavaScriptSource(
                target,
                evidence,
                supportingFactor * OrchardJavaScriptFactor(target.Scope)),
            "manual-validation" => ManualValidation(
                target,
                supportingFactor * OrchardCoreFactor(target.Scope),
                24m),
            "polyglot-source-backbone" => PolyglotSource(target, evidence, 0.7m),
            "project-setup" => ProjectSetup(target, supportingFactor),
            "security-surface" => SecurityEffort(target, evidence, 1.15m),
            "ui-surface" => UiEffort(target, evidence, 1.1m),
            "unit-tests" => TestEffort(target, evidence, 1.3m),
            "validation-surface" => ValidationEffort(target, evidence, 1.1m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static bool OrchardSampleScope(string scope) =>
        scope.Contains("Samples", StringComparison.Ordinal) ||
        scope.Contains("Sample", StringComparison.Ordinal) ||
        scope.Contains("Templates", StringComparison.Ordinal);

    private static decimal OrchardSourceFactor(string scope)
    {
        if (scope.StartsWith("src/OrchardCore.Modules/", StringComparison.Ordinal))
        {
            return 1.15m;
        }

        return scope switch
        {
            "src/OrchardCore/OrchardCore/OrchardCore.csproj" => 1.4m,
            "src/OrchardCore/OrchardCore.Abstractions/OrchardCore.Abstractions.csproj" => 1.3m,
            "src/OrchardCore/OrchardCore.Infrastructure/OrchardCore.Infrastructure.csproj" => 1.3m,
            _ when scope.StartsWith("src/OrchardCore/", StringComparison.Ordinal) => 1.2m,
            _ => 1m,
        };
    }

    private static decimal OrchardJavaScriptFactor(string scope) =>
        scope.StartsWith("src/OrchardCore.Modules/", StringComparison.Ordinal)
            ? 1.1m
            : 1m;

    private static decimal OrchardCoreFactor(string scope)
    {
        if (scope.StartsWith("src/OrchardCore.Modules/", StringComparison.Ordinal))
        {
            return 1.1m;
        }

        return scope.StartsWith("src/OrchardCore/", StringComparison.Ordinal)
            ? 1.2m
            : 1m;
    }
}
