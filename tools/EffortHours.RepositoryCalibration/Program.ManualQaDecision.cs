namespace EffortHours.RepositoryCalibration;

internal static partial class Program
{
    private static async Task<int> RunManualQaDecisionTemplateAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (!ManualQaDecisionTemplateOptions.TryParse(
                arguments,
                out ManualQaDecisionTemplateOptions? options,
                out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            WriteUsage(Console.Error);
            return 2;
        }

        return await RunManualQaDecisionAsync(
            () => ManualQaDecisionRunner.FreezeTemplateAsync(options!, cancellationToken))
            .ConfigureAwait(false);
    }

    private static async Task<int> RunManualQaDecisionCompileAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (!ManualQaDecisionCompileOptions.TryParse(
                arguments,
                out ManualQaDecisionCompileOptions? options,
                out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            WriteUsage(Console.Error);
            return 2;
        }

        return await RunManualQaDecisionAsync(
            () => ManualQaDecisionRunner.CompileAsync(options!, cancellationToken))
            .ConfigureAwait(false);
    }

    private static async Task<int> RunManualQaDecisionAsync(Func<Task> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
            return 0;
        }
        catch (Calibration.CalibrationEvaluationException exception)
        {
            foreach (string error in exception.Errors)
            {
                await Console.Error.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return 2;
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
