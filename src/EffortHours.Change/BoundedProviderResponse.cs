using System.Text;

namespace EffortHours.Change;

internal sealed class ProviderResponseBoundException(int limit, int observed) : Exception
{
    public int Limit { get; } = limit;
    public int Observed { get; } = observed;
}

internal static class BoundedProviderResponse
{
    public static async Task<ExternalCommandResult> RunAsync(IExternalCommandRunner commands,
        string directory, IReadOnlyList<string> arguments, int limit, CancellationToken token)
    {
        StringBuilder text = new();
        ExternalCommandResult result = await commands.RunStreamingAsync("gh", directory, arguments,
            async (reader, cancellation) =>
            {
                char[] buffer = new char[4096];
                while (true)
                {
                    int count = await reader.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, limit - text.Length + 1)), cancellation).ConfigureAwait(false);
                    if (count == 0) break;
                    if (count > limit - text.Length) throw new ProviderResponseBoundException(limit, limit + 1);
                    text.Append(buffer, 0, count);
                }
            }, token, requireSuccess: false).ConfigureAwait(false);
        return result with { StandardOutput = text.ToString() };
    }
}
