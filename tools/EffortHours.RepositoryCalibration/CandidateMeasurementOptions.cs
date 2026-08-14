namespace EffortHours.RepositoryCalibration;

internal sealed record CandidateBenchmarkProjectionOptions
{
    public required string EvidencePath { get; init; }

    public required bool ApplyCandidate { get; init; }

    public string? ModelPath { get; init; }

    public string? ExpectedModelDigest { get; init; }

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out CandidateBenchmarkProjectionOptions? options,
        out string? error)
    {
        string[] allowed = ["--evidence", "--mode", "--model", "--expected-model-digest"];
        if (!TryRead(arguments, allowed, out Dictionary<string, string>? values, out error) ||
            !Require(values!, ["--evidence", "--mode"], out error))
        {
            options = null;
            return false;
        }

        string mode = values!["--mode"];
        if (mode is not ("seed" or "candidate"))
        {
            options = null;
            error = "Option '--mode' must be 'seed' or 'candidate'.";
            return false;
        }

        bool candidate = mode == "candidate";
        bool hasModel = values.ContainsKey("--model");
        bool hasDigest = values.ContainsKey("--expected-model-digest");
        if (candidate != hasModel || candidate != hasDigest)
        {
            options = null;
            error = "Candidate benchmark mode requires both '--model' and '--expected-model-digest'; seed mode accepts neither.";
            return false;
        }

        options = new CandidateBenchmarkProjectionOptions
        {
            EvidencePath = Path.GetFullPath(values["--evidence"]),
            ApplyCandidate = candidate,
            ModelPath = hasModel ? Path.GetFullPath(values["--model"]) : null,
            ExpectedModelDigest = hasDigest ? values["--expected-model-digest"].ToLowerInvariant() : null,
        };
        error = null;
        return true;
    }

    internal static bool TryRead(
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> allowed,
        out Dictionary<string, string>? values,
        out string? error)
    {
        values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < arguments.Count; index += 2)
        {
            if (index + 1 >= arguments.Count ||
                !arguments[index].StartsWith("--", StringComparison.Ordinal))
            {
                error = index < arguments.Count
                    ? $"Option '{arguments[index]}' requires a value."
                    : "A candidate-measurement option requires a value.";
                return false;
            }

            string name = arguments[index];
            if (!allowed.Contains(name, StringComparer.Ordinal))
            {
                error = $"Unknown option '{name}'.";
                return false;
            }

            if (!values.TryAdd(name, arguments[index + 1]))
            {
                error = $"Option '{name}' was supplied more than once.";
                return false;
            }
        }

        error = null;
        return true;
    }

    internal static bool Require(
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<string> required,
        out string? error)
    {
        string? missing = required.FirstOrDefault(option => !values.ContainsKey(option));
        error = missing is null ? null : $"Required option '{missing}' is missing.";
        return missing is null;
    }
}

internal sealed record CandidateMeasurementOptions
{
    public required string ModelPath { get; init; }

    public required string OperationalPreflightPath { get; init; }

    public required string EvidenceTemplatePath { get; init; }

    public required string ScannerBenchmarkPath { get; init; }

    public required string WorkspacePath { get; init; }

    public required string Platform { get; init; }

    public required string MeasurementImplementationCommit { get; init; }

    public required string RunId { get; init; }

    public required int RunAttempt { get; init; }

    public required int RunsPerShape { get; init; }

    public required string ReportPath { get; init; }

    public string? MutationSuitePath { get; init; }

    public string? MutationFixturesPath { get; init; }

    public string? CliPath { get; init; }

    public string? MutationReportPath { get; init; }

    public string? SeedInstallPath { get; init; }

    public string? CandidateInstallPath { get; init; }

    public bool MeasuresMutations => MutationSuitePath is not null;

    public bool MeasuresPackage => SeedInstallPath is not null;

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out CandidateMeasurementOptions? options,
        out string? error)
    {
        string[] allowed =
        [
            "--model", "--operational-preflight", "--evidence-template", "--scanner-benchmark",
            "--workspace", "--platform", "--source-commit", "--run-id", "--run-attempt", "--runs",
            "--output", "--mutation-suite", "--mutation-fixtures", "--cli", "--mutation-output",
            "--seed-install", "--candidate-install",
        ];
        string[] required =
        [
            "--model", "--operational-preflight", "--evidence-template", "--scanner-benchmark",
            "--workspace", "--platform", "--source-commit", "--run-id", "--run-attempt", "--runs",
            "--output",
        ];
        if (!CandidateBenchmarkProjectionOptions.TryRead(
                arguments, allowed, out Dictionary<string, string>? values, out error) ||
            !CandidateBenchmarkProjectionOptions.Require(values!, required, out error))
        {
            options = null;
            return false;
        }

        string platform = values!["--platform"].ToLowerInvariant();
        if (platform is not ("windows" or "linux" or "macos") ||
            !LogicalCandidateModelOptions.IsCommit(values["--source-commit"]) ||
            !int.TryParse(values["--run-attempt"], out int attempt) || attempt <= 0 ||
            !int.TryParse(values["--runs"], out int runs) || runs < 5)
        {
            options = null;
            error = "Platform, source commit, run attempt, or fresh-process run count is invalid.";
            return false;
        }

        string[] mutationOptions = ["--mutation-suite", "--mutation-fixtures", "--cli", "--mutation-output"];
        int mutationCount = mutationOptions.Count(values.ContainsKey);
        string[] packageOptions = ["--seed-install", "--candidate-install"];
        int packageCount = packageOptions.Count(values.ContainsKey);
        if (mutationCount is not (0 or 4) || packageCount is not (0 or 2))
        {
            options = null;
            error = "Mutation options must be supplied together, as must the two installed-package paths.";
            return false;
        }

        options = new CandidateMeasurementOptions
        {
            ModelPath = Full(values, "--model"),
            OperationalPreflightPath = Full(values, "--operational-preflight"),
            EvidenceTemplatePath = Full(values, "--evidence-template"),
            ScannerBenchmarkPath = Full(values, "--scanner-benchmark"),
            WorkspacePath = Full(values, "--workspace"),
            Platform = platform,
            MeasurementImplementationCommit = values["--source-commit"].ToLowerInvariant(),
            RunId = values["--run-id"],
            RunAttempt = attempt,
            RunsPerShape = runs,
            ReportPath = Full(values, "--output"),
            MutationSuitePath = OptionalFull(values, "--mutation-suite"),
            MutationFixturesPath = OptionalFull(values, "--mutation-fixtures"),
            CliPath = OptionalFull(values, "--cli"),
            MutationReportPath = OptionalFull(values, "--mutation-output"),
            SeedInstallPath = OptionalFull(values, "--seed-install"),
            CandidateInstallPath = OptionalFull(values, "--candidate-install"),
        };
        error = null;
        return true;
    }

    private static string Full(Dictionary<string, string> values, string key) =>
        Path.GetFullPath(values[key]);

    private static string? OptionalFull(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out string? value) ? Path.GetFullPath(value) : null;
}

internal sealed record CandidateMeasurementAggregateOptions
{
    public required string OperationalPreflightPath { get; init; }

    public required string ModelPath { get; init; }

    public required string MutationSuitePath { get; init; }

    public required string MutationReportPath { get; init; }

    public required string MeasurementDirectory { get; init; }

    public required string MeasurementReportPath { get; init; }

    public required string PreflightReportPath { get; init; }

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out CandidateMeasurementAggregateOptions? options,
        out string? error)
    {
        string[] required =
        [
            "--operational-preflight", "--model", "--mutation-suite", "--mutation-report",
            "--measurements", "--measurement-output", "--output",
        ];
        if (!LogicalCandidateModelOptions.TryRead(
                arguments, required, out Dictionary<string, string>? values, out error))
        {
            options = null;
            return false;
        }

        options = new CandidateMeasurementAggregateOptions
        {
            OperationalPreflightPath = Path.GetFullPath(values!["--operational-preflight"]),
            ModelPath = Path.GetFullPath(values["--model"]),
            MutationSuitePath = Path.GetFullPath(values["--mutation-suite"]),
            MutationReportPath = Path.GetFullPath(values["--mutation-report"]),
            MeasurementDirectory = Path.GetFullPath(values["--measurements"]),
            MeasurementReportPath = Path.GetFullPath(values["--measurement-output"]),
            PreflightReportPath = Path.GetFullPath(values["--output"]),
        };
        error = null;
        return true;
    }
}
