using System.Text;

namespace EffortHours.Change;

internal static partial class ExternalCommand
{
    private static async Task<string> ReadBoundedErrorAsync(TextReader reader, CancellationToken token)
    {
        const int limit = 65536;
        StringBuilder text = new();
        char[] buffer = new char[4096];
        while (true)
        {
            int count = await reader.ReadAsync(buffer, token).ConfigureAwait(false);
            if (count == 0) return text.ToString();
            int retained = Math.Min(count, limit - text.Length);
            text.Append(buffer, 0, retained);
        }
    }
}
