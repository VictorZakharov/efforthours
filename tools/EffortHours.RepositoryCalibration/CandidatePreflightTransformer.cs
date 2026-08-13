using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class CandidatePreflightTransformer
{
    public const string CandidateId = "scope-marginality/0.1.0";
    public const string EstimatorVersion = "candidate-scope-marginality/0.1.0+seed-rules/0.4.0";
    public const string FeatureContractVersion = "scope-marginality-features/1.0.0";
    public const string BaselineEstimatorVersion = "seed-rules/0.4.0";
    public const decimal DiscountFactor = 0.25m;
    public const decimal LowFactor = 0.77m;
    public const decimal HighFactor = 1.26m;

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

    public static EstimateReport Transform(EstimateReport source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.EstimatorVersion != BaselineEstimatorVersion ||
            source.Profile != EstimationProfile.Implementation)
        {
            throw new InvalidDataException(
                $"Candidate '{CandidateId}' supports implementation-profile " +
                $"'{BaselineEstimatorVersion}' reports only.");
        }

        WorkItem[] workItems = [.. source.WorkItems.Select(TransformItem)];
        EffortRange total = ContractValidation.Sum(workItems.Select(item => item.Hours));
        CategoryEstimate[] categories =
        [
            .. workItems
                .GroupBy(item => item.Category)
                .OrderBy(group => group.Key)
                .Select(group => new CategoryEstimate
                {
                    Category = group.Key,
                    Hours = ContractValidation.Sum(group.Select(item => item.Hours)),
                }),
        ];

        return source with
        {
            EstimatorVersion = EstimatorVersion,
            TotalEffort = total,
            TotalCost = ProjectCost(source.RateCard, total),
            Categories = categories,
            WorkItems = workItems,
            Assumptions =
            [
                .. source.Assumptions,
                "Development-only candidate preflight; this estimate is not admitted for product use.",
            ],
        };
    }

    internal static decimal GetPointFactor(WorkItem item)
    {
        string kind = GetKind(item.Id);
        ScopeRoles roles = ClassifyScope(item.Scope);
        if ((roles.Test && SemanticKinds.Contains(kind)) ||
            (roles.Benchmark && kind == "application-entry-point") ||
            (roles.Benchmark && SourceKinds.Contains(kind)) ||
            (roles.Generated && SourceKinds.Contains(kind)) ||
            (roles.Generated && GeneratedSupportKinds.Contains(kind)))
        {
            return 0m;
        }

        if ((roles.Test && (SourceKinds.Contains(kind) || kind == "application-entry-point")) ||
            (roles.Benchmark && SemanticKinds.Contains(kind)))
        {
            return DiscountFactor;
        }

        return 1m;
    }

    private static WorkItem TransformItem(WorkItem source)
    {
        decimal factor = GetPointFactor(source);
        decimal expected = source.Hours.Expected * factor;
        string action = factor switch
        {
            0m => "excluded duplicated test, benchmark, or generated-fixture semantics",
            DiscountFactor => "discounted supporting test or benchmark implementation",
            _ => "retained the seed expected point",
        };
        EffortRange hours = expected == 0m
            ? new EffortRange { Low = 0m, Expected = 0m, High = 0m }
            : new EffortRange
            {
                Low = expected * LowFactor,
                Expected = expected,
                High = expected * HighFactor,
            };

        return source with
        {
            Hours = hours,
            Reason =
                $"Candidate '{CandidateId}' {action} with point factor {factor}; " +
                $"seed item '{source.Id}' supplied {source.Hours.Expected} expected hours under " +
                $"'{BaselineEstimatorVersion}'. Range factors are {LowFactor}/{HighFactor}.",
            Estimator = new EstimatorReference
            {
                Id = "candidate-scope-marginality",
                Version = "0.1.0",
                Kind = EstimatorKind.Rule,
            },
        };
    }

    private static string GetKind(string id)
    {
        string[] parts = id.Split(':');
        if (parts.Length < 3 || parts[0] != "work" || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new InvalidDataException(
                $"Candidate '{CandidateId}' cannot classify work-item ID '{id}'.");
        }

        return parts[1];
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

    private static CostRange? ProjectCost(RateCard? rateCard, EffortRange hours) =>
        rateCard is null
            ? null
            : new CostRange
            {
                Low = RoundMoney(hours.Low * rateCard.HourlyRate),
                Expected = RoundMoney(hours.Expected * rateCard.HourlyRate),
                High = RoundMoney(hours.High * rateCard.HourlyRate),
                Currency = rateCard.Currency,
            };

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private readonly record struct ScopeRoles(bool Test, bool Benchmark, bool Generated);
}
