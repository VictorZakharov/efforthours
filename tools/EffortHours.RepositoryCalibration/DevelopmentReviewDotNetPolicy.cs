using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static partial class DevelopmentReviewPolicy
{
    private static ReviewJudgment PollyExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        if (kind is "api-surface" && target.Scope.StartsWith("src/Polly", StringComparison.Ordinal))
        {
            return Exclude("classes named as circuit controllers are resilience internals, not ASP.NET route controllers, and their implementation is retained in the source backbone.");
        }

        if ((kind is "application-entry-point" or "dotnet-source-backbone") &&
            (IsBenchmarkScope(target.Scope) || IsTestScope(target.Scope)))
        {
            return Exclude("benchmark or test-host code is represented by validation/test work and is not a separate production capability.");
        }

        if (kind is "external-integration" &&
            (IsTestScope(target.Scope) ||
             target.Scope == "src/Snippets/Snippets.csproj" ||
             target.Scope.StartsWith("src/Polly.Extensions", StringComparison.Ordinal) ||
             IsBenchmarkScope(target.Scope)))
        {
            return Exclude("the qualified names occur in tests, documentation snippets, benchmarks, or ordinary system telemetry and do not establish a separate external product integration.");
        }

        if (kind == "validation-surface" && IsTestScope(target.Scope))
        {
            return Exclude("validation calls in the test project are already represented by test authoring.");
        }

        decimal expected = kind switch
        {
            "specification-comprehension" => 24m,
            "solution-coordination" => 18m,
            "self-review" => 30m,
            "build-tooling" => 16m,
            "ci-infrastructure" => 18m,
            "documentation" => 160m,
            "api-surface" => 1.5m,
            "application-entry-point" => EntryPoint(target, evidence,
                target.Scope.StartsWith("samples/", StringComparison.Ordinal) ? 0.8m : 0.6m),
            "dotnet-source-backbone" => DotNetSource(target, evidence, PollySourceFactor(target.Scope)),
            "javascript-source-backbone" => JavaScriptSource(target, evidence, 0.6m),
            "polyglot-source-backbone" => PolyglotSource(target, evidence, 0.8m),
            "unit-tests" => TestEffort(target, evidence,
                target.Scope.Contains("Polly.Specs", StringComparison.Ordinal) ? 1.2m : 1.1m),
            "external-integration" => IntegrationEffort(target, evidence, 0.8m),
            "validation-surface" => ValidationEffort(target, evidence, 1.1m),
            "project-setup" => ProjectSetup(target, IsTestScope(target.Scope) ? 0.7m : 1m),
            "architecture-design" => Architecture(target, IsTestScope(target.Scope) ? 0.65m : 1m),
            "manual-validation" => ManualValidation(target, IsTestScope(target.Scope) ? 0.7m : 1m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static decimal PollySourceFactor(string scope) => scope switch
    {
        "src/Polly/Polly.csproj" => 1.35m,
        "src/Polly.Core/Polly.Core.csproj" => 1.5m,
        "src/Polly.Extensions/Polly.Extensions.csproj" => 1.2m,
        "src/Polly.RateLimiting/Polly.RateLimiting.csproj" => 1.15m,
        "src/Polly.Testing/Polly.Testing.csproj" => 1.1m,
        _ when scope.StartsWith("samples/", StringComparison.Ordinal) => 0.7m,
        _ => 0.8m,
    };

    private static ReviewJudgment FastEndpointsExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        bool testSemantic = IsTestScope(target.Scope) || IsBenchmarkScope(target.Scope);
        if ((kind is "api-surface" or "external-integration" or "security-surface" or
             "validation-surface" or "data-persistence") && testSemantic)
        {
            return Exclude("the semantic evidence belongs to a benchmark, test, or test host and is already represented by the applicable test/validation capability.");
        }

        if ((kind is "application-entry-point" or "dotnet-source-backbone") &&
            IsBenchmarkScope(target.Scope))
        {
            return Exclude("benchmark harness construction is represented as validation evidence, not as a separate production application.");
        }

        decimal expected = kind switch
        {
            "specification-comprehension" => 30m,
            "solution-coordination" => 30m,
            "self-review" => 40m,
            "build-tooling" => 12m,
            "ci-infrastructure" => 4m,
            "documentation" => 30m,
            "background-work" => 12m,
            "api-surface" => ApiEffort(target, evidence,
                target.Scope.StartsWith("TestHarness/", StringComparison.Ordinal) ? 0.45m : 0.8m),
            "application-entry-point" => EntryPoint(target, evidence,
                target.Scope.StartsWith("TestHarness/", StringComparison.Ordinal) ? 0.65m : 1m),
            "data-persistence" => DataEffort(target, evidence, 0.8m),
            "dotnet-source-backbone" => DotNetSource(target, evidence, FastEndpointsSourceFactor(target.Scope)),
            "polyglot-source-backbone" => PolyglotSource(target, evidence, 0.5m),
            "external-integration" => IntegrationEffort(target, evidence,
                target.Scope.StartsWith("Src/Messaging", StringComparison.Ordinal) ? 1.3m : 0.9m),
            "security-surface" => SecurityEffort(target, evidence, 1.1m),
            "ui-surface" => UiEffort(target, evidence, 0.7m),
            "unit-tests" => TestEffort(target, evidence, 1.2m),
            "validation-surface" => ValidationEffort(target, evidence, 0.9m),
            "project-setup" => ProjectSetup(target, TestHarnessFactor(target.Scope)),
            "architecture-design" => Architecture(target, TestHarnessFactor(target.Scope), 16m),
            "manual-validation" => ManualValidation(target, TestHarnessFactor(target.Scope), 16m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static decimal FastEndpointsSourceFactor(string scope)
    {
        if (scope.StartsWith("TestHarness/", StringComparison.Ordinal))
        {
            return 0.35m;
        }

        return scope switch
        {
            "Src/Library/FastEndpoints.csproj" => 1.4m,
            "Src/OpenApi/FastEndpoints.OpenApi.csproj" => 1.3m,
            "Src/Swagger/FastEndpoints.Swagger.csproj" => 1.2m,
            "Src/Generator.Cli/FastEndpoints.Generator.Cli.csproj" => 1.2m,
            _ when scope.StartsWith("Src/Messaging", StringComparison.Ordinal) => 1.15m,
            _ => 1m,
        };
    }

    private static decimal TestHarnessFactor(string scope) =>
        IsTestScope(scope) || IsBenchmarkScope(scope) ? 0.65m : 1m;

    private static ReviewJudgment FluentValidationExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        if (kind == "application-entry-point" && IsBenchmarkScope(target.Scope) ||
            kind == "dotnet-source-backbone" && IsBenchmarkScope(target.Scope))
        {
            return Exclude("benchmark harness code is validation support rather than a separate production capability.");
        }

        if (kind == "validation-surface" && IsTestScope(target.Scope))
        {
            return Exclude("validator constructions in tests exercise the retained library behavior and are already represented by test authoring.");
        }

        decimal expected = kind switch
        {
            "specification-comprehension" => 16m,
            "solution-coordination" => 5m,
            "self-review" => 22m,
            "build-tooling" => 6m,
            "ci-infrastructure" => 3m,
            "documentation" => 60m,
            "dotnet-source-backbone" => DotNetSource(target, evidence,
                target.Scope == "src/FluentValidation/FluentValidation.csproj" ? 1.6m : 1.1m),
            "unit-tests" => TestEffort(target, evidence, 1.35m),
            "project-setup" => ProjectSetup(target, IsTestScope(target.Scope) ? 0.75m : 1m),
            "architecture-design" => Architecture(target, IsTestScope(target.Scope) ? 0.7m : 1m),
            "manual-validation" => ManualValidation(target, IsTestScope(target.Scope) ? 0.75m : 1m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static ReviewJudgment CommandLineExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        if ((kind is "application-entry-point" or "ui-surface") &&
            (IsTestScope(target.Scope) || IsBenchmarkScope(target.Scope)))
        {
            return Exclude("command construction in tests or benchmarks is already represented by test/validation authoring and is not another product command surface.");
        }

        if (kind == "dotnet-source-backbone" && IsBenchmarkScope(target.Scope))
        {
            return Exclude("benchmark harness source is validation support rather than production implementation.");
        }

        decimal expected = kind switch
        {
            "specification-comprehension" => 16m,
            "solution-coordination" => 8m,
            "self-review" => 24m,
            "build-tooling" => 30m,
            "ci-infrastructure" => 3m,
            "documentation" => 20m,
            "packaging-release" => 6m,
            "application-entry-point" => EntryPoint(target, evidence, 1m),
            "dotnet-source-backbone" => DotNetSource(target, evidence, CommandLineSourceFactor(target.Scope)),
            "polyglot-source-backbone" => PolyglotSource(target, evidence, 0.9m),
            "ui-surface" => Positive(0.5m + (evidence.Sum(target, "commands") * 0.5m)),
            "unit-tests" => TestEffort(target, evidence, 1.3m),
            "end-to-end-tests" => TestEffort(target, evidence, 1.4m),
            "validation-surface" => ValidationEffort(target, evidence, 0.8m),
            "project-setup" => ProjectSetup(target, IsTestScope(target.Scope) ? 0.75m : 1m),
            "architecture-design" => Architecture(target, IsTestScope(target.Scope) ? 0.7m : 1m),
            "manual-validation" => ManualValidation(target, IsTestScope(target.Scope) ? 0.75m : 1m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static decimal CommandLineSourceFactor(string scope) => scope switch
    {
        "src/System.CommandLine/System.CommandLine.csproj" => 1.5m,
        "src/System.CommandLine.StaticCompletions/System.CommandLine.StaticCompletions.csproj" => 1.3m,
        "src/System.CommandLine.Suggest/dotnet-suggest.csproj" => 1.2m,
        _ => 0.8m,
    };
}
