namespace EffortHours.RepositoryCalibration;

internal static class Program
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

        if (arguments.Length > 0 && arguments[0] == "candidate-preflight")
        {
            return await RunCandidatePreflightAsync(arguments[1..], cancellationToken).ConfigureAwait(false);
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
        """);
}
