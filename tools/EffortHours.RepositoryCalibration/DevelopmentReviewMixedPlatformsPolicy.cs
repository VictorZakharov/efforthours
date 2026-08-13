using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static partial class DevelopmentReviewPolicy
{
    private static ReviewJudgment SimplCommerceExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        if ((kind is "data-persistence" or "external-integration") && IsTestScope(target.Scope))
        {
            return Exclude("the semantic evidence belongs to a test fixture and is already represented by test authoring.");
        }

        if (kind == "validation-surface" &&
            target.Scope.StartsWith("build/", StringComparison.Ordinal))
        {
            return Exclude("attributes on build tooling do not establish product input-validation behavior.");
        }

        decimal expected = kind switch
        {
            "specification-comprehension" => 45m,
            "solution-coordination" => 35m,
            "self-review" => 55m,
            "build-tooling" => 5m,
            "ci-infrastructure" => 5m,
            "container-deployment" => 6m,
            "documentation" => 12m,
            "api-surface" => ApiEffort(target, evidence, 1.1m),
            "application-entry-point" => EntryPoint(target, evidence, 1m),
            "architecture-design" => SimplCommerceArchitecture(target),
            "background-work" => 8m,
            "data-persistence" => target.Scope == "."
                ? 24m
                : DataEffort(target, evidence, 0.9m),
            "dotnet-source-backbone" => DotNetSource(target, evidence, SimplCommerceSourceFactor(target.Scope)),
            "external-integration" => IntegrationEffort(target, evidence, 1.3m),
            "javascript-source-backbone" => JavaScriptSource(target, evidence, 0.75m),
            "manual-validation" => SimplCommerceValidation(target),
            "polyglot-source-backbone" => PolyglotSource(target, evidence, 0.6m),
            "project-setup" => ProjectSetup(target, IsTestScope(target.Scope) ? 0.65m : 1m),
            "security-surface" => SecurityEffort(target, evidence, 1.1m),
            "ui-surface" => UiEffort(target, evidence, 1m),
            "unit-tests" => TestEffort(target, evidence, 1.2m),
            "validation-surface" => ValidationEffort(target, evidence, 1m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static decimal SimplCommerceSourceFactor(string scope) => scope switch
    {
        "src/Modules/SimplCommerce.Module.Core/SimplCommerce.Module.Core.csproj" => 1.3m,
        "src/Modules/SimplCommerce.Module.Catalog/SimplCommerce.Module.Catalog.csproj" => 1.3m,
        "src/SimplCommerce.Infrastructure/SimplCommerce.Infrastructure.csproj" => 1.2m,
        _ when scope.StartsWith("src/Modules/", StringComparison.Ordinal) => 1.1m,
        _ when scope.StartsWith("build/", StringComparison.Ordinal) => 0.7m,
        _ => 1m,
    };

    private static decimal SimplCommerceArchitecture(CalibrationAuthoringTarget target) => target.Scope switch
    {
        "src/Modules/SimplCommerce.Module.Core/SimplCommerce.Module.Core.csproj" => 25m,
        "src/Modules/SimplCommerce.Module.Catalog/SimplCommerce.Module.Catalog.csproj" => 20m,
        "src/SimplCommerce.WebHost/SimplCommerce.WebHost.csproj" => 20m,
        "src/SimplCommerce.Infrastructure/SimplCommerce.Infrastructure.csproj" => 15m,
        "." => 20m,
        _ => Architecture(target, IsTestScope(target.Scope) ? 0.6m : 1m, 14m),
    };

    private static decimal SimplCommerceValidation(CalibrationAuthoringTarget target) => target.Scope switch
    {
        "src/Modules/SimplCommerce.Module.Core/SimplCommerce.Module.Core.csproj" => 25m,
        "src/Modules/SimplCommerce.Module.Catalog/SimplCommerce.Module.Catalog.csproj" => 22m,
        "src/SimplCommerce.WebHost/SimplCommerce.WebHost.csproj" => 24m,
        "src/SimplCommerce.Infrastructure/SimplCommerce.Infrastructure.csproj" => 15m,
        "." => 24m,
        _ => ManualValidation(target, IsTestScope(target.Scope) ? 0.65m : 1m, 14m),
    };

    private static ReviewJudgment SquidexExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        bool testScope = IsTestScope(target.Scope);
        if ((kind is "data-persistence" or "external-integration" or "security-surface" or
             "ui-surface") && testScope)
        {
            return Exclude("the semantic evidence belongs to tests or test support and is already represented by test authoring.");
        }

        if (kind == "dotnet-source-backbone" && testScope)
        {
            return Exclude("the TestSuite helper is supporting test infrastructure rather than a separate production component.");
        }

        decimal expected = kind switch
        {
            "specification-comprehension" => 70m,
            "solution-coordination" => 30m,
            "self-review" => 80m,
            "build-tooling" => 35m,
            "ci-infrastructure" => 25m,
            "container-deployment" => 30m,
            "documentation" => 30m,
            "packaging-release" => 8m,
            "api-surface" => ApiEffort(target, evidence, 1.2m),
            "application-entry-point" => EntryPoint(target, evidence, 1m),
            "architecture-design" => SquidexArchitecture(target),
            "background-work" => target.Scope == "frontend" ? 4m : 18m,
            "data-persistence" => DataEffort(target, evidence, SquidexDataFactor(target.Scope)),
            "dotnet-source-backbone" => DotNetSource(target, evidence, SquidexSourceFactor(target.Scope)),
            "end-to-end-tests" => TestEffort(target, evidence, 1.4m),
            "external-integration" => IntegrationEffort(target, evidence, 1.2m),
            "javascript-source-backbone" => JavaScriptSource(target, evidence,
                target.Scope == "frontend" ? 1.25m : 0.9m),
            "manual-validation" => SquidexValidation(target),
            "polyglot-source-backbone" => PolyglotSource(target, evidence, 0.7m),
            "project-setup" => ProjectSetup(target, testScope ? 0.65m : 1m),
            "security-surface" => SecurityEffort(target, evidence,
                target.Scope == "frontend" ? 1.1m : 1.2m),
            "ui-surface" => UiEffort(target, evidence,
                target.Scope == "frontend" ? 1m : 0.9m),
            "unit-tests" => TestEffort(target, evidence, 1.3m),
            "validation-surface" => ValidationEffort(target, evidence, 1m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static decimal SquidexSourceFactor(string scope) => scope switch
    {
        "backend/src/Squidex.Domain.Apps.Entities/Squidex.Domain.Apps.Entities.csproj" => 1.3m,
        "backend/src/Squidex.Domain.Apps.Core.Operations/Squidex.Domain.Apps.Core.Operations.csproj" => 1.3m,
        "backend/src/Squidex.Domain.Apps.Core.Model/Squidex.Domain.Apps.Core.Model.csproj" => 1.15m,
        "backend/src/Squidex.Infrastructure/Squidex.Infrastructure.csproj" => 1.2m,
        "backend/src/Squidex/Squidex.csproj" => 1.2m,
        _ when scope.Contains("Squidex.Data", StringComparison.Ordinal) => 1.15m,
        _ => 1m,
    };

    private static decimal SquidexDataFactor(string scope) => scope switch
    {
        "backend/src/Squidex.Data.EntityFramework/Squidex.Data.EntityFramework.csproj" => 1.1m,
        "backend/src/Squidex.Data.MongoDb/Squidex.Data.MongoDb.csproj" => 1.1m,
        _ => 1m,
    };

    private static decimal SquidexArchitecture(CalibrationAuthoringTarget target) => target.Scope switch
    {
        "backend/src/Squidex/Squidex.csproj" => 100m,
        "backend/src/Squidex.Domain.Apps.Entities/Squidex.Domain.Apps.Entities.csproj" => 80m,
        "frontend" => 70m,
        "backend/src/Squidex.Infrastructure/Squidex.Infrastructure.csproj" => 40m,
        "backend/src/Squidex.Domain.Apps.Core.Operations/Squidex.Domain.Apps.Core.Operations.csproj" => 40m,
        "backend/src/Squidex.Data.EntityFramework/Squidex.Data.EntityFramework.csproj" => 30m,
        "backend/src/Squidex.Data.MongoDb/Squidex.Data.MongoDb.csproj" => 30m,
        "." => 30m,
        _ => Architecture(target, IsTestScope(target.Scope) ? 0.6m : 1m, 20m),
    };

    private static decimal SquidexValidation(CalibrationAuthoringTarget target) => target.Scope switch
    {
        "backend/src/Squidex/Squidex.csproj" => 120m,
        "backend/src/Squidex.Domain.Apps.Entities/Squidex.Domain.Apps.Entities.csproj" => 90m,
        "frontend" => 90m,
        "backend/src/Squidex.Infrastructure/Squidex.Infrastructure.csproj" => 45m,
        "backend/src/Squidex.Domain.Apps.Core.Operations/Squidex.Domain.Apps.Core.Operations.csproj" => 45m,
        "." => 35m,
        _ => ManualValidation(target, IsTestScope(target.Scope) ? 0.65m : 1m, 20m),
    };
}
