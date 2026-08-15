namespace EffortHours.RepositoryCalibration;

internal static partial class Program
{
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

    private static async Task<int> RunValidationReviewAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (!ValidationReviewOptions.TryParse(
                arguments,
                out ValidationReviewOptions? options,
                out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            WriteUsage(Console.Error);
            return 2;
        }

        try
        {
            await ValidationReviewPlanBuilder.RunAsync(options!, cancellationToken)
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
