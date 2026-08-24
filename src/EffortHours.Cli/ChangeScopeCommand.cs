using EffortHours.Change;
using EffortHours.Contracts;

namespace EffortHours.Cli;

internal static class ChangeScopeCommand
{
    public static async Task<int> ExecuteAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (arguments is ["show", "engineering"])
        {
            try
            {
                await standardOutput.WriteAsync(
                    ContractJson.ToCanonicalDocument(
                        ContractJson.Serialize(EngineeringScopeProfile.Load().Contract)))
                    .ConfigureAwait(false);
                return CliExitCodes.Success;
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                await standardError.WriteLineAsync($"eh: {exception.Message}").ConfigureAwait(false);
                return CliExitCodes.InvalidInput;
            }
        }

        await standardError.WriteLineAsync(
            "eh: expected 'change scope show engineering'.").ConfigureAwait(false);
        return CliExitCodes.UsageError;
    }
}
