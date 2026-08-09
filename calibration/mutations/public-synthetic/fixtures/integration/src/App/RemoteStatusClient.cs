using System.Net.Http;

namespace EffortHoursSynthetic;

public sealed class RemoteStatusClient(HttpClient client)
{
    public async Task<string> ReadAsync(CancellationToken cancellationToken) =>
        await client.GetStringAsync("status", cancellationToken);
}
