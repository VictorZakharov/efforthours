using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static partial class DevelopmentReviewPolicy
{
    private static ReviewJudgment KyExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        if (kind == "api-surface")
        {
            return Exclude(
                "all route-like evidence comes from HTTP-client tests; it exercises the retained client and test capabilities rather than defining a server API.");
        }

        decimal expected = kind switch
        {
            "specification-comprehension" => 16m,
            "solution-coordination" => 4m,
            "self-review" => 18m,
            "build-tooling" => 10m,
            "ci-infrastructure" => 3m,
            "documentation" => 70m,
            "packaging-release" => 4m,
            "architecture-design" => 12m,
            "background-work" => 8m,
            "external-integration" => IntegrationEffort(target, evidence, 1m),
            "javascript-source-backbone" => JavaScriptSource(target, evidence, 1.25m),
            "unit-tests" => TestEffort(target, evidence, 1.35m),
            "project-setup" => ProjectSetup(target, 1m),
            "manual-validation" => 16m,
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static ReviewJudgment AxiosExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        if (kind == "api-surface")
        {
            return Exclude(
                "the sole route fact is an adapter test server and is already represented by test authoring.");
        }

        if (kind == "external-integration")
        {
            return Exclude(
                "the qualified calls occur in smoke/module tests, examples, or documentation tooling; the client transport itself remains in the source backbone.");
        }

        if (kind is "security-surface" or "ui-surface")
        {
            return Exclude(
                "the HTML evidence is an example or sandbox client already represented by documentation, source, and validation work, not a separate shipped UI/security surface.");
        }

        if (kind == "background-work" &&
            target.Scope.StartsWith("tests/", StringComparison.Ordinal))
        {
            return Exclude(
                "the background signal is a smoke-test progress fixture already represented by test authoring.");
        }

        decimal expected = kind switch
        {
            "specification-comprehension" => 24m,
            "self-review" => 28m,
            "build-tooling" => 16m,
            "ci-infrastructure" => 8m,
            "documentation" => 110m,
            "packaging-release" => 6m,
            "application-entry-point" => 1.5m,
            "architecture-design" => Architecture(target, 1m, 18m),
            "background-work" => 4m,
            "javascript-source-backbone" => JavaScriptSource(target, evidence, 1.2m),
            "unit-tests" => TestEffort(
                target,
                evidence,
                target.Scope == "docs" ? 0.6m : 1.25m),
            "project-setup" => ProjectSetup(
                target,
                target.Scope.StartsWith("tests/", StringComparison.Ordinal) ? 0.7m : 1m),
            "manual-validation" => ManualValidation(target, 1m, 18m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static ReviewJudgment NxExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        bool testScope = NxTestScope(target.Scope);
        bool exampleScope = NxExampleScope(target.Scope);
        if (kind == "javascript-source-backbone" && IsBenchmarkScope(target.Scope))
        {
            return Exclude(
                "benchmark harness source is performance-validation support rather than production implementation.");
        }

        if (kind == "application-entry-point" && (testScope || exampleScope))
        {
            return Exclude(
                "the entry point belongs to an end-to-end fixture, benchmark, or example whose represented behavior is retained by source and validation work.");
        }

        if ((kind is "data-persistence" or "external-integration") && testScope)
        {
            return Exclude(
                "the semantic evidence belongs to end-to-end or test support and is already represented by test authoring.");
        }

        if (kind == "validation-surface" && target.Scope == ".")
        {
            return Exclude(
                "root script validation is part of the retained build-tooling capability rather than product input validation.");
        }

        decimal supportingFactor = testScope
            ? 0.6m
            : exampleScope
                ? 0.65m
                : target.Scope.StartsWith("nx-dev/", StringComparison.Ordinal) ||
                  target.Scope.StartsWith("graph/", StringComparison.Ordinal)
                    ? 0.85m
                    : 1m;
        decimal expected = kind switch
        {
            "specification-comprehension" => 80m,
            "solution-coordination" => 80m,
            "self-review" => 100m,
            "build-tooling" => 80m,
            "ci-infrastructure" => 30m,
            "container-deployment" => 8m,
            "documentation" => 300m,
            "packaging-release" => 40m,
            "application-entry-point" => EntryPoint(target, evidence, 0.9m),
            "architecture-design" => Architecture(
                target,
                supportingFactor * NxCoreFactor(target.Scope),
                20m),
            "background-work" => Clamp(
                2m + (target.EvidenceIds.Count * 0.4m),
                2m,
                target.Scope == "packages/nx" ? 30m : 12m),
            "data-persistence" => DataEffort(
                target,
                evidence,
                target.Scope == "packages/nx" ? 0.8m : 0.55m),
            "dotnet-source-backbone" => DotNetSource(target, evidence, 1.1m),
            "end-to-end-tests" => TestEffort(target, evidence, 1.3m),
            "external-integration" => IntegrationEffort(target, evidence, 1m),
            "integration-tests" => TestEffort(target, evidence, 1.2m),
            "javascript-source-backbone" => JavaScriptSource(
                target,
                evidence,
                supportingFactor * NxSourceFactor(target.Scope)),
            "manual-validation" => ManualValidation(
                target,
                supportingFactor * NxCoreFactor(target.Scope),
                20m),
            "polyglot-source-backbone" => PolyglotSource(
                target,
                evidence,
                target.Scope.StartsWith("packages/", StringComparison.Ordinal) ? 1.1m : 0.7m),
            "project-setup" => ProjectSetup(target, supportingFactor),
            "security-surface" => SecurityEffort(target, evidence, exampleScope ? 0.7m : 1m),
            "ui-surface" => UiEffort(target, evidence, supportingFactor),
            "unit-tests" => TestEffort(target, evidence, 1.2m),
            "validation-surface" => ValidationEffort(target, evidence, 1m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static bool NxTestScope(string scope) =>
        IsTestScope(scope) ||
        IsBenchmarkScope(scope) ||
        scope.StartsWith("e2e/", StringComparison.Ordinal) ||
        scope.Contains("/fixtures/", StringComparison.Ordinal) ||
        scope.Contains("/test-fixtures/", StringComparison.Ordinal);

    private static bool NxExampleScope(string scope) =>
        scope.StartsWith("examples/", StringComparison.Ordinal) ||
        scope == "astro-docs";

    private static decimal NxSourceFactor(string scope) => scope switch
    {
        "packages/nx" => 1.35m,
        "packages/devkit" => 1.25m,
        "packages/plugin" => 1.2m,
        _ when scope.StartsWith("packages/", StringComparison.Ordinal) => 1.1m,
        _ => 1m,
    };

    private static decimal NxCoreFactor(string scope) => scope switch
    {
        "packages/nx" => 1.3m,
        "packages/devkit" => 1.2m,
        _ => 1m,
    };
}
