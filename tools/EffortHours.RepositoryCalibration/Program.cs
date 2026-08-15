namespace EffortHours.RepositoryCalibration;

internal static partial class Program
{
    public static async Task<int> Main(string[] arguments)
    {
        using CancellationTokenSource cancellation = new();
        void CancelHandler(object? _, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        }

        Console.CancelKeyPress += CancelHandler;
        try
        {
            return await RunAsync(arguments, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            await Console.Error.WriteLineAsync("Repository calibration cancelled.").ConfigureAwait(false);
            return 130;
        }
        finally
        {
            Console.CancelKeyPress -= CancelHandler;
        }
    }

    private static async Task<int> RunAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (arguments.Length > 0 && arguments[0] == "review-development")
        {
            return await RunDevelopmentReviewAsync(arguments[1..], cancellationToken).ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "validation-open")
        {
            return await RunValidationOpenAsync(arguments[1..], cancellationToken).ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "candidate-preflight")
        {
            return await RunCandidatePreflightAsync(arguments[1..], cancellationToken).ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "candidate-fit")
        {
            return await RunLogicalCandidateFitAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "candidate-numerical-preflight")
        {
            return await RunLogicalCandidatePreflightAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "candidate-project")
        {
            return await RunLogicalCandidateProjectionAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "candidate-operational-preflight")
        {
            return await RunLogicalCandidateOperationalAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "candidate-benchmark-project")
        {
            return await RunCandidateBenchmarkProjectionAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "candidate-measure")
        {
            return await RunCandidateMeasurementAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length > 0 && arguments[0] == "candidate-measurement-aggregate")
        {
            return await RunCandidateMeasurementAggregateAsync(arguments[1..], cancellationToken)
                .ConfigureAwait(false);
        }

        if (arguments.Length == 1 && arguments[0] is "--help" or "-h")
        {
            WriteUsage(Console.Out);
            return 0;
        }

        if (!ReproductionOptions.TryParse(arguments, out ReproductionOptions? options, out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            WriteUsage(Console.Error);
            return 2;
        }

        try
        {
            await RepositoryCalibrationReproducer.RunAsync(
                    options!,
                    Console.Error,
                    cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            HttpRequestException or
            System.Text.Json.JsonException)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
    }

    private static async Task<int> RunDevelopmentReviewAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (!DevelopmentReviewOptions.TryParse(
                arguments,
                out DevelopmentReviewOptions? options,
                out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            WriteUsage(Console.Error);
            return 2;
        }

        try
        {
            await DevelopmentReviewPlanBuilder.RunAsync(options!, cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            System.Text.Json.JsonException)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
    }

    private static async Task<int> RunValidationOpenAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (!ValidationOpenOptions.TryParse(
                arguments,
                out ValidationOpenOptions? options,
                out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            WriteUsage(Console.Error);
            return 2;
        }

        try
        {
            await ValidationOpeningRunner.RunAsync(
                    options!,
                    Console.Error,
                    cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            HttpRequestException or
            System.Text.Json.JsonException)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
    }

    private static async Task<int> RunCandidatePreflightAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (!CandidatePreflightOptions.TryParse(
                arguments,
                out CandidatePreflightOptions? options,
                out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            WriteUsage(Console.Error);
            return 2;
        }

        try
        {
            await CandidatePreflightBuilder.RunAsync(options!, cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            System.Text.Json.JsonException)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
    }

    private static async Task<int> RunLogicalCandidateFitAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (!LogicalCandidateModelOptions.TryParse(
                arguments,
                out LogicalCandidateModelOptions? options,
                out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            WriteUsage(Console.Error);
            return 2;
        }

        try
        {
            await LogicalCandidateModelFitter.RunAsync(options!, cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            System.Text.Json.JsonException)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
    }

    private static async Task<int> RunLogicalCandidatePreflightAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (!LogicalCandidatePreflightOptions.TryParse(
                arguments,
                out LogicalCandidatePreflightOptions? options,
                out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            WriteUsage(Console.Error);
            return 2;
        }

        try
        {
            await LogicalCandidatePreflightBuilder.RunAsync(options!, cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            System.Text.Json.JsonException)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
    }

    private static async Task<int> RunLogicalCandidateProjectionAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (!LogicalCandidateProjectionOptions.TryParse(
                arguments,
                out LogicalCandidateProjectionOptions? options,
                out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            WriteUsage(Console.Error);
            return 2;
        }

        try
        {
            await LogicalCandidateProjectionRunner.RunAsync(
                    options!,
                    Console.Out,
                    Console.Error,
                    cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            System.Text.Json.JsonException)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
    }

    private static async Task<int> RunLogicalCandidateOperationalAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (!LogicalCandidateOperationalOptions.TryParse(
                arguments,
                out LogicalCandidateOperationalOptions? options,
                out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            WriteUsage(Console.Error);
            return 2;
        }

        try
        {
            await LogicalCandidateOperationalBuilder.RunAsync(options!, cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            System.Text.Json.JsonException)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
    }

    private static void WriteUsage(TextWriter writer) => writer.WriteLine("""
        Usage:
          dotnet EffortHours.RepositoryCalibration.dll
            --plan <sampling-plan.json>
            --workspace <ignored-directory>
            --cli <efforthours.dll>
            --packets <published-packet-directory>
            --output <reproduction-manifest.json>
            --custody <holdout-custody.json>
            [--gh <gh-executable>]

        Verifies all frozen public snapshots and licenses. Only development families
        are scanned and projected into blind authoring packets. Validation and test
        families are never passed to EffortHours by this command.

        Development review:
          dotnet EffortHours.RepositoryCalibration.dll review-development
            --plan <sampling-plan.json>
            --manifest <reproduction-manifest.json>
            --packets <blind-packet-directory>
            --outputs <ignored-development-output-directory>
            --output <review-plan.json>

        The review command accepts no estimate input. It verifies the selected
        evidence and blind-packet digests before writing the complete 15-family
        development-only host-AI teacher cohort.

        Validation opening:
          dotnet EffortHours.RepositoryCalibration.dll validation-open
            --repository-root <efforthours-checkout>
            --plan <sampling-plan.json>
            --reproduction-manifest <reproduction-manifest.json>
            --custody <holdout-custody.json>
            --candidate-manifest <candidate-manifest.json>
            --source-commit <40-hex-commit>
            --workspace <ignored-directory>
            --cli <efforthours.dll>
            --packets <published-validation-packet-directory>
            --output <validation-opening.json>
            [--gh <gh-executable>]

        Verifies the exact finite candidate, selection rule, complete artifact
        chain, source custody, and nine-family validation matrix before any network
        or output access. It then verifies and scans validation only, emits strict-
        blind packets, and structurally excludes frozen-challenger output and every
        test family.

        Candidate preflight:
          dotnet EffortHours.RepositoryCalibration.dll candidate-preflight
            --plan <sampling-plan.json>
            --corpus <development-corpus.json>
            --seed-evaluation <development-evaluation.json>
            --outputs <ignored-development-output-directory>
            --source-commit <40-hex-commit>
            --output <candidate-preflight.json>

        Reproduces the seed development evaluation, applies the bounded transparent
        challenger in memory, serializes every computed or deliberately unrun gate,
        and fails closed without reading validation or test source, outputs, or labels.

        Logical-capability fitting:
          dotnet EffortHours.RepositoryCalibration.dll candidate-fit
            --corpus <development-corpus.json>
            --outputs <ignored-development-output-directory>
            --source-commit <40-hex-commit>
            --output <logical-capability-model.json>

        Logical-capability numerical preflight:
          dotnet EffortHours.RepositoryCalibration.dll candidate-numerical-preflight
            --plan <sampling-plan.json>
            --corpus <development-corpus.json>
            --seed-evaluation <development-evaluation.json>
            --outputs <ignored-development-output-directory>
            --model <logical-capability-model.json>
            --source-commit <40-hex-commit>
            --output <candidate-preflight.json>

        These commands fit and evaluate only the complete development partition.
        They cannot scan, label, or evaluate validation or test repositories.

        Logical-capability saved-evidence projection:
          dotnet EffortHours.RepositoryCalibration.dll candidate-project
            --estimate <seed-estimate.json>
            --evidence <repository-evidence.json>
            --model <logical-capability-model.json>
            --expected-model-digest <sha256:digest>
            --primary-stratum <stratum>

        Projects one bounded, digest-matched saved estimate/evidence pair. Supported
        repository-model-admission/1.0.0 strata use the candidate; every other
        stratum retains the complete named seed fallback with a stderr diagnostic.

        Logical-capability operational preflight:
          dotnet EffortHours.RepositoryCalibration.dll candidate-operational-preflight
            --plan <sampling-plan.json>
            --corpus <development-corpus.json>
            --seed-evaluation <development-evaluation.json>
            --outputs <ignored-development-output-directory>
            --model <logical-capability-model.json>
            --numerical-preflight <frozen-numerical-preflight.json>
            --source-commit <40-hex-commit>
            --output <operational-preflight.json>

        Evaluates development-only stratum, material-category, shape/size,
        saved-explanation, and safety gates. It stops later measured gates after
        any failure and never reads validation or test source, outputs, or labels.

        Measured operational gates:
          dotnet EffortHours.RepositoryCalibration.dll candidate-measure [options]
          dotnet EffortHours.RepositoryCalibration.dll candidate-measurement-aggregate [options]

        The platform command runs five or more paired fresh-process projections for
        deterministic small/medium/large saved evidence plus scanner, optional
        mutation, and optional installed-package measurements. The aggregate command
        requires exact Windows, Linux, and macOS records and writes the fail-closed
        measured checkpoint. Neither command accepts holdout inputs.
        """);
}
