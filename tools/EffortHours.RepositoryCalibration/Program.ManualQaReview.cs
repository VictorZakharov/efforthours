namespace EffortHours.RepositoryCalibration;

internal static partial class Program
{
    private static async Task<int> RunManualQaReviewFreezeAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (!ManualQaReviewFreezeOptions.TryParse(
                arguments,
                out ManualQaReviewFreezeOptions? options,
                out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            WriteUsage(Console.Error);
            return 2;
        }

        try
        {
            await ManualQaReviewFreezeRunner.RunAsync(options!, cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            System.Text.Json.JsonException or
            Calibration.CalibrationEvaluationException)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
    }
}
