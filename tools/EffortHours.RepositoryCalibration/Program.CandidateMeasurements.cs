namespace EffortHours.RepositoryCalibration;

internal static partial class Program
{
    private static async Task<int> RunCandidateBenchmarkProjectionAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (!CandidateBenchmarkProjectionOptions.TryParse(
                arguments,
                out CandidateBenchmarkProjectionOptions? options,
                out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            return 2;
        }

        try
        {
            await CandidateBenchmarkProjectionRunner.RunAsync(
                options!,
                Console.Out,
                cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (IsCandidateMeasurementInputError(exception))
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
    }

    private static async Task<int> RunCandidateMeasurementAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (!CandidateMeasurementOptions.TryParse(
                arguments,
                out CandidateMeasurementOptions? options,
                out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            return 2;
        }

        try
        {
            await CandidateMeasurementRunner.RunAsync(options!, cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (IsCandidateMeasurementInputError(exception))
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
    }

    private static async Task<int> RunCandidateMeasurementAggregateAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (!CandidateMeasurementAggregateOptions.TryParse(
                arguments,
                out CandidateMeasurementAggregateOptions? options,
                out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            return 2;
        }

        try
        {
            bool passed = await CandidateMeasurementAggregator.RunAsync(
                options!,
                cancellationToken).ConfigureAwait(false);
            return passed ? 0 : 3;
        }
        catch (Exception exception) when (IsCandidateMeasurementInputError(exception))
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
    }

    private static bool IsCandidateMeasurementInputError(Exception exception) =>
        exception is IOException or
        UnauthorizedAccessException or
        InvalidDataException or
        System.Text.Json.JsonException or
        EffortHours.Calibration.CalibrationEvaluationException;
}
