namespace EffortHours.RepositoryCalibration;

internal static partial class Program
{
    private static async Task<int> RunManualQaCandidateProjectionAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (!ManualQaCandidateProjectionOptions.TryParse(
                arguments,
                out ManualQaCandidateProjectionOptions? options,
                out string? error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            WriteUsage(Console.Error);
            return 2;
        }

        try
        {
            await ManualQaCandidateProjectionRunner.RunAsync(
                    options!,
                    Console.Out,
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
}
