using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class LogicalCandidateScorer
{
    public const string Version = "normalized-evidence-seed-anchor/0.1.0";

    public const string LegacyVersion = "evidence-logical-units/0.1.0";

    public static readonly string[] SizeBands =
        ["xs:<=1", "s:<=2", "m:<=4", "l:<=8", "xl:<=16", "2xl:<=32", "3xl:<=64", "4xl:>64"];

    private static readonly HashSet<string> SemanticKinds = new(StringComparer.Ordinal)
    {
        "api-surface",
        "data-persistence",
        "external-integration",
        "security-surface",
        "ui-surface",
        "validation-surface",
    };

    private static readonly HashSet<string> SourceKinds = new(StringComparer.Ordinal)
    {
        "dotnet-source-backbone",
        "javascript-source-backbone",
        "polyglot-source-backbone",
    };

    private static readonly HashSet<string> GeneratedSupportKinds = new(StringComparer.Ordinal)
    {
        "architecture-design",
        "manual-validation",
        "project-setup",
        "ui-surface",
    };

    public static IReadOnlyList<LogicalCapabilityGroup> Build(
        EstimateReport report,
        RepositoryEvidence evidence,
        string scorerVersion = Version,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(evidence);
        if (!string.Equals(
                report.Repository.SourceDigest,
                evidence.Repository.SourceDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("Candidate estimate and repository evidence source digests differ.");
        }

        Dictionary<string, EvidenceFact> facts = evidence.Facts.ToDictionary(
            fact => fact.Id,
            StringComparer.Ordinal);
        LogicalCandidateEvidenceNormalizer normalizer =
            LogicalCandidateEvidenceNormalizer.Create(evidence.Facts);
        List<LogicalCapabilityGroup> groups = [];
        foreach (IGrouping<string, WorkItem> group in report.WorkItems.GroupBy(
                     item => CapabilityId(item.Id),
                     StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            groups.Add(BuildGroup(
                group.Key,
                [.. group],
                facts,
                normalizer,
                scorerVersion,
                cancellationToken));
        }

        return groups;
    }

    public static string SizeBand(decimal value) => value switch
    {
        <= 1m => "xs",
        <= 2m => "s",
        <= 4m => "m",
        <= 8m => "l",
        <= 16m => "xl",
        <= 32m => "2xl",
        <= 64m => "3xl",
        _ => "4xl",
    };

    private static LogicalCapabilityGroup BuildGroup(
        string id,
        IReadOnlyList<WorkItem> items,
        Dictionary<string, EvidenceFact> facts,
        LogicalCandidateEvidenceNormalizer normalizer,
        string scorerVersion,
        CancellationToken cancellationToken)
    {
        string kind = Kind(id);
        string scope = Single(items.Select(item => item.Scope), id, "scope");
        EffortCategory category = Single(items.Select(item => item.Category), id, "category");
        string[] evidenceIds =
        [
            .. items.SelectMany(item => item.EvidenceIds)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        decimal seedExpected = items.Sum(item => item.Hours.Expected);
        bool legacy = scorerVersion == LegacyVersion;
        if (!legacy && scorerVersion != Version)
        {
            throw new InvalidDataException($"Unsupported logical-capability scorer '{scorerVersion}'.");
        }

        LogicalCandidateNormalizedEvidence normalized = normalizer.Accumulate(
            evidenceIds,
            facts,
            kind,
            scope,
            normalize: !legacy,
            cancellationToken);
        decimal roleFactor = ScopeFactor(kind, scope);
        decimal evidencePoint = RawPoint(
            kind,
            normalized.Measurements,
            normalized.EvidenceCount,
            seedExpected);
        if (!legacy)
        {
            evidencePoint = kind switch
            {
                "api-surface" => decimal.Max(1.25m, evidencePoint),
                "external-integration" => decimal.Max(1.5m, evidencePoint),
                _ => evidencePoint,
            };
        }
        decimal logicalExpected = evidencePoint * roleFactor;
        decimal normalizedSeedExpected = seedExpected * roleFactor;
        return new LogicalCapabilityGroup(
            id,
            kind,
            scope,
            category,
            items,
            evidenceIds,
            logicalExpected,
            normalizedSeedExpected,
            roleFactor,
            SizeBand(logicalExpected));
    }

    private static decimal RawPoint(
        string kind,
        IReadOnlyDictionary<string, decimal> values,
        int evidenceCount,
        decimal seedExpected) => kind switch
        {
            "dotnet-source-backbone" => Positive(
                Get(values, "files") * 0.22m +
                Get(values, "types") * 0.14m +
                Get(values, "methods") * 0.07m +
                Get(values, "branch-points") * 0.035m +
                Get(values, "async-methods") * 0.12m +
                Get(values, "public-types") * 0.08m +
                Get(values, "public-methods") * 0.025m),
            "javascript-source-backbone" => Positive(
                Get(values, "files") * 0.15m +
                Get(values, "functions") * 0.035m +
                Get(values, "methods") * 0.06m +
                Get(values, "branch-points") * 0.035m +
                Get(values, "classes") * 0.08m +
                Get(values, "exports") * 0.04m +
                Get(values, "async-functions") * 0.08m),
            "polyglot-source-backbone" => Positive(
                Get(values, "files") * 0.4m +
                Get(values, "functions") * 0.3m +
                Get(values, "methods") * 0.25m +
                Get(values, "branch-points") * 0.12m +
                Get(values, "external-commands") * 0.08m +
                Get(values, "file-operations") * 0.2m +
                Get(values, "network-operations") * 0.3m +
                Get(values, "process-operations") * 0.25m),
            "unit-tests" or "integration-tests" or "end-to-end-tests" =>
                TestPoint(values, evidenceCount),
            "api-surface" => ApiPoint(values),
            "ui-surface" => UiPoint(values),
            "data-persistence" => DataPoint(values),
            "external-integration" => Positive(
                0.5m +
                Get(values, "client-constructions") * 0.5m +
                Diminishing(Get(values, "integration-calls"), 0.4m, 0.15m, 40m) +
                Get(values, "integration-namespaces") * 0.5m),
            "security-surface" or "accessibility-surface" => SecurityPoint(values),
            "validation-surface" => Positive(
                0.5m +
                Get(values, "validation-attributes") * 0.25m +
                Get(values, "validation-rules") * 0.5m +
                Get(values, "validation-usages") * 0.1m +
                Get(values, "validator-types") * 2m),
            "application-entry-point" => Clamp(
                0.5m +
                Get(values, "main-methods") * 1.5m +
                Get(values, "host-builder-calls") +
                Get(values, "root-commands") * 0.25m +
                Get(values, "commands") * 0.12m +
                Get(values, "entry-points") * 0.5m +
                Get(values, "top-level-statements") * 0.08m,
                0.5m,
                8m),
            "project-setup" => Clamp(0.75m + evidenceCount * 0.12m, 0.5m, 8m),
            "architecture-design" => Clamp(1m + evidenceCount * 0.1m, 0.5m, 12m),
            "manual-validation" => Clamp(0.75m + evidenceCount * 0.07m, 0.5m, 12m),
            _ => seedExpected,
        };

    private static decimal TestPoint(IReadOnlyDictionary<string, decimal> values, int evidenceCount)
    {
        decimal primary = decimal.Max(Get(values, "test-methods"), Get(values, "test-cases"));
        if (primary == 0m)
        {
            return Clamp(0.5m + evidenceCount * 0.15m, 0.5m, 8m);
        }

        return Positive(
            0.5m +
            decimal.Min(primary, 50m) * 0.45m +
            decimal.Min(decimal.Max(primary - 50m, 0m), 200m) * 0.28m +
            decimal.Min(decimal.Max(primary - 250m, 0m), 750m) * 0.16m +
            decimal.Max(primary - 1000m, 0m) * 0.08m +
            Get(values, "parameterized-cases") * 0.05m +
            decimal.Min(Get(values, "assertions"), 500m) * 0.015m +
            decimal.Max(Get(values, "assertions") - 500m, 0m) * 0.007m +
            Get(values, "mock-usages") * 0.15m +
            Get(values, "test-suites") * 0.1m +
            Get(values, "accessibility-checks") * 0.2m);
    }

    private static decimal ApiPoint(IReadOnlyDictionary<string, decimal> values)
    {
        decimal endpoints = Get(values, "endpoints");
        return Positive(
            0.5m +
            Get(values, "controllers") * 1.2m +
            Get(values, "attributed-endpoints") * 0.45m +
            Get(values, "minimal-api-endpoints") * 1.5m +
            decimal.Min(endpoints, 50m) * 0.5m +
            decimal.Max(endpoints - 50m, 0m) * 0.08m);
    }

    private static decimal UiPoint(IReadOnlyDictionary<string, decimal> values) => Positive(
        0.5m +
        Get(values, "files") * 0.5m +
        Get(values, "components") * 1.5m +
        Get(values, "pages") * 1.5m +
        Get(values, "forms") * 1.25m +
        Get(values, "template-structure-units") * 0.35m +
        Get(values, "style-structure-units") * 0.2m +
        Get(values, "bindings") * 0.08m +
        Get(values, "component-parameters") * 0.08m +
        Get(values, "component-usages") * 0.015m +
        Get(values, "design-token-units") * 0.2m +
        Get(values, "responsive-surfaces") * 0.2m +
        Get(values, "custom-elements") * 0.4m +
        Get(values, "elements") * 0.02m);

    private static decimal DataPoint(IReadOnlyDictionary<string, decimal> values) => Positive(
        0.5m +
        Get(values, "db-contexts") * 6m +
        Get(values, "db-sets") * 1.5m +
        Get(values, "entity-configurations") * 1.5m +
        Get(values, "migrations") * 1.8m +
        Get(values, "repository-types") * 1.2m +
        Get(values, "tables") * 1.5m +
        Get(values, "indexes") * 0.7m +
        Diminishing(Get(values, "data-calls"), 0.12m, 0.035m, 100m) +
        Diminishing(Get(values, "queries"), 0.15m, 0.04m, 50m) +
        Diminishing(Get(values, "statements"), 0.04m, 0.01m, 100m));

    private static decimal SecurityPoint(IReadOnlyDictionary<string, decimal> values) => Positive(
        0.5m +
        Get(values, "authorization-attributes") * 0.3m +
        Get(values, "security-configuration-calls") * 2m +
        Get(values, "security-usages") * 0.5m +
        Get(values, "accessibility-units") * 0.35m +
        Get(values, "labels") * 0.05m +
        Get(values, "keyboard-interactions") * 0.3m +
        Get(values, "focus-controls") * 0.2m);

    private static decimal ScopeFactor(string kind, string scope)
    {
        ScopeRoles roles = ClassifyScope(scope);
        if ((roles.Test && SemanticKinds.Contains(kind)) ||
            (roles.Benchmark && kind == "application-entry-point") ||
            (roles.Benchmark && SourceKinds.Contains(kind)) ||
            (roles.Generated && SourceKinds.Contains(kind)) ||
            (roles.Generated && GeneratedSupportKinds.Contains(kind)))
        {
            return 0m;
        }

        return (roles.Test && (SourceKinds.Contains(kind) || kind == "application-entry-point")) ||
               (roles.Benchmark && SemanticKinds.Contains(kind))
            ? 0.25m
            : 1m;
    }

    private static ScopeRoles ClassifyScope(string scope)
    {
        string normalized = scope.Replace('\\', '/').ToLowerInvariant();
        string delimited = $"/{normalized.Trim('/')}/";
        bool test =
            normalized.StartsWith("test/", StringComparison.Ordinal) ||
            normalized.StartsWith("tests/", StringComparison.Ordinal) ||
            normalized.Contains("/test/", StringComparison.Ordinal) ||
            normalized.Contains("/tests/", StringComparison.Ordinal) ||
            normalized.Contains(".tests", StringComparison.Ordinal) ||
            normalized.Contains("unittests", StringComparison.Ordinal) ||
            normalized.Contains("integrationtests", StringComparison.Ordinal) ||
            normalized.Contains("testsuite", StringComparison.Ordinal) ||
            normalized.Contains("testharness", StringComparison.Ordinal) ||
            normalized.Contains("testcomponents", StringComparison.Ordinal);
        bool benchmark =
            delimited.Contains("/bench/", StringComparison.Ordinal) ||
            delimited.Contains("/benchmark/", StringComparison.Ordinal) ||
            delimited.Contains("/benchmarks/", StringComparison.Ordinal) ||
            normalized.Contains("benchmark", StringComparison.Ordinal);
        bool generated =
            normalized.Contains("test-goldens", StringComparison.Ordinal) ||
            normalized.Contains("test-output", StringComparison.Ordinal) ||
            normalized.Contains("/goldens/", StringComparison.Ordinal) ||
            normalized.Contains("/test-projects/", StringComparison.Ordinal);
        return new ScopeRoles(test, benchmark, generated);
    }

    private static string CapabilityId(string itemId)
    {
        string[] parts = itemId.Split(':');
        if (parts.Length < 4 || parts[0] != "work")
        {
            throw new InvalidDataException($"Cannot derive a capability ID from work item '{itemId}'.");
        }

        return string.Join(':', parts[..3]);
    }

    private static string Kind(string capabilityId) => capabilityId.Split(':')[1];

    private static T Single<T>(IEnumerable<T> values, string id, string field)
    {
        T[] distinct = [.. values.Distinct()];
        return distinct.Length == 1
            ? distinct[0]
            : throw new InvalidDataException($"Logical capability '{id}' has mixed {field} values.");
    }

    private static decimal Get(IReadOnlyDictionary<string, decimal> values, string name) =>
        values.GetValueOrDefault(name);

    private static decimal Diminishing(
        decimal count,
        decimal firstRate,
        decimal laterRate,
        decimal firstBand) =>
        decimal.Min(count, firstBand) * firstRate + decimal.Max(count - firstBand, 0m) * laterRate;

    private static decimal Positive(decimal value) => decimal.Max(0.5m, RoundQuarter(value));

    private static decimal Clamp(decimal value, decimal minimum, decimal maximum) =>
        decimal.Min(maximum, decimal.Max(minimum, RoundQuarter(value)));

    private static decimal RoundQuarter(decimal value) =>
        decimal.Round(value * 4m, 0, MidpointRounding.AwayFromZero) / 4m;

    private readonly record struct ScopeRoles(bool Test, bool Benchmark, bool Generated);
}

internal sealed record LogicalCapabilityGroup(
    string Id,
    string Kind,
    string Scope,
    EffortCategory Category,
    IReadOnlyList<WorkItem> Items,
    IReadOnlyList<string> EvidenceIds,
    decimal LogicalExpected,
    decimal SeedExpected,
    decimal ScopeRoleFactor,
    string LogicalSizeBand);
