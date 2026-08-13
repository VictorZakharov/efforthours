using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static partial class DevelopmentReviewPolicy
{
    private static ReviewJudgment ZodExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        bool fixture = ZodFixtureScope(target.Scope);
        if (kind == "javascript-source-backbone" && fixture)
        {
            return Exclude("the package is a benchmark, compiler-resolution, integration, or tree-shaking fixture whose behavior is retained in test/validation work rather than product implementation.");
        }

        if (kind == "validation-surface")
        {
            return Exclude("Zod constructions here exercise the validation library in tests, benchmarks, and fixtures; the retained source and test capabilities already represent that work.");
        }

        decimal expected = kind switch
        {
            "specification-comprehension" => 24m,
            "solution-coordination" => 10m,
            "self-review" => 28m,
            "build-tooling" => 20m,
            "ci-infrastructure" => 6m,
            "documentation" => 100m,
            "packaging-release" => 8m,
            "api-surface" => ApiEffort(target, evidence, 0.8m),
            "application-entry-point" => EntryPoint(target, evidence, 0.8m),
            "external-integration" => IntegrationEffort(target, evidence, 0.8m),
            "javascript-source-backbone" => JavaScriptSource(target, evidence, 1.25m),
            "polyglot-source-backbone" => PolyglotSource(target, evidence, 0.6m),
            "ui-surface" => UiEffort(target, evidence,
                target.Scope == "packages/docs" ? 1.1m : 0.7m),
            "unit-tests" => TestEffort(target, evidence,
                target.Scope == "." ? 1.6m : 1m),
            "project-setup" => ProjectSetup(target, fixture ? 0.55m : 1m),
            "architecture-design" => Architecture(target, fixture ? 0.55m : 1m),
            "manual-validation" => ManualValidation(target, fixture ? 0.6m : 1m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static bool ZodFixtureScope(string scope) => scope is
        "packages/bench" or
        "packages/integration" or
        "packages/resolution" or
        "packages/treeshake" or
        "packages/tsc";

    private static ReviewJudgment FastifyExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        if (kind == "api-surface" && IsTestScope(target.Scope))
        {
            return Exclude("routes declared by bundler tests exercise Fastify and are already represented by test authoring, not a second application API.");
        }

        decimal expected = kind switch
        {
            "specification-comprehension" => 24m,
            "self-review" => 28m,
            "build-tooling" => 16m,
            "ci-infrastructure" => 20m,
            "documentation" => 100m,
            "packaging-release" => 8m,
            "api-surface" => ApiEffort(target, evidence, 0.45m),
            "application-entry-point" => EntryPoint(target, evidence, 1m),
            "architecture-design" => 18m,
            "background-work" => 15m,
            "external-integration" => IntegrationEffort(target, evidence, 1.2m),
            "javascript-source-backbone" => JavaScriptSource(target, evidence, 0.8m),
            "unit-tests" => TestEffort(target, evidence,
                target.Scope == "." ? 1.5m : 0.8m),
            "validation-surface" => ValidationEffort(target, evidence, 1m),
            "project-setup" => ProjectSetup(target, IsTestScope(target.Scope) ? 0.6m : 1m),
            "manual-validation" => 24m,
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static ReviewJudgment LitExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        bool generatedFixture = IsGeneratedFixtureScope(target.Scope);
        if (generatedFixture &&
            (kind is
                "javascript-source-backbone" or
                "ui-surface" or
                "project-setup" or
                "architecture-design" or
                "manual-validation"))
        {
            return Exclude("the scope is a checked-in generated/golden test fixture; its represented value belongs to the retained generator and test capability, not a parallel product package.");
        }

        if (kind == "javascript-source-backbone" && IsBenchmarkScope(target.Scope))
        {
            return Exclude("benchmark harness code is represented by performance validation rather than production implementation.");
        }

        decimal expected = kind switch
        {
            "specification-comprehension" => 40m,
            "solution-coordination" => 40m,
            "self-review" => 50m,
            "build-tooling" => 64m,
            "ci-infrastructure" => 12m,
            "documentation" => 160m,
            "packaging-release" => 25m,
            "api-surface" => ApiEffort(target, evidence, 0.7m),
            "application-entry-point" => EntryPoint(target, evidence, 0.8m),
            "end-to-end-tests" => TestEffort(target, evidence, 1.2m),
            "external-integration" => IntegrationEffort(target, evidence, 1m),
            "integration-tests" => TestEffort(target, evidence, 1.25m),
            "javascript-source-backbone" => JavaScriptSource(target, evidence, LitSourceFactor(target.Scope)),
            "polyglot-source-backbone" => PolyglotSource(target, evidence, 0.7m),
            "ui-surface" => UiEffort(target, evidence, LitSupportingFactor(target.Scope)),
            "unit-tests" => TestEffort(target, evidence, 1.25m),
            "project-setup" => ProjectSetup(target, LitSupportingFactor(target.Scope)),
            "architecture-design" => Architecture(target, LitSupportingFactor(target.Scope), 16m),
            "manual-validation" => ManualValidation(target, LitSupportingFactor(target.Scope), 16m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static decimal LitSourceFactor(string scope)
    {
        if (scope.StartsWith("examples/", StringComparison.Ordinal) ||
            scope.StartsWith("playground/", StringComparison.Ordinal))
        {
            return 0.55m;
        }

        return scope switch
        {
            "packages/lit-html" => 1.45m,
            "packages/reactive-element" => 1.4m,
            "packages/lit-element" => 1.25m,
            "packages/lit" => 1.2m,
            _ when scope.StartsWith("packages/labs/", StringComparison.Ordinal) => 1.1m,
            _ when scope.Contains("starter", StringComparison.Ordinal) => 0.8m,
            _ => 1m,
        };
    }

    private static decimal LitSupportingFactor(string scope)
    {
        if (IsBenchmarkScope(scope))
        {
            return 0.55m;
        }

        if (scope.StartsWith("examples/", StringComparison.Ordinal) ||
            scope.StartsWith("playground/", StringComparison.Ordinal))
        {
            return 0.65m;
        }

        return 1m;
    }

    private static ReviewJudgment ExecaExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        decimal expected = Kind(target) switch
        {
            "specification-comprehension" => 20m,
            "self-review" => 26m,
            "build-tooling" => 8m,
            "ci-infrastructure" => 4m,
            "documentation" => 50m,
            "packaging-release" => 6m,
            "architecture-design" => 16m,
            "background-work" => 18m,
            "javascript-source-backbone" => JavaScriptSource(target, evidence, 1.4m),
            "unit-tests" => TestEffort(target, evidence, 1.35m),
            "project-setup" => ProjectSetup(target, 1m),
            "manual-validation" => 24m,
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }
}
