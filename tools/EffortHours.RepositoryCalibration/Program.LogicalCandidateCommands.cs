namespace EffortHours.RepositoryCalibration;

internal static partial class Program
{
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
}
