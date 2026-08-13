using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static partial class DevelopmentReviewPolicy
{
    private static ReviewJudgment BtcPayExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        if ((kind is "api-surface" or "data-persistence" or "external-integration" or
             "security-surface" or "validation-surface") && IsTestScope(target.Scope))
        {
            return Exclude("the semantic evidence belongs to the test suite and is already represented by unit or end-to-end test authoring.");
        }

        if (kind == "application-entry-point" && IsTestScope(target.Scope))
        {
            return Exclude("test-host startup is supporting test infrastructure, not a separate production application entry point.");
        }

        decimal expected = kind switch
        {
            "specification-comprehension" => 60m,
            "solution-coordination" => 18m,
            "self-review" => 60m,
            "build-tooling" => 10m,
            "ci-infrastructure" => 15m,
            "container-deployment" => 45m,
            "documentation" => 40m,
            "api-surface" => ApiEffort(target, evidence, 1.3m),
            "application-entry-point" => EntryPoint(target, evidence, 1m),
            "architecture-design" => BtcPayArchitecture(target),
            "background-work" => 80m,
            "data-persistence" => DataEffort(target, evidence,
                target.Scope == "BTCPayServer.Data/BTCPayServer.Data.csproj" ? 1.15m : 1m),
            "dotnet-source-backbone" => DotNetSource(target, evidence, BtcPaySourceFactor(target.Scope)),
            "end-to-end-tests" => TestEffort(target, evidence, 1.4m),
            "external-integration" => IntegrationEffort(target, evidence, 1.5m),
            "javascript-source-backbone" => JavaScriptSource(target, evidence, 1.1m),
            "manual-validation" => BtcPayValidation(target),
            "polyglot-source-backbone" => PolyglotSource(target, evidence, 0.8m),
            "project-setup" => ProjectSetup(target, IsTestScope(target.Scope) ? 0.7m : 1m),
            "security-surface" => SecurityEffort(target, evidence,
                target.Scope == "BTCPayServer/BTCPayServer.csproj" ? 1.5m : 1m),
            "ui-surface" => UiEffort(target, evidence,
                target.Scope == "BTCPayServer/BTCPayServer.csproj" ? 1.6m : 1.1m),
            "unit-tests" => TestEffort(target, evidence, 1.3m),
            "validation-surface" => ValidationEffort(target, evidence, 1.2m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static decimal BtcPaySourceFactor(string scope) => scope switch
    {
        "BTCPayServer/BTCPayServer.csproj" => 1.25m,
        "BTCPayServer.Data/BTCPayServer.Data.csproj" => 1.1m,
        "BTCPayServer.Client/BTCPayServer.Client.csproj" => 1.2m,
        "BTCPayServer.Rating/BTCPayServer.Rating.csproj" => 1.3m,
        _ => 1m,
    };

    private static decimal BtcPayArchitecture(CalibrationAuthoringTarget target) => target.Scope switch
    {
        "BTCPayServer/BTCPayServer.csproj" => 80m,
        "BTCPayServer.Data/BTCPayServer.Data.csproj" => 20m,
        "BTCPayServer.Client/BTCPayServer.Client.csproj" => 12m,
        "BTCPayServer.Rating/BTCPayServer.Rating.csproj" => 10m,
        "." => 16m,
        _ => Architecture(target, IsTestScope(target.Scope) ? 0.65m : 1m, 16m),
    };

    private static decimal BtcPayValidation(CalibrationAuthoringTarget target) => target.Scope switch
    {
        "BTCPayServer/BTCPayServer.csproj" => 100m,
        "BTCPayServer.Data/BTCPayServer.Data.csproj" => 20m,
        "BTCPayServer.Client/BTCPayServer.Client.csproj" => 12m,
        "BTCPayServer.Rating/BTCPayServer.Rating.csproj" => 10m,
        "." => 18m,
        _ => ManualValidation(target, IsTestScope(target.Scope) ? 0.7m : 1m, 16m),
    };

    private static ReviewJudgment MudBlazorExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        bool testScope = IsTestScope(target.Scope) ||
            target.Scope.Contains("TestComponents", StringComparison.Ordinal);
        if ((kind is "external-integration" or "ui-surface" or "validation-surface") && testScope)
        {
            return Exclude("the evidence belongs to component or analyzer test fixtures and is already represented by test authoring.");
        }

        if ((kind is "application-entry-point" or "dotnet-source-backbone") &&
            IsBenchmarkScope(target.Scope))
        {
            return Exclude("benchmark harness construction is represented as validation work rather than a production application.");
        }

        decimal expected = kind switch
        {
            "specification-comprehension" => 50m,
            "solution-coordination" => 20m,
            "self-review" => 70m,
            "build-tooling" => 10m,
            "ci-infrastructure" => 15m,
            "documentation" => 20m,
            "api-surface" => ApiEffort(target, evidence, 1m),
            "application-entry-point" => EntryPoint(target, evidence, testScope ? 0.6m : 1m),
            "architecture-design" => MudBlazorArchitecture(target, testScope),
            "dotnet-source-backbone" => DotNetSource(target, evidence, MudBlazorSourceFactor(target.Scope)),
            "external-integration" => IntegrationEffort(target, evidence, 0.9m),
            "integration-tests" => TestEffort(target, evidence, 1.5m),
            "javascript-source-backbone" => JavaScriptSource(target, evidence, 1.1m),
            "manual-validation" => MudBlazorValidation(target, testScope),
            "polyglot-source-backbone" => PolyglotSource(target, evidence, 0.9m),
            "project-setup" => ProjectSetup(target, testScope ? 0.6m : 1m),
            "ui-surface" => UiEffort(target, evidence, MudBlazorUiFactor(target.Scope)),
            "unit-tests" => TestEffort(target, evidence,
                target.Scope.Contains("MudBlazor.UnitTests/MudBlazor.UnitTests", StringComparison.Ordinal) ? 1.5m : 1m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static decimal MudBlazorSourceFactor(string scope)
    {
        if (IsTestScope(scope) || scope.Contains("TestComponents", StringComparison.Ordinal))
        {
            return 0.35m;
        }

        return scope switch
        {
            "src/MudBlazor/MudBlazor.csproj" => 1.5m,
            "src/MudBlazor.Docs/MudBlazor.Docs.csproj" => 1m,
            "src/MudBlazor.Analyzers/MudBlazor.Analyzers.csproj" => 1.3m,
            _ => 1m,
        };
    }

    private static decimal MudBlazorUiFactor(string scope) => scope switch
    {
        "src/MudBlazor/MudBlazor.csproj" => 2m,
        "src/MudBlazor.Docs/MudBlazor.Docs.csproj" => 1.1m,
        "src/MudBlazor.UnitTests.Viewer/MudBlazor.UnitTests.Viewer.csproj" => 0.5m,
        "src" => 1.2m,
        _ => 1m,
    };

    private static decimal MudBlazorArchitecture(
        CalibrationAuthoringTarget target,
        bool testScope) => target.Scope switch
        {
            "src/MudBlazor/MudBlazor.csproj" => 50m,
            "src/MudBlazor.Docs/MudBlazor.Docs.csproj" => 25m,
            "src" => 16m,
            _ => Architecture(target, testScope ? 0.55m : 1m, 16m),
        };

    private static decimal MudBlazorValidation(
        CalibrationAuthoringTarget target,
        bool testScope) => target.Scope switch
        {
            "src/MudBlazor/MudBlazor.csproj" => 80m,
            "src/MudBlazor.Docs/MudBlazor.Docs.csproj" => 35m,
            "src" => 20m,
            _ => ManualValidation(target, testScope ? 0.6m : 1m, 16m),
        };
}
