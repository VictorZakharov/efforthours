using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static partial class DevelopmentReviewPolicy
{
    private static decimal DotNetSource(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence,
        decimal factor = 1m)
    {
        decimal raw =
            (evidence.Sum(target, "files") * 0.22m) +
            (evidence.Sum(target, "types") * 0.14m) +
            (evidence.Sum(target, "methods") * 0.07m) +
            (evidence.Sum(target, "branch-points") * 0.035m) +
            (evidence.Sum(target, "async-methods") * 0.12m) +
            (evidence.Sum(target, "public-types") * 0.08m) +
            (evidence.Sum(target, "public-methods") * 0.025m);
        return Positive(raw * factor);
    }

    private static decimal JavaScriptSource(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence,
        decimal factor = 1m)
    {
        decimal raw =
            (evidence.Sum(target, "files") * 0.15m) +
            (evidence.Sum(target, "functions") * 0.035m) +
            (evidence.Sum(target, "methods") * 0.06m) +
            (evidence.Sum(target, "branch-points") * 0.035m) +
            (evidence.Sum(target, "classes") * 0.08m) +
            (evidence.Sum(target, "exports") * 0.04m) +
            (evidence.Sum(target, "async-functions") * 0.08m);
        return Positive(raw * factor);
    }

    private static decimal PolyglotSource(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence,
        decimal factor = 1m)
    {
        decimal raw =
            (evidence.Sum(target, "files") * 0.4m) +
            (evidence.Sum(target, "functions") * 0.3m) +
            (evidence.Sum(target, "methods") * 0.25m) +
            (evidence.Sum(target, "branch-points") * 0.12m) +
            (evidence.Sum(target, "external-commands") * 0.08m) +
            (evidence.Sum(target, "file-operations") * 0.2m) +
            (evidence.Sum(target, "network-operations") * 0.3m) +
            (evidence.Sum(target, "process-operations") * 0.25m);
        return Positive(raw * factor);
    }

    private static decimal TestEffort(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence,
        decimal factor = 1m)
    {
        decimal primary = Math.Max(
            evidence.Sum(target, "test-methods"),
            evidence.Sum(target, "test-cases"));
        if (primary == 0m)
        {
            return Clamp(0.5m + (target.EvidenceIds.Count * 0.15m), 0.5m, 8m);
        }

        decimal raw = 0.5m +
            TestCaseEffort(primary) +
            (evidence.Sum(target, "parameterized-cases") * 0.05m) +
            AssertionEffort(evidence.Sum(target, "assertions")) +
            (evidence.Sum(target, "mock-usages") * 0.15m) +
            (evidence.Sum(target, "test-suites") * 0.1m) +
            (evidence.Sum(target, "accessibility-checks") * 0.2m);
        return Positive(raw * factor);
    }

    private static decimal ApiEffort(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence,
        decimal factor = 1m)
    {
        decimal endpointCalls = evidence.Sum(target, "endpoints");
        decimal raw = 0.5m +
            (evidence.Sum(target, "controllers") * 1.2m) +
            (evidence.Sum(target, "attributed-endpoints") * 0.45m) +
            (evidence.Sum(target, "minimal-api-endpoints") * 1.5m) +
            (Math.Min(endpointCalls, 50m) * 0.5m) +
            (Math.Max(0m, endpointCalls - 50m) * 0.08m);
        return Positive(raw * factor);
    }

    private static decimal UiEffort(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence,
        decimal factor = 1m)
    {
        decimal raw = 0.5m +
            (evidence.Sum(target, "files") * 0.5m) +
            (evidence.Sum(target, "components") * 1.5m) +
            (evidence.Sum(target, "pages") * 1.5m) +
            (evidence.Sum(target, "forms") * 1.25m) +
            (evidence.Sum(target, "template-structure-units") * 0.35m) +
            (evidence.Sum(target, "style-structure-units") * 0.2m) +
            (evidence.Sum(target, "bindings") * 0.08m) +
            (evidence.Sum(target, "component-parameters") * 0.08m) +
            (evidence.Sum(target, "component-usages") * 0.015m) +
            (evidence.Sum(target, "design-token-units") * 0.2m) +
            (evidence.Sum(target, "responsive-surfaces") * 0.2m) +
            (evidence.Sum(target, "custom-elements") * 0.4m) +
            (evidence.Sum(target, "elements") * 0.02m);
        return Positive(raw * factor);
    }

    private static decimal DataEffort(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence,
        decimal factor = 1m)
    {
        decimal raw = 0.5m +
            (evidence.Sum(target, "db-contexts") * 6m) +
            (evidence.Sum(target, "db-sets") * 1.5m) +
            (evidence.Sum(target, "entity-configurations") * 1.5m) +
            (evidence.Sum(target, "migrations") * 1.8m) +
            (evidence.Sum(target, "repository-types") * 1.2m) +
            (evidence.Sum(target, "tables") * 1.5m) +
            (evidence.Sum(target, "indexes") * 0.7m) +
            Diminishing(evidence.Sum(target, "data-calls"), 0.12m, 0.035m, 100m) +
            Diminishing(evidence.Sum(target, "queries"), 0.15m, 0.04m, 50m) +
            Diminishing(evidence.Sum(target, "statements"), 0.04m, 0.01m, 100m);
        return Positive(raw * factor);
    }

    private static decimal IntegrationEffort(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence,
        decimal factor = 1m)
    {
        decimal raw = 0.5m +
            (evidence.Sum(target, "client-constructions") * 0.5m) +
            Diminishing(evidence.Sum(target, "integration-calls"), 0.4m, 0.15m, 40m) +
            (evidence.Sum(target, "integration-namespaces") * 0.5m);
        return Positive(raw * factor);
    }

    private static decimal SecurityEffort(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence,
        decimal factor = 1m)
    {
        decimal raw = 0.5m +
            (evidence.Sum(target, "authorization-attributes") * 0.3m) +
            (evidence.Sum(target, "security-configuration-calls") * 2m) +
            (evidence.Sum(target, "security-usages") * 0.5m) +
            (evidence.Sum(target, "accessibility-units") * 0.35m) +
            (evidence.Sum(target, "labels") * 0.05m) +
            (evidence.Sum(target, "keyboard-interactions") * 0.3m) +
            (evidence.Sum(target, "focus-controls") * 0.2m);
        return Positive(raw * factor);
    }

    private static decimal ValidationEffort(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence,
        decimal factor = 1m)
    {
        decimal raw = 0.5m +
            (evidence.Sum(target, "validation-attributes") * 0.25m) +
            (evidence.Sum(target, "validation-rules") * 0.5m) +
            (evidence.Sum(target, "validation-usages") * 0.1m) +
            (evidence.Sum(target, "validator-types") * 2m);
        return Positive(raw * factor);
    }

    private static decimal ProjectSetup(
        CalibrationAuthoringTarget target,
        decimal factor = 1m) => Clamp(
            (0.75m + (target.EvidenceIds.Count * 0.12m)) * factor,
            0.5m,
            8m);

    private static decimal Architecture(
        CalibrationAuthoringTarget target,
        decimal factor = 1m,
        decimal maximum = 12m) => Clamp(
            (1m + (target.EvidenceIds.Count * 0.1m)) * factor,
            0.5m,
            maximum);

    private static decimal ManualValidation(
        CalibrationAuthoringTarget target,
        decimal factor = 1m,
        decimal maximum = 12m) => Clamp(
            (0.75m + (target.EvidenceIds.Count * 0.07m)) * factor,
            0.5m,
            maximum);

    private static decimal EntryPoint(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence,
        decimal factor = 1m) => Clamp(
            (0.5m +
             (evidence.Sum(target, "main-methods") * 1.5m) +
             (evidence.Sum(target, "host-builder-calls") * 1m) +
             (evidence.Sum(target, "root-commands") * 0.25m) +
             (evidence.Sum(target, "commands") * 0.12m) +
             (evidence.Sum(target, "entry-points") * 0.5m) +
             (evidence.Sum(target, "top-level-statements") * 0.08m)) * factor,
            0.5m,
            8m);

    private static decimal TestCaseEffort(decimal count) =>
        (Math.Min(count, 50m) * 0.45m) +
        (Math.Min(Math.Max(count - 50m, 0m), 200m) * 0.28m) +
        (Math.Min(Math.Max(count - 250m, 0m), 750m) * 0.16m) +
        (Math.Max(count - 1000m, 0m) * 0.08m);

    private static decimal AssertionEffort(decimal count) =>
        (Math.Min(count, 500m) * 0.015m) +
        (Math.Max(count - 500m, 0m) * 0.007m);

    private static decimal Diminishing(
        decimal count,
        decimal firstRate,
        decimal laterRate,
        decimal firstBand) =>
        (Math.Min(count, firstBand) * firstRate) +
        (Math.Max(count - firstBand, 0m) * laterRate);

    private static decimal Positive(decimal value) =>
        Math.Max(0.5m, RoundQuarter(value));

    private static bool IsTestScope(string scope)
    {
        string value = scope.Replace('\\', '/').ToLowerInvariant();
        return value.StartsWith("test/", StringComparison.Ordinal) ||
               value.StartsWith("tests/", StringComparison.Ordinal) ||
               value.Contains("/test/", StringComparison.Ordinal) ||
               value.Contains("/tests/", StringComparison.Ordinal) ||
               value.Contains(".tests", StringComparison.Ordinal) ||
               value.Contains("unittests", StringComparison.Ordinal) ||
               value.Contains("integrationtests", StringComparison.Ordinal) ||
               value.Contains("testsuite", StringComparison.Ordinal) ||
               value.Contains("testharness", StringComparison.Ordinal);
    }

    private static bool IsBenchmarkScope(string scope)
    {
        string value = scope.Replace('\\', '/').ToLowerInvariant();
        return value.StartsWith("bench/", StringComparison.Ordinal) ||
               value.StartsWith("benchmark/", StringComparison.Ordinal) ||
               value.Contains("/bench/", StringComparison.Ordinal) ||
               value.Contains("benchmark", StringComparison.Ordinal);
    }

    private static bool IsGeneratedFixtureScope(string scope)
    {
        string value = scope.Replace('\\', '/').ToLowerInvariant();
        return value.Contains("test-goldens", StringComparison.Ordinal) ||
               value.Contains("test-output", StringComparison.Ordinal) ||
               value.Contains("/goldens/", StringComparison.Ordinal) ||
               value.Contains("/test-projects/", StringComparison.Ordinal);
    }
}
