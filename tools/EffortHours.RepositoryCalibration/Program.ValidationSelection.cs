namespace EffortHours.RepositoryCalibration;

internal static partial class Program
{
    private static async Task<int> RunValidationSelectionAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (!ValidationSelectionOptions.TryParse(
                arguments,
                out ValidationSelectionOptions? options,
                out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            WriteUsage(Console.Error);
            return 2;
        }

        try
        {
            await ValidationSelectionRunner.RunAsync(options!, cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            System.Text.Json.JsonException or
            EffortHours.Calibration.CalibrationEvaluationException)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
    }
}
